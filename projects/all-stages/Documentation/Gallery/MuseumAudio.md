미술관 선택 오디오

GalleryGame이 미술관 루트에 MuseumAudio를 설치한다. 이동·물체·니케·초상·손의 효과음과 C01 BGM을 해당 미술관에서 재생한다. 다른 스테이지의 기존 이동/물체 오디오 경로는 유지한다.

음원과 Unity 임포터 설정은 Assets/Parallax/Gallery/Resources/MuseumAudio/Selected에 있다. 실제 배포 음원은 효과음20개와 BGM1개이며, 원작자·원본 URL·라이선스·편집내역은 MuseumAudio-Credits.txt, 파일별 SHA256은 MuseumAudio-Manifest.json에 기록했다. 실행본을 빌드할 때 기존 미술관 에셋 크레딧과 오디오 크레딧을 함께 복사한다.

| 상황 | 선택 번호 |
| --- | --- |
| 발걸음 / 착지 | 81 / 72 |
| 잡기 / 놓기 | 128 / 156 |
| 목재: 가벼움 / 중간 / 무거움 | 5 / 8 / 132 |
| 금속: 가벼움 / 중간 / 무거움 | 11 / 14 / 157 |
| 무거운 돌 접촉 | 139 |
| 니케 조건 충족 / 붓질 / 문 개방 | 53 / 146 / 161 |
| 초상 전체 연출 | 151 |
| 손 벌어짐 / 마지막 쿵 | 58 / 166 |
| UI 확인 / 취소 / 불가 | 42 / 142 / 145 |
| BGM | C01 Hard Boiled — Kevin MacLeod |

발걸음·착지·잡기·접촉음은 각각 독립된 AudioSource로 재생한다. 다른 소리가 시작되거나 사물을 다시 잡아도 기존 음원의 clip/pitch를 바꾸지 않는다. 사용 중인 voice를 빼앗지 않으며, 게임 일시정지는 재생 위치를 보존한다. UI는 메뉴에서도 재생한다. 실내 효과음에는 짧은 잔향을 적용하고, BGM 반복 경계는 두 소스로1.5초간 교차 재생한다.

초상 연출은 흡수 시작을0초로 계산한다.

| 시점 | 동작 |
| --- | --- |
| 0~2초 | BGM을 서서히 줄인 뒤 Pause |
| 2~2.5초 | 짧은 정적 |
| 2.5초 | 빨간 조명이 시작되고151을 한 번 재생 |
| 4.5초 | 미소 시작.151은 이어서 재생 |
| 7.5초 | 커튼 개방 시작 |
| 12초 | 커튼 완전 개방과9.5초 효과음 종료 |
| 실제 checkpoint3 진입 | 보존한 BGM 위치에서0.5초간 재개 |

151은 원래 녹음의 첫0.12초를 보존하고 음높이를 유지하는 편집으로9.5초로 늘렸다. 끝0.32초를 완화했다. 짧은 녹음의 긴 연장은 질감에 영향을 줄 수 있으므로 최종 분위기와 음량은 플레이 중 확인한다. 손58은 사용자 선택 H0의 약0.7387배 pitch와 기존2.2초 동작을 유지하고, 벌어짐 gain을 약3dB 높였다. 종료166의 음량은 유지했다.

기존 액자 튀어나옴의 퓽은 이전 파형 후반부를 그대로 유지한다. 작은 돌 접촉·커튼·부품 자리맞춤·일반 완료·원근 확대/축소·점프 시작에는 새 음원을 넣지 않았다. 조건부57은161과 겹치는 긴 문 녹음이어서 bank에 포함하지 않는다.

Unity6000.3.24f1에서 저장된 Gallery 씬을 사용한다. 맵이나 조명을 재생성하지 않는다. 일반 저장소 clone에서도 아래 진입점으로 검증하고 Windows 실행본을 만들 수 있다. UnityEditor 실행 파일 경로와 새 출력 폴더를 환경에 맞게 지정한다.

```text
Unity.exe -batchmode -quit -force-d3d11 -projectPath "<repo>/projects/all-stages" -executeMethod ShadeLink.Editor.MuseumSelectedAudioBuild.ValidateAndBuild -museumAudioOutput "<fresh-output-directory>"
```

출력 옵션을 생략하면 프로젝트의 Builds/MuseumSelectedAudio/<buildId>에 저장한다. 해당 경로는 Git에서 제외된다. 기존 실행본이 있는 출력 폴더에는 덮어쓰지 않는다. 클립 preload/임포터·기존 이동/물리/조작·신규 오디오 회귀 검사를 수행하고, Gallery와 기존 Library 목적지를 포함해 빌드한다. 임시 제품명/그래픽 설정은 복원한다.

```text
Parallax.exe -gallery-selected-audio-test -museumAudioOutput "<validation-directory>" -logFile "<validation-directory>/player.log"
Parallax.exe -gallery-cathedral-rumble-test -validationDir "<hand-validation-directory>"
```

오디오 검증은 실제 실행본의 DSP 시계·음원 겹침·Pause/Resume·2초 페이드·2.5초 Red 이벤트·9.5초 자연 종료·실제 CharacterController 커튼 통과·초기화·BGM 루프를 검사한다. 공개 정답 배치, 이동 cue 상태, 곡 끝 seek fixture를 사용한다. 주관적 청취나 최종 믹스를 자동 검증으로 대신하지 않는다. PR 작업 폴더의 실제 결과는 MuseumAudio-Validation.json에 요약한다.
