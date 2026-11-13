using System;
using UnityEngine;

namespace ShadeLink.Beach
{
    public sealed class BeachLabel : MonoBehaviour
    {
        public string caption;
        public float size = .22f;
        public Color color = Color.white;
        public FontStyle style = FontStyle.Normal;
        void Start()
        {
            var font = FindFirstObjectByType<BeachGame>().font;
            var text = gameObject.AddComponent<TextMesh>(); text.text = caption; text.font = font; text.fontSize = 96;
            text.characterSize = size / 10; text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center; text.color = color;
            text.fontStyle = style;
            var material = new Material(Shader.Find("ShadeLink/WorldText")); material.mainTexture = font.material.mainTexture;
            GetComponent<MeshRenderer>().sharedMaterial = material;
            GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Font.textureRebuilt += UpdateFont;
        }
        void UpdateFont(Font font) { var mesh = GetComponent<TextMesh>(); if (mesh != null && font == mesh.font) GetComponent<Renderer>().sharedMaterial.mainTexture = font.material.mainTexture; }
        void OnDestroy() { Font.textureRebuilt -= UpdateFont; }
    }
}
