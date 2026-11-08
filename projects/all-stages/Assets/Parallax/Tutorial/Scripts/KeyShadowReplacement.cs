using ShadeLink.Gallery;
using UnityEngine;

namespace ShadeLink.Tutorial
{
        // Both solutions reveal the same clearly readable key shadow.
    public static class KeyShadowReplacement
    {
        static readonly Quaternion[] KeyRotations =
        {
            Quaternion.Euler(30.486f, -57.252f, -3.113f),
            Quaternion.Euler(30.486f, -57.252f, -3.113f)
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
                    KeyRotations[direction]);
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
                // Move the interaction pivot to the mesh's center without moving
                // the visible model from its calibrated replacement pose.
                Vector3 heightOffset = Vector3.up * 0.4f;
                Vector3 visualPosition = instance.transform.position + heightOffset;
                root.transform.position = root.transform.TransformPoint(localBounds.center) + heightOffset;
                // The pedestal's root and its centered collider define the display center.
                var pedestal = GameObject.Find("Museum pedestal · movable");
                if (pedestal != null)
                {
                    var pedestalCollider = pedestal.GetComponent<BoxCollider>();
                    Vector3 center = pedestalCollider != null
                        ? pedestal.transform.TransformPoint(pedestalCollider.center)
                        : pedestal.transform.position;
                    Vector3 horizontalOffset = new Vector3(center.x - root.transform.position.x, 0f,
                        center.z - root.transform.position.z);
                    root.transform.position += horizontalOffset;
                    visualPosition += horizontalOffset;
                }
                instance.transform.position = visualPosition;
                var collider = root.AddComponent<BoxCollider>();
                collider.center = Vector3.zero;
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
