#!/bin/bash
set -euo pipefail
script_dir="$(cd -- "$(dirname -- "$0")" && pwd)"
app="$script_dir/ShadeLink.app"
if [[ ! -d "$app" ]]; then
    printf '%s\n' 'ShadeLink.app과 같은 폴더에 이 파일을 두세요.'
    exit 1
fi
result_parent="$HOME/Library/Logs/ShadeLink-Validation"
mkdir -p "$result_parent"
results="$(mktemp -d "$result_parent/run.XXXXXX")"
printf '%s\n' '자동 검증을 시작합니다. 게임 창이 열렸다가 자동으로 닫힙니다.'
/usr/bin/open -n -W "$app" --args -key-smoke -shade-offscreen -shade-output "$results" \
    -screen-fullscreen 0 -screen-width 1600 -screen-height 900 -logFile "$results/player.log"
if [[ ! -f "$results/key-smoke-results.json" ]]; then
    printf '%s\n' '검증 결과가 생성되지 않았습니다. 먼저 ShadeLink.app을 직접 열어 실행을 허용해주세요.'
    /usr/bin/open "$results"
    exit 1
fi
if /usr/bin/grep -Eq '"passed"[[:space:]]*:[[:space:]]*true' "$results/key-smoke-results.json"; then
    printf '%s\n' '자동 검증을 통과했습니다. 결과 폴더가 열립니다.'
else
    printf '%s\n' '검증 실패: 결과 JSON과 player.log를 개발 담당자에게 전달해주세요.'
    /usr/bin/open "$results"
    exit 1
fi
/usr/bin/open "$results"
