# 균형 퍼즐의 열쇠 보상 — 2026-11-13

균형 완료 후 중앙 표시부 덮개가 아래로 80도 열리고, 트레이가 30cm 앞으로 나옵니다. 열쇠 높이는 약 1.65m입니다. 서 있는 상태에서 열쇠를 클릭해 고정 크기로 들고 이동하고, 기존 호두나무 문의 오른쪽 손잡이 아래 황동 열쇠구멍(중심 높이 1.25m)을 클릭하면 자동 정렬·삽입·90도 회전 후 잠금이 해제됩니다. 이 보상 열쇠는 별도의 원근 크기 퍼즐이 아닙니다.

`GalleryBottleBalanceKeyReward`는 중앙 덮개·트레이·열쇠·장식판만 소유합니다. 균형 조건과 문 개방 애니메이션은 Course가 제어하며, `Unlocked` 전에는 출구 문이 열리지 않아야 합니다. `CompleteBalance()` → `Tick(dt)` → `TryInteract(camera)` 흐름을 사용하고, 구역 초기화 시 `Reset()`, Backspace 시 `Cancel()`을 호출합니다. 취소하면 열쇠가 트레이로 돌아갑니다. 벽이나 다른 물체에 가린 열쇠와 열쇠구멍은 클릭되지 않습니다.

외부 열쇠는 **Savino의 Antique key(CC0 1.0)**입니다. 원본 OBJ(304면)를 수정하지 않고 보존했으며, 길이 18cm 정규화와 기존 미술관 황동 재질만 런타임에 적용합니다. `RewardAntiqueKey.obj.meta`의 `isReadable: 1`은 런타임에서 모델의 고리/날 방향을 확인하기 위해 필요합니다.

- 원작자 자료: https://opengameart.org/content/antique-key
- 접근 가능한 공식 미러: https://lpc.opengameart.org/content/antique-key
- 원본: https://lpc.opengameart.org/sites/default/files/low_2.obj
- 라이선스: https://creativecommons.org/publicdomain/zero/1.0/
- SHA256: `b8b62e4cda17c18bf5066a689d17cc4981b4046d4aecb8fe64babcb28ebf4d2a`

앞서 도안에서 보여 준 **plaggy CC0 Keys는 실제 사용하지 않았습니다.** 원작자 페이지의 무료 다운로드가 Sellfy 체크아웃으로 연결되어 익명 원본 파일을 확보하지 못했습니다. 이메일 입력·주문·결제는 수행하지 않았고, 구현은 사전 지정된 무료 대안인 Savino 모델로 변경했습니다. 이 모델 변경은 사용자에게 별도로 고지해야 합니다.

열쇠구멍 장식판은 도안 검토 때 만든 **직접 제작한 3D 시제품**입니다. 판 외형은 18×8cm, 두께 약 6mm이며, 원형 상부와 세로 슬롯을 합친 실제 관통형 구멍과 나사 2개가 있습니다. 새 원본 OBJ/MTL을 Resources에 동일 해시로 복사했습니다. 외부 상용 에셋이나 무료 다운로드 에셋으로 표기하지 않습니다.

출처·원문 HTML·해시·크레딧은 `Assets/Parallax/Gallery/ThirdParty/BottleBalanceKeyReward/`에 있습니다.

사운드는 이번 구현에서 추가하거나 재생하지 않습니다. 추후 연결할 이벤트 `HatchOpened`, `TrayExtended`, `KeyTaken`, `KeyInserted`, `KeyTurned`, `LockReleased`만 노출합니다. 기존 사운드 선정 상태를 변경하지 않습니다.
