"""Package the Library v0.9.0 Unity project, preserving Gallery, with file hashes."""
from pathlib import Path
import argparse
import hashlib
import json
import re
import zipfile

project = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--output', type=Path, default=(project.parent / '공유용_소스/이전버전' if project.parent.name == '유니티' else project / 'SourcePackages'))
args = parser.parse_args()
scene = 'Assets/Parallax/Library/Scenes/Library.unity'
integration_record = 'Documentation/ThirdParty/Library-Asset-Integration.json'
for required in (scene, integration_record, 'Assets/ThirdParty/RinkuChoudhary/LibraryKit.fbx'):
    if not (project / required).is_file():
        raise RuntimeError('Final source requirement missing: ' + required)
integration = json.loads((project / integration_record).read_text(encoding='utf-8-sig'))
build_report = project / 'Validation/Library/build-result.txt'
if not build_report.is_file():
    raise RuntimeError('Build the authentic Library stage before exporting source.')
build_text = build_report.read_text(encoding='utf-8-sig')
if 'Result: Succeeded' not in build_text or 'Mode: authentic-asset' not in build_text:
    raise RuntimeError('The final authentic-asset Library build must succeed before source export.')

included = []
for name in ('Assets', 'Packages', 'ProjectSettings', 'Documentation', 'Tools'):
    for path in sorted((project / name).rglob('*')):
        if path.is_file() and '__pycache__' not in path.parts and path.suffix != '.pyc' and path.name != 'tool-moves.json':
            included.append(path)
included.extend(project / name for name in ('README.md', '.gitignore'))
for path in (project / 'Assets').rglob('*'):
    if path.name.endswith('.meta'):
        continue
    if not path.with_name(path.name + '.meta').exists():
        raise RuntimeError('Missing Unity meta: ' + str(path.relative_to(project)))
version_text = (project / 'ProjectSettings/ProjectVersion.txt').read_text(encoding='utf-8-sig')
unity = re.search(r'^m_EditorVersion:\s*(\S+)', version_text, re.MULTILINE).group(1)
manifest = {
    'project': 'Parallax — Library and Gallery',
    'version': '0.9.0',
    'unity': unity,
    'scene': scene,
    'preservedScenes': ['Assets/Parallax/Gallery/Scenes/Gallery.unity'],
    'assetIntegrationRecord': integration_record,
    'assetIntegration': integration,
    'thirdPartyLicenseNote': 'Medieval Library Kit by Rinku Choudhary uses the source listing Free Standard (NoAI) license. This project archive does not grant separate redistribution rights to third-party assets.',
    'files': {path.relative_to(project).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest() for path in included},
}
output = args.output.resolve()
output.mkdir(parents=True, exist_ok=True)
archive = output / 'Parallax_Library_v0.9.0_Source.zip'
prefix = 'Parallax_Library_Source/'
with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as bundle:
    for path in sorted((project / 'Assets').rglob('*')):
        if path.is_dir():
            bundle.write(path, prefix + path.relative_to(project).as_posix() + '/')
    for path in included:
        bundle.write(path, prefix + path.relative_to(project).as_posix())
    bundle.writestr(prefix + 'SourceManifest.json', json.dumps(manifest, ensure_ascii=False, indent=2).encode('utf-8'))
with zipfile.ZipFile(archive) as bundle:
    if bundle.testzip() is not None:
        raise RuntimeError('ZIP CRC verification failed')
    for name, expected in manifest['files'].items():
        if hashlib.sha256(bundle.read(prefix + name)).hexdigest() != expected:
            raise RuntimeError('ZIP hash mismatch: ' + name)
sha = hashlib.sha256(archive.read_bytes()).hexdigest()
(output / (archive.name + '.sha256')).write_text(sha + '  ' + archive.name + '\n', encoding='utf-8')
print(json.dumps({'archive': str(archive), 'files': len(included) + 1, 'bytes': archive.stat().st_size, 'sha256': sha}, ensure_ascii=False))
