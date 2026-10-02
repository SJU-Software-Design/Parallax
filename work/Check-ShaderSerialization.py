from pathlib import Path
import sys,struct,hashlib
sys.path.insert(0,str(Path(__file__).parent/'shader-tools'))
import UnityPy
source=Path('Parallax_Unity_v0.8.2_Ceramics/Parallax_Data/sharedassets0.assets')
env=UnityPy.load(str(source))
for obj in env.objects:
    if obj.path_id!=30:continue
    file=obj.assets_file
    print('FILE',file.header)
    print('OBJECT',{k:getattr(obj,k) for k in ('path_id','byte_start','byte_size','type_id')})
    before=obj.get_raw_data();tree=obj.read_typetree();obj.save_typetree(tree);after=obj.data
    print('OBJECT ROUNDTRIP',len(before),len(after),before==after)
    if before!=after:
        first=next(i for i in range(min(len(before),len(after))) if before[i]!=after[i]);print('FIRST',first,before[first-16:first+48].hex(),after[first-16:first+48].hex())
    saved=file.save();original=source.read_bytes()
    print('FILE ROUNDTRIP',len(original),len(saved),original==saved)
    Path('work/shader-inspection/unmodified-roundtrip.assets').write_bytes(saved)
    print('HEADERS',original[:64].hex(),saved[:64].hex())
    differences=[i for i in range(min(file.header.data_offset,len(saved),len(original))) if original[i]!=saved[i]]
    print('METADATA DIFF',len(differences),differences[:100])
