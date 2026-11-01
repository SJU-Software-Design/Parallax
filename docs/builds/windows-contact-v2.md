# 접촉 수정판 — 정식 Windows 빌드

2026-11-01. Unity Hub 3.22.5와 Unity Editor **6000.3.24f1 (4e7b9b5b6244)**, 기본 Windows Mono 지원을 설치했다. 설치 파일은 공식 배포 경로와 유효한 Unity 서명을 확인했다. 계정 로그인·라이선스 활성화는 사용자가 직접 진행했다.

## 실행

- [Gallery 수정판](../../play-gallery.cmd) 또는 [기존 한글 실행 바로가기](../../추가판_실행.cmd): `builds/windows/gallery/Parallax.exe`.
- [Starship 수정판](../../play-starship.cmd): `builds/windows/all-stages/Parallax_Unity_v0.13.1_Starship/Parallax.exe`.
- [이전 Gallery](../../play-archive-gallery.cmd)는 별도 보존했다.

두 새 실행본은 DLL만 교체한 파일이 아니다. 현재 소스로 씬과 셰이더를 재생성하고 정식 BuildPipeline으로 만들었다. 두 빌드 모두 결과는 Succeeded, Errors 0, Warnings 0이다. 배포할 때는 각 exe가 들어 있는 폴더 전체를 복사한다.

Gallery는 사용자에게 실행해 두었다. 좌클릭 잡기·놓기, 우클릭을 누른 채 마우스로 회전, Backspace 취소. 놓기 불가능한 작은 미리보기나 플레이어 겹침 상태에서도 운반 추적은 유지하며, 안전한 위치로 옮긴 뒤 놓을 수 있다.

## 검사 결과

두 프로젝트의 정식 Editor 물리·조작 검사 334개와 명령 검사 9개를 실행했다. 실제 GPU를 사용하는 Gallery 실행 검사는 16개가 통과했고 Unity 오류는 0개였다. 접근·이탈 축소/확대, 회전·환경 비관통, 잘못된 놓기 거부, 정확한 놓기 자세, 낙하·착지·그림자 및 별도로 표시한 근접 전시물 분리 사례를 포함한다.

**Starship은 빌드 성공과 전체 시나리오 통과를 구분한다.** 실행 검사에서 기본 운반·취소·확대·놓기, 반사판, 모듈과 물살 등은 확인했으나 상층 플랫폼 자동 이동 경로에서 검사가 끝까지 통과하지 못했다. 이 결과를 전체 플레이 성공으로 표시하지 않았다. 수동 플레이나 입력 지연·FPS 성능 계측도 별도다.

이전 런타임 검사에는 변경 전 입력 계약이 남아 있었다. Gallery 검사는 이미 구현된 플레이어 전용 축소율, 초기 운반 방향 정렬, 미리보기와 놓기 조건의 분리를 반영했다. 독립적인 환경 침투 검사와 정확한 놓기 자세 검사는 유지한다. Starship 자동 입력은 키보드 상승 시간, 실제 LateUpdate 놓기 처리, 점프 대기·유지 입력, 목표 높이에서의 착지를 반영했다. 이 보완 과정에서 게임의 이동·점프·투영 수치는 바꾸지 않았다. 읽기 전용 별도 코드 검토에서 검증 계약을 약화하는 중대한 문제는 발견하지 못했다.

전체 로그와 JSON 결과: `artifacts/validation/editor-build/`. 각 실행 폴더의 `CodeBuildManifest.json`에는 실제 빌드에 사용한 C# 소스의 SHA-256을 남겼다. Editor 가져오기 전 소스·설정·에셋은 같은 검증 폴더의 `*-before-editor.zip`에 백업했다.

## 다시 빌드

```powershell
$editor = "$env:LOCALAPPDATA\Unity\Editors\6000.3.24f1\Editor\Unity.exe"
.\scripts\build-windows.ps1 -UnityEditor $editor
.\projects\all-stages\Tools\Build\Build-Starship.ps1 -UnityEditor $editor
```

빌드 도구는 씬을 코드로 재생성하므로 수동 편집한 씬은 별도 이름으로 보존한다. 이번 빌드는 두 프로젝트의 소스 백업을 만든 뒤 실행했다.
