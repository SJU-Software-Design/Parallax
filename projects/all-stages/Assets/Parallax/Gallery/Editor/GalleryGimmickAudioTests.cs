using System;
using System.Reflection;
using ShadeLink.Gallery;
using UnityEngine;

namespace ShadeLink.Editor
{
    // The retained frame burst is an explicit user exception to the external
    // audio replacement. Check its samples against the pre-existing generator.
    public static class GalleryGimmickAudioTests
    {
        public static void Run()
        {
            var flags = BindingFlags.Static | BindingFlags.NonPublic;
            var originalMethod = typeof(GalleryNikeCanvas).GetMethod("MakeFusionClip", flags);
            var burstMethod = typeof(GalleryNikeCanvas).GetMethod("MakeEjectBurstClip", flags);
            Require(originalMethod != null && burstMethod != null, "Nike burst generators are missing");
            foreach (float brushSeconds in new[] { 2.6f, 3.2f })
            {
                AudioClip original = null, burst = null;
                try
                {
                    original = (AudioClip)originalMethod.Invoke(null, new object[] { brushSeconds });
                    burst = (AudioClip)burstMethod.Invoke(null, new object[] { brushSeconds });
                    Require(burst != null && burst.channels == 1 && burst.frequency == original.frequency,
                        "Retained burst changed its channel layout or sample rate");
                    Require(Mathf.Abs(burst.length - .48f) < .001f,
                        "Frame ejection must contain only the .48-second burst, without charge or painting silence");
                    var whole = new float[original.samples];
                    var tail = new float[burst.samples];
                    Require(original.GetData(whole, 0) && burst.GetData(tail, 0), "Burst sample data could not be read");
                    int start = Mathf.CeilToInt((.62f + brushSeconds) * original.frequency);
                    float peak = 0;
                    for (int i = 0; i < tail.Length; i++)
                    {
                        Require(tail[i] == whole[start + i], "Approved ejection timbre changed at sample " + i);
                        Require(!float.IsNaN(tail[i]) && !float.IsInfinity(tail[i]), "Invalid burst sample");
                        peak = Mathf.Max(peak, Mathf.Abs(tail[i]));
                    }
                    Require(peak > .1f && peak <= 1, "The retained frame burst is silent or clips");
                }
                finally
                {
                    if (original != null) UnityEngine.Object.DestroyImmediate(original);
                    if (burst != null) UnityEngine.Object.DestroyImmediate(burst);
                }
            }
            Debug.Log("GALLERY_GIMMICK_AUDIO_TESTS_OK retainedBurst=sample-identical timings=2");
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("GALLERY_GIMMICK_AUDIO: " + message);
        }
    }
}
