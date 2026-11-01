"""Bake downloaded glTF geometry into a simple, auditable Unity import format."""
from pathlib import Path
import struct,json,numpy as np
ROOT=Path(__file__).resolve().parents[4]
SRC=ROOT/'에셋/Beach_Surf_and_Paddle_Kit_3DAssets'
OUT=Path(__file__).resolve().parents[2]/'Assets/ThirdParty/3DAssetsBeach';OUT.mkdir(parents=True,exist_ok=True)
def convert(path):
 b=path.read_bytes();assert b[:4]==b'glTF';n,t=struct.unpack_from('<II',b,12);j=json.loads(b[20:20+n]);o=20+n;bn,bt=struct.unpack_from('<II',b,o);blob=b[o+8:o+8+bn]
 assert not j.get('textures'),'Textures need explicit import support'
 def acc(i):
  a=j['accessors'][i];v=j['bufferViews'][a['bufferView']];dtype={5126:'<f4',5125:'<u4',5123:'<u2',5122:'<i2',5121:'u1',5120:'i1'}[a['componentType']];cols={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[a['type']];size=np.dtype(dtype).itemsize
  data=np.ndarray((a['count'],cols),dtype=dtype,buffer=blob,offset=v.get('byteOffset',0)+a.get('byteOffset',0),strides=(v.get('byteStride',cols*size),size)).copy()
  if a.get('normalized',False):data=np.maximum(-1,data.astype(float)/np.iinfo(dtype).max)
  return data
 parts=[]
 def node(i,parent):
  nd=j['nodes'][i];m=np.eye(4)
  if 'matrix' in nd:m=np.array(nd['matrix']).reshape(4,4).T
  else:
   x,y,z,w=nd.get('rotation',[0,0,0,1]);r=np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])
   m[:3,:3]=r@np.diag(nd.get('scale',[1,1,1]));m[:3,3]=nd.get('translation',[0,0,0])
  world=parent@m
  if 'mesh' in nd:
   for prim in j['meshes'][nd['mesh']]['primitives']:
    assert prim.get('mode',4)==4
    p=acc(prim['attributes']['POSITION']);p=(np.c_[p,np.ones(len(p))]@world.T)[:,:3];p[:,2]*=-1
    ix=acc(prim['indices']).reshape(-1,3) if 'indices' in prim else np.arange(len(p)).reshape(-1,3)
    ix=ix[:,[0,2,1]]
    mat=j.get('materials',[{}])[prim.get('material',0)].get('pbrMetallicRoughness',{})
    parts.append({'vertices':p.tolist(),'triangles':ix.flatten().tolist(),'color':mat.get('baseColorFactor',[1,1,1,1]),'metallic':mat.get('metallicFactor',0),'roughness':mat.get('roughnessFactor',.8)})
  for c in nd.get('children',[]):node(c,world)
 for i in j['scenes'][j.get('scene',0)]['nodes']:node(i,np.eye(4))
 xyz=np.concatenate([p['vertices'] for p in parts]);lo=xyz.min(0);hi=xyz.max(0);offset=np.array([(lo[0]+hi[0])/2,lo[1],(lo[2]+hi[2])/2])
 for p in parts:
  p['vertices']=(np.array(p['vertices'])-offset).flatten().round(6).tolist()
 d={'name':path.stem,'size':(hi-lo).tolist(),'parts':parts}
 (OUT/(path.stem+'.json')).write_text(json.dumps(d,separators=(',',':')),encoding='utf-8')
 print(path.stem,'size',np.round(hi-lo,3),'parts',len(parts),'triangles',sum(len(x['triangles'])//3 for x in parts))
for p in SRC.glob('*.glb'):convert(p)
(OUT/'LICENSE.txt').write_text('Beach Surf and Paddle Kit by 3DAssets.dev\nCC0 1.0 Universal\nhttps://3dassets.dev/packs/beach-surf-and-paddle-kit\nhttps://creativecommons.org/publicdomain/zero/1.0/\nPublisher discloses AI-assisted geometry.\nOriginal GLB files and SHA256 records are preserved in the project asset archive.\nMesh conversion: glTF transforms baked, right-to-left handed coordinates, centre X/Z and ground origin.\n',encoding='utf-8')
(OUT/'download-records.json').write_bytes((SRC/'download-records.json').read_bytes())
