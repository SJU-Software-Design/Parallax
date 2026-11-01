# Parallax — 마지막 해변 v0.11.0 · 미술관·도서관 소스

최신 추가 스테이지는 **마지막 해변 v0.11.0**입니다. `Assets/Parallax/Beach/Scenes/Beach.unity`를 열어 Play를 누르거나 작업 폴더의 `바다스테이지실행.cmd`를 실행합니다. 확대 거울, 판자와 파라솔, 물살 운반, 모래성 정상 탈출의 네 구역으로 구성했습니다.

빌드는 `Tools/Build/Build-Beach.ps1`, 검증은 `Tools/Validation/Test-Beach.ps1`입니다. [해변 안내](Documentation/Beach/플레이안내.txt) · [검증 범위](Documentation/Beach/검증결과.txt) · [코드 길잡이](Documentation/Beach/코드길잡이.md). 최신 공유 소스는 `Parallax_Beach_v0.11.0_Source.zip`입니다. 세 스테이지를 하나의 플레이 흐름으로 연결하는 작업은 남아 있습니다.

아래는 보존한 미술관·도서관의 개별 버전 안내입니다.

현재 미술관은 **v0.10.0**입니다. `Assets/Parallax/Gallery/Scenes/Gallery.unity`를 열거나 작업 폴더의 `미술관스테이지실행.cmd`를 실행하세요. 메뉴의 **거울 전시실 바로 체험**으로 새 퍼즐에 바로 들어갑니다. 거울 클릭 후 Q/E 회전, 광센서 문, 차광판으로 반사광 가리기, 전시물을 들고 WASD 이동 및 Space 점프를 추가했습니다.

빌드: `Tools/Build/Build-Gallery.ps1` · 새 검증: `Tools/Validation/Test-Gallery-Mirror.ps1` · 기존 원근 검증: `Tools/Validation/Test-Gallery.ps1`. 두 검증 도구는 현재 미술관 실행 파일을 기본으로 사용합니다. 출력은 `배포자료/Parallax_Unity_v0.10.0_MirrorGallery/Parallax.exe`입니다. 소스 공유는 `Parallax_Gallery_v0.10.0_Source.zip`을 사용합니다. 도서관과 공통·이전 실험 코드도 포함합니다.

두 번째 스테이지 **도서관(Library) v0.9.0**을 추가했습니다. 도서관을 열려면 Unity **6000.3.24f1 / Built-in**에서 `Assets/Parallax/Library/Scenes/Library.unity`를 선택하고 Play를 누르세요. 기존 미술관 씬과 코드는 별도로 유지합니다.

| 도서관 위치 | 역할 |
| --- | --- |
| `Assets/Parallax/Library/Scenes/Library.unity` | 도서관 시작 씬 |
| `Assets/Parallax/Library/Scripts` | 도서관 맵·원근 조작·물리·그림자·UI |
| `Assets/Parallax/Library/Editor` | 원본 에셋 적용과 씬·빌드 생성 |
| `Assets/Parallax/Library/Validation` | 책 개별 조작과 원근·그림자·클리어 검증 |
| `Assets/ThirdParty/RinkuChoudhary` | Medieval Library Kit 원본 FBX와 텍스처 |
| `Documentation/Library` | [플레이 안내](Documentation/Library/플레이안내.txt) · [코드 길잡이](Documentation/Library/코드길잡이.md) |

도서관은 기존과 동일하게 **카메라와 배치 지점 사이의 거리에 비례해 실제 크기를 변경**하며 휠을 사용하지 않습니다. 서가 속 책을 한 권씩 집고, 가구의 그늘을 이어 네 구역을 통과합니다.

책 40권·책꽂이 5개·탁자 2개·의자 2개, 총 49개 사물을 조작할 수 있습니다. 서가 속 책 36권도 각각 독립된 사물입니다.

```powershell
.\Tools\Build\Build-Library.ps1
.\Tools\Validation\Test-Library.ps1
python Tools/Share/Export-Library-Source.py --output ../03_공유용_소스
```

현재 미술관과 도서관을 함께 공유할 때는 상단의 **v0.10.0 소스 ZIP**을 사용합니다. 이전 `Parallax_Library_v0.9.0_Source.zip`에는 개편 전 미술관이 들어 있습니다. 도서관 빌드·내보내기는 원본 에셋 통합을 필수로 확인합니다. 도서관 검증 근거는 `Documentation/Library/검증결과.txt` 및 `Documentation/Library/Verification`에 있습니다.

원근에 따라 전시물의 실제 크기를 바꾸고, 거울·발판·그림자 경로를 만들어 높은 출구로 이동하는 Unity 퍼즐 게임입니다. 현재 개발 기준은 **미술관(Gallery) v0.10.0**입니다.

## 처음 실행하기

1. Unity Hub에서 **Unity 6000.3.24f1**을 설치합니다. 렌더 파이프라인은 **Built-in**입니다.
2. Hub의 **Add / 디스크에서 프로젝트 추가**로 이 README와 `Assets`, `Packages`, `ProjectSettings`가 있는 폴더를 선택합니다.
3. `Assets/Parallax/Gallery/Scenes/Gallery.unity`를 엽니다.
4. Play를 누르고 **미술관 입장**을 선택합니다.

처음 열 때 Unity가 `Library` 캐시를 생성합니다. 원본 CGTrader 파일을 다시 다운로드하거나 추출할 필요는 없습니다. 씬에 필요한 가공 에셋과 텍스처가 포함되어 있습니다.

## 소스 구조

| 위치 | 역할 |
| --- | --- |
| `Assets/Parallax/Gallery/Scenes` | 현재 미술관 씬 |
| `Assets/Parallax/Gallery/Scripts` | 현재 미술관의 조작·퍼즐·맵·UI 코드 |
| `Assets/Parallax/Gallery/Resources` | 화면의 그림자와 안전 판정을 맞추는 셰이더 |
| `Assets/Parallax/Gallery/Generated` | 미술관에서 사용하는 생성 메시와 재질 |
| `Assets/Parallax/Gallery/Editor` | 씬 생성, Windows 빌드, 소스 참조 검증 도구 |
| `Assets/Parallax/Gallery/Validation` | 원근 조작·운반·거울 퍼즐의 자동 플레이 검증 코드 |
| `Assets/Parallax/Shared` | 미술관과 이전 실험이 함께 사용하는 규칙·자료형·셰이더·빌드 경로 |
| `Assets/Parallax/Legacy` | 이전 야외·열쇠·그림자 조합·실내 통로 실험 |
| `Assets/ThirdParty/ALLrounder18` | Art Gallery에서 추출한 원본 메시·텍스처 |
| `Documentation/Gallery` | 미술관 플레이·코드 안내 |
| `Documentation/Legacy` | 이전 실험 안내 |
| `Documentation/ThirdParty` | 외부 에셋 출처와 다운로드 기록 |
| `Tools/Build`, `Tools/Validation`, `Tools/Import`, `Tools/Share` | 빌드·검증·원본 추출·소스 ZIP 도구 |

**미술관 코드를 읽을 때는 `Gallery/Scripts/GalleryGame.cs`부터 시작하세요.** `Shared`와 `Legacy`를 임의로 삭제하지 마세요. 이전 실험에서 발전한 공통 코드의 참조가 남아 있으며 전체 소스 묶음에 포함했습니다. 기존 `ShadeLink` 네임스페이스는 호환성을 위해 유지합니다.

## 조작과 핵심 규칙

- WASD / 마우스: 이동 / 시점, Space: 점프.
- 왼쪽 클릭: 잡기 / 놓기. 잡은 동안 시선이 향한 표면의 거리에 따라 실제 크기가 바뀝니다.
- 잡은 상태에서도 WASD 이동과 Space 점프가 가능합니다. 충돌체는 계속 유지합니다.
- **마우스 휠과 F는 미술관 사물의 크기·거리 조절에 사용하지 않습니다.**
- Q/E: 사물 좌우 회전 또는 클릭한 거울 회전, Z/X: 기울이기, C: 바로 세우기.
- 오른쪽 클릭: 잡기 취소, R: 현재 구역 초기화, Shift+R: 전체 초기화, H: 힌트, Esc: 일시정지.
- 놓은 사물은 변경된 크기를 유지하고 중력·충돌·회전 물리가 적용됩니다.

자세한 동선과 해결 방향은 [플레이 안내](Documentation/Gallery/플레이안내.txt), 코드별 역할은 [코드 길잡이](Documentation/Gallery/코드길잡이.md)를 참고하세요.

## 빌드와 검증

Unity 메뉴 **Parallax → 미술관 Windows 빌드**를 실행합니다. 이 메뉴는 미술관 씬을 코드로 다시 생성합니다. 씬을 수동 편집했다면 먼저 별도 씬으로 저장하세요.

PowerShell에서는 프로젝트 폴더를 기준으로 다음 명령을 사용할 수 있습니다.

```powershell
.\Tools\Build\Build-Gallery.ps1
.\Tools\Validation\Test-Gallery.ps1
.\Tools\Validation\Test-Gallery-Mirror.ps1
```

원래 작업 폴더에서는 상위 `배포자료`에 출력하고, 소스 ZIP만 따로 압축 해제한 환경에서는 이 프로젝트의 `Builds`에 출력합니다. Unity 실행 파일이 자동 검색되지 않으면 빌드 스크립트의 `-UnityEditor` 인수로 지정합니다. 자동 검증에는 먼저 생성한 Windows 실행 파일이 필요합니다.

## 공유하기

팀원에게는 **`Parallax_Gallery_v0.10.0_Source.zip` 하나**를 전달합니다. ZIP 안의 `Parallax_Gallery_Source` 폴더를 Unity Hub에 추가하면 됩니다.

`Assets`의 `.meta` 파일, `Packages`, `ProjectSettings`는 반드시 함께 유지합니다. `Library`, `Logs`, `Temp`, `UserSettings`, 실행 파일과 과거 검증 결과는 소스 ZIP에서 제외합니다. 최종 미술관 검증 근거는 `Documentation/Gallery/검증결과.txt`와 `Documentation/Gallery/Verification`에 포함합니다.

외부 에셋은 `Documentation/ThirdParty`의 제작자·출처·이용 조건 기록을 함께 유지합니다. 새 도서관 맵은 상단의 Library 안내를 참고하세요.
