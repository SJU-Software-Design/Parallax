using System;
using UnityEngine;

namespace ShadeLink.Gallery
{
    public sealed class GalleryCaster : MonoBehaviour
    {
        public BoxCollider shape;
        public Matrix4x4 Inverse => Matrix4x4.TRS(shape.transform.TransformPoint(shape.center),
            shape.transform.rotation, Vector3.Scale(shape.transform.lossyScale, shape.size)).inverse;
    }
}
