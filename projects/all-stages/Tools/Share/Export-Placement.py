"""Export verified current players and full Unity source with file hashes."""
from pathlib import Path
import json,hashlib,shutil,zipfile

project=Path(__file__).resolve().parents[2];root=project.parents[1];version='0.12.1'
stage_versions={'Beach':version,'Gallery':'0.12.0','Library':'0.12.0'}
docs=project/'Documentation/Placement';archives=[]
def pack(folder,archive,prefix,files,manifest_name,manifest):
    manifest['files']={p.relative_to(folder).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in files}
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
        if folder==project:
            for path in (project/'Assets').rglob('*'):
                if path.is_dir():z.write(path,prefix+path.relative_to(project).as_posix()+'/')
        for p in files:z.write(p,prefix+p.relative_to(folder).as_posix())
        z.writestr(prefix+manifest_name,json.dumps(manifest,ensure_ascii=False,indent=2).encode('utf-8'))
    with zipfile.ZipFile(archive) as z:
        assert z.testzip() is None
        for name,digest in manifest['files'].items():assert hashlib.sha256(z.read(prefix+name)).hexdigest()==digest,name
    digest=hashlib.sha256(archive.read_bytes()).hexdigest();archive.with_suffix('.zip.sha256').write_text(digest+'  '+archive.name+'\n',encoding='utf-8')
    return {'path':str(archive),'bytes':archive.stat().st_size,'sha256':digest,'files':len(files)+1}

for stage in ['Beach','Gallery','Library']:
    stage_version=stage_versions[stage]
    report=json.loads((project/f'Validation/Placement/{stage}/placement-results.json').read_text(encoding='utf-8-sig'));assert report['passed'] and not report['errors']
    assert 'Result: Succeeded' in (project/f'Validation/{stage}/build-result.txt').read_text(encoding='utf-8-sig')
    evidence=docs/'Verification'/stage;evidence.mkdir(parents=True,exist_ok=True)
    for name in ['placement-results.json','player.log']:
        shutil.copy2(project/f'Validation/Placement/{stage}'/name,evidence/name)
    shutil.copy2(project/f'Validation/{stage}/build-result.txt',evidence/'build-result.txt')
    regression={'Beach':['Validation/Beach/Player/beach-results.json'],'Gallery':['Validation/GalleryMirror/mirror-results.json','Validation/GalleryPerspective/perspective-results.json'],'Library':['Validation/Library/Player/library-results.json']}[stage]
    for name in regression:
        data=json.loads((project/name).read_text(encoding='utf-8-sig'));assert data['passed'] and not data['errors']
        shutil.copy2(project/name,evidence/Path(name).name)
    if stage=='Beach':
        before=project/'Validation/BeachWater/Baseline/water-results.json'
        original=json.loads(before.read_text(encoding='utf-8-sig'));assert not original['passed'] and original['failures'] and not original['errors']
        shutil.copy2(before,evidence/'water-before-results.json')
        water=project/'Validation/BeachWater/Fixed/water-results.json'
        data=json.loads(water.read_text(encoding='utf-8-sig'));assert data['passed'] and not data['errors'] and not data['failures']
        shutil.copy2(water,evidence/'water-results.json')
    suffix='MirrorGallery' if stage=='Gallery' else stage;folder=root/f'윈도우/실행파일/Parallax_Unity_v{stage_version}_{suffix}'
    credits=folder/'ThirdPartyNotices';credits.mkdir(exist_ok=True)
    for p in (project/'Documentation/ThirdParty').iterdir():
        if p.is_file():shutil.copy2(p,credits/p.name)
    shutil.copy2(project/'Assets/ThirdParty/3DAssetsBeach/LICENSE.txt',credits/'Beach-CC0.txt')
    shutil.copy2(project/f'Documentation/{stage}/플레이안내.txt',folder/'플레이안내.txt')
    (folder/'게임실행.cmd').write_bytes(b'@echo off\r\ncd /d "%~dp0"\r\nstart "" "Parallax.exe"\r\n')
    if stage=='Beach':shutil.copy2(project/'Documentation/Beach/물속회수수정.txt',folder/'물속회수수정.txt')
    for name in ['설치규칙안내.txt','검증결과.txt']:shutil.copy2(docs/name,folder/name)
    files=[p for p in sorted(folder.rglob('*')) if p.is_file() and p.name!='BuildManifest.json']
    manifest={'version':stage_version,'stage':stage,'platform':'Windows 64-bit','placementChecks':len(report['checks']),'scope':report['scope']}
    if stage=='Beach':manifest['waterRecoveryChecks']=len(data['checks'])
    archives.append(pack(folder,root/f'윈도우/공유용ZIP/Parallax_{stage}_v{stage_version}_Windows.zip',f'Parallax_{stage}_v{stage_version}/',files,'BuildManifest.json',manifest))
    (folder/'BuildManifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
(docs/'release-manifest.json').write_text(json.dumps({'version':version,'players':archives},ensure_ascii=False,indent=2),encoding='utf-8')

for p in (project/'Assets').rglob('*'):
    if p.suffix!='.meta':assert p.with_name(p.name+'.meta').exists(),'Missing Unity meta: '+str(p)
files=[]
for name in ['Assets','Packages','ProjectSettings','Documentation','Tools']:
    files.extend(p for p in sorted((project/name).rglob('*')) if p.is_file() and '__pycache__' not in p.parts and p.suffix!='.pyc' and p.name!='tool-moves.json')
files.extend(project/name for name in ['README.md','.gitignore'])
output=root/'유니티/공유용_소스';output.mkdir(exist_ok=True)
manifest={'version':version,'stageVersions':stage_versions,'project':'Parallax — placed shadow rules and underwater recovery','unity':'6000.3.24f1','scenes':[f'Assets/Parallax/{s}/Scenes/{s}.unity' for s in ['Beach','Gallery','Library']],'verification':'Documentation/Placement/검증결과.txt','license':'Original per-asset licenses retained in Assets/ThirdParty and Documentation/ThirdParty.'}
archives.append(pack(project,output/f'Parallax_Placement_v{version}_Source.zip','Parallax_Placement_Source/',files,'SourceManifest.json',manifest))
print(json.dumps(archives,ensure_ascii=False))
