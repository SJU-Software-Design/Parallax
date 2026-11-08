using UnityEngine;

namespace ShadeLink.Tutorial
{
    // A separate resource keeps the imported FBX and scene untouched while
    // selecting the geometry for one-variable difficulty comparisons.
    public sealed class KeyShadowPassageSettings : ScriptableObject
    {
        public const string ResourcePath = "Tutorial/KeyShadowPassageTest";
        public Mesh originalPassage;
        public Mesh narrowPassage;
        public bool useNarrowPassage = true;

        public Mesh SelectedMesh => useNarrowPassage ? narrowPassage : originalPassage;
        public string Label => useNarrowPassage ? "1안 · 구멍 폭 약 25%" : "원본 · 넓은 통로";
    }
}
