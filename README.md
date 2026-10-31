# Parallax

공유 Unity 프로젝트는 `Parallax_Gallery_Source`입니다. 현재 공유된 소스는 Gallery v0.8.2이며, Unity **6000.3.24f1**과 Built-in 렌더 파이프라인을 사용합니다.

2026-10-31 조작감 개선은 Gallery 단독판과 `Parallax_AllStages_v0.13.1_Source/Parallax_Starship_Source`의 Gallery·Library·Beach·Starship에 적용했습니다. 왼쪽 클릭으로 잡기/놓기, 오른쪽 버튼을 누른 채 마우스로 회전, Backspace로 잡기 취소가 가능합니다. 이동·점프·원근 배치 수치와 검증 결과는 [조작감 적용 기록](docs/controls/superliminal-control-feel.md)에 정리했습니다.

후속 물리 패키지를 반영해 운반 중 접촉 분리, 감쇠·질량 기준, CCD 복귀, 취소 복원을 보강했습니다. [물리 변경 전후 요약](docs/controls/superliminal-physics-package-update.md)과 새 실행 파일은 `Builds/PhysicsUpdate/`에서 확인할 수 있습니다.

## OS별 빌드

Unity Hub에서 위 버전의 Editor를 설치하고 로그인한 뒤, 프로젝트를 한 번 열어 에셋 가져오기를 완료합니다. 커맨드라인 빌드 전에는 같은 프로젝트를 연 Unity Editor를 종료하세요. 사용하는 OS의 Build Support (Mono)가 없다면 Unity Hub에서 추가합니다.

아래 명령은 저장소 최상위 폴더에서 실행합니다. 두 명령은 같은 Gallery 소스를 사용하며, 기존 방식처럼 빌드 전에 Gallery 씬을 코드로 다시 생성합니다. 수동 편집한 씬은 먼저 별도 씬으로 저장하세요.

| OS | 명령 | 실행 파일 |
| --- | --- | --- |
| macOS | `./scripts/build-mac.sh` | `Builds/macOS/Parallax.app` |
| Windows PowerShell | `.\scripts\build-windows.ps1` | `Builds/Windows/Parallax.exe` |

macOS 빌드는 Apple silicon과 Intel을 함께 지원하는 Universal 빌드입니다. Windows 빌드는 64비트입니다. 빌드 결과와 로그는 OS별 폴더에 보관하고 Git에서는 제외합니다. Windows 실행 파일을 공유할 때는 `Parallax_Data` 등 관련 파일이 들어 있는 `Builds/Windows` 폴더 전체를 전달하세요.

맥에서 빌드 후 실행:

```bash
open Builds/macOS/Parallax.app
```

Editor가 기본 설치 경로에 없다면 실행 파일 경로를 지정할 수 있습니다. 지정한 Editor도 **6000.3.24f1**이어야 합니다.

```bash
./scripts/build-mac.sh "/path/to/Unity.app/Contents/MacOS/Unity"
```

```powershell
.\scripts\build-windows.ps1 -UnityEditor 'C:\path\to\Editor\Unity.exe'
```

두 스크립트 모두 `UNITY_EDITOR_PATH` 환경 변수도 지원합니다. 기본적으로 macOS는 `/Applications/Unity/Hub/Editor`, Windows는 Unity Hub 및 로컬 Editor 설치 경로에서 프로젝트의 Unity 버전을 찾습니다.

Windows에서 스크립트 실행 정책 때문에 차단되면, 해당 빌드 프로세스에만 적용되는 다음 명령을 사용합니다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-windows.ps1
```

빌드가 실패하면 `Builds/macOS/build.log` 또는 `Builds/Windows/build.log`를 확인하세요. `build-result.txt`에는 Unity 빌드 결과가 기록됩니다. Unity 메뉴의 **Parallax → OS별 빌드**에서도 같은 빌드를 실행할 수 있습니다.

## 명령 검증

실제 Unity를 실행하지 않고 경로·인수 전달, 출력 분리, 실패 처리를 검증합니다.

```bash
python3 -m unittest discover -s tests -v
```

PowerShell 7의 `pwsh`가 있으면 Windows 명령도 함께 검증합니다. 별도 경로에 있다면 `PWSH_BINARY` 환경 변수로 지정하세요. 이 검증은 실제 Unity 컴파일·플레이 검증을 대체하지 않습니다.
