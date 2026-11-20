using UnityEngine;

namespace ShadeLink.Eclipse
{
    // Self-contained math also works with the existing exported runtime's stripped API.
    internal static class EclipseMath
    {
        public const float PI = 3.14159265358979323846f;
        public const float Rad2Deg = 180f / PI, Deg2Rad = PI / 180f;
        static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177;
                return (h ^ (h >> 16)) / 4294967295f;
            }
        }
        public static float Noise(float x, float y)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float u = x - ix, v = y - iy;
            u = u * u * (3 - 2 * u); v = v * v * (3 - 2 * v);
            return Mathf.Lerp(Mathf.Lerp(Hash(ix, iy), Hash(ix + 1, iy), u),
                Mathf.Lerp(Hash(ix, iy + 1), Hash(ix + 1, iy + 1), u), v);
        }
    }
}
