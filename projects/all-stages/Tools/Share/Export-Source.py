"""Create a portable Unity source ZIP with a per-file SHA-256 manifest."""
from pathlib import Path
import argparse, hashlib, json, zipfile

project = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser()
parser.add_argument('--output', type=Path, default=(project.parent / '공유용_소스/이전버전' if project.parent.name == '유니티' else project / 'SourcePackages'))
args = parser.parse_args()
output = args.output.resolve()
output.mkdir(parents=True, exist_ok=True)
archive = output / 'Parallax_Gallery_v0.10.0_Source.zip'
prefix = 'Parallax_Gallery_Source/'
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
manifest = {
    'project': 'Parallax — Gallery', 'version': '0.10.0', 'unity': '6000.3.24f1',
    'scene': 'Assets/Parallax/Gallery/Scenes/Gallery.unity',
    'files': {path.relative_to(project).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest() for path in included},
}
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
