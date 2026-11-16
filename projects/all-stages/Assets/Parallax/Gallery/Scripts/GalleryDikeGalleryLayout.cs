using UnityEngine;

namespace ShadeLink.Gallery
{
    // The exit is the rear wall at EndZ. Keep the two work exhibits on either
    // side of it and leave the central receiver visible from the entrance.
    public static class GalleryDikeGalleryLayout
    {
        public static Vector3 BottleStation => new Vector3(-6,0,GalleryBottleBalanceLayout.EndZ-3.5f);
        public static Vector3 ScaleStation => new Vector3(6,0,GalleryBottleBalanceLayout.EndZ-3.8f);
        public static Vector3 ReceiverStation => new Vector3(0,0,
            GalleryBottleBalanceLayout.InsertAt+GalleryBottleBalanceLayout.Length*.5f);
        public static Vector3 BottleInstruction => BottleStation+new Vector3(0,.025f,-3.6f);
        public static float FrontLightingZ => Mathf.Min(BottleStation.z,ScaleStation.z)-3.5f;
        public static float RearLightingZ => Mathf.Min(GalleryBottleBalanceLayout.EndZ-1.2f,
            Mathf.Max(BottleStation.z,ScaleStation.z)+1.5f);
    }
}
