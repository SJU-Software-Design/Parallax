"""Package the tested Starship player and editable four-stage Unity project."""
from pathlib import Path
import json, hashlib, shutil, zipfile, re

project=Path(__file__).resolve().parents[2]
root=project.parents[1]
version=re.search(r'public const string Version="([^"]+)"', (project/"Assets/Parallax/Starship/Scripts/StarshipGame.cs").read_text(encoding="utf-8-sig")).group(1)
docs=project/"Documentation/Starship"
report=json.loads((project/"Validation/Starship/Player/starship-results.json").read_text(encoding="utf-8-sig"))
assert report["passed"] and not report["errors"], "Starship runtime validation must pass before packaging"
assert report["version"]==version, "The runtime report must match the packaged version"
build_result=(project/"Validation/Starship/build-result.txt").read_text(encoding="utf-8-sig")
assert "Result: Succeeded" in build_result and f"Parallax_Unity_v{version}_Starship" in build_result
evidence=docs/"Verification";evidence.mkdir(parents=True,exist_ok=True)
for source in [project/"Validation/Starship/Player/starship-results.json",project/"Validation/Starship/build-result.txt"]:
    shutil.copy2(source,evidence/source.name)
for name in ["01-cargo-start.png","02-optical-room.png","05-upper-deck.png","06-empty-exit-room.png","08-black-corridor.png","09-space-departure.png"]:
    source=project/"Validation/Starship/Player"/name
    if source.exists():shutil.copy2(source,evidence/name)
baseline=root/".tmp/starship/existing-stage-hashes.json"
if baseline.exists():
    original=json.loads(baseline.read_text(encoding="utf-8-sig"))
    retained=[]
    for entry in original:
        path=Path(entry["path"])
        digest=hashlib.sha256(path.read_bytes()).hexdigest()
        assert digest.upper()==entry["sha256"].upper(),"An existing-stage source changed: "+str(path)
        retained.append({"path":path.relative_to(project).as_posix(),"sha256":digest})
    (evidence/"existing-stage-sources.json").write_text(json.dumps({"checked":len(retained),"changed":0,"files":retained},ensure_ascii=False,indent=2),encoding="utf-8")
(docs/"검증결과.txt").write_text(f"""PARALLAX / HELIOS 우주선 v{version} 검증 기록

Unity 6000.3.24f1 / Built-in / Windows 64비트
빌드 성공, 오류 0개, 경고 0개.
런타임 검사 {len(report['checks'])}개 통과, Unity 오류 {len(report['errors'])}개.
상호작용 사물 {report['props']}개. HoldFix 쿼리 예산 최대 {report['maxHoldQueries']}회.
먼 표면 조준 시 화물 크기 {report['growth']:.2f}배 변화.
캡슐이 실제 물살로 {report['driftDistance']:.2f}m 운반되어 잠금 해제.

검사한 동작
고정 광원과 5 → 2 → 6 → 8 → 4 방 배치, 각 구역의 보호되는 체크포인트.
실제 카메라 선택, 들고 이동·점프, 확대, 장애물 접촉 후 자유 공간 복구.
시선 중심의 확대 의도 보존, 취소 복원, 이동 후 놓기와 0.35초 안착.
반사판의 실제 두 센서 충전과 광학 유리, 작은 반사판의 실제 빛 누락.
이전 3.5m 조건 이하에서도 두 센서가 켜지면 충전·해제되는 회귀 검사.
부분 충전 중 문 충돌, 한 센서만 켜진 실패, 해제 유지와 실제 통과 공간.
랙의 모듈 분리 조건, 각각 선택되는 모듈, 빈 랙 재사용, 탁자 아래 그늘.
첫 수로의 실제 발판 통과, 물살 운반, 회수한 캡슐·발판으로 상층 이동.
상층 쉼터 저장·복귀, 높은 출구와 방 사이 연결 복도 이동.
확대한 EXIT 내부 진입, 검은 복도·에어록 이동, 우주 부유·엔딩 완료 상태.
재시작 시 중력·문·장비·초기 체크포인트 복원.

검증 범위
기믹별 준비 배치를 사용했고, 준비된 발판·상층 경로와 연결 복도·엔딩은
일반 이동·점프와 Unity PhysX로 검사했습니다. 처음부터 힌트 없이 전체
퍼즐을 푸는 사람의 플레이 테스트와 정식 FPS·입력 지연 측정은 별도입니다.
PNG는 카메라 렌더링이며 HUD와 THE END의 IMGUI 글자는 포함하지 않습니다.
자세한 항목은 Verification/starship-results.json에서 확인할 수 있습니다.
""",encoding="utf-8")
player=root/f"윈도우/실행파일/Parallax_Unity_v{version}_Starship"
credits=player/"ThirdPartyNotices";credits.mkdir(exist_ok=True)
shutil.copy2(project/"Assets/ThirdParty/KenneySpaceStation/License.txt",credits/"Kenney-SpaceStation-CC0.txt")
shutil.copy2(docs/"플레이안내.txt",player/"플레이안내.txt")
shutil.copy2(docs/"태양광문수정.txt",player/"태양광문수정.txt")
shutil.copy2(docs/"provenance.json",player/"SourceProvenance.json")
shutil.copy2(evidence/"starship-results.json",player/"검증결과.json")
shutil.copy2(docs/"검증결과.txt",player/"검증결과.txt")
(player/"게임실행.cmd").write_bytes(b'@echo off\r\ncd /d "%~dp0"\r\nstart "" "Parallax.exe"\r\n')
(root/"윈도우/우주선스테이지실행.cmd").write_bytes(
    b'@echo off\r\ncd /d "%~dp0' + f'실행파일\\Parallax_Unity_v{version}_Starship'.encode("cp949") + b'"\r\nstart "" "Parallax.exe"\r\n')
archives=[]
def pack(folder, archive, prefix, files, manifest_name, manifest):
    manifest["files"]={p.relative_to(folder).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in files}
    with zipfile.ZipFile(archive,"w",zipfile.ZIP_DEFLATED,compresslevel=6) as z:
        if folder==project:
            for path in (project/"Assets").rglob("*"):
                if path.is_dir():z.write(path,prefix+path.relative_to(project).as_posix()+"/")
        for p in files:z.write(p,prefix+p.relative_to(folder).as_posix())
        z.writestr(prefix+manifest_name,json.dumps(manifest,ensure_ascii=False,indent=2).encode("utf-8"))
    with zipfile.ZipFile(archive) as z:
        assert z.testzip() is None
        for name,digest in manifest["files"].items():
            assert hashlib.sha256(z.read(prefix+name)).hexdigest()==digest,name
    digest=hashlib.sha256(archive.read_bytes()).hexdigest()
    archive.with_suffix(".zip.sha256").write_text(digest+"  "+archive.name+"\n",encoding="utf-8")
    return {"path":str(archive),"bytes":archive.stat().st_size,"sha256":digest,"files":len(files)+1}
files=[p for p in sorted(player.rglob("*")) if p.is_file() and p.name!="BuildManifest.json"]
manifest={"version":version,"stage":"Starship","platform":"Windows 64-bit","runtimeChecks":len(report["checks"]),"verificationScope":report["scope"],"difficulty":"Composite puzzle, limited equipment, default-hidden hints","assetSource":"https://kenney.nl/assets/space-station-kit","license":"CC0","interactionBase":"0.12.1 + HoldFix P1 source port"}
archive=root/f"윈도우/공유용ZIP/Parallax_Starship_v{version}_Windows.zip";archive.parent.mkdir(exist_ok=True)
archives.append(pack(player,archive,f"Parallax_Starship_v{version}/",files,"BuildManifest.json",manifest))
(player/"BuildManifest.json").write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding="utf-8")
for p in (project/"Assets").rglob("*"):
    if p.suffix!=".meta":assert p.with_name(p.name+".meta").exists(),"Missing Unity meta: "+str(p)
files=[]
for name in ["Assets","Packages","ProjectSettings","Documentation","Tools"]:
    files.extend(p for p in sorted((project/name).rglob("*")) if p.is_file() and "__pycache__" not in p.parts and p.suffix!=".pyc" and p.name not in ["tool-moves.json","release-manifest.json"])
files.extend(project/name for name in ["README.md",".gitignore"])
source_manifest={"version":version,"unity":"6000.3.24f1","stages":["Starship","Beach","Gallery","Library"],"scenes":[f"Assets/Parallax/{s}/Scenes/{s}.unity" for s in ["Starship","Beach","Gallery","Library"]],"verification":"Documentation/Starship/Verification/starship-results.json","license":"Per-asset original licenses preserved."}
archive=root/f"유니티/공유용_소스/Parallax_Starship_v{version}_Source.zip";archive.parent.mkdir(exist_ok=True)
archives.append(pack(project,archive,"Parallax_Starship_Source/",files,"SourceManifest.json",source_manifest))
(docs/"release-manifest.json").write_text(json.dumps(archives,ensure_ascii=False,indent=2),encoding="utf-8")
print(json.dumps(archives,ensure_ascii=False,indent=2))
