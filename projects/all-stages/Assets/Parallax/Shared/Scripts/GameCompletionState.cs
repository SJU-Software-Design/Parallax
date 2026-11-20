using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace ShadeLink
{
    // Completion is separate from the checkpoint slot: a new game does not
    // remove the credits unlock, and admin travel never changes the player profile.
    public static class GameCompletionState
    {
        const string Magic = "PARALLAX_ENDING_SEEN";
        [Serializable] sealed class Profile
        { public string magic; public int version; public bool endingSeen; public string completedUtc; }
        static string loadedPath;
        static bool persistentUnlocked, sessionUnlocked;
        public static string ProfilePath => Path.Combine(Path.GetDirectoryName(GameProgress.SavePath), "ParallaxEndingSeen.json");
        public static string BackupPath => ProfilePath + ".bak";
        public static bool CreditsUnlocked
        {
            get
            {
                if (GameProgress.IsUnisolatedValidation) return false;
                LoadCurrentProfile(); return persistentUnlocked || sessionUnlocked;
            }
        }
        static bool Read(string path)
        {
            try
            {
                if (!File.Exists(path) || new FileInfo(path).Length > 16384) return false;
                var value = JsonUtility.FromJson<Profile>(File.ReadAllText(path));
                return value != null && value.magic == Magic && value.version == 1 && value.endingSeen;
            }
            catch (Exception) { return false; }
        }
        static void LoadCurrentProfile()
        {
            string path = ProfilePath;
            if (string.Equals(path, loadedPath, StringComparison.OrdinalIgnoreCase)) return;
            loadedPath = path; sessionUnlocked = false;
            persistentUnlocked = Read(path) || Read(BackupPath);
        }
        // Called only after the successful natural EXIT -> MainMenu handoff.
        internal static void MarkEndingSeen(bool sandbox)
        {
            if (GameProgress.IsUnisolatedValidation) return;
            LoadCurrentProfile(); sessionUnlocked = true;
            if (sandbox || persistentUnlocked) return;
            string temporary = ProfilePath + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath));
                var value = new Profile { magic = Magic, version = 1, endingSeen = true, completedUtc = DateTime.UtcNow.ToString("O") };
                byte[] bytes = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(value, true));
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (File.Exists(ProfilePath)) File.Replace(temporary, ProfilePath, BackupPath);
                else File.Move(temporary, ProfilePath);
                persistentUnlocked = true;
                if (!Read(BackupPath))
                {
                    string backupTemporary = BackupPath + ".tmp";
                    using (var stream = new FileStream(backupTemporary, FileMode.Create, FileAccess.Write, FileShare.None))
                    { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                    if (File.Exists(BackupPath)) File.Replace(backupTemporary, BackupPath, null);
                    else File.Move(backupTemporary, BackupPath);
                }
            }
            catch (Exception error)
            { Debug.LogWarning("Credits unlock remains available this session; profile write failed: " + error.Message); }
        }
    }
}
