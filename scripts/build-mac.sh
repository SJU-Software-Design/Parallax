#!/bin/bash
set -euo pipefail

if [[ $# -gt 1 ]]; then
    echo "Usage: $0 [path/to/Unity.app/Contents/MacOS/Unity]" >&2
    exit 2
fi

repo_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
project_dir="$repo_dir/projects/gallery"
version_file="$project_dir/ProjectSettings/ProjectVersion.txt"
if [[ ! -f "$version_file" ]]; then
    echo "The complete projects/gallery Unity project is required." >&2
    exit 1
fi
unity_version="$(sed -n 's/^m_EditorVersion: //p' "$version_file" | tr -d '\r')"
unity_editor="${1:-${UNITY_EDITOR_PATH:-/Applications/Unity/Hub/Editor/$unity_version/Unity.app/Contents/MacOS/Unity}}"
if [[ ! -x "$unity_editor" ]]; then
    echo "Unity $unity_version Editor was not found. Install it with Unity Hub, pass its executable path, or set UNITY_EDITOR_PATH." >&2
    exit 1
fi

output_dir="$repo_dir/builds/macos/gallery"
log_file="$output_dir/build.log"
result_file="$output_dir/build-result.txt"
mkdir -p "$output_dir"
rm -f "$result_file"

echo "Building Gallery for macOS with Unity $unity_version. Log: $log_file"
if "$unity_editor" -batchmode -nographics -quit \
    -buildTarget StandaloneOSX \
    -standaloneBuildSubtarget Player \
    -projectPath "$project_dir" \
    -executeMethod ShadeLink.Editor.GalleryPlatformBuild.BuildMacOS \
    -parallaxBuildOutput "$output_dir/Parallax.app" \
    -logFile "$log_file"; then
    if [[ ! -d "$output_dir/Parallax.app/Contents/MacOS" || ! -f "$result_file" ]] ||
        ! grep -q '^Result: Succeeded' "$result_file"; then
        echo "Unity did not produce a successful macOS build. See $log_file" >&2
        exit 1
    fi
else
    status=$?
    echo "macOS build failed ($status). See $log_file" >&2
    exit "$status"
fi
echo "Built: $output_dir/Parallax.app"
echo "Run: open \"$output_dir/Parallax.app\""
