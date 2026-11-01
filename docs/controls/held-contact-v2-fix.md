# 들고 있는 물체의 막힘·끼임 수정과 저장소 정리

> 후속 작업: 정식 Editor 설치·Windows 재빌드와 실제 Gallery 실행 검사를 완료했다. 최신 실행 경로와 Starship 자동 진행의 남은 범위는 [정식 빌드 기록](../builds/windows-contact-v2.md)을 참고한다. 아래의 Editor 미설치 설명은 최초 검증 당시의 기록이다.

2026-11-01. Unity 6000.3.24f1 / Built-in / Mono. 전체 스테이지와 Gallery 단독판의 실제 코드를 수정했다. 원본 Superliminal의 후속 분석 `Held_Contact_REA_v2.md`와 대조했다.

## 확인한 원인

1. 배치용 `MinScale`이 운반 투영의 최소 크기로도 사용됐다. 가까운 장애물에 맞추려면 더 줄어야 하는데 `Feasible=false`가 되어 이전 자세에 머물렀다. 앞·옆 접촉에서 네 맵 모두 수정 전 실패를 재현했다.
2. 운반 미리보기에 놓기 검사를 그대로 적용했다. 바닥을 내려다볼 때 작은 미리보기가 플레이어 캡슐과 겹치면 원근 해가 있어도 이전 시선에 남았다. 실제 플레이어 높이·캡슐을 사용하는 검사에서 재현했다.
3. `autoSyncTransforms=false`일 때 같은 프레임에 Transform으로 이동한 장애물을 조회하면 이전 물리 위치가 사용됐다. 장애물이 멀어져도 크기 회복이 한 프레임 늦는 것을 재현했다.
4. 환경 ray 시작점이 고체 내부에 있으면 Unity raycast가 그 고체를 누락한다. 이를 자유 공간으로 해석하던 경계 사례를 추가로 재현했다.

## 수정한 동작

- 운반용 하한을 `0.001`로 분리했다. 실제 놓기에서는 기존 소품의 최소 크기와 최대 크기 정책을 유지한다. 너무 작으면 계속 들고 움직일 수 있지만, 놓기에는 크기를 회복해야 한다.
- `ValidatePreview`와 `ValidateRelease`를 분리했다. 운반 중에는 플레이어와의 solver 접촉이 분리되어 있으므로 미리보기는 현재 시선을 계속 따른다. 플레이어와 겹치는 상태의 실제 놓기는 거부한다.
- `ReleaseRestriction`을 통해 운반 가능성과 놓기 가능성을 구분했다. 놓을 수 없는 미리보기도 움직이며, 잘못된 유효 표시 대신 이유와 비유효 표시를 유지한다.
- 투영 쿼리 직전에 `Physics.SyncTransforms`를 호출해 움직이는 장애물의 현재 위치를 조회한다.
- 카메라 원점이 환경 고체 내부이거나 극히 가까운 경우를 별도 검사한다. 이때는 고체를 무시한 먼 위치에 놓지 않고 배치 불가능으로 처리한다. 카메라를 고체 밖으로 옮기면 다시 계산한다.
- Starship의 예외 복구도 운반 하한과 같은 기준을 사용한다.

잡을 때 저장한 크기/거리 기준은 계속 고정이다. 매 프레임 같은 거리 해에서 위치와 크기를 계산하므로 이탈하면 다시 커지고, `.99` 접촉 여유가 누적 축소되지 않는다. 다른 물체는 계속 환경 쿼리에 포함한다. 보유 Collider와 자식 레이어, 놓기·취소 시 물리 복원은 유지했다.

유한한 투영 샘플이 놓칠 수 있는 얇은 모서리는 기존 환경 부피 검사로 보완한다. 맵 경계, 카메라가 고체 안인 경우, 실제 공간이 없는 경우까지 관통을 허용하는 변경은 아니다. 원본의 native solver를 복제한 구현도 아니다.

## 수정 파일

두 Unity 프로젝트의 같은 상대 경로에 반영했다.

- `Assets/Parallax/Shared/Scripts/ObjectInteraction.cs`: 운반 하한, 미리보기/놓기 검사, 놓기 제한 이유.
- `Assets/Parallax/Shared/Scripts/PerspectivePlacement.cs`: 현재 Transform 동기화, 원점 고체 검사.
- 각 스테이지의 `Scripts/*Game.cs`: 미리보기용 검사와 일관된 비유효 표시. 기존 공개 `ValidPlacement(prop, out reason)` 서명은 유지했다.
- Starship `StarshipHold.Intent.cs`, `StarshipHold.Movement.cs`: 복구 하한과 놓기 안내.
- `Assets/Parallax/Shared/Editor/HeldContactTests.cs`: 실제 맵 어댑터와 native 물리를 사용하는 새 검사.
- `PhysicsPackageValidation.cs`: 새 검사를 전체 검증에 포함.
- `ObjectInteractionTests.cs`: 기존 가까운 벽 검사의 기대를 새 운반 동작에 맞추고, 벽 뒤 배치와 놓기 거부 검사는 유지.

[전체 스테이지 코드](../../projects/all-stages/Assets/Parallax/Shared/Scripts/ObjectInteraction.cs) · [Gallery 코드](../../projects/gallery/Assets/Parallax/Shared/Scripts/ObjectInteraction.cs) · [접촉 검사](../../projects/all-stages/Assets/Parallax/Shared/Editor/HeldContactTests.cs).

## 검증

신규 접촉 검사는 네 맵 각각 아래 7개를 실행한다. Gallery 단독판은 같은 7개를 실행한다.

- 앞쪽 장애물에 접근해 최소 배치 크기 이하로 축소하고, 이탈 시 다시 확대·놓기.
- 같은 프레임에 이동한 장애물의 현재 Transform 조회.
- 옆 장애물 접근 중 카메라 위치 추적과 화면 크기 비율 유지·회복.
- 실제 눈 높이 1.55와 캡슐 높이 1.6/반지름 0.3에서 가까운 접촉.
- 바닥을 내려다보는 시선 추적과 플레이어 내부 놓기 거부.
- 카메라가 고체 내부일 때 장애물 무시·놓기 방지.
- 40회 반복 접촉에서 누적 축소 방지, 자식 Collider/레이어 및 취소 복원.

원인별 검사를 먼저 실패시킨 뒤 수정 후 통과를 확인했다. 플레이어 겹침은 놓기 검사를 운반에 다시 적용하는 임시 변이에서도 실패를 확인했다. 기존 조작·낙하·CCD·질량·레이어·취소·호스트 종료 검사와 기존 퍼즐 검사도 실행했다.

조작·물리 검사는 **334개(전체 스테이지 250개 + Gallery 단독판 84개)**가 통과했다. 기존 퍼즐 검사 묶음도 통과했다. 아래 개수는 native 빈 씬에서 실행한 검사 수이며 정식 Editor 재빌드 수가 아니다.

검증 결과와 전체 로그는 저장소의 `artifacts/validation/held-contact-v2/`에 있다. 빌드 명령 검사 8개와 macOS 패키징 경로 검사 1개(3개 레이아웃)는 한글·공백 경로, Windows와 macOS 출력 분리, 명시적 Editor 경로, 프로세스 실패, 결과 파일 누락·실패·오래된 결과를 검사한다.

### 검증 환경과 한계

이 PC에 Unity Editor가 없어 **정식 Editor 재빌드는 수행하지 않았다.** 포함된 Starship Mono 플레이어의 별도 임시 복사본을 빈 검증 씬으로 만들고, 두 프로젝트를 각각 실제 C# 소스로 컴파일해 같은 Unity 6000.3.24f1 native 물리에서 실행했다. Gallery 소스 검사도 공통 셰이더가 포함된 이 빈 플레이어를 호스트로 사용했다. 기존 배포본과 실제 소스 에셋은 바꾸지 않았다.

축약된 배포 바인딩 때문에 [공식 동일 버전 Unity](https://unity.com/releases/editor/whats-new/6000.3.24f1)의 Windows Mono 모듈에서 관리 바인딩을 가져왔고, 원래 플레이어 BCL에 맞춰 타입 참조를 복원했다. 컴파일 사본에서 링크 시 제거된 상수만 같은 값으로 치환했다. 테스트 몸체와 실제 생산 로직은 그대로 사용하며, Editor의 빈 씬 생성만 런타임 설정 어댑터로 대체했다.

이는 실제 렌더링 결과, 완성 씬의 직렬화·셰이더 재빌드, 마우스 입력 체감이나 FPS별 성능 계측을 대체하지 않는다. 이전 Gallery 실행본은 현재 소스와 씬 직렬화 구조가 달라 단순 DLL 교체만으로 배포하면 안 된다. 기존 실행본은 `builds/archive`에 그대로 보존했고 새 실행 파일에는 Unity Editor 빌드가 필요하다.

### 재실행

Editor가 있는 환경에서는 두 프로젝트 각각 다음 진입점을 사용한다.

```text
Unity -batchmode -nographics -quit -projectPath <projects/all-stages 또는 projects/gallery의 절대 경로> -executeMethod ShadeLink.Editor.PhysicsPackageValidation.RunAll -logFile <절대 로그 경로>
```

이번 native 검증을 재현하려면 `scripts/test-runtime.ps1`을 사용한다. Roslyn C# 컴파일러, UnityPy가 있는 Python, 동일 버전의 복원된 Unity 바인딩이 필요하다. `scripts/runtime/RetargetBindings.cs`는 공식 바인딩의 타입 참조를 실제 플레이어 BCL 정의로 연결한 검증용 소스다. 이 도구를 .NET 8과 Mono.Cecil 0.11.6으로 컴파일한 뒤 `<공식 모듈 Managed> <복원 바인딩 출력> <원래 플레이어 Managed>`를 인수로 실행한다. 임시 폴더만 사용한다.

```powershell
.\scripts\test-runtime.ps1 -Project .\projects\all-stages `
  -Player .\builds\archive\starship-0.13.1 `
  -WorkDirectory <새 임시 폴더> -Compiler <Roslyn csc.exe> `
  -Python <python.exe> -UnityPyPath <UnityPy 설치 폴더> `
  -UnityBindings <복원 바인딩 폴더> -Suite PhysicsPackageValidation.RunAll
```

`-Project`만 `projects/gallery`로 바꾸고 별도의 임시 폴더를 주면 Gallery 단독 소스를 검사한다. 원본 플레이어를 임시 폴더로 지정하는 작업은 차단한다.

## 폴더 정리

[이동 목록](../project-layout-migration.json)에 각 이전·현재 경로를 남겼다. Unity 프로젝트는 통째로 이동해 에셋과 `.meta`의 대응을 유지했다. `projects`는 현재 소스, `builds/archive`는 이전 실행본, `artifacts/reports`는 기존 산출물, `archive/work`는 과거 제작 작업이다. 빌드 명령과 출력 경로, 도구의 경로 탐색, 실행 바로가기를 함께 갱신했다.

정식 빌드 완료 후 `추가판_실행.cmd`는 새 Gallery 수정판으로 연결했다. 이전 실행본은 `play-archive-gallery.cmd`, 새 실행본은 `play-gallery.cmd` 또는 `play-starship.cmd`를 사용한다. 빌드가 없으면 이전 버전을 새 버전처럼 대신 실행하지 않고 안내한다. 과거 기록의 버전·해시·절대 경로는 기록으로 보존했다.

## 코드 검토에서 남긴 범위

별도 읽기 전용 검토에서 확인한 놓기 가능 표시와 가까운 벽 검사 기대는 수정했다. macOS 패키저의 입력 경로도 새 빌드 출력과 맞췄고, 실제 패키저를 실행하는 실패→통과 검사로 확인했다.

이전 릴리스용 `Export-Starship.py`, `Export-Placement.py`는 과거 배포 경로에 의존하므로 `Tools/Share/README.md`에 기록용임을 표시했다. 예외 복구 경로의 부가 안내는 기존 흐름을 유지한다. 보통 운반 경로와 실제 놓기 검사는 새 제한 이유를 사용한다. 사람이 평가할 조작감·FPS 성능·완성 씬 재빌드는 이 검토의 통과 주장에 포함하지 않는다.
