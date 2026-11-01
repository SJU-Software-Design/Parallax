# Gallery Backward Distance Correction

Goal: 뒤로 걷거나 다시 잡아도 보유 물체의 최대 배치 거리는 실제 표면과 Gallery 외곽으로 결정한다. 사용자 영상의 고정 9.12m/3.20 배치를 재현한다.

Evidence: 영상 0·3·6초 9.12m/3.20; 8초 놓음; 10·11초 13.26m/3.20; 12초 놓음; 16·18·20초 16.81m/3.20. 저상 전시대 maxScale=3.2. 잡을 때 q=scale/distance를 재저장하고 현재 solver는 maxDepth=maxScale/q로 제한한다.

Design: Gallery의 IInteractionProp.MaxScale을 무제한으로 제공한다. 기존 직렬화 필드는 데이터 호환용으로 보존한다. 공통 solver의 크기 상한 기능과 다른 스테이지 정책은 유지한다. Gallery의 모든 꼭짓점 경계·표면·부피 검사는 기존대로 유한한 배치를 보장한다. 재잡기의 기준 비율 재계산도 정상적인 원근 동작이므로 유지한다.

- [x] 실제 Gallery adapter + PhysX 저상 전시대 회귀 검사: 이전 상한 3.20을 넘어 먼 벽에 배치, 뒤로 2m 이동 시 깊이/크기 증가 및 벽 쪽 끝점 유지, 놓기/재잡기 후에도 동일 동작. 수정 전 실패 확인.
- [x] 두 프로젝트 GalleryProp.MaxScale 정책만 수정. 전체 PhysicsPackageValidation 통과 확인.
- [x] v5 별도 Windows 빌드와 기존 Gallery 실행 검사. v4 진단 HUD 보존, 새 바로가기와 영상 원인 MD 제공.
