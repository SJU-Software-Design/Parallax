# Parallax — 미술관 프로토타입 소스 v0.8.2

원근에 따라 전시물의 실제 크기를 바꾸고, 발판과 그림자 경로를 만들어 높은 출구로 이동하는 Unity 퍼즐 게임입니다. 현재 개발 기준은 **미술관(Gallery) v0.8.2**입니다.

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
| `Assets/Parallax/Gallery/Validation` | 현재 원근 조작의 자동 플레이 검증 코드 |
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
- **마우스 휠과 F는 미술관 사물의 크기·거리 조절에 사용하지 않습니다.**
- Q/E: 좌우 회전, Z/X: 기울이기, C: 바로 세우기.
- 오른쪽 클릭: 잡기 취소, R: 현재 구역 초기화, Shift+R: 전체 초기화, H: 힌트, Esc: 일시정지.
- 놓은 사물은 변경된 크기를 유지하고 중력·충돌·회전 물리가 적용됩니다.

자세한 동선과 해결 방향은 [플레이 안내](Documentation/Gallery/플레이안내.txt), 코드별 역할은 [코드 길잡이](Documentation/Gallery/코드길잡이.md)를 참고하세요.

## 빌드와 검증

Unity 메뉴 **Parallax → 미술관 Windows 빌드**를 실행합니다. 이 메뉴는 미술관 씬을 코드로 다시 생성합니다. 씬을 수동 편집했다면 먼저 별도 씬으로 저장하세요.

PowerShell에서는 프로젝트 폴더를 기준으로 다음 명령을 사용할 수 있습니다.

```powershell
.\Tools\Build\Build-Gallery.ps1
.\Tools\Validation\Test-Gallery.ps1
```

원래 작업 폴더에서는 상위 `배포자료`에 출력하고, 소스 ZIP만 따로 압축 해제한 환경에서는 이 프로젝트의 `Builds`에 출력합니다. Unity 실행 파일이 자동 검색되지 않으면 빌드 스크립트의 `-UnityEditor` 인수로 지정합니다. 자동 검증에는 먼저 생성한 Windows 실행 파일이 필요합니다.

## 공유하기

팀원에게는 **`Parallax_Gallery_v0.8.2_Source.zip` 하나**를 전달합니다. ZIP 안의 `Parallax_Gallery_Source` 폴더를 Unity Hub에 추가하면 됩니다.

`Assets`의 `.meta` 파일, `Packages`, `ProjectSettings`는 반드시 함께 유지합니다. `Library`, `Logs`, `Temp`, `UserSettings`, 실행 파일과 과거 검증 결과는 소스 ZIP에 포함하지 않습니다. 게임 동작을 바꾸지 않고 폴더와 참조 경로를 정리한 소스입니다.

외부 에셋은 `Documentation/ThirdParty`의 제작자·출처·이용 조건 기록을 함께 유지합니다. 도서관 맵은 에셋 조사 단계이며 아직 이 프로젝트에 구현되어 있지 않습니다.
