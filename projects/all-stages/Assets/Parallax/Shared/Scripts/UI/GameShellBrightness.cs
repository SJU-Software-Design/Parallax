using UnityEngine;

/// <summary>Final camera color gain. It never changes lights, shadow tests or puzzle visibility logic.</summary>
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public sealed class GameShellBrightness : MonoBehaviour
{
    Material material;
    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        float brightness = GameShellSettingsRuntime.Brightness;
        if (Mathf.Approximately(brightness, 1f)) { Graphics.Blit(source, destination); return; }
        if (material == null)
        {
            var shader = Resources.Load<Shader>("GameShell/SettingsBrightness");
            if (shader == null || !shader.isSupported) { Graphics.Blit(source, destination); return; }
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }
        material.SetFloat("_Brightness", brightness);
        Graphics.Blit(source, destination, material);
    }
    void OnDestroy() { if (material != null) Destroy(material); }
}
