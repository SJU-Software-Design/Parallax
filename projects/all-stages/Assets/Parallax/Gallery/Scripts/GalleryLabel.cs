using System;
using UnityEngine;

namespace ShadeLink.Gallery
{
    public sealed class GalleryLabel : MonoBehaviour
    {
        public string caption;
        public float size = .22f;
        public Color color = Color.white;
        public Font fontOverride;
        public TextAnchor anchor = TextAnchor.MiddleCenter;
        public TextAlignment alignment = TextAlignment.Center;
        public float maximumWidth;
        void Start()
        {
            var font = fontOverride != null ? fontOverride : FindFirstObjectByType<GalleryGame>().font;
            var text = gameObject.AddComponent<TextMesh>(); text.text = caption; text.font = font; text.fontSize = 96;
            text.characterSize = size / 10; text.anchor = anchor; text.alignment = alignment; text.color = color;
            float width = GetComponent<Renderer>().localBounds.size.x;
            if (maximumWidth > 0 && width > maximumWidth) text.characterSize *= maximumWidth / width;
            var material = new Material(Shader.Find("ShadeLink/WorldText")); material.mainTexture = font.material.mainTexture;
            GetComponent<MeshRenderer>().sharedMaterial = material;
            Font.textureRebuilt += UpdateFont;
        }
        void UpdateFont(Font font) { var mesh = GetComponent<TextMesh>(); if (mesh != null && font == mesh.font) GetComponent<Renderer>().sharedMaterial.mainTexture = font.material.mainTexture; }
        void OnDestroy() { Font.textureRebuilt -= UpdateFont; }
    }
}
