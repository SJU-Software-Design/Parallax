using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ShadeLink.Editor
{
    public static class TutorialElevatorAudioAssets
    {
        public static readonly string[] Names = {
            "EL02_DoorOpen", "EL04_DoorClose", "EL07_DoorLatch", "EL09_MotorStart",
            "EL11_MotorRun", "EL25_MotorStop", "EL21_ArrivalBell", "EL23_Ventilation"
        };
        const string Folder = "Assets/Parallax/Tutorial/Resources/Tutorial/ElevatorAudio";

        public static void Prepare()
        {
            foreach (string name in Names)
            {
                string path = Folder + "/" + name + ".wav";
                if (!File.Exists(path)) throw new FileNotFoundException("Selected elevator audio missing", path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null) throw new InvalidOperationException("AudioImporter missing: " + path);
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.preloadAudioData = true;
                settings.compressionFormat = AudioCompressionFormat.PCM;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                importer.defaultSampleSettings = settings;
                if (!importer.SetOverrideSampleSettings(BuildTargetGroup.Standalone, settings))
                    throw new InvalidOperationException("Standalone audio settings failed: " + path);
                importer.forceToMono = false;
                importer.loadInBackground = false;
                importer.SaveAndReimport();
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null || clip.length <= 0 || clip.samples <= 0) throw new InvalidOperationException("Empty selected clip: " + path);
            }
            Debug.Log("ELEVATOR_SELECTED_AUDIO_PREPARED clips=8 format=PCM preload=1");
        }

        public static void WriteCredits(string output)
        {
            string credits = Folder + "/CREDITS.txt";
            if (!File.Exists(credits)) throw new FileNotFoundException("Elevator sound attribution missing", credits);
            File.Copy(credits, Path.Combine(output, "ElevatorAudio-CREDITS.txt"), true);
            const string baselineCredits = "Assets/Parallax/Tutorial/Resources/TutorialAudio/CREDITS.txt";
            if (!File.Exists(baselineCredits)) throw new FileNotFoundException("Original tutorial sound attribution missing", baselineCredits);
            File.Copy(baselineCredits, Path.Combine(output, "TutorialAudio-CREDITS.txt"), true);
        }
    }
}
