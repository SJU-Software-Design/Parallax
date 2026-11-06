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

## 미술관 v0.25.0

마지막 성당 조형물은 충분히 키워 내려놓으면 두 손이 2.2초 동안 벌어집니다. 손 사이 통로는 기존보다 약 21% 넓고, 조형물·충돌·도서관 포털과 진입 판정이 같은 폭을 사용합니다. 개방을 시작할 때 0.25초 동안 화면 진동이 커지고, 개방 중에는 유지되며, 손이 멈춘 뒤 0.8초 동안 서서히 잦아듭니다. R/전체 초기화와 재집기·일시정지는 진동을 해제합니다.

눈·코·입 방은 왼쪽의 메이 웨스트 원작과 오른쪽의 설치 사진을 보고 중앙에서 얼굴을 만드는 공간입니다. 커튼 외곽은 이마·볼·턱의 윤곽으로 이어집니다. 완성한 얼굴을 1초 바라보면 조형물이 2.5초 동안 커튼으로 들어가고, 장식 조명이 2초 동안 붉어지며, 입술이 3초 동안 천천히 웃은 뒤 커튼이 4.5초 동안 열립니다.

이 방의 천창은 닫혀 있고 통로 바닥에는 직접광·반사광·장식 조명이 적용되지 않습니다. 연출 중에도 이동할 수 있으며, **R**로 조형물·조명·표정·커튼을 함께 초기화할 수 있습니다.

니케 방은 고정 조명 앞에서 조각상을 움직여 그림자 전체를 금색 윤곽 안에 넣는 퍼즐입니다. 조각상을 내려놓으면 니케가 캔버스로 흡수되고, 팀원이 만든 `NikeShadowStudy.png`가 기존의 좌상단→우하단 붓결로 약 2.6초 동안 채워집니다. 완성된 액자가 앞으로 튀어나와 내려앉은 뒤 집을 수 있습니다.

액자를 원근 조작으로 키우거나 줄여 아치문 중앙의 홈에 끼우세요. 크기는 ±14% 오차를 허용하며, 삽입하면 그림과 액자가 반으로 갈라져 양쪽 문과 함께 열립니다. 니케 방은 안전 구역입니다. 붓질 중에는 액자 집기와 문 열기가 차단되며, **R**로 그림·조각상·액자·문을 초기화할 수 있습니다.

첫 방의 청동 그림자 이동, 로댕 〈성당〉 출구와 기존 Library 연결은 유지합니다. 튜토리얼 변경은 이번 미술관 통합에 포함하지 않았습니다.

Windows에서는 저장소 최상위의 `미술관_실행.cmd`를 사용합니다. Unity 편집 경로는 `projects/all-stages`이며 Gallery 씬을 엽니다.

## 해변 v0.16.1

- 목표 모래성과 깃발이 입구에서 보이도록 시야를 열고, 해변 소품·물 표현·파라솔과 태양광 수신기를 정리했습니다.
- 첫 물길은 방향 흐름 없이 판자를 놓는 구간입니다. 이어지는 길은 작은 섬과 그늘 배치로 변화를 줬습니다.
- 세 번째 구간은 세워진 보드의 윤곽에 선체·차양 그림자를 맞춥니다. 완성된 같은 배가 작아져 작업대에 떨어지면 직접 잡고 원근으로 확대해, 실제 차양 그림자 아래에서 타고 이동합니다. 조각 잡기 중심과 놓을 때의 작은 정렬 보정도 적용했습니다.
- 마지막 모래성은 빠진 정면 계단 조각 두 개를 각각 연결합니다. 아래 고정 파라솔에서 시작해 평평한 중간 공간에 직접 파라솔을 설치하고, 두 번째 조각으로 정상에 오릅니다. 사용한 첫 조각은 회수하지 않습니다. 오른쪽 점프 우회로와 복잡한 외부 구조는 제거했습니다.
- 의자·공 등은 장식이며 필수 상호작용 사물은 11개입니다. 배 도착 문과 정상 클리어는 실제 이동·지지·그늘 판정에 따라 처리합니다.
- 공통 조작에서는 잡을 때의 월드 방향을 유지하며, 시선을 돌려도 사물이 자동 회전하지 않습니다. 회전은 명시적인 회전 조작으로 바꿉니다.

Unity에서 위 표의 Beach 씬을 열고 Play를 누르면 시작합니다. Windows 저장소 최상위의 `해변_실행.cmd`는 저장된 씬을 별도 `Builds/StageProject`에 복사해 빌드하고 실행합니다. `해변_그림자배_체험.cmd`, `해변_모래성_체험.cmd`는 해당 구간부터 체험합니다. 실행파일은 소스 PR에 포함하지 않습니다.

해변 전용 Editor 검증 진입점은 `ShadeLink.Editor.BeachEntranceValidation.Run`, `BeachRouteValidation.Run`, `BeachBoatValidation.Run`, `BeachShadowHarborValidation.Run`, `BeachCastleValidation.Run`입니다. 네임스페이스는 모두 `ShadeLink.Editor`이며, `-batchmode -force-d3d11 -projectPath <프로젝트> -executeMethod <진입점> -shade-output <새 결과 폴더> -logFile <로그>`로 각각 실행합니다. 실제 PlayMode·렌더링 검증에는 `-nographics`와 `-quit`를 사용하지 않습니다. 이전 구간과 일부 물체 배치는 각 검사에서 명시한 준비 조건이며, 사람이 처음부터 끝까지 힌트 없이 플레이한 결과는 아닙니다.
