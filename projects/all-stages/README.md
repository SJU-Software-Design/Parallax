# Parallax — 전체 스테이지

Unity **6000.3.24f1**, Built-in, Mono. 이 폴더를 Unity Hub에서 엽니다.

| 스테이지 | 씬 |
| --- | --- |
| Gallery | Assets/Parallax/Gallery/Scenes/Gallery.unity |
| Library | Assets/Parallax/Library/Scenes/Library.unity |
| Beach | Assets/Parallax/Beach/Scenes/Beach.unity |
| Starship | Assets/Parallax/Starship/Scenes/Starship.unity |

공통 조작·투영 코드는 `Assets/Parallax/Shared/Scripts`에 있습니다. 좌클릭 잡기·놓기, 우클릭 누른 채 회전, Backspace 취소를 사용합니다.

2026-11-01: 다른 물체에 닿으면 운반 미리보기의 거리와 크기를 함께 줄이고, 멀어지면 잡을 때의 기준으로 회복합니다. 운반 중 최소 크기와 플레이어 겹침은 놓기 조건과 분리했습니다. 놓을 수 없는 미리보기는 계속 추적하면서 이유를 표시합니다. 다른 소품·벽에 대한 투영 검사와 실제 놓기 충돌 검사는 유지합니다.

전체 회귀 검사는 Editor를 닫고 `-batchmode -nographics -quit -projectPath <이 폴더> -executeMethod ShadeLink.Editor.PhysicsPackageValidation.RunAll -logFile <로그>`로 실행합니다. Windows 빌드 메뉴는 `Parallax` 아래에 있으며, 저장소에서는 `builds/windows/all-stages/`에 출력됩니다. 프로젝트만 별도 배포한 경우 `Builds/windows/`를 사용합니다.

상세 변경·검증: [저장소 문서](../../docs/controls/held-contact-v2-fix.md). 이전 안내는 [기록](Documentation/Legacy/README-through-v0.13.1.md)에 보존했습니다. 에셋 저작자·라이선스는 `Assets/ThirdParty`와 `Documentation/ThirdParty`에 있습니다.
