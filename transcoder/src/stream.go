package src

import (
	"bufio"
	"context"
	"fmt"
	"log"
	"os/exec"
	"slices"
	"strings"
	"sync"
)

type TranscodeStream interface {
	getTranscodeArgs(segments string) []string
	getOutPath() string
}

type Stream struct {
	TranscodeStream
	file    *FileStream
	Clients []string
	// channel open if the segment is not ready. closed if ready.
	// one can check if segment 1 is open by doing:
	//
	//  _, ready := <- ts.segments[1]
	//
	segments []chan (struct{})
	heads    []int32
	// the lock used for the segments array and the heads
	lock sync.RWMutex
	ctx  context.Context
}

func (ts *Stream) run(start int32) error {
	// Start the transcode up to the 100th segment (or less)
	// Stop at the first finished segment
	end := min(start+100, int32(len(ts.file.Keyframes)))
	ts.lock.RLock()
	for i := start; i < end; i++ {
		if _, ready := <-ts.segments[i]; ready {
			end = i
			break
		}
	}
	encoder_id := len(ts.heads)
	ts.heads = append(ts.heads, start)
	ts.lock.RUnlock()

	log.Printf(
		"Starting transcode for %s (from %d to %d out of %d segments)",
		ts.file.Path,
		start,
		end,
		len(ts.file.Keyframes),
	)

	// We do not need the first value (start of the transcode)
	segments := make([]string, end-start-1)
	for i := range segments {
		segments[i] = fmt.Sprintf("%.6f", ts.file.Keyframes[int(start)+i+1])
	}
	segments_str := strings.Join(segments, ",")

	args := []string{
		"-nostats", "-hide_banner", "-loglevel", "warning",
		"-copyts",

		"-ss", fmt.Sprintf("%.6f", ts.file.Keyframes[start]),
		"-to", fmt.Sprintf("%.6f", ts.file.Keyframes[end]),
		"-i", ts.file.Path,
	}
	args = append(args, ts.getTranscodeArgs(segments_str)...)
	args = append(args, []string{
		"-f", "segment",
		"-segment_time_delta", "0.2",
		"-segment_format", "mpegts",
		"-segment_times", segments_str,
		"-segment_start_number", fmt.Sprint(start),
		"-segment_list_type", "flat",
		"-segment_list", "pipe:1",
		ts.getOutPath(),
	}...)

	cmd := exec.CommandContext(
		ts.ctx,
		"ffmpeg",
		args...,
	)
	log.Printf("Running %s", strings.Join(cmd.Args, " "))

	stdout, err := cmd.StdoutPipe()
	if err != nil {
		return err
	}
	var stderr strings.Builder
	cmd.Stderr = &stderr

	go func() {
		scanner := bufio.NewScanner(stdout)
		for scanner.Scan() {
			var segment int32
			_, _ = fmt.Sscanf(scanner.Text(), "segment-%d.ts", &segment)

			ts.lock.Lock()
			close(ts.segments[segment])
			ts.heads[encoder_id] = segment
			ts.lock.Unlock()
		}

		if err := scanner.Err(); err != nil {
			log.Println("Error reading stdout of ffmpeg", err)
		}
	}()

	go func() {
		err := cmd.Wait()
		if err != nil {
			log.Println("ffmpeg occured an error", err, stderr.String())
		} else {
			log.Println("ffmpeg finished successfully")
		}
	}()

	return nil
}

func (ts *Stream) GetIndex(client string) (string, error) {
	index := `#EXTM3U
#EXT-X-VERSION:3
#EXT-X-PLAYLIST-TYPE:VOD
#EXT-X-ALLOW-CACHE:YES
#EXT-X-TARGETDURATION:4
#EXT-X-MEDIA-SEQUENCE:0
`

	for segment := 1; segment < len(ts.file.Keyframes); segment++ {
		index += fmt.Sprintf("#EXTINF:%.6f\n", ts.file.Keyframes[segment]-ts.file.Keyframes[segment-1])
		index += fmt.Sprintf("segment-%d.ts\n", segment)
	}
	index += `#EXT-X-ENDLIST`
	return index, nil
}

func (ts *Stream) GetSegment(segment int32, client string) (string, error) {
	ts.lock.RLock()
	_, ready := <-ts.segments[segment]
	ts.lock.RUnlock()

	if !ready {
		// Only start a new encode if there is more than 10s between the current encoder and the segment.
		if ts.getMinEncoderDistance(ts.file.Keyframes[segment]) > 10 {
			err := ts.run(segment)
			if err != nil {
				return "", err
			}
		}

		ts.lock.RLock()
		ready_chan := ts.segments[segment]
		ts.lock.RUnlock()

		<-ready_chan
	}
	return fmt.Sprintf(ts.getOutPath(), segment), nil
}

func (ts *Stream) getMinEncoderDistance(time float64) float64 {
	ts.lock.RLock()
	defer ts.lock.RUnlock()
	distances := Map(ts.heads, func(i int32, _ int) float64 { return max(0, ts.file.Keyframes[i]-time) })
	return slices.Min(distances)
}