# 디케 균형 전시실과 해변 출구

니케 전시실과 눈·코·입 전시실 사이에 22 × 17m 균형 전시실을 추가한다. 병은 출구 왼쪽 벽 앞, 디케 동상과 저울은 오른쪽 벽 앞, P02 베르사유 대리석 검 받침은 방 중앙에 놓인다.

병을 일반 원근 조작으로 확대하고 기울여 크기가 유지되는 법전을 꺼낸다. 법전을 오른쪽 접시에 올려 왼쪽 검과 균형을 2초 유지하면 동상이 빛나고 손의 검이 실제 중력으로 떨어진다. 검을 집어 중앙 받침에 꽂으면 1.5초 삽입, 짧은 진동, 양개문 개방으로 이어진다. 병 전체를 저울에 올리는 시도는 안내와 UI 불가음으로 거절한다.

고정 전시물은 실제 표면 충돌을 사용한다. 문·저울·움직이는 퍼즐 물체의 기존 충돌은 보존하고, 아치 통로와 검 낙하 경로를 비워 둔다. 병의 반복 바닥 충돌음은 최소 1.10초 간격과 접촉 법선 속도 기준으로 제한하며, 다시 집어 떨어뜨린 첫 충돌은 즉시 재생한다. 내부 법전–유리 접촉과 다른 사물의 소리는 별도다.

마지막 손 사이에는 현재 저장된 Beach 씬에서 추출한 실시간 해변 풍경이 보인다. 시점 이동과 손 확대에 따라 원근이 달라지고, 손을 통과하면 실제 Beach 씬의 첫 체크포인트로 이동한다. 도서관 원본은 보존한다.

## 실행과 빌드

Unity 6000.3.24f1에서 `projects/all-stages`를 열고 저장된 `Assets/Parallax/Gallery/Scenes/Gallery.unity`를 재생한다. 이 씬은 추가 전시실을 활성화하는 설정을 저장한다. 테스트에서 새로 만드는 일반 미술관 fixture의 기본값은 비활성이다. 기존 `-gallery-bottle-balance` 및 이전 배포본의 제품명 활성화 방식도 호환한다.

기존 저장 씬과 베이크 조명을 재생성하지 않고 Windows 실행본을 만들려면 다음을 사용한다.

```text
Unity.exe -batchmode -quit -projectPath <projects/all-stages 경로> -executeMethod ShadeLink.Editor.MuseumBottleBalanceBuild.ValidateAndBuild -bottleBalanceOutput <새 출력 폴더> -logFile <빌드 로그>
```

출력 옵션을 생략하면 프로젝트 내부 `Builds/MuseumBottleBalance/<buildId>` 아래에 빌드와 결과가 저장된다. 이 빌더는 저장된 Gallery와 Beach 두 씬, 충돌 메시 사본, 현재 해변 프리뷰, 선택 음원과 크레딧을 준비하고 씬·조명·설정 해시 보존을 검사한다. 일반 미술관 빌드와 스테이지 런처도 해변 목적지와 같은 크레딧 복사를 사용한다.

## 검증 명령

빌드한 실행 파일에 아래 인자와 `-validationDir <결과 폴더>`를 전달한다.

- `-gallery-dike-sword-test`: 병 확대와 법전 추출, 실제 검 낙하·회수·취소·삽입, 문 개방, 효과음, 중앙 받침 옆 통과.
- `-museum-beach-integration-test`: 고정물과 실제 플레이어 충돌, 병 낙하·반복음 제한·재집기, 해변 프리뷰 시차, 실제 손 통과와 Beach 로드·이동·도착 카메라 렌더.
- `-gallery-cathedral-test`: 작은 손과 확대한 손의 실제 카메라 화면, 해변 재질과 색상, 시차, 가림, 비활성화·재활성화 정리.

이 검사는 명시한 배치 fixture를 사용하는 실제 PhysX·렌더·음향 이벤트 통합 검사다. 연속 수동 완주나 사용자의 최종 미감·믹스 승인을 의미하지 않는다.

## 에셋 출처

디케·P02의 CC BY 4.0 저작자·출처·라이선스·변경 사실과 검·법전 등 CC0 출처는 `MuseumDike-assets.md`, `MuseumBottleBalance-Credits.txt`, 각 `ThirdParty` 폴더의 `CREDITS.txt`에 기록한다. 선택 효과음 13개의 편집·해시·출처는 `MuseumDikeAudio-selection.json`과 `MuseumDikeAudio-Credits.txt`에 있다. 해변 프리뷰는 현재 해변의 원래 텍스처를 사용하며 외부 에셋의 기존 출처·약관을 유지한다. 실행본에도 해당 크레딧을 복사한다.
