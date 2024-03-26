{pkgs ? import <nixpkgs> {}}:
  pkgs.mkShell {
    packages = with pkgs; [
      kubectl
      minikube
      kubernetes-helm
    ];
  }
