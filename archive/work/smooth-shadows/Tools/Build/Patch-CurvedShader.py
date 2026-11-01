"""Compile the owned GallerySurface fragment for the copied DX11 Windows player.

No Unity Editor is available on this host. The ShaderLab source remains the
authoritative source for ordinary Unity builds. Only this fragment is replaced;
existing vertex bytecode and Unity binding metadata are preserved and verified.
"""
from pathlib import Path
import argparse, hashlib, json, struct, subprocess, sys
sys.path.insert(0,str(Path(__file__).parent/'shader-tools'))
import UnityPy
import lz4.block

def digest(data):return hashlib.sha256(data).hexdigest()

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--source',type=Path,required=True)
    parser.add_argument('--target',type=Path,required=True)
    parser.add_argument('--resources',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--fxc',type=Path,default=Path('C:/Program Files (x86)/Windows Kits/10/bin/10.0.26100.0/x64/fxc.exe'))
    args=parser.parse_args()
    if args.source.resolve()==args.target.resolve():raise ValueError('Never patch the source player in place')
    if not args.target.is_file():raise ValueError('Target must be an existing separate player copy')
    args.output.mkdir(parents=True,exist_ok=True)
    source=(args.resources/'GallerySurface.shader').read_text(encoding='utf-8-sig')
    fragment=source[source.index('            float sunlit('):source.index('            ENDCG')]
    if fragment.count('tex2D(_MainTex,i.uv)')!=1:raise ValueError('Unexpected texture sampling contract')
    fragment=fragment.replace('tex2D(_MainTex,i.uv)','_MainTex.Sample(sampler_MainTex,i.uv)')
    prefix='''cbuffer Globals : register(b0) {
    float4 _MainTex_ST : packoffset(c2);
    float4 _Color : packoffset(c3);
    float4 _Selection : packoffset(c4);
    float _Grid : packoffset(c5.x);
    float _Diagnostic : packoffset(c5.y);
    int _GalleryCasterCount : packoffset(c5.z);
    column_major float4x4 _GalleryCasterInverse[128] : packoffset(c6);
    float4 _GalleryWindows[4] : packoffset(c518);
    float3 _GalleryToSun : packoffset(c522);
    float _GalleryRoof : packoffset(c522.w);
};
Texture2D _MainTex : register(t0);
SamplerState sampler_MainTex : register(s0);
struct v2f {float4 pos:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float2 uv:TEXCOORD2;};
'''
    hlsl=args.output/'GallerySurface.fragment.hlsl'
    hlsl.write_text(prefix+(args.resources/'GalleryCurvedShadow.cginc').read_text(encoding='utf-8-sig')+'\n'+fragment,encoding='utf-8')
    compiled=args.output/'GallerySurface.fragment.dxbc'
    fxc=args.fxc
    subprocess.run([str(fxc),'/nologo','/T','ps_4_0','/E','frag','/O3','/Gis','/Zpc','/Fo',str(compiled),'/Fc',str(args.output/'GallerySurface.fragment.asm'),str(hlsl)],check=True)
    replacement=compiled.read_bytes()
    env=UnityPy.load(str(args.source))
    selected=[]
    before={obj.path_id:digest(obj.get_raw_data()) for obj in env.objects}
    for obj in env.objects:
        if obj.type.name!='Shader':continue
        data=obj.read_typetree()
        if data.get('m_ParsedForm',{}).get('m_Name')=='Parallax/GallerySurface':selected.append((obj,data))
    if len(selected)!=1:raise ValueError('Expected exactly one owned GallerySurface shader')
    obj,data=selected[0]
    passes=data['m_ParsedForm']['m_SubShaders'][0]['m_Passes']
    if len(passes)!=1:raise ValueError('Unexpected pass count')
    shader_pass=passes[0]
    names={index:name for name,index in shader_pass['m_NameIndices']}
    parameters=shader_pass['progFragment']['m_CommonParameters']
    buffers=parameters['m_ConstantBuffers']
    if len(buffers)!=1 or buffers[0]['m_Size']!=8368:raise ValueError('Unexpected fragment constant buffer layout')
    actual={names[p['m_NameIndex']]:(p['m_Index'],p['m_ArraySize']) for p in buffers[0]['m_VectorParams']+buffers[0]['m_MatrixParams']}
    expected={'_Color':(48,0),'_Selection':(64,0),'_Grid':(80,0),'_Diagnostic':(84,0),'_GalleryCasterCount':(88,0),'_GalleryCasterInverse':(96,128),'_GalleryWindows':(8288,4),'_GalleryToSun':(8352,0),'_GalleryRoof':(8364,0)}
    if actual!=expected:raise ValueError(f'Incompatible shader binding ABI: {actual}')
    textures=parameters['m_TextureParams']
    if len(textures)!=1 or names[textures[0]['m_NameIndex']]!='_MainTex' or textures[0]['m_Index']!=0 or textures[0]['m_SamplerIndex']!=0:raise ValueError('Unexpected texture binding ABI')
    if data['platforms']!=[4] or len(data['offsets'][0])!=1:raise ValueError('Only the verified single-segment DX11 player is supported')
    raw=lz4.block.decompress(bytes(data['compressedBlob']),uncompressed_size=data['decompressedLengths'][0][0])
    count=struct.unpack_from('<i',raw)[0]
    entries=[]
    for i in range(count):
        offset,length,segment=struct.unpack_from('<3i',raw,4+i*12)
        if segment:raise ValueError('Unexpected shader segment')
        entry=raw[offset:offset+length]
        if i==3:
            if struct.unpack_from('<i',entry,4)[0]!=17:raise ValueError('Unexpected fragment program type')
            dx=entry.index(b'DXBC')
            old_length=struct.unpack_from('<I',entry,28)[0]
            old_end=(32+old_length+3)&~3
            dx_length=struct.unpack_from('<I',entry,dx+24)[0]
            if dx+dx_length!=32+old_length:raise ValueError('Unexpected Unity DXBC wrapper')
            entry=entry[:28]+struct.pack('<I',dx-32+len(replacement))+entry[32:dx]+replacement
            entry+=b'\x00'*((-len(entry))%4)+raw[offset+old_end:offset+length]
        entries.append(entry)
    table=bytearray(struct.pack('<I',count));position=4+count*12
    for entry in entries:
        table.extend(struct.pack('<3i',position,len(entry),0));position+=len(entry)
    patched=bytes(table)+b''.join(entries)
    compressed=lz4.block.compress(patched,store_size=False)
    data['compressedBlob']=compressed;data['offsets']=[[0]]
    data['compressedLengths']=[[len(compressed)]];data['decompressedLengths']=[[len(patched)]]
    obj.save_typetree(data)
    # Avoid unnecessary whole-file alignment changes. Preserve every original
    # object byte/offset and append only the rewritten shader object instead.
    source_bytes=args.source.read_bytes()
    object_bytes=obj.data
    if object_bytes is None:raise ValueError('Shader serialization produced no data')
    header=obj.assets_file.header
    if header.version!=22 or header.endian!='<':raise ValueError('Unverified serialized-file format')
    record=struct.pack('<qqIi',obj.path_id,obj.byte_start-header.data_offset,obj.byte_size,obj.type_id)
    metadata=source_bytes[:header.data_offset]
    record_at=metadata.find(record)
    if record_at<0 or metadata.find(record,record_at+1)>=0:raise ValueError('Object record must be unique')
    appended_at=(len(source_bytes)+15)&~15
    serialized=bytearray(source_bytes+b'\x00'*(appended_at-len(source_bytes))+object_bytes)
    struct.pack_into('<qI',serialized,record_at+8,appended_at-header.data_offset,len(object_bytes))
    struct.pack_into('>Q',serialized,24,len(serialized))
    serialized=bytes(serialized)
    check=UnityPy.load(serialized)
    after={item.path_id:digest(item.get_raw_data()) for item in check.objects}
    altered=[key for key in before if before[key]!=after.get(key)]
    if altered!=[obj.path_id]:raise ValueError(f'Unexpected modified objects: {altered}')
    args.target.write_bytes(serialized)
    report={'source':str(args.source),'target':str(args.target),'source_sha256':digest(args.source.read_bytes()),'target_sha256':digest(serialized),'changed_object_ids':altered,'fragment_sha256':digest(replacement),'curve_include_sha256':digest((args.resources/'GalleryCurvedShadow.cginc').read_bytes()),'preserved':'all other serialized objects, vertex bytecode, resource bindings','limitation':'DX11 existing-player fragment replacement; not a Unity Editor build'}
    (args.output/'shader-patch-report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    print(json.dumps(report,indent=2))

if __name__=='__main__':main()
