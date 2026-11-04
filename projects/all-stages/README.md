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

## 미술관 v0.22.0

명화와 실제 조각 스캔을 전시한 네 구역으로 구성합니다. 첫 방은 접힌 청동 조형물로 햇빛을 가립니다. 둘째 방은 고정 조명 앞에서 원본 니케 조각을 옮기고 원근으로 크기를 바꾸어 벽의 윤곽에 그림자를 겹칩니다. 목표 윤곽은 원본 전체 메시를 4096px로 투영해 만들고, 내려놓아 안정된 상태로 약 1초 맞추면 같은 메시·같은 시점의 회화풍 이미지가 약 2.6초 동안 좌상단에서 우하단으로 붓결을 따라 나타납니다. 그림이 완성되면 유리문이 열리고, 벽의 명화 컬렉션에 걸린 모네 작품을 옮겨 출구까지 실제 그늘을 만듭니다. 니케를 옮긴 뒤에도 완성된 그림과 열린 문은 유지됩니다.

고정 조명은 잡을 수 없으며 조작 가능한 사물은 총 7개입니다. 실제 비스콘티의 1919년 니케 유화는 옆 벽의 전시 작품이고, 퍼즐의 완성 그림은 원본 3D 스캔에서 생성한 이미지입니다. 눈·코·입 방은 중앙 천창 폭 4.4m와 양옆 그늘 통로를 유지합니다. 커튼이 열리면 완성한 얼굴은 그대로 두고 옆 통로로 이동합니다. 얼굴·로댕 〈성당〉 출구의 편의성 보정과 공통 조작도 유지합니다. 두 손 사이를 통과하면 낙하 연출을 거쳐 저장된 Library 씬으로 이어지며 바람 소리는 없습니다.

v0.22.0은 Unity 6000.3.24f1 Windows 빌드 오류·경고 0입니다. 청동·니케·모네 실제 잡기·원근 조작·놓기와 두 방의 보행 검사를 2,245틱 통과했습니다. 정상 경로는 재시작과 빛 노출이 없었고, 열린 문도 중앙에서 약 1.245m 비켜 실제로 통과했습니다. 모네 그늘 없이 출구로 걷는 실패 검사는 의도대로 한 번 재시작되었습니다. 네 구역 통합 검사 3,977틱, 편의성 검사 7개 묶음, 포털의 실제 6개 시점과 원근·뒷배경 가림·무음·해제 및 재생성 검사도 통과했습니다. 검사는 접근 위치 준비와 경로 탐색 보조를 포함하며 무보조 전체 퍼즐 공략이나 GPU·CPU 픽셀 일치 검증을 뜻하지 않습니다. 저장된 Library 씬의 해시는 빌드 전후 동일합니다.

Windows에서는 저장소 최상위의 `미술관_실행.cmd`를 실행합니다. 처음에는 Unity 6000.3.24f1로 미술관을 준비하고 저장된 도서관 씬과 함께 빌드합니다. 도서관 씬은 재생성하지 않습니다. Unity에서는 Gallery 씬을 열고, 필요한 경우 **Parallax → 미술관 씬 준비**를 실행합니다. 앞의 두 구역은 빛 노출 구간이고, 시점·출구 구역에서는 안전하게 조작할 수 있습니다. **H**는 현재 힌트, **R**은 현재 구역 초기화입니다.

[상세 실행·조작·작품 출처·검토 기록](../../docs/museum-v0.20-review.md)을 확인하세요. 〈성당〉은 Migg의 CC BY 4.0 스캔, 니케는 Cosmo Wenman의 CC BY 3.0 석고 캐스트 스캔입니다. 세부 고지는 `Documentation/Gallery`와 빌드의 `Museum-Credits.txt`에 보존합니다.

## 해변 v0.16.1

- 목표 모래성과 깃발이 입구에서 보이도록 시야를 열고, 해변 소품·물 표현·파라솔과 태양광 수신기를 정리했습니다.
- 첫 물길은 방향 흐름 없이 판자를 놓는 구간입니다. 이어지는 길은 작은 섬과 그늘 배치로 변화를 줬습니다.
- 세 번째 구간은 세워진 보드의 윤곽에 선체·차양 그림자를 맞춥니다. 완성된 같은 배가 작아져 작업대에 떨어지면 직접 잡고 원근으로 확대해, 실제 차양 그림자 아래에서 타고 이동합니다. 조각 잡기 중심과 놓을 때의 작은 정렬 보정도 적용했습니다.
- 마지막 모래성은 빠진 정면 계단 조각 두 개를 각각 연결합니다. 아래 고정 파라솔에서 시작해 평평한 중간 공간에 직접 파라솔을 설치하고, 두 번째 조각으로 정상에 오릅니다. 사용한 첫 조각은 회수하지 않습니다. 오른쪽 점프 우회로와 복잡한 외부 구조는 제거했습니다.
- 의자·공 등은 장식이며 필수 상호작용 사물은 11개입니다. 배 도착 문과 정상 클리어는 실제 이동·지지·그늘 판정에 따라 처리합니다.
- 공통 조작에서는 잡을 때의 월드 방향을 유지하며, 시선을 돌려도 사물이 자동 회전하지 않습니다. 회전은 명시적인 회전 조작으로 바꿉니다.

Unity에서 위 표의 Beach 씬을 열고 Play를 누르면 시작합니다. Windows 저장소 최상위의 `해변_실행.cmd`는 저장된 씬을 별도 `Builds/StageProject`에 복사해 빌드하고 실행합니다. `해변_그림자배_체험.cmd`, `해변_모래성_체험.cmd`는 해당 구간부터 체험합니다. 실행파일은 소스 PR에 포함하지 않습니다.

해변 전용 Editor 검증 진입점은 `ShadeLink.Editor.BeachEntranceValidation.Run`, `BeachRouteValidation.Run`, `BeachBoatValidation.Run`, `BeachShadowHarborValidation.Run`, `BeachCastleValidation.Run`입니다. 네임스페이스는 모두 `ShadeLink.Editor`이며, `-batchmode -force-d3d11 -projectPath <프로젝트> -executeMethod <진입점> -shade-output <새 결과 폴더> -logFile <로그>`로 각각 실행합니다. 실제 PlayMode·렌더링 검증에는 `-nographics`와 `-quit`를 사용하지 않습니다. 이전 구간과 일부 물체 배치는 각 검사에서 명시한 준비 조건이며, 사람이 처음부터 끝까지 힌트 없이 플레이한 결과는 아닙니다.
