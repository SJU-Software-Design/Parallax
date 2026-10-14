"""Download public CC0 geometry only; preserve source metadata and hashes."""
from pathlib import Path
import json, urllib.request, hashlib, concurrent.futures
ROOT=Path(__file__).resolve().parents[4]
OUT=ROOT/'에셋/Beach_Surf_and_Paddle_Kit_3DAssets'
manifest=json.loads((OUT/'manifest.json').read_text(encoding='utf-8'))['data']
wanted={'Beach Umbrella':'umbrella','Folding Beach Chair':'chair','Beach Ball':'ball','Volleyball':'volleyball','Striped Beach Towel':'mat','Sand Castle':'sandcastle','Dune Fence Run':'fence','Driftwood Log':'driftwood','Longboard Surfboard':'surfboard','Shade Shelter':'shelter'}
def download(a):
    name=a['title'].split(' (')[0]
    if name not in wanted:return None
    key=wanted[name];url=a['cdnUrl']
    assert url.startswith('https://cdn.3dassets.dev/assets/')
    r=urllib.request.Request(url,headers={'User-Agent':'Mozilla/5.0','Referer':'https://3dassets.dev/'})
    data=urllib.request.urlopen(r,timeout=45).read()
    assert data[:4]==b'glTF'
    (OUT/(key+'.glb')).write_bytes(data)
    return {'key':key,'title':a['title'],'source':a['url'],'download':url,'bytes':len(data),'sha256':hashlib.sha256(data).hexdigest(),'license':manifest['licenseLabel'],'aiGenerated':a.get('aiGenerated',True)}
with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool: records=[r for r in pool.map(download,manifest['assets']) if r]
(OUT/'download-records.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(records,ensure_ascii=False,indent=2))
