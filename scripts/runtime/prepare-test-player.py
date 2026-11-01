"""Prepare an isolated empty Unity scene for native-engine source tests.
Requires UnityPy; never changes an original player or Unity source assets.
"""
import argparse
import json
import sys
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("--player", type=Path, required=True)
parser.add_argument("--project", type=Path, required=True)
parser.add_argument("--unitypy-path", default="")
args = parser.parse_args()
player = args.player.resolve()
marker = player / ".parallax-runtime-copy"
if not marker.is_file() or player == Path(marker.read_text(encoding="utf-8")).resolve():
    raise SystemExit("Refusing to change an original player: no isolated-copy marker.")
if args.unitypy_path:
    sys.path.insert(0, args.unitypy_path)
import UnityPy

data = player / "Parallax_Data"
scene = UnityPy.load(str(data / "level0"))
scene.file.objects.clear()
(data / "level0").write_bytes(scene.file.save())
settings = (args.project / "ProjectSettings/TagManager.asset").read_text(encoding="utf-8-sig")
section = settings.split("  layers:\n", 1)[1].split("  m_SortingLayers:", 1)[0]
layers = [line[4:].strip() for line in section.splitlines() if line.startswith("  - ")]
assert len(layers) == 32, "Expected exactly 32 project layers"
manager = UnityPy.load(str(data / "globalgamemanagers"))
for obj in manager.objects:
    if obj.type.name == "TagManager":
        tree = obj.read_typetree()
        tree["layers"] = layers
        obj.save_typetree(tree)
(data / "globalgamemanagers").write_bytes(manager.file.save())
(data / "RuntimeInitializeOnLoads.json").write_text(json.dumps({"root": [{
    "assemblyName": "Assembly-CSharp", "nameSpace": "", "className": "RuntimeTestBootstrap",
    "methodName": "Initialize", "loadTypes": 0, "isUnityClass": False
}]}), encoding="utf-8")
print("Prepared empty native test scene and synchronized project layer names.")
