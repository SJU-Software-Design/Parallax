# Superliminal 물리 패키지 반영 — 이전 / 이후

2026-10-31. 사용자가 제공한 `/Users/seob/Downloads/Superliminal_Physics_REA_Package`의 설명서, 복원 소스, 엔진 설정과 충돌표를 현재 Parallax에 대조했다. 사용한 핵심 문서·소스·JSON 50개의 크기와 SHA256은 패키지 MANIFEST와 일치했다. 패키지의 설치·추출 명령이나 원본 어셈블리는 실행하지 않았다.

## 달라진 동작

| 이전 | 이번 수정 후 |
| --- | --- |
| kinematic으로 든 소품이 다른 동적 소품을 밀 수 있었음 | 운반 전용 `HeldProp` 레이어29로 일반 solver 접촉을 분리. Collider는 켜 두므로 투영·몸 겹침 검사는 계속 가능 |
| 잡힌 소품과 환경의 충돌 필터가 명확히 분리되지 않았음 | 환경 투영·선택 mask와 물리 접촉 matrix를 분리. 벽·일반 소품은 여전히 배치거리를 제한 |
| 일반 소품에 감쇠0.03/0.15와 전시물 접촉 재질0.7/0.6을 일괄 적용 | 새 일반 Rigidbody는 감쇠0/0.05·보간None·최대 각속도7·solver6/1. 미지정 마찰은 엔진 fallback, 기존 명시적 재질과 Rigidbody 설정은 보존 |
| 기존 Rigidbody가 할당된 소품은 초기화가 일부 생략되거나 질량·제약을 덮을 수 있었음 | body의 원래 설정을 유지하면서 PlacementSupport와 질량 기준을 초기화. 처음 생성할 때의 플랫폼/일반 소품 질량 균형도 유지 |
| 플랫폼 라벨과 절대 scale로 질량을 매번 재계산 | 최초 질량 × (현재 local X scale / 최초 local X scale)³. 이후 라벨 변경으로 원래 질량이 바뀌지 않음. 기존0.25~5000 제한 유지 |
| 초기 임시 kinematic/speculative 모드가 기본 CCD 복귀 모드로 남을 수 있었음 | 임시 보유 모드 이전의 원래 모드 보존. 놓은 뒤2초 ContinuousDynamic 후 원래 모드로 복귀 |
| 취소 시 일부 물리 정책만 복구 | 기존 위치·회전·크기·속도·부유 상태에 더해 질량·감쇠·각속도 제한·제약·보간·solver·COM/관성 정책·남은 CCD 시간을 복원 |
| 자식 Rigidbody의 Collider까지 함께 활성/비활성 또는 보유 상태로 취급할 수 있었음 | 가장 가까운 실제 Rigidbody owner를 기준으로 owned Collider/부착 계층 구분. 비활성 owned 자식의 원래 레이어도 보존 |
| 배치 후보가 거부돼도 후보 질량만 남을 수 있었음 | 검증에 통과한 배치에서만 질량을 갱신해 거부 시 이전 자세·질량 유지 |
| 조작 호스트만 비활성화하면 소품이 보유 상태로 남을 수 있었음 | 호스트 종료에서도 잡기를 취소하고 월드 레이어·물리 상태 복원 |

들고 있을 때는 힘/스프링으로 손을 따라오는 방식이 아니라 현재 카메라 기준 투영으로 자세를 정한다. 놓기 때 플레이어 이동속도를 물려받는 던지기는 추가하지 않았다. 기존 클릭 토글, 오른쪽 버튼 회전, Backspace 취소, 이동·가변 점프·수평 유지·물/공/거울/그림자 퍼즐은 유지한다.

## 원본을 그대로 복제하지 않은 부분

- 원본의 레이어 번호11 대신 현재 프로젝트에서 비어 있던29를 사용한다. 그림자 미리보기를30으로 분리하고, 반사면31의 렌더 제외 상태도 유지한다. 현재 맵에 없는 특수 접촉 레이어·포털·멀티플레이 규칙은 추가하지 않았다.
- 원본의 local/lossyScale 혼용은 일관된 local X 기준으로 보정했다.
- 기존의 최종 환경 볼륨 검증과 안전한 스케일 하한을 유지했다. 원본에서 실행 재현되지 않은 overlap/미검사 midpoint 문제를 옮기지 않았다.
- 원본 재질표의 오리·달걀·소다·공 반발 계수를 이름만 보고 Parallax의 다른 소품에 배정하지 않았다. null 물리 재질을 특정 수치의 마찰이라고 단정하지 않는다.
- 명시적 `Physics.SyncTransforms`를 사용하고 autoSyncTransforms는 끈 기존 정책을 유지했다. 원본 Unity2019의 native solver나 raw enum 설정 전체를 Unity6000에 덮어쓰지 않았다.

## 검증 결과

| 범위 | 결과 |
| --- | --- |
| 기존 전체 스테이지 조작/물리 | 171개 통과 |
| 새 held solver/레이어 검사 | 통합9개 + Gallery7개 통과 |
| 새 참조 물리 검사 | 통합30개 + Gallery9개 통과 |
| 실제 네 맵의 잡기·취소·배치 거부·호스트 종료 | 12개 통과 |
| 기존 Gallery 단독판 검사 | 61개 통과 |
| 합계 | 조작/물리299개 통과 |
| 기존 퍼즐 | Rule / Indoor / Courtyard / Key / ShadowPuzzle 통과 |
| 빌드 명령 | Python 회귀 검사8개 통과 |

새 검사에는 동적 소품 밀림 방지와 접촉 복구, query 가능한 보유 Collider, inactive/nested 소유권, 기존 Rigidbody와 재질 보존, 원래 질량/초기 크기 기준, COM/관성·CCD 타이머 취소 복구, 질량과 무관한 실제 자유낙하, 4cm 바닥에 대한 고속 CCD 낙하가 포함된다. Editor에서 실행한 disable 검사는 실제 private callback을 호출하므로 네이티브 PlayMode lifecycle 전달 자체의 테스트와 구분한다.

패키지 분석은 원본 게임의 실행 측정이나 native solver 복원이 아니다. 이 검증은 현재 Unity6000.3.24f1의 Parallax 동작을 확인하며, 원본과 동일한 낙하 궤적·모든 충돌 조합·실제 입력 지연까지 일치한다는 뜻은 아니다.

## 실행

- 새 Gallery: `Builds/PhysicsUpdate/Gallery/Parallax.app`
- 새 우주선: `Builds/PhysicsUpdate/Starship/Parallax.app`
- 로그·검사 보고서: `Builds/PhysicsUpdate/Validation/`

두 Unity 프로젝트에서 `-executeMethod ShadeLink.Editor.PhysicsPackageValidation.RunAll`로 재검증한다. 기존 macOS 빌드 메서드는 그대로 사용할 수 있으며, `-parallaxBuildOutput <앱 절대 경로>`로 새 출력 위치를 지정할 수 있다.
