using ShadeLink.Gallery;
using UnityEngine;

namespace ShadeLink.Tutorial
{
    // The two key poses were measured against the two sculpture reference views.
    public static class KeyShadowReplacement
    {
        static readonly Quaternion[] KeyRotations =
        {
            Quaternion.Euler(-114.658f, 90f, -186.441f),
            Quaternion.Euler(-78.329f, -90f, -186.441f)
        };
        public static GalleryProp Create(GameObject model, Transform sculptureVisual,
            int direction, Quaternion beamAdjustment, Material silver)
        {
            if (model == null || sculptureVisual == null || direction < 0 || direction >= KeyRotations.Length)
                return null;
            var root = new GameObject("Matched silver key · reward");
            try
            {
                root.transform.SetPositionAndRotation(sculptureVisual.position,
                    beamAdjustment * KeyRotations[direction]);
                var instance = Object.Instantiate(model, root.transform, false);
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                    if (silver != null)
                    {
                        var materials = renderer.sharedMaterials;
                        for (int i = 0; i < materials.Length; i++) materials[i] = silver;
                        renderer.sharedMaterials = materials;
                    }
                bool hasBounds = false;
                Bounds localBounds = default;
                foreach (var filter in instance.GetComponentsInChildren<MeshFilter>())
                {
                    if (filter.sharedMesh == null) continue;
                    Bounds mesh = filter.sharedMesh.bounds;
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 corner = mesh.center + Vector3.Scale(mesh.extents,
                            new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                        Vector3 local = root.transform.InverseTransformPoint(filter.transform.TransformPoint(corner));
                        if (!hasBounds) { localBounds = new Bounds(local, Vector3.zero); hasBounds = true; }
                        else localBounds.Encapsulate(local);
                    }
                }
                if (!hasBounds) { Debug.LogError("Key replacement: the key model has no mesh."); Object.Destroy(root); return null; }
                // Imported model units may differ from the scene instance used for
                // calibration. Fit actual mesh dimensions instead of multiplying
                // an imported prefab scale by the old Inspector scale a second time.
                var source = sculptureVisual.GetComponentInChildren<MeshFilter>();
                if (source == null || source.sharedMesh == null) { Debug.LogError("Key replacement: the sculpture has no mesh."); Object.Destroy(root); return null; }
                Vector3 sourceSize = Vector3.Scale(source.sharedMesh.bounds.size, source.transform.lossyScale);
                float sourceLength = Mathf.Max(Mathf.Abs(sourceSize.x), Mathf.Abs(sourceSize.y), Mathf.Abs(sourceSize.z));
                float keyLength = Mathf.Max(localBounds.size.x, localBounds.size.y, localBounds.size.z);
                if (sourceLength < .0001f || keyLength < .0001f) { Debug.LogError($"Key replacement: invalid model size (sculpture {sourceLength}, key {keyLength})."); Object.Destroy(root); return null; }
                root.transform.localScale = Vector3.one * (sourceLength / keyLength);
                var collider = root.AddComponent<BoxCollider>();
                collider.center = localBounds.center;
                collider.size = localBounds.size;
                var body = root.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                var prop = root.AddComponent<GalleryProp>();
                prop.title = "Matched silver key";
                prop.mountedAtHome = true;
                return prop;
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                Object.Destroy(root);
                return null;
            }
        }
    }
}
