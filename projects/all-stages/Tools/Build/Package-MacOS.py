"""Validate the cross-built Mac bundle, then ZIP it with Unix execute permissions.

Uses Python's standard library. This checks binary/resource integrity, not Apple's
certificate trust or Gatekeeper approval; only a real Mac can validate launch.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import plistlib
import shutil
import stat
import struct
import zipfile

PROJECT = Path(__file__).resolve().parents[2]
REPOSITORY = next((parent for parent in PROJECT.parents
                   if (parent / 'projects').is_dir() and (parent / 'scripts').is_dir()), None)
DELIVERY = REPOSITORY / 'builds/macos' / PROJECT.name if REPOSITORY else PROJECT / 'Builds/macos'
FOLDER = DELIVERY / 'Parallax_Unity_v0.6_Key_macOS'
APP = FOLDER / 'ShadeLink.app'
ARCHIVE = DELIVERY / 'parallax-unity-v0.6-key-macos.zip'
CPUS = {0x1000007: 'x86_64', 0x100000C: 'arm64'}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def code_directories(binary):
    require(binary[:4] == b'\xcf\xfa\xed\xfe', 'Expected a 64-bit Mach-O slice')
    position = 32
    for _ in range(struct.unpack_from('<I', binary, 16)[0]):
        command, length = struct.unpack_from('<II', binary, position)
        require(length >= 8, 'Invalid Mach-O load command')
        if command == 0x1D:
            offset, size = struct.unpack_from('<II', binary, position + 8)
            signature = binary[offset:offset + size]
            magic, _, count = struct.unpack_from('>III', signature)
            require(magic == 0xFADE0CC0, 'Missing embedded code signature')
            directories = []
            for i in range(count):
                _, start = struct.unpack_from('>II', signature, 12 + i * 8)
                blob_magic, blob_length = struct.unpack_from('>II', signature, start)
                if blob_magic == 0xFADE0C02:
                    directories.append(signature[start:start + blob_length])
            require(directories, 'Missing code directory')
            return directories
        position += length
    raise ValueError('Mach-O has no code signature')


def audit_macho(path):
    data = path.read_bytes()
    require(data[:4] == b'\xca\xfe\xba\xbe', f'Not a universal Mach-O: {path.name}')
    count = struct.unpack_from('>I', data, 4)[0]
    require(count == 2, f'Expected two CPU architectures: {path.name}')
    records = []
    for i in range(count):
        cpu, _, offset, size, _ = struct.unpack_from('>IIIII', data, 8 + i * 20)
        require(cpu in CPUS and offset + size <= len(data), 'Invalid universal binary slice')
        binary = data[offset:offset + size]
        directories = code_directories(binary)
        hashes = []
        for cd in directories:
            hash_offset, _, special_count, page_count, limit = struct.unpack_from('>5I', cd, 16)
            hash_size, hash_type, _, page_shift = struct.unpack_from('4B', cd, 36)
            require(hash_type in (1, 2, 3), 'Unsupported code hash algorithm')
            algorithm = 'sha1' if hash_type == 1 else 'sha256'
            page_size = 1 << page_shift
            require(page_count == math.ceil(limit / page_size), 'Code page count mismatch')
            for page in range(page_count):
                payload = binary[page * page_size:min((page + 1) * page_size, limit)]
                expected = cd[hash_offset + page * hash_size:hash_offset + (page + 1) * hash_size]
                require(hashlib.new(algorithm, payload).digest()[:hash_size] == expected,
                        f'Code page hash mismatch: {path.name}, {CPUS[cpu]}, page {page}')
            if path.parent.name == 'MacOS':
                for slot, target in ((1, APP / 'Contents/Info.plist'),
                                     (3, APP / 'Contents/_CodeSignature/CodeResources')):
                    require(special_count >= slot, 'Missing bundle metadata signature')
                    start = hash_offset - slot * hash_size
                    require(hashlib.new(algorithm, target.read_bytes()).digest()[:hash_size] == cd[start:start + hash_size],
                            f'Signed bundle metadata mismatch: {target.name}')
            hashes.append(hashlib.new(algorithm, cd).digest()[:20].hex())
        records.append({'cpu': CPUS[cpu], 'bytes': size, 'codeDirectoryHashes': hashes})
    require({r['cpu'] for r in records} == {'x86_64', 'arm64'}, 'Missing CPU architecture')
    return records


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--replace', action='store_true', help='Replace the existing Mac ZIP after validating the new ZIP')
    args = parser.parse_args()
    require(not ARCHIVE.exists() or args.replace, 'Mac ZIP already exists; use --replace to update it')
    info = plistlib.loads((APP / 'Contents/Info.plist').read_bytes())
    require(info['CFBundlePackageType'] == 'APPL', 'Not an application bundle')
    require(info['CFBundleIdentifier'] == 'com.shadelink.prototype', 'Unexpected bundle ID')
    executable = APP / 'Contents/MacOS' / info['CFBundleExecutable']
    require(executable.is_file(), 'Bundle executable is missing')
    require((APP / 'Contents/Resources/Data/Managed/Assembly-CSharp.dll').is_file(), 'Game code is missing')
    native = {}
    for path in sorted(APP.rglob('*')):
        require(not path.is_symlink(), 'Symlinks require explicit packaging support')
        if path.is_file():
            with path.open('rb') as stream:
                magic = stream.read(4)
            if magic in (b'\xca\xfe\xba\xbe', b'\xcf\xfa\xed\xfe'):
                native[path.relative_to(APP / 'Contents').as_posix()] = audit_macho(path)
    require(len(native) >= 5, 'Missing native player or Mono runtime libraries')
    resources = plistlib.loads((APP / 'Contents/_CodeSignature/CodeResources').read_bytes())['files2']
    resource_count = 0
    for relative, record in resources.items():
        path = APP / 'Contents' / relative
        require(path.is_file(), f'Signed resource missing: {relative}')
        if 'hash2' in record:
            require(hashlib.sha256(path.read_bytes()).digest() == record['hash2'], f'Resource hash mismatch: {relative}')
            resource_count += 1
        elif 'cdhash' in record:
            known = {h for arch in native[relative] for h in arch['codeDirectoryHashes']}
            require(record['cdhash'].hex() in known, f'Native resource signature mismatch: {relative}')
        else:
            raise ValueError(f'Unhandled signed resource: {relative}')

    shutil.copyfile(PROJECT / 'Documentation/Legacy/배포안내/Mac_먼저읽어주세요.txt', FOLDER / '먼저읽어주세요.txt')
    shutil.copyfile(PROJECT / 'Documentation/Legacy/배포안내/Mac-자동검증.command', FOLDER / 'Mac-자동검증.command')
    files = sorted(path for path in FOLDER.rglob('*') if path.is_file())
    executable_paths = {APP / 'Contents' / name for name in native} | {FOLDER / 'Mac-자동검증.command'}
    temporary = ARCHIVE.with_suffix('.zip.partial')
    with zipfile.ZipFile(temporary, 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for path in files:
            entry = zipfile.ZipInfo.from_file(path, path.relative_to(FOLDER.parent).as_posix())
            entry.create_system = 3  # Unix: Archive Utility restores execute bits.
            entry.external_attr = (stat.S_IFREG | (0o755 if path in executable_paths else 0o644)) << 16
            entry.compress_type = zipfile.ZIP_DEFLATED
            archive.writestr(entry, path.read_bytes())
    with zipfile.ZipFile(temporary) as archive:
        require(archive.testzip() is None, 'ZIP CRC check failed')
        require(len(archive.namelist()) == len(files), 'ZIP file count mismatch')
        for path in files:
            name = path.relative_to(FOLDER.parent).as_posix()
            entry = archive.getinfo(name)
            require(hashlib.sha256(archive.read(name)).digest() == hashlib.sha256(path.read_bytes()).digest(), f'ZIP content mismatch: {name}')
            expected_mode = 0o755 if path in executable_paths else 0o644
            require(entry.create_system == 3 and stat.S_IMODE(entry.external_attr >> 16) == expected_mode,
                    f'ZIP permissions mismatch: {name}')
    temporary.replace(ARCHIVE)
    report = {
        'platform': 'macOS', 'unity': '6000.3.24f1', 'minimumOS': info['LSMinimumSystemVersion'],
        'architecture': ['x86_64', 'arm64'], 'nativeBinaries': native,
        'verifiedResourceHashes': resource_count, 'verifiedZipFiles': len(files),
        'unixExecutePermissionsPreserved': True, 'codePageHashesVerified': True,
        'appleDeveloperIDNotarized': False, 'runtimeTestedOnMac': False,
        'archive': ARCHIVE.name, 'bytes': ARCHIVE.stat().st_size,
        'sha256': hashlib.sha256(ARCHIVE.read_bytes()).hexdigest()
    }
    (PROJECT / 'Validation/macos-package-results.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({key: value for key, value in report.items() if key != 'nativeBinaries'}, ensure_ascii=True, indent=2))


if __name__ == '__main__':
    main()
