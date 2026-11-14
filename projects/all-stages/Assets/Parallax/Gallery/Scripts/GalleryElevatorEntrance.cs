using UnityEngine;
using ShadeLink.Tutorial;

namespace ShadeLink.Gallery
{
    // Adapt the saved museum entrance only for an arriving tutorial cabin.
    public static class GalleryElevatorEntrance
    {
        public static void Open(GalleryGame game)
        {
            var wall = game.transform.Find("Museum entrance end wall");
            if (wall == null)
            {
                foreach (var item in game.GetComponentsInChildren<Transform>(true))
                    if (item.name == "Museum entrance end wall") { wall = item; break; }
            }
            if (wall == null) throw new System.InvalidOperationException("Museum entrance wall not found");
            Material finish = wall.GetComponent<Renderer>().sharedMaterial;
            wall.gameObject.SetActive(false);
            foreach (var item in game.GetComponentsInChildren<Transform>(true))
                if (item.name == "Layered museum wall finish" &&
                    Mathf.Abs(item.position.z - .16f) < .05f && Mathf.Abs(item.position.x) < .1f)
                    item.gameObject.SetActive(false);
            var root = new GameObject("B1 · E01 museum arrival entrance").transform;
            root.SetParent(game.transform, false);
            float width = GalleryConceptWorld.Width, roof = GalleryConceptWorld.Roof;
            const float halfOpening = 1.48f, openingHeight = 3.1f;
            for (int side = -1; side <= 1; side += 2)
                Block(root, "Museum entrance side wall", new Vector3(side * (width / 2 + halfOpening) / 2, roof / 2, -.16f),
                    new Vector3(width / 2 - halfOpening, roof, .32f), finish);
            Block(root, "Museum entrance lintel", new Vector3(0, (roof + openingHeight) / 2, -.16f),
                new Vector3(halfOpening * 2, roof - openingHeight, .32f), finish);
            var palette = GalleryConceptArchitecture.MuseumPalette(GalleryWorld.Materials);
            float trimWidth = 7.8f - halfOpening;
            for (int side = -1; side <= 1; side += 2)
                GalleryConceptArchitecture.WallFinish(root, new Vector3(side * (halfOpening + trimWidth / 2), 0, .16f),
                    Quaternion.Euler(0, 180, 0), trimWidth, roof, palette);
            foreach (var mesh in root.GetComponentsInChildren<MeshFilter>())
            {
                if (mesh.GetComponent<Collider>() != null || mesh.sharedMesh == null) continue;
                var contact = mesh.gameObject.AddComponent<MeshCollider>(); contact.sharedMesh = mesh.sharedMesh;
            }
            var landing = new GameObject("Museum arrival warm landing light");
            landing.transform.SetParent(root, false); landing.transform.localPosition = new Vector3(0, 2.8f, .85f);
            var light = landing.AddComponent<Light>(); light.type = LightType.Point;
            light.color = new Color(1, .84f, .63f); light.intensity = .85f; light.range = 6;
            game.lighting.Rebuild();
        }

        static void Block(Transform parent, string name, Vector3 at, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = at; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            go.AddComponent<GalleryCaster>().shape = go.GetComponent<BoxCollider>();
        }
    }
}
