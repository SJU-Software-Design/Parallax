using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>Called only in the generated build project. Creates a native mixer without changing any selected clip.</summary>
public static class GameShellSettingsAssets
{
    const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    public const string MixerPath = "Assets/Parallax/Shared/Resources/GameShell/SettingsMixer.mixer";

    public static void Ensure()
    {
        var existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        if (existing != null) { Validate(existing); return; }
        Directory.CreateDirectory(Path.GetDirectoryName(MixerPath));
        AssetDatabase.Refresh();
        Type controllerType = EditorType("UnityEditor.Audio.AudioMixerController");
        var controller = (AudioMixer)controllerType.GetMethod("CreateMixerControllerAtPath", Flags)
            .Invoke(null, new object[] { MixerPath });
        object master = controllerType.GetProperty("masterGroup", Flags).GetValue(controller);
        Type groupType = master.GetType();
        object[] groups = new object[4]; groups[0] = master;
        string[] groupNames = { "Master", "Music", "Sfx", "Ambient" };
        for (int i = 1; i < groups.Length; i++)
        {
            groups[i] = controllerType.GetMethod("CreateNewGroup", Flags).Invoke(controller, new object[] { groupNames[i], false });
            controllerType.GetMethod("AddChildToParent", Flags).Invoke(controller, new[] { groups[i], master });
        }
        PropertyInfo exposed = controllerType.GetProperty("exposedParameters", Flags);
        Type parameterType = exposed.PropertyType.GetElementType();
        Array parameters = Array.CreateInstance(parameterType, groups.Length);
        for (int i = 0; i < groups.Length; i++)
        {
            object parameter = Activator.CreateInstance(parameterType);
            parameterType.GetField("name", Flags).SetValue(parameter, groupNames[i] + "Volume");
            object guid = groupType.GetMethod("GetGUIDForVolume", Flags).Invoke(groups[i], null);
            parameterType.GetField("guid", Flags).SetValue(parameter, guid);
            parameters.SetValue(parameter, i);
        }
        exposed.SetValue(controller, parameters);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(MixerPath, ImportAssetOptions.ForceUpdate);
        Validate(AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath));
        Debug.Log("GAME_SHELL_SETTINGS_MIXER_READY " + MixerPath);
    }
    static Type EditorType(string name)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var result = assembly.GetType(name, false);
            if (result != null) return result;
        }
        var loaded = Assembly.Load("UnityEditor.AudioModule");
        return loaded.GetType(name, true);
    }
    static void Validate(AudioMixer mixer)
    {
        if (mixer == null) throw new InvalidOperationException("Settings mixer asset was not created.");
        foreach (string groupName in new[] { "Music", "Sfx", "Ambient" })
        {
            bool found = false;
            foreach (var group in mixer.FindMatchingGroups(groupName)) if (group.name == groupName) found = true;
            if (!found) throw new InvalidOperationException("Settings mixer group missing: " + groupName);
        }
        foreach (string parameter in new[] { "MasterVolume", "MusicVolume", "SfxVolume", "AmbientVolume" })
            if (!mixer.GetFloat(parameter, out var value))
                throw new InvalidOperationException("Settings mixer parameter missing: " + parameter);
    }
}
