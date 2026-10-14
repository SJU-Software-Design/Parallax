"""Extract the downloaded artist's embedded textures and mesh inventory; no network."""
from pathlib import Path
import json, base64, struct

project = Path(__file__).resolve().parents[2]
root = project.parent.parent
source = root / '에셋/Art_Gallery_ALLrounder18/원본/Art Gallery GLB.gltf'
dest = project / 'Assets/ThirdParty/ALLrounder18'
if not source.is_file():
    raise FileNotFoundError(
        f'Original ALLrounder18 download not found: {source}. '
        'The Unity project already includes extracted assets; re-extraction is optional. '
        'Place your downloaded Art Gallery GLB.gltf at this path only when rebuilding the imported meshes.'
    )
try:
    import numpy as np
except ImportError as exc:
    raise RuntimeError('Re-extraction requires NumPy. Install it with python -m pip install numpy.') from exc
dest.mkdir(parents=True, exist_ok=True)
g = json.loads(source.read_text())
buffers = [base64.b64decode(b['uri'].split(',',1)[1]) for b in g['buffers']]
def view(i):
    v=g['bufferViews'][i]; o=v.get('byteOffset',0)
    return buffers[v['buffer']][o:o+v['byteLength']]
for im in g['images']:
    (dest / (im['name']+'.png')).write_bytes(view(im['bufferView']))
def acc(i):
    a=g['accessors'][i]; dtype={5126:'<f4',5125:'<u4',5123:'<u2'}[a['componentType']]
    width={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[a['type']]
    return np.frombuffer(view(a['bufferView']),dtype=dtype,offset=a.get('byteOffset',0),count=a['count']*width).reshape(-1,width)
report=[]
for m in g['meshes']:
    for p in m['primitives']:
        pts=acc(p['attributes']['POSITION']); tri=acc(p['indices']).reshape(-1,3); uv=acc(p['attributes']['TEXCOORD_0'])
        # Weld shared positions before finding connected components.
        keys=[tuple(np.round(x,3)) for x in pts]; parent=list(range(len(pts))); seen={}
        def find(i):
            while parent[i]!=i: parent[i]=parent[parent[i]]; i=parent[i]
            return i
        def union(a,b): parent[find(a)]=find(b)
        for i,k in enumerate(keys):
            if k in seen: union(i,seen[k])
            else: seen[k]=i
        for t in tri: union(int(t[0]),int(t[1])); union(int(t[1]),int(t[2]))
        groups={}
        for i,t in enumerate(tri): groups.setdefault(find(int(t[0])),[]).append(i)
        for idx,ts in enumerate(groups.values()):
            verts=sorted(set(tri[ts].flatten().tolist())); vs=pts[verts]*.01
            # glTF node rotates X 90 degrees. Convert right-handed glTF into Unity left-handed.
            vs=np.stack([-vs[:,0],-vs[:,2],vs[:,1]],axis=1)
            info={'mesh':m['name'],'part':idx,'vertices':len(verts),'triangles':len(ts),'min':vs.min(0).tolist(),'max':vs.max(0).tolist(),'uvMin':uv[verts].min(0).tolist(),'uvMax':uv[verts].max(0).tolist()}
            report.append(info)
            name=m['name'].replace('.','_')+'_'+str(idx)
            # Export the original artist geometry and UVs as OBJ for Unity import.
            mapping={v:i+1 for i,v in enumerate(verts)}
            lines=['o '+name]+['v '+' '.join(map(str,v)) for v in vs]
            lines+=['vt '+str(u[0])+' '+str(1-u[1]) for u in uv[verts]]
            lines+=['f '+' '.join(f'{mapping[v]}/{mapping[v]}' for v in reversed(t.tolist())) for t in tri[ts]]
            (dest/(name+'.obj')).write_text('\n'.join(lines))
(dest/'mesh-inventory.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
