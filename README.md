# Parallax

최신 통합본은 **0.29.2 · 튜토리얼 → E01 엘리베이터 → 미술관 → 해변 → 해적선 출항·엔딩**입니다.
원근으로 바위를 키워 수중 천막 다리를 인양하고 실제 그늘을 건너 해적선에 승선합니다. 출항 효과음과 **HB02 Wanderlust** 전환·비행·암전까지 포함합니다.

## Windows에서 바로 플레이

[Windows 실행본 다운로드](https://github.com/SJU-Software-Design/Parallax/releases/tag/complete-harbor-preview-20261117)에서 **Parallax-CompleteHarbor-Windows-x64.zip**을 받으세요.
ZIP 전체를 새 폴더에 압축 해제한 뒤 **Parallax.exe**를 실행하면 튜토리얼 첫 방에서 시작합니다. Unity 설치는 필요 없습니다. exe만 따로 옮기지 말고 `Parallax_Data`, `MonoBleedingEdge`, `UnityPlayer.dll`과 크레딧 파일을 함께 두세요. 이 배포는 통합 PR 미리보기입니다.

## 소스를 받아 빌드·실행

Unity **6000.3.24f1 / Built-in / Mono + Windows Build Support**가 필요합니다. 통합 소스는 **projects/all-stages**입니다.
저장소 루트의 **[run-complete.cmd](run-complete.cmd)**를 더블클릭하면 별도 생성 프로젝트에서 세 맵을 준비하고 빌드한 뒤 실행합니다. 성공한 동일 소스 빌드가 있으면 재사용합니다. 처음 임포트·빌드는 시간이 걸립니다.

```powershell
.\scripts\run-complete.ps1
.\scripts\run-complete.ps1 -BuildOnly
.\scripts\run-complete.ps1 -Rebuild -UnityEditor 'X:\Unity\6000.3.24f1\Editor\Unity.exe' -WorkspaceRoot 'X:\ParallaxBuilds'
```

- 튜토리얼: 원근 조작·발판·그늘 수업 → E01 엘리베이터로 미술관 이동.
- 미술관: 니케 → 병·법전·디케 저울과 검·P02 받침 → 눈코입 → 손 포털로 해변 이동.
- 해변: 태양광 수신기 → 물길·그늘 → 그림자 배 조립·승선 → 바위·도르래로 수중 천막 다리 인양 → 해적선 갑판에서 E → 출항·비행·END.
- **Esc → 맵 이동**에서 튜토리얼 / 미술관 / 해변으로 이동합니다. WASD 이동, 마우스 시점, 좌클릭 잡기·놓기, 우클릭+마우스 회전, Space 점프입니다.

상세 빌드·검증 범위·출처는 **[통합 코스 안내](docs/CompleteCourse.md)**를 확인하세요. 소스/실행본은 기존 미술관·튜토리얼 UI를 사용하며 별도 UI 시안 PR은 포함하지 않습니다. 팀 제공 TEAM11/TEAM12의 원 출처·라이선스 미확인 상태 및 기존 작품 권리 고지는 보존했습니다.

---
## 이전 버전 기록 · 레거시 실행 경로

아래는 과거 Gallery 단독판·Library/Starship 연결 및 당시 빌드/검증 이력입니다. 현재 세 맵 통합 실행은 위의 run-complete.cmd를 사용하세요. 기존 기록과 이전 명령은 보존합니다.

미술관 최신 소스는 **v0.25.0**입니다. 마지막 성당 조형물의 손 사이 통로를 약 21% 넓히고, 손이 2.2초 동안 천천히 벌어지도록 바꿨습니다. 개방 초반 0.25초 동안 화면 진동이 커지고, 손이 멈춘 뒤에는 0.8초 동안 여진이 잦아듭니다. 기존 얼굴 커튼 연출·안전한 벽 조명·니케 그림과 액자 열쇠·도서관 연결을 유지합니다. 자세한 조작은 통합 프로젝트 README를 확인하세요.

해변맵 최신 소스는 **v0.16.1**입니다. 모래성 목표가 보이는 열린 해변, 태양광 센서, 그림자 조립 보드와 직접 운반하는 그늘 배, 두 성벽 조각과 파라솔로 오르는 마지막 퍼즐을 포함합니다. Unity에서 `projects/all-stages/Assets/Parallax/Beach/Scenes/Beach.unity`를 열거나 Windows에서 `해변_실행.cmd`를 실행하세요. 첫 실행은 설치된 Unity 6000.3.24f1로 저장된 씬을 빌드합니다. [해변 플레이·검증 안내](projects/all-stages/README.md#해변-v0161).

이전 Gallery 조작 수정판 **v5**에서는 뒤로 이동할 때 크기 상한 때문에 빈 공간에서 거리가 고정되던 문제를 수정했습니다. [영상 원인 분석과 수정](docs/controls/held-backward-v5-review.md). 물체를 잡으면 거리 제한 원인과 지점을 표시하며 **F3**으로 켜고 끕니다. [진단 화면 읽는 법](docs/controls/held-distance-diagnostics.md).

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
