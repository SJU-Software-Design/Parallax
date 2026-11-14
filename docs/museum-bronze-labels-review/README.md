# 미술관 작품 설명판 — A안 브론즈

승인된 `reference-A-comparison.png`의 왼쪽 A안을 기준으로 미술관의 그림 설명판 12개를 교체했습니다. 최종 배치는 **액자 오른쪽에 12cm 간격, 액자와 설명판 아랫선 수평 정렬**입니다. 가는 수평 브러시 결의 브론즈, 얇은 모서리, 아이보리 명조체와 구분선을 적용했습니다. 표면은 기존 `Parallax/MuseumSurface`의 PBR 금속 재질로 렌더링하며, 각도에 따라 넓고 부드러운 반사가 바뀝니다.

- 표면 metallic 0.82, roughness 0.58. 글씨는 나눔명조 Bold/Regular 원본을 포함합니다.
- 작품명 / 작가·연도 / 재료의 세 줄을 왼쪽 정렬합니다. 긴 제목과 작가 정보는 판 안으로 맞춥니다.
- 오른쪽 배치 초안보다 판과 글씨의 가로·세로를 1.75배 확대했습니다. 최종 판 크기는 약 96.6 × 36.4cm입니다.
- 모든 그림과 설명판을 한 묶음으로 올렸습니다. 10점은 12cm, 낮게 걸린 손문 방의 《절규》와 《메이 웨스트의 얼굴》은 각각 16.5cm·31.5cm 올려 몰딩 위에 최소 4cm 여백을 확보했습니다.
- 커진 설명판이 옆 작품·기둥과 겹치지 않도록 첫 전시실의 장식용 그림 간격을 정리했습니다.
- 저장된 Gallery 씬을 부분 수정했습니다. 이번 후속 변경의 씬 차이는 그림/설명판 Transform 24개이며, 나머지 씬 컴포넌트와 참조는 동일합니다. 기존 라이트맵 2개 및 조명/반사 관련 파일 18개도 보존됐습니다.
- `GalleryConceptArchitecture.FramedArtwork`도 같은 설명판을 생성하므로 씬을 다시 준비해도 디자인을 유지합니다.

## 실제 게임 캡처

- `raised/after-context.png`: 오른쪽 하단 정렬과 몰딩 위 여백.
- `raised/after-closeup.png`: 확대된 판의 제목, 글씨, 브러시 결과 테두리.
- `raised/after-oblique.png`: 비스듬한 시점의 금속 반사.
- `raised/after-entry.png`, `raised/after-painting-room.png`: 첫 전시실의 작품 간격.
- `raised/after-portrait-room.png`: 눈코입 방에 적용된 배치.

최종 캡처는 `raised/`에 있습니다. 루트의 PNG들은 초기 제작 상태의 기록입니다.

캡처는 macOS ARM64 게임 빌드의 실제 플레이어 카메라와 기존 후처리로 렌더링했습니다. 디자인 생성 이미지나 합성 사진으로 대체하지 않았습니다.

## 검증

- Unity 6000.3.24f1, Apple M4 Pro.
- macOS 빌드 성공, 오류 0 (`raised/build-result.txt`).
- 실제 게임에서 12개 설명판·36개 텍스트의 존재, 왼쪽 정렬, 포함된 서체, 빈 글자/가로 넘침, 금속 재질을 검사했습니다. `MUSEUM_PLAQUES_PLAYER_OK count=12`.
- 실제 메쉬를 기준으로 설명판 크기·오른쪽 하단 정렬·이웃 그림 겹침을 검사했습니다.
- 12개 작품의 상승량과 몰딩 여백을 확인했습니다 (`raised/position-checks.json`).
- 기존 시각 검증을 포함한 17개 카메라 렌더가 오류 없이 완료됐습니다 (`raised/capture-manifest.json`).
- 이번 변경의 범위는 시각 요소입니다. 퍼즐 전체 풀이와 Windows 실행 검증은 반복하지 않았습니다.

폰트의 원본 출처·라이선스는 `projects/all-stages/Documentation/Gallery/MuseumTypography-Credits.md`에 기록했습니다. 브론즈 텍스처는 기본 `image_gen` 도구로 생성했고, 정확한 프롬프트는 `bronze-texture-prompt.txt`, 최종 게임 리소스는 `projects/all-stages/Assets/Parallax/Gallery/Resources/MuseumMaterials/PlaqueBronzeAlbedo.png`입니다.
