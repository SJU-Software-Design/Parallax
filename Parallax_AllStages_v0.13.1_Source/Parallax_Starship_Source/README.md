# Parallax v0.13.1 — 우주선 종합 퍼즐과 엔딩

1인칭 원근·그림자 퍼즐 게임입니다. 사물을 잡고 이동·점프하면서 크기와 방향을 바꿀 수 있습니다. **운반 중에는 보호 효과가 없고 내려놓아 0.35초 안착한 뒤 실제 그늘과 발판이 활성화됩니다.** 네 스테이지는 현재 별도 실행합니다.

Unity **6000.3.24f1 / Built-in / Windows 64비트** 프로젝트입니다. Unity에서 씬을 열고 Play하거나 작업 폴더의 `윈도우/우주선스테이지실행.cmd`를 실행합니다. 기존 세 스테이지에도 각각 실행 바로가기가 있습니다.

| 스테이지 | 시작 씬 | 빌드 폴더 |
| --- | --- | --- |
| 우주선 엔딩 | Assets/Parallax/Starship/Scenes/Starship.unity | Parallax_Unity_v0.13.1_Starship |
| 미술관 | Assets/Parallax/Gallery/Scenes/Gallery.unity | Parallax_Unity_v0.12.0_MirrorGallery |
| 도서관 | Assets/Parallax/Library/Scenes/Library.unity | Parallax_Unity_v0.12.0_Library |
| 해변 | Assets/Parallax/Beach/Scenes/Beach.unity | Parallax_Unity_v0.12.1_Beach |

Windows 빌드는 작업 폴더의 `윈도우/실행파일`, Mac 빌드는 `맥/실행파일`에 생성됩니다. Windows 공유 ZIP은 `윈도우/공유용ZIP`, Unity 소스 ZIP은 `유니티/공유용_소스`에 저장합니다. 최신 공유 소스 ZIP은 `Parallax_Starship_v0.13.1_Source.zip`이며 빌드 캐시를 제외하고 네 씬과 에셋·라이선스·도구를 포함합니다.

우주선 v0.13.1은 요청한 텐키 순서 5 → 2 → 6 → 8 → 4의 방 배치와 90도씩 바뀌는 진행 방향을 사용합니다. 외부 항성광은 고정되어 있습니다. 화물 확대·그림자, 반사판과 두 센서, 랙 모듈 분리, 부력·물살·재사용 발판을 종합했습니다. EXIT를 확대해 검은 복도로 들어간 뒤 에어록을 지나 우주로 떠오릅니다. 조작은 친구의 v0.12.1 HoldFix P1 소스를 이식해 접촉 후 회복과 확대 의도 보존을 적용했습니다.

해변 v0.12.1은 확대된 사물이 물속 깊이 제한에 걸려 움직이지 않던 문제를 고쳤습니다. 수로 바닥을 추가해 뜨지 않는 사물도 계속 가라앉지 않고 회수할 수 있습니다. 물 접촉 시 플레이어 복귀, 부력·물살 운반, 운반 중 그림자 제한은 유지합니다. [물속 회수 변경·검증](Documentation/Beach/물속회수수정.txt). 미술관·도서관 실행본은 v0.12.0입니다.

## 조작과 규칙

WASD 이동 / 마우스 시점 / Space 점프 / 좌클릭 잡기·놓기 / 우클릭 취소.
Q/E 회전 / Z/X 기울기 / C 바로 세우기 / H 구역 힌트 / T 설치 연습 안내.
R 구역 초기화 / Shift+R 전체 초기화 / Esc 메뉴.
가까운 표면을 보면 작아지고 먼 표면을 보면 커집니다. 마우스 휠로 크기를 조절하지 않습니다.

미술관·도서관·해변·우주선은 `Assets/Parallax/Shared/Scripts/ObjectInteraction.cs`의 잡기·놓기·취소 처리를 함께 사용합니다. 대상 선택과 배치 표면 탐색은 `InteractionRaycast.cs`를 사용하며 거리 상한이 없습니다. 보이는 먼 사물도 다시 잡을 수 있습니다.

배치 깊이는 공통 `PerspectivePlacement.cs`에서 계산합니다. 잡은 물체의 카메라 기준 방향과 화면상 크기를 유지하면서, 복합 BoxCollider의 보이는 면 전체에 화면 간격 기준으로 레이를 분포합니다. 각 레이에서 물체의 뒤쪽 면과 배경 사이의 간격을 검사해 겹치지 않는 가장 먼 깊이를 구하며, 물체의 빈 틈은 검사 면에서 제외합니다. 최종 위치는 전체 충돌 부피로 다시 확인합니다. 아주 얇은 구조물은 유한한 레이 간격 사이로 빠질 수 있어 최종 충돌 검사와 기존 접촉 복구를 함께 사용합니다. Q/E, Z/X로 직접 회전하면 화면 모양은 달라질 수 있습니다. 벽의 가림, 사물 크기 범위, 맵 경계, 충돌 검사와 각 스테이지의 퍼즐 규칙은 유지합니다.

[설치 규칙과 예외](Documentation/Placement/설치규칙안내.txt) · [검증 결과와 범위](Documentation/Placement/검증결과.txt) · [공통 코드 설명](Documentation/Placement/코드길잡이.md).
[해변 안내](Documentation/Beach/플레이안내.txt) · [미술관 안내](Documentation/Gallery/플레이안내.txt) · [도서관 안내](Documentation/Library/플레이안내.txt).
[우주선 안내](Documentation/Starship/플레이안내.txt) · [우주선 검증 범위](Documentation/Starship/검증결과.txt) · [변경 출처](Documentation/Starship/provenance.json).

## 빌드와 검사

공통 상호작용 검사는 Unity Editor를 닫은 상태에서 아래처럼 실행합니다. Editor를 계속 사용할 때는 `Assets`, `Packages`, `ProjectSettings`를 별도 폴더에 복사하고 `-projectPath`에 그 경로를 지정하세요. 검사는 임시 씬에서 실제 물리 컴포넌트로 네 맵의 먼 거리 잡기·배치, 놓기·취소와 충돌 차단을 확인하고 JSON 결과를 남깁니다. 화면 크기 유지, 빠른 시점 회전, 좁은 기둥, 면 안쪽 돌출부, 복합 물체의 빈 틈, 최소 크기로도 배치 불가능한 벽과 검사 횟수 기록을 포함한 53개 항목을 검사합니다.

```sh
"/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics -quit -projectPath "$PWD" \
  -executeMethod ShadeLink.Editor.ObjectInteractionTests.Run \
  -interactionReport /tmp/parallax-interaction-results.json \
  -logFile /tmp/parallax-interaction.log
```

```powershell
.\Tools\Build\Build-Starship.ps1
.\Tools\Validation\Test-Starship.ps1
.\Tools\Build\Build-Placement.ps1
.\Tools\Validation\Test-Placement.ps1 -Stage Beach
.\Tools\Validation\Test-Placement.ps1 -Stage Gallery
.\Tools\Validation\Test-Placement.ps1 -Stage Library
.\Tools\Validation\Test-Beach.ps1
.\Tools\Validation\Test-Beach-Water.ps1
.\Tools\Validation\Test-Gallery-Mirror.ps1
.\Tools\Validation\Test-Library.ps1
```

사람의 플레이로 최종 난이도와 힌트 이해도를 확인할 필요가 있습니다. 우주선·해변 자동 검사는 기믹별 준비 배치를 사용합니다. 우주선 연결 복도와 엔딩은 일반 이동으로 검사합니다. 프레임과 지연의 정식 성능 측정은 별도입니다.

에셋별 원래 라이선스와 제작자는 `Documentation/ThirdParty` 및 `Assets/ThirdParty`에 보존합니다. 우주선은 Kenney Space Station Kit의 CC0 모델을 사용하며 원본 라이선스는 `Assets/ThirdParty/KenneySpaceStation/License.txt`에 있습니다.

[v0.11.0까지의 개발 기록](Documentation/Legacy/README-through-v0.11.0.md)은 이전 버전 근거입니다.
