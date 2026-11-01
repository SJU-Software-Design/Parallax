# Parallax

최신 Gallery 실행본은 **v5**입니다. 뒤로 이동할 때 크기 상한 때문에 빈 공간에서 거리가 고정되던 문제를 수정했습니다. [영상 원인 분석과 수정](docs/controls/held-backward-v5-review.md). 물체를 잡으면 거리 제한 원인과 지점을 표시하며 **F3**으로 켜고 끕니다. [진단 화면 읽는 법](docs/controls/held-distance-diagnostics.md).

Unity **6000.3.24f1 / Built-in / Mono** 원근·그림자 퍼즐 프로젝트입니다. 개발의 기준은 **`projects/all-stages`**이며 Gallery·Library·Beach·Starship이 들어 있습니다. Gallery 단독판도 `projects/gallery`에 보존하고 같은 공통 조작 수정을 적용합니다.

## 폴더

```text
projects/
  all-stages/       네 스테이지의 주 Unity 프로젝트
  gallery/          Gallery 단독 Unity 프로젝트
builds/
  archive/          기존 배포 실행본 — 이번 소스 수정은 포함하지 않음
  windows/          새 Windows 빌드 출력
  macos/            새 macOS 빌드 출력
artifacts/
  reports/          이전 검토·검증 산출물
  validation/       이번 검사 로그와 결과, Git 제외
archive/work/       이전 제작 도구·임시 소스·진단 실행본 보존
scripts/            저장소 공통 빌드·검증 명령
tests/              빌드 명령 회귀 검사
docs/               현재 설명과 변경 기록
```

파일을 삭제하지 않고 이동했습니다. [이전→현재 경로](docs/project-layout-migration.json)를 참고하세요. Unity의 `Assets` 내부 경로와 기존 `.meta` GUID는 유지합니다.

## 이번 접촉 수정

가까운 사물이나 벽에 접근하면 **들고 있는 물체의 거리와 크기를 함께 줄이고, 멀어지면 다시 회복**합니다. 운반 미리보기의 크기 하한과 플레이어 겹침 허용은 놓기 조건과 분리했습니다. 놓을 수 없는 상태에서도 시선 추적은 계속하며, 그 이유를 표시합니다. 다른 소품과 벽은 여전히 투영 장애물이며 실제 놓기 검사는 유지합니다.

상세 원인·동작·제한·검증은 [접촉 수정 기록](docs/controls/held-contact-v2-fix.md)에 정리했습니다. 좌클릭 잡기·놓기, 우클릭 누른 채 마우스 회전, Backspace 취소를 사용합니다.

## 열기와 실행

Unity Hub에서 `projects/all-stages` 또는 `projects/gallery`를 프로젝트로 추가합니다. 네 스테이지의 씬은 `projects/all-stages/Assets/Parallax/<스테이지>/Scenes/`에 있습니다.

기존 실행본은 `builds/archive`에 보존했습니다. **`추가판_실행.cmd` 또는 `play-gallery.cmd`는 2026-11-01의 새 Gallery 수정판**, `play-starship.cmd`는 새 Starship 수정판을 실행합니다. 이전 Gallery는 `play-archive-gallery.cmd`로 실행합니다. 새 실행본은 정식 Unity Editor에서 씬·셰이더·코드를 모두 재빌드했습니다.

## 빌드

Editor를 닫고 저장소 최상위에서 실행합니다. Build Support (Mono)가 필요합니다. 기존처럼 빌드 도구가 씬을 재생성하므로 수동 편집한 씬은 별도 이름으로 저장하세요.

```powershell
# Gallery 단독판 → builds/windows/gallery/Parallax.exe
.\scripts\build-windows.ps1 -UnityEditor 'C:\path\to\Editor\Unity.exe'

# 전체 프로젝트의 Starship → builds/windows/all-stages/Parallax_Unity_v0.13.1_Starship/
.\projects\all-stages\Tools\Build\Build-Starship.ps1 -UnityEditor 'C:\path\to\Editor\Unity.exe'
```

```bash
# Gallery 단독판 → builds/macos/gallery/Parallax.app
./scripts/build-mac.sh '/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity'
```

Editor 경로는 `UNITY_EDITOR_PATH` 환경 변수도 지원합니다. 전체 프로젝트의 다른 스테이지는 각 `Tools/Build` 명령이나 Unity의 Parallax 메뉴를 사용합니다. 내보낸 Unity 프로젝트만 별도로 연 경우 빌드는 해당 프로젝트의 `Builds` 아래로 저장됩니다.

## 검증

두 Unity 프로젝트 각각에서 아래 Editor 진입점을 실행합니다.

```text
-executeMethod ShadeLink.Editor.PhysicsPackageValidation.RunAll
```

2026-11-01에 Unity 6000.3.24f1과 Windows Mono 지원을 설치하고 두 프로젝트를 정식 Editor에서 검사·빌드했습니다. Gallery 실제 실행 검사 16개는 통과했습니다. Starship은 빌드 오류가 없지만 전체 자동 진행은 상층 이동 경로에서 미통과로 남았습니다. 자세한 결과는 [정식 Windows 빌드 기록](docs/builds/windows-contact-v2.md)에 있습니다. 처음의 임시 Mono 검사 기록은 [접촉 수정 기록](docs/controls/held-contact-v2-fix.md)에 보존했습니다.

빌드 명령의 경로·출력·실패 처리는 다음으로 검사합니다. Windows에서는 PowerShell 7과 .NET Framework C# 컴파일러, macOS 명령까지 검사하려면 Git Bash가 필요합니다.

```text
python -m unittest discover -s tests -v
```

에셋 저작자·라이선스는 각 프로젝트의 `Assets/ThirdParty`, `Documentation/ThirdParty` 및 기존 실행본의 고지 파일에 보존합니다.
