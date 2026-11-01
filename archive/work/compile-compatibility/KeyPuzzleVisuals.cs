using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadeLink
{
    public sealed class KeyPuzzleVisuals : MonoBehaviour
    {
        public Transform key;
        public int clickSounds;
        public AudioClip clickClip;
        KeyPuzzle puzzle;
        Transform leaf, lockRoot, shackle, barrel;
        Renderer lamp;
        Material gold, black, steel, teal, ivory, glow, textMaterial;
        readonly List<Object> resources = new List<Object>();
        AudioSource source;
        Font font;
        float rejectUntil;
        Material Material(string name, Color color, float metallic = 0)
        {
            var m = new Material(Shader.Find("Standard")) { name = name, color = color };
            m.SetFloat("_Metallic", metallic); m.SetFloat("_Glossiness", .38f); resources.Add(m); return m;
        }
        Transform Root(string name, Transform parent = null)
        { var t = new GameObject(name).transform; t.SetParent(parent == null ? transform : parent, false); return t; }
        Transform Shape(string name, PrimitiveType type, Vector3 at, Vector3 size, Material material, Transform parent)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = ShadeVisuals.ToUnity(at); go.transform.localScale = size;
            Destroy(go.GetComponent<Collider>()); var r = go.GetComponent<Renderer>(); r.sharedMaterial = material;
            // The key is a lock tool, not a new analytical shelter.
            r.shadowCastingMode = ShadowCastingMode.Off; return go.transform;
        }
        Transform Box(string name, Vector3 p, Vector3 size, Material m, Transform parent) => Shape(name, PrimitiveType.Cube, p, size, m, parent);
        Transform Cylinder(string name, Vector3 p, float radius, float depth, Material m, Transform parent)
        {
            var t = Shape(name, PrimitiveType.Cylinder, p, new Vector3(radius * 2, depth / 2, radius * 2), m, parent);
            t.localRotation = Quaternion.Euler(90, 0, 0); return t;
        }
        Transform Ring(string name, Vector3 p, float outer, float inner, float depth, Material m, Transform parent)
        {
            const int n = 48; var verts = new List<Vector3>(); var tris = new List<int>();
            for (int side = 0; side < 2; side++) for (int radius = 0; radius < 2; radius++) for (int i = 0; i < n; i++)
            { float a = i * 3.14159274f * 2 / n, r = radius == 0 ? outer : inner; verts.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, (side == 0 ? -1 : 1) * depth / 2)); }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Quad(tris, i, j, n + j, n + i); Quad(tris, 2*n+i, 3*n+i, 3*n+j, 2*n+j);
                Quad(tris, i, 2*n+i, 2*n+j, j); Quad(tris, n+i, n+j, 3*n+j, 3*n+i);
            }
            for (int i = 0; i < tris.Count; i += 3) { int swap = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = swap; }
            var mesh = new Mesh { name = name }; mesh.SetVertices(verts); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); resources.Add(mesh);
            var t = Root(name, parent); t.localPosition = ShadeVisuals.ToUnity(p); t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = t.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = m; renderer.shadowCastingMode = ShadowCastingMode.Off; return t;
        }
        static void Quad(List<int> t, int a, int b, int c, int d) { t.Add(a); t.Add(b); t.Add(c); t.Add(a); t.Add(c); t.Add(d); }
        void Text(string value, Vector3 p, float size, Color color, Transform parent, bool floor = false)
        {
            var t = Root(value, parent); t.localPosition = ShadeVisuals.ToUnity(p); t.localRotation = Quaternion.Euler(floor ? 90 : 0, 180, 0);
            var mesh = t.gameObject.AddComponent<TextMesh>(); mesh.text = value; mesh.font = font; mesh.fontSize = 96;
            mesh.characterSize = size * 10 / 96; mesh.anchor = TextAnchor.MiddleCenter; mesh.alignment = TextAlignment.Center; mesh.color = color;
            var r = t.GetComponent<MeshRenderer>(); r.sharedMaterial = textMaterial; r.shadowCastingMode = ShadowCastingMode.Off;
        }
        public void Build(KeyPuzzle owner, Font koreanFont)
        {
            puzzle = owner; font = koreanFont;
            gold = Material("Key warm brass", new Color(.88f, .55f, .17f), .68f);
            steel = Material("Lock polished steel", new Color(.48f, .59f, .61f), .8f);
            black = Material("Keyhole dark", new Color(.013f, .022f, .024f));
            teal = Material("Door enamel", new Color(.08f, .22f, .23f), .35f);
            ivory = Material("Key marker", new Color(.90f, .83f, .62f));
            glow = Material("Lock status", ShadeVisuals.Amber); glow.EnableKeyword("_EMISSION");
            textMaterial = new Material(Shader.Find("ShadeLink/WorldText")); textMaterial.mainTexture = font.material.mainTexture; resources.Add(textMaterial);
            Font.textureRebuilt += RefreshFont;
            key = Root("Carryable perspective key");
            var bow = Ring("Hollow key bow", new Vector3(0, 0, .25f), .26f, .17f, .07f, gold, key); bow.localRotation = Quaternion.Euler(90, 0, 0);
            Cylinder("Key shaft", new Vector3(0, 0, -.24f), .043f, .64f, gold, key);
            Cylinder("Key collar", new Vector3(0, 0, -.055f), .073f, .065f, steel, key);
            Box("Key tooth long", new Vector3(.10f, 0, -.485f), new Vector3(.20f, .08f, .075f), gold, key);
            Box("Key tooth short", new Vector3(.067f, 0, -.365f), new Vector3(.13f, .08f, .07f), gold, key);
            Shape("Key tip", PrimitiveType.Sphere, new Vector3(0, 0, -.558f), new Vector3(.086f, .08f, .08f), gold, key);
            Box("Key resting marker", new Vector3(KeyState.Start.x, .006f, KeyState.Start.y), new Vector3(1.15f, .01f, 1.5f), teal, transform);
            Text("KEY  /  열쇠", new Vector3(KeyState.Start.x, .02f, KeyState.Start.y + .94f), .25f, ShadeVisuals.Amber, transform, true);

            leaf = Root("Sliding ground-level door");
            Box("Door panel", new Vector3(4.65f, 1.3f, -15.81f), new Vector3(1.71f, 2.58f, .16f), teal, leaf);
            Box("Door inset", new Vector3(4.65f, 1.4f, -15.718f), new Vector3(1.41f, 2.04f, .017f), black, leaf);
            Box("Door center panel", new Vector3(4.65f, 1.4f, -15.701f), new Vector3(1.35f, 1.98f, .025f), teal, leaf);
            Text("잠금 장치", new Vector3(4.65f, 2.18f, -15.67f), .20f, ShadeVisuals.Amber, leaf);
            lockRoot = Root("Padlock", leaf); lockRoot.localPosition = ShadeVisuals.ToUnity(KeyPuzzle.Socket);
            Box("Lock housing", new Vector3(0, -.045f, -.085f), new Vector3(.57f, .55f, .17f), gold, lockRoot);
            barrel = Root("Rotating keyway", lockRoot);
            Cylinder("Lock barrel", Vector3.zero, .195f, .07f, steel, barrel);
            Cylinder("Key round hole", new Vector3(0, 0, .038f), .055f * KeyState.TargetScale, .005f, black, barrel);
            Box("Key bit slot", new Vector3(.102f * KeyState.TargetScale, 0, .041f), new Vector3(.205f, .087f, .006f) * KeyState.TargetScale, black, barrel);
            shackle = Root("Opening shackle", lockRoot);
            // U-shaped shackle: a curved top and two straight legs.
            for (int i = 0; i < 17; i++)
            {
                float a = i * 3.14159274f / 16;
                Shape("Shackle arc", PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * .18f, .33f + Mathf.Sin(a) * .18f, -.10f), Vector3.one * .072f, steel, shackle);
            }
            Box("Shackle left", new Vector3(-.18f, .24f, -.10f), new Vector3(.071f, .22f, .071f), steel, shackle);
            Box("Shackle right", new Vector3(.18f, .24f, -.10f), new Vector3(.071f, .22f, .071f), steel, shackle);
            lamp = Shape("Lock indicator", PrimitiveType.Sphere, new Vector3(0, -.23f, .016f), new Vector3(.052f, .052f, .025f), glow, lockRoot).GetComponent<Renderer>();
            Text("열쇠를 맞춰 넣으세요", new Vector3(2.38f, 1.74f, -15.96f), .21f, ShadeVisuals.Amber, transform);
            Text("F  크기 조절\nE  열쇠 넣기", new Vector3(2.38f, 1.25f, -15.96f), .18f, ShadeVisuals.Teal, transform);
            source = gameObject.AddComponent<AudioSource>(); source.spatialBlend = 0; source.volume = .5f;
            clickClip = MakeClick(); resources.Add(clickClip);
        }
        static AudioClip MakeClick()
        {
            const int rate = 44100; var samples = new float[(int)(rate * .32f)]; var random = new System.Random(4107);
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate;
                float transient = (float)(random.NextDouble() * 2 - 1) * Mathf.Exp(-t * 105) * .64f;
                float metal = (Mathf.Sin(2 * 3.14159274f * 1750 * t) + .4f * Mathf.Sin(2 * 3.14159274f * 3120 * t)) * Mathf.Exp(-t * 45) * .21f;
                float latch = t > .105f ? Mathf.Sin(2 * 3.14159274f * 310 * (t - .105f)) * Mathf.Exp(-(t - .105f) * 52) * .38f : 0;
                samples[i] = transient + metal + latch;
            }
            var clip = AudioClip.Create("Key turn and latch click", samples.Length, 1, rate, false); clip.SetData(samples, 0); return clip;
        }
        public void Click() { clickSounds++; source.PlayOneShot(clickClip); }
        public void Reject() { rejectUntil = Time.unscaledTime + .45f; }
        public void StopSound() { source.Stop(); clickSounds = 0; rejectUntil = 0; }
        public void SetPaused(bool paused) { if (paused) source.Pause(); else source.UnPause(); }
        void RefreshFont(Font changed) { if (changed == font) textMaterial.mainTexture = font.material.mainTexture; }
        public void Sync()
        {
            var s = puzzle.state; var cam = puzzle.game.visuals.view.transform;
            if (s.Busy)
            {
                float blend = s.stage == KeyStage.Inserting ? Mathf.SmoothStep(0, 1, s.clock / .3f) : s.stage == KeyStage.Opening ? 1 - Mathf.SmoothStep(0, 1, (s.clock / KeyState.OpenSeconds - .5f) * 2) : 1;
                Quaternion closeup = Quaternion.LookRotation(ShadeVisuals.ToUnity(KeyPuzzle.Socket + new Vector3(0, .07f, .18f)) - puzzle.unlockView);
                cam.SetPositionAndRotation(Vector3.Lerp(puzzle.viewFrom, puzzle.unlockView, blend), Quaternion.Slerp(puzzle.viewRotation, closeup, blend));
            }
            float turn = s.stage == KeyStage.Turning ? Mathf.SmoothStep(0, 1, s.clock / KeyState.TurnSeconds) : s.stage >= KeyStage.Opening ? 1 : 0;
            float opening = s.stage == KeyStage.Opening ? Mathf.SmoothStep(0, 1, s.clock / KeyState.OpenSeconds) : s.stage == KeyStage.Unlocked ? 1 : 0;
            leaf.localPosition = new Vector3(opening * 1.9f, 0, 0);
            shackle.localPosition = new Vector3(0, opening * .20f, 0); shackle.localRotation = Quaternion.Euler(0, -opening * 25, 0);
            barrel.localRotation = Quaternion.Euler(0, 0, -90 * turn);
            Color color = s.stage >= KeyStage.Opening ? ShadeVisuals.Teal : Time.unscaledTime < rejectUntil ? new Color(1, .23f, .07f) : ShadeVisuals.Amber;
            glow.color = color; glow.SetColor("_EmissionColor", color * .6f);
            key.localScale = Vector3.one * s.scale;
            if (s.stage == KeyStage.Floor || s.stage == KeyStage.Resizing)
            {
                key.SetPositionAndRotation(ShadeVisuals.ToUnity(new Vector3(s.position.x, .035f + .045f * s.scale, s.position.y)), Quaternion.identity);
            }
            else if (s.stage == KeyStage.Carrying)
            {
                key.SetPositionAndRotation(cam.TransformPoint(new Vector3(.44f, -.27f, 1.18f)), cam.rotation * Quaternion.Euler(120, 15, -20));
            }
            else
            {
                // Tip first, then rotate around the shaft; retain its chosen scale.
                Vector3 final = ShadeVisuals.ToUnity(KeyPuzzle.Socket + new Vector3(0, 0, .22f * s.scale));
                if (s.stage == KeyStage.Inserting)
                {
                    float t = Mathf.SmoothStep(0, 1, s.clock / KeyState.InsertSeconds);
                    Vector3 aligned = final + Vector3.forward * .75f;
                    key.position = t < .45f ? Vector3.Lerp(puzzle.insertionFrom, aligned, t / .45f) : Vector3.Lerp(aligned, final, (t - .45f) / .55f);
                    key.rotation = Quaternion.Slerp(puzzle.insertionRotation, Quaternion.identity, Mathf.Clamp01(t / .45f));
                }
                else key.SetPositionAndRotation(final + leaf.localPosition, Quaternion.Euler(0, 0, -90 * turn));
            }
        }
        void OnDestroy()
        {
            Font.textureRebuilt -= RefreshFont;
            foreach (Object item in resources) if (item != null) Destroy(item);
        }
    }
}
