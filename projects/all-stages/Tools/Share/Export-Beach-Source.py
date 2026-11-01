"""Package the Beach v0.11.0 Unity project with preserved stages and file hashes."""
from pathlib import Path
import argparse, hashlib, json, zipfile

project=Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--output',type=Path,default=(project.parent/'공유용_소스/이전버전' if project.parent.name=='유니티' else project/'SourcePackages'))
args=parser.parse_args()
output=args.output.resolve();output.mkdir(parents=True,exist_ok=True)
if 'Result: Succeeded' not in (project/'Validation/Beach/build-result.txt').read_text(encoding='utf-8-sig'):
    raise RuntimeError('Build Beach successfully before exporting.')
report=json.loads((project/'Validation/Beach/Player/beach-results.json').read_text(encoding='utf-8-sig'))
if not report['passed']:raise RuntimeError('Beach runtime verification must pass.')
included=[]
for name in ('Assets','Packages','ProjectSettings','Documentation','Tools'):
    included.extend(p for p in sorted((project/name).rglob('*')) if p.is_file() and '__pycache__' not in p.parts and p.suffix!='.pyc' and p.name!='tool-moves.json')
included.extend(project/name for name in ('README.md','.gitignore'))
for p in (project/'Assets').rglob('*'):
    if not p.name.endswith('.meta') and not p.with_name(p.name+'.meta').exists():raise RuntimeError('Missing Unity meta: '+str(p))
manifest={'project':'Parallax — Beach, Gallery and Library','version':'0.11.0','unity':'6000.3.24f1','scene':'Assets/Parallax/Beach/Scenes/Beach.unity','preservedScenes':['Assets/Parallax/Gallery/Scenes/Gallery.unity','Assets/Parallax/Library/Scenes/Library.unity'],'beachAssetLicense':'CC0 1.0. Existing gallery/library assets retain their original terms.','verificationScope':report['scope'],'files':{p.relative_to(project).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in included}}
archive=output/'Parallax_Beach_v0.11.0_Source.zip';prefix='Parallax_Beach_Source/'
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as bundle:
    for p in sorted((project/'Assets').rglob('*')):
        if p.is_dir():bundle.write(p,prefix+p.relative_to(project).as_posix()+'/')
    for p in included:bundle.write(p,prefix+p.relative_to(project).as_posix())
    bundle.writestr(prefix+'SourceManifest.json',json.dumps(manifest,ensure_ascii=False,indent=2).encode('utf-8'))
with zipfile.ZipFile(archive) as bundle:
    if bundle.testzip() is not None:raise RuntimeError('ZIP CRC verification failed')
    for name,expected in manifest['files'].items():
        if hashlib.sha256(bundle.read(prefix+name)).hexdigest()!=expected:raise RuntimeError('ZIP hash mismatch: '+name)
sha=hashlib.sha256(archive.read_bytes()).hexdigest()
(output/(archive.name+'.sha256')).write_text(sha+'  '+archive.name+'\n',encoding='utf-8')
print(json.dumps({'archive':str(archive),'files':len(included)+1,'bytes':archive.stat().st_size,'sha256':sha},ensure_ascii=False))
