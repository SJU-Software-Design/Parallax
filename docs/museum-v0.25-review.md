# 미술관 v0.25 실행·검토

저장소 최상위의 `미술관_실행.cmd`로 Gallery와 기존 Library 씬을 함께 빌드합니다. Unity 6000.3.24f1에서 `projects/all-stages`를 열어 Gallery 씬을 실행할 수도 있습니다.

## 플레이 동작

- 니케: 고정 조명 앞에서 조각상을 움직여 그림자를 맞추고 내려놓습니다. 조각상이 캔버스로 흡수되면 팀원의 그림이 대각선 붓질로 2.6초 동안 그려지고, 완성 액자가 튀어나옵니다. 액자의 크기를 맞춰 문 중앙 홈에 끼우면 그림과 문이 양쪽으로 열립니다.
- 얼굴: 양쪽 벽의 메이 웨스트 원작과 설치 사진을 참고해 눈·코·입을 배치합니다. 완성한 얼굴을 1초 바라보면 흡수 2.5초, 붉은 조명 전환 2초, 미소 3초, 커튼 개방 4.5초로 이어집니다. 이 방은 천창을 닫고 이동 바닥을 직사광·반사광·장식 조명에서 제외했습니다.
- 성당: 충분히 키운 조형물을 내려놓으면 두 손이 2.2초 동안 벌어집니다. 통로 폭은 이전보다 약 21.4% 넓습니다. 화면 진동은 첫 0.25초 동안 커지고, 개방 중 유지되다가 손이 멈춘 뒤 0.8초 동안 잦아듭니다. 문턱은 완전 개방 후 통과할 수 있으며, 계단을 올라 손 사이로 들어가면 기존 Library로 이동합니다.

R은 현재 구역, Shift+R은 전체 초기화입니다. 성당의 진동은 초기화·다시 집기·일시정지 때 해제됩니다. 사운드는 이번 변경에 추가하지 않았습니다.

## 검증

Windows 빌드는 성공했으며 오류와 경고는 각각 0개입니다. 검증 결과는 [v0.25.0 결과 JSON](../projects/all-stages/Documentation/Gallery/Verification/v0.25.0-results.json)에 기록했습니다.

| 실행 인자 | 확인 범위 |
| --- | --- |
| `-gallery-cathedral-rumble-test` | 실제 2.2초 개방·0.8초 여진, 충돌·포털 폭 동기화, 카메라 누적 방지, 조기 진입 차단과 초기화 |
| `-gallery-mechanics-test` | 눈·코·입의 입력 조립, 실제 계단·손문 통과, Library 씬 이동 |
| `-gallery-cathedral-test` | 실제 스캔, 도서관 영상·시차·가림, 포털 리소스 정리 |
| `-gallery-comfort-test` | 얼굴 오차 허용과 성당 크기·안착 보조, 기존 청동상 구역 위험 유지 |
| `-gallery-portrait-performance-test` | 얼굴 연출 중 실제 걷기, 빛 노출·리스폰 없음, 중간·전체 초기화 |
| `-gallery-shadow-gameplay-test` | 기존 청동상·니케의 잡기·운반·크기 변경·놓기 |

빌드된 실행 파일에 위 인자와 `-validationDir <결과 폴더>`를 전달합니다. Editor에서는 `ShadeLink.Editor.GalleryMuseumBuildChecks.Run`, `ShadeLink.Editor.GalleryCathedralPlacementChecks.Run`, `Parallax.Editor.SourceLayoutValidation.Run`을 확인했습니다.

검증은 명시한 준비 배치·시작 위치를 사용하는 통합 검사입니다. 걷기와 충돌·조명·진입·초기화는 실제 게임 경로를 사용하며, 무보조 전체 공략을 뜻하지 않습니다.

## 변경 범위와 작품 출처

Gallery 소스·저장 씬·필요 생성 에셋과 문서를 포함합니다. 공통 코드에는 니케 조각상과 액자의 표시를 위한 `PlacementSupport.PreviewWhileHeld` 옵션을 추가하며 기본값은 기존 동작을 유지하는 `true`입니다.

팀원 PR #18의 미술관 니케 그림·액자 개선을 기존 붓질과 통합했습니다. 해당 PR의 튜토리얼 변경은 포함하지 않았습니다. 다른 스테이지와 공통 이동·투영 코드는 변경하지 않습니다.

작품·사진의 출처와 권리 정보는 [MuseumArt-Credits.md](../projects/all-stages/Documentation/Gallery/MuseumArt-Credits.md)에 기록했습니다. 조각 스캔과 도서관 자산 고지는 기존 Gallery 크레딧과 빌드의 `Museum-Credits.txt`에 유지합니다.
