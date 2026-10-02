from pathlib import Path
import sys, json, struct
sys.path.insert(0, str(Path(__file__).parent / 'shader-tools'))
import UnityPy
import lz4.block
root=Path(sys.argv[1])
out=Path(__file__).parent/'shader-inspection'
out.mkdir(exist_ok=True)
for path in [root/'sharedassets0.assets',root/'globalgamemanagers.assets',root/'Resources'/'unity_builtin_extra',root/'Resources'/'unity default resources']:
    env=UnityPy.load(str(path))
    for obj in env.objects:
        if obj.type.name!='Shader':continue
        data=obj.read_typetree()
        name=data.get('m_Name') or data.get('m_ParsedForm',{}).get('m_Name')
        print(path.name,obj.path_id,name)
        if name!='Parallax/GallerySurface':continue
        blob=bytes(data['compressedBlob'])
        summary={k:v for k,v in data.items() if k!='compressedBlob'}
        (out/'shader-tree.json').write_text(json.dumps(summary,indent=2),encoding='utf-8')
        for i,p in enumerate(data['platforms']):
            for j,offset in enumerate(data['offsets'][i]):
                size=data['compressedLengths'][i][j]
                raw=lz4.block.decompress(blob[offset:offset+size],uncompressed_size=data['decompressedLengths'][i][j])
                (out/f'platform-{p}-segment-{j}.bin').write_bytes(raw)
                print('PLATFORM',p,'SEGMENT',j,'length',len(raw),'DXBC positions',[n for n in range(len(raw)) if raw[n:n+4]==b'DXBC'])
                if j==0:
                    count=struct.unpack_from('<i',raw)[0]
                    print('ENTRIES',count,[struct.unpack_from('<3i',raw,4+k*12) for k in range(count)])
                    for k in range(count):
                        at,length,segment=struct.unpack_from('<3i',raw,4+k*12)
                        entry=raw[at:at+length]
                        dx=entry.find(b'DXBC')
                        if dx<0:continue
                        code_size=struct.unpack_from('<I',entry,dx+24)[0]
                        (out/f'entry-{k}.dxbc').write_bytes(entry[dx:dx+code_size])
                        print('HEADER',k,entry[:dx].hex(),'DXBCSIZE',code_size,'SUFFIX',entry[dx+code_size:].hex())
