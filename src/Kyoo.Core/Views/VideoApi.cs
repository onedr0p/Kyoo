// Kyoo - A portable and vast media library solution.
// Copyright (c) Kyoo.
//
// See AUTHORS.md and LICENSE file in the project root for full license information.
//
// Kyoo is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// any later version.
//
// Kyoo is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with Kyoo. If not, see <https://www.gnu.org/licenses/>.

using System.IO;
using System.Threading.Tasks;
using Kyoo.Abstractions.Controllers;
using Kyoo.Abstractions.Models;
using Kyoo.Abstractions.Models.Exceptions;
using Kyoo.Abstractions.Models.Permissions;
using Kyoo.Core.Models.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace Kyoo.Core.Api
{
	[Route("video")]
	[ApiController]
	public class VideoApi : Controller
	{
		private readonly ILibraryManager _libraryManager;
		private readonly ITranscoder _transcoder;
		private readonly IOptions<BasicOptions> _options;
		private readonly IFileSystem _files;

		public VideoApi(ILibraryManager libraryManager,
			ITranscoder transcoder,
			IOptions<BasicOptions> options,
			IFileSystem files)
		{
			_libraryManager = libraryManager;
			_transcoder = transcoder;
			_options = options;
			_files = files;
		}

		public override void OnActionExecuted(ActionExecutedContext ctx)
		{
			base.OnActionExecuted(ctx);
			// Disabling the cache prevent an issue on firefox that skip the last 30 seconds of HLS files.
			ctx.HttpContext.Response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
			ctx.HttpContext.Response.Headers.Add("Pragma", "no-cache");
			ctx.HttpContext.Response.Headers.Add("Expires", "0");
		}

		// TODO enable the following line, this is disabled since the web app can't use bearers. [Permission("video", Kind.Read)]
		[HttpGet("{slug}")]
		[HttpGet("direct/{slug}")]
		public async Task<IActionResult> Direct(string slug)
		{
			try
			{
				Episode episode = await _libraryManager.Get<Episode>(slug);
				return _files.FileResult(episode.Path, true);
			}
			catch (ItemNotFoundException)
			{
				return NotFound();
			}
		}

		[HttpGet("transmux/{slug}/master.m3u8")]
		[Permission("video", Kind.Read)]
		public async Task<IActionResult> Transmux(string slug)
		{
			try
			{
				Episode episode = await _libraryManager.Get<Episode>(slug);
				string path = await _transcoder.Transmux(episode);

				if (path == null)
					return StatusCode(500);
				return _files.FileResult(path, true);
			}
			catch (ItemNotFoundException)
			{
				return NotFound();
			}
		}

		[HttpGet("transcode/{slug}/master.m3u8")]
		[Permission("video", Kind.Read)]
		public async Task<IActionResult> Transcode(string slug)
		{
			try
			{
				Episode episode = await _libraryManager.Get<Episode>(slug);
				string path = await _transcoder.Transcode(episode);

				if (path == null)
					return StatusCode(500);
				return _files.FileResult(path, true);
			}
			catch (ItemNotFoundException)
			{
				return NotFound();
			}
		}

		[HttpGet("transmux/{episodeLink}/segments/{chunk}")]
		[Permission("video", Kind.Read)]
		public IActionResult GetTransmuxedChunk(string episodeLink, string chunk)
		{
			string path = Path.GetFullPath(Path.Combine(_options.Value.TransmuxPath, episodeLink));
			path = Path.Combine(path, "segments", chunk);
			return PhysicalFile(path, "video/MP2T");
		}

		[HttpGet("transcode/{episodeLink}/segments/{chunk}")]
		[Permission("video", Kind.Read)]
		public IActionResult GetTranscodedChunk(string episodeLink, string chunk)
		{
			string path = Path.GetFullPath(Path.Combine(_options.Value.TranscodePath, episodeLink));
			path = Path.Combine(path, "segments", chunk);
			return PhysicalFile(path, "video/MP2T");
		}
	}
}