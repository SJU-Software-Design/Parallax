# 미술관 외부 3D 조각 출처

확인 및 원본 다운로드: 2026-11-04. 이 문서는 직접 제작한 대체 도형과 실제 외부 조각 자산을 구분하고, 원본 파일의 출처·라이선스·변경 사항을 보존한다.

## 사모트라케의 니케 — 실제 포토그래메트리 스캔

- 스캔 제작자: **Cosmo Wenman**.
- 촬영 대상: **Skulpturhalle Basel, accession 248**에 있는 1892년 실물 크기 석고 캐스트. 루브르 소장 대리석 원작을 직접 촬영한 데이터라고 표기하면 안 된다.
- 제작 과정: 제작자가 박물관 허가 아래 2013년 9월 촬영한 사진을 Autodesk ReCap Photo로 처리했다. 원작자는 날개 끝의 촬영 누락 부위를 소폭 보정했다고 설명한다.
- [원작자 설명 및 원 배포 링크](https://cosmowenman.com/2013/12/28/winged-victory-published-update-12282013/)
- [원 배포 페이지: Thingiverse 196038](https://www.thingiverse.com/thing:196038)
- [공개 보존본: Internet Archive](https://archive.org/details/thingiverse-196038)
- [보존 ZIP 안의 원본 라이선스](https://archive.org/download/thingiverse-196038/Winged_Victory_of_Samothrace_196038.zip/LICENSE.txt)
- 적용 라이선스: **[Creative Commons Attribution 3.0 Unported](https://creativecommons.org/licenses/by/3.0/)**. Archive의 재작성 메타데이터에는 4.0 링크가 있지만, 원 배포 ZIP의 `LICENSE.txt`와 `README.txt`가 모두 명시하는 **3.0**을 따른다.
- 원본: `artifacts/museum-assets/source/Nike/20150903 WingedVictory Full Scale Decimated to 1MTris.stl`
- 크기: **49,034,584 bytes**, **980,690 triangles**. 원본 STL의 84-byte 헤더 및 50-byte 삼각형 레코드 수와 파일 길이가 일치함을 검증했다.
- SHA-256: `6176b3bd473ebb9ecd75631f00120c0878010b78dba7e77cd2e4a492530d2542`
- 제작자가 제공한 ZIP도 같은 폴더에 보존: `20150903_WingedVictory_Full_Scale_Decimated_to_1MTris.zip`, 35,454,196 bytes, SHA-256 `deb1560149eb3882a13ae6962808524d500948800deb4fc2abe4649cab94ea7b`.
- 확보 방법: Thingiverse 직접 접속이 403을 반환하여, 원작자 게시글이 가리키는 같은 배포물의 공개 Archive 보존본을 사용했다. 표준 HTTP Range로 ZIP의 해당 원본 멤버를 받았으며 계정·인증 토큰·우회 접근은 사용하지 않았다.
- 프로젝트 변경: 원본 파일은 수정하지 않았다. Unity 파생 Mesh에서는 좌표축·크기·셰이딩·인덱스를 게임용으로 변환할 수 있다. 변환은 자체 원시 도형 제작이 아니라 이 스캔의 기하를 사용한다.

배포할 때 사용할 표시 예시:

> Winged Victory of Samothrace — 3D scan by Cosmo Wenman, Skulpturhalle Basel plaster cast. CC BY 3.0. Converted and scaled for Parallax. Source: https://www.thingiverse.com/thing:196038 — License: https://creativecommons.org/licenses/by/3.0/

## 메두사 론다니니 — 실제 레이저 스캔, 원본 보존

- 스캔 제작자: **Cosmo Wenman**.
- 촬영 대상: 뮌헨 Glyptothek의 Medusa Rondanini를 본뜬 **Skulpturhalle Basel, accession 140**의 석고 캐스트.
- [원작자 설명](https://cosmowenman.com/2014/10/20/the-medusa-rondanini-published/): 제작자가 직접 레이저 스캔임을 명시한다.
- [원 배포 페이지: Thingiverse 196047](https://www.thingiverse.com/thing:196047)
- [공개 보존본](https://archive.org/details/thingiverse-196047)
- [보존된 원본 라이선스](https://archive.org/download/thingiverse-196047/The_Medusa_Rondanini_196047.zip/LICENSE.txt)
- 적용 라이선스: **[CC BY 3.0](https://creativecommons.org/licenses/by/3.0/)**. 이 작품도 ZIP 내부의 명시 조건을 따른다.
- 원본: `artifacts/museum-assets/source/Medusa/20141017_3_MedusaRondanini_LoRes_WallReady_by_CosmoWenman.obj`
- 크기: **55,259,559 bytes**, **698,196 vertices**, **1,396,420 faces**. OBJ 정점·면 행 수를 확인했다.
- SHA-256: `5e0b5a39873d66d20bde50446a282edff1aba03ea0c19d79a12dd0e2676d7c8a`
- 원본 OBJ, `Medusa-LICENSE.txt`, `Medusa-README.txt`만 보존한다. 현재 Unity Resources에는 복사하지 않았다. 파일명의 `LoRes`는 제작자가 붙인 상대적 구분이며 게임 관점에서는 여전히 고밀도 모델이다.

향후 게임에 사용할 때의 표시 예시:

> Medusa Rondanini — laser scan by Cosmo Wenman, Skulpturhalle Basel plaster cast. CC BY 3.0. Converted and scaled for Parallax. Source: https://www.thingiverse.com/thing:196047 — License: https://creativecommons.org/licenses/by/3.0/

## Marble Bust 01 — CC0 외부 조각 모델

- 제작자: **Rico Cilliers**, 배포: **Poly Haven**.
- [공식 모델 페이지](https://polyhaven.com/a/marble_bust_01), [공식 CC0 설명](https://polyhaven.com/license), [원본 파일 API](https://api.polyhaven.com/files/marble_bust_01).
- 라이선스: **[CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/)**.
- 르네상스 양식의 남성 대리석 흉상, 공개 사양 17,456 triangles, 높이 약 0.515m. 특정 역사적 작품명이나 실물 캡처 방식은 원작자 페이지에서 확정하지 않으므로 유명 작품의 실물 스캔으로 소개하지 않는다.
- 원본 보존: `artifacts/museum-assets/source/MarbleBust/`.
- Unity 복사본: `Assets/Parallax/Gallery/Resources/MuseumScans/MarbleBust/`.
- 원본 FBX와 2K JPG albedo, OpenGL normal, roughness를 그대로 복사했다. 총 **2,165,007 bytes**. 전 파일 MD5를 공식 API와 대조했고 SHA-256·다운로드 URL은 동봉한 `source.json`에 기록했다.
- 노멀은 **OpenGL RGB**이다. 일반 텍스처로 샘플할 때 `rgb * 2 - 1`로 해석한다. Unity의 별도 NormalMap importer를 쓰면 셰이더도 그 인코딩에 맞춰야 한다. albedo만 sRGB, normal·roughness는 linear이다.

선택적 표시: `Marble Bust 01 — Rico Cilliers / Poly Haven, CC0.`

## Potted Plant 02 — CC0 외부 화분 식물 모델

- 제작자: **Rico Cilliers**, 배포: **Poly Haven**.
- [공식 모델 페이지](https://polyhaven.com/a/potted_plant_02), [공식 CC0 설명](https://polyhaven.com/license), [원본 파일 API](https://api.polyhaven.com/files/potted_plant_02).
- 라이선스: **[CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/)**. 캡처 방식은 확인되지 않아 외부 모델로 표기한다.
- 공개 사양: **69,806 triangles**, 원본 높이 약 0.8m. 실제 FBX와 원본 UV 텍스처를 사용하며 대체 도형을 제작하지 않았다.
- 저장: `Assets/Parallax/Gallery/Resources/MuseumScans/PottedPlant/`.
- 다운로드: FBX, 잎·화분의 2K JPG albedo/normal/roughness, 별도 2K 잎 alpha PNG, Cutout용 알파 포함 1K 잎 diffuse PNG. 총 **9,739,496 bytes**. PNG 헤더의 RGBA color type 6을 확인했다. JPG diffuse에는 알파가 없으므로 잎 Cutout에는 해당 RGBA PNG 또는 별도 알파가 필요하다.
- FBX 머티리얼 이름: `potted_plant_02_leaves`, `potted_plant_02_pot`, `potted_plant_02_dirt`. 잎은 Cutout, 화분·흙은 불투명 UV 재질로 구분한다.
- 원본 FBX·이미지 바이트는 변경하지 않았다. MD5를 전 파일 공식 API와 대조했으며, 다운로드 URL·SHA-256은 동봉한 `source.json`에 기록했다.
- 선택적 표시: `Potted Plant 02 — Rico Cilliers / Poly Haven, CC0.`

## 로댕의 성당 — 실제 스캔을 이용한 출구

- 작품: Auguste Rodin, **La Cathédrale (성당)**. 스캔 제작자: **Migg**, 파리 로댕박물관에서 RealityScan 촬영.
- [원 배포 모델](https://sketchfab.com/3d-models/la-cathedrale-d0ba1f143173484f87961fc50d46bdcf), [공개 보존본](https://zenodo.org/records/10364942).
- 라이선스: **[CC BY 4.0](https://creativecommons.org/licenses/by/4.0/)**. 작품과 스캔 제작자, 출처, 라이선스, 변경 사항을 배포 크레딧에 함께 표시한다.
- 원본 GLB: 40,940,028 bytes. Zenodo MD5 `7e257aff8e43783d4b7aa7134a183716`과 다운로드 파일을 대조했다. SHA-256 `04ee8899c52c78ed509cecd4150f85916352758c1656c07a29b49d0b8234bdee`.
- 원본의 999,754개 삼각형에서 함께 촬영된 나무 받침과 분리된 찌꺼기를 제거했다. 실제 두 손과 조각의 석재 받침은 스캔 형상을 유지한다.
- 게임용 파생 모델은 754,102 triangles, 531,273 vertices, 높이 2m, Y-up이다. 원래 UV 텍스처를 사용한다. 출구가 열릴 때 손바닥이 최대 8cm씩 벌어지는 모프를 추가했으며, 받침과 손끝의 접촉 위치는 고정한다.
- 게임 안에서는 원근 조작으로 확대하고 내려놓은 뒤 손 사이의 통로로 들어간다. 바람 소리는 사용하지 않는다.
- Unity 자산과 개별 출처 기록: `Assets/Parallax/Gallery/Resources/MuseumScans/Cathedral/`.
- 포털 안의 서가·책·탁자·의자는 기존 도서관의 **Rinku Choudhary / Medieval Library Kit**를 재사용한다. 이 자산의 **Free Standard (NoAI)** 조건은 성당 스캔의 CC BY 4.0과 별개이다. 자세한 기록은 `Documentation/ThirdParty/Library-Asset-Integration.json`을 따른다.

## 오르쿠스 조사 결과 (이전 출구 후보)

- 검색어: Bomarzo, Orcus, Bocca dell'Orco, Sacro Bosco, Hell Mouth. 일반 웹 검색, Sketchfab 공개 검색 API, Internet Archive Thingiverse 보존본 검색을 확인했다.
- [Monash University의 Digital Bomarzo Underground](https://research.monash.edu/en/projects/digital-bomarzo-underground/)는 LiDAR·사진측량을 이용하는 실제 디지털 기록 연구를 설명한다. 그러나 재배포 가능한 오르쿠스 모델 파일과 라이선스는 확인하지 못했다.
- 공개된 Bomarzo 관련 Sketchfab 결과는 여러 건 있었지만 오르쿠스 조각의 다운로드 허용 스캔으로 확정되는 결과는 없었다. 이름이 Orcus인 판타지 캐릭터·천체 모델은 이 조각과 구분했다.
- 따라서 오르쿠스 실물 스캔을 확보했다고 주장하지 않는다. 계정 생성, 구매, 제작자 연락은 하지 않았다.

## 게임 배포 시

CC BY 조각을 실제 게임에 넣으면 제작자, 작품명, 원 배포 주소, CC BY 3.0 링크, 변환 사실을 플레이어가 열 수 있는 크레딧 또는 배포물의 저작권 고지에 함께 제공한다. 이 저장소 문서만 두고 게임 배포본의 표시를 생략하지 않는다.

## Aurelius (PhotoScan) — movable Nike-room light cover

- Creator: **ryanfb**. Portrait bust of Marcus Aurelius, Art Institute of Chicago.
- Original model: https://sketchfab.com/3d-models/aurelius-photoscan-cad3a072546a457fa1e2e5a90b25063a
- License: **Creative Commons Attribution 4.0**, https://creativecommons.org/licenses/by/4.0/
- Original OBJ: 123,409 vertices / 246,073 triangles; original UV albedo retained.
- Changes: display scale, museum shading, game carry colliders and a separate marble cover pedestal; texture imported at up to 4K with mipmaps. No geometry simplification.
- Local provenance: `Resources/MuseumScans/Aurelius/source.json`.
