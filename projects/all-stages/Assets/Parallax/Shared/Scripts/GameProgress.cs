using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace ShadeLink
{
    [Serializable]
    public sealed class GameProgressRecord
    {
        public int version = GameProgress.Version;
        public string stage = "Tutorial";
        public int checkpoint;
        public int tutorialStage = 1, tutorialStep, galleryRoom;
        public float tutorialFirstSize;
        public int milestones;
        public bool swordEncountered; // Optional in version 1; absent legacy values stay false.
        public string savedUtc, saveId;
        public GameProgressRecord Copy() => JsonUtility.FromJson<GameProgressRecord>(JsonUtility.ToJson(this));
        public string CheckpointKey => stage + ":" + checkpoint + ":" + tutorialStage + ":" + tutorialStep + ":" + galleryRoom + ":" + milestones + ":" + swordEncountered;
    }

    // One bounded, versioned slot. A new JSON file is flushed before an atomic
    // same-volume replace; a valid previous primary is kept as the backup.
    public static class GameProgress
    {
        public const int Version = 1;
        const string Magic = "PARALLAX_CHECKPOINT_V1";
        [Serializable] sealed class Envelope { public string magic, payload, sha256; }
        static string directoryOverride;
        static readonly bool ValidationProcess = Array.Exists(Environment.GetCommandLineArgs(), arg =>
            arg.StartsWith("-", StringComparison.Ordinal) && (arg.IndexOf("validat", StringComparison.OrdinalIgnoreCase) >= 0 ||
            arg.EndsWith("-test", StringComparison.OrdinalIgnoreCase) || string.Equals(arg, "-batchmode", StringComparison.OrdinalIgnoreCase)));
        public static bool IsUnisolatedValidation => ValidationProcess && directoryOverride == null;
        public static string LastError { get; private set; }
        public static bool RecoveredBackup { get; private set; }
        public static string SavePath => Path.Combine(directoryOverride ?? Application.persistentDataPath, "ParallaxCheckpoint.json");
        public static string BackupPath => SavePath + ".bak";
        public static string AdminSavePath => Path.Combine(directoryOverride ?? Application.persistentDataPath, "ParallaxCheckpoint.admin-session.json");

        public static bool SetSaveDirectoryForValidation(string directory)
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-player-shell-validate") < 0 &&
                Array.IndexOf(Environment.GetCommandLineArgs(), "-game-progress-validate") < 0 &&
                Array.IndexOf(Environment.GetCommandLineArgs(), "-eclipse-ending-validate") < 0)
            { LastError = "검증 전용 저장 경로는 검증 실행에서만 사용할 수 있습니다."; return false; }
            if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathRooted(directory))
            { LastError = "검증 저장 경로는 절대 경로여야 합니다."; return false; }
            directoryOverride = Path.GetFullPath(directory);
            LastError = null;
            return true;
        }

        public static bool Validate(GameProgressRecord value, out string reason)
        {
            reason = null;
            if (value == null || value.version != Version) reason = "지원하지 않는 저장 버전입니다.";
            else if (value.stage != "Tutorial" && value.stage != "Gallery" && value.stage != "Beach" && value.stage != "Eclipse") reason = "저장된 맵이 올바르지 않습니다.";
            else if (value.checkpoint < 0 || value.checkpoint > (value.stage == "Eclipse" ? 0 : value.stage == "Tutorial" ? 2 : 3)) reason = "저장된 체크포인트가 올바르지 않습니다.";
            else if (value.tutorialStage < 1 || value.tutorialStage > 3 || value.tutorialStep < 0 || value.tutorialStep > 3) reason = "튜토리얼 진행 값이 올바르지 않습니다.";
            else if (float.IsNaN(value.tutorialFirstSize) || float.IsInfinity(value.tutorialFirstSize) ||
                value.tutorialFirstSize < 0 || value.tutorialFirstSize > 4.66f) reason = "튜토리얼 크기 값이 올바르지 않습니다.";
            else if (value.galleryRoom < 0 || value.galleryRoom > 4 || value.milestones < 0 || value.milestones > 31) reason = "기믹 진행 값이 올바르지 않습니다.";
            else if (value.stage == "Tutorial" && value.tutorialStage == 1 && value.tutorialStep == 1 && value.tutorialFirstSize < 3.7f)
                reason = "첫 수업의 안전 배치를 복구할 수 없습니다.";
            else if (value.stage == "Gallery" && value.checkpoint != (value.galleryRoom == 0 ? 0 : value.galleryRoom < 3 ? 1 : value.galleryRoom - 1))
                reason = "전시실과 체크포인트가 일치하지 않습니다.";
            return reason == null;
        }

        static string Digest(string value)
        {
            using (var hash = SHA256.Create())
            {
                var result = hash.ComputeHash(Encoding.UTF8.GetBytes(value));
                return BitConverter.ToString(result).Replace("-", "").ToLowerInvariant();
            }
        }

        static bool Read(string path, out GameProgressRecord result, out string reason)
        {
            result = null; reason = null;
            if (!File.Exists(path)) { reason = "저장된 진행이 없습니다."; return false; }
            try
            {
                var info = new FileInfo(path);
                if (info.Length <= 0 || info.Length > 32768) throw new InvalidDataException("저장 파일 크기가 올바르지 않습니다.");
                var envelope = JsonUtility.FromJson<Envelope>(File.ReadAllText(path, Encoding.UTF8));
                if (envelope == null || envelope.magic != Magic || string.IsNullOrEmpty(envelope.payload) ||
                    !string.Equals(Digest(envelope.payload), envelope.sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("저장 파일의 무결성을 확인할 수 없습니다.");
                result = JsonUtility.FromJson<GameProgressRecord>(envelope.payload);
                if (!Validate(result, out reason)) { result = null; return false; }
                if (string.IsNullOrEmpty(result.saveId) || !DateTime.TryParse(result.savedUtc, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out _)) throw new InvalidDataException("저장 파일의 시간/식별 값이 올바르지 않습니다.");
                return true;
            }
            catch (Exception error) { result = null; reason = error.Message; return false; }
        }

        public static bool TryLoad(out GameProgressRecord result)
        {
            RecoveredBackup = false;
            if (Read(SavePath, out result, out var primaryError)) { LastError = null; return true; }
            if (Read(BackupPath, out result, out _)) { RecoveredBackup = true; LastError = null; return true; }
            LastError = primaryError;
            return false;
        }

        public static bool TrySave(GameProgressRecord record) => Write(record, SavePath, BackupPath);
        public static bool TrySaveAdminSession(GameProgressRecord record) => Write(record, AdminSavePath, AdminSavePath + ".bak");

        static bool Write(GameProgressRecord record, string target, string backup)
        {
            if (IsUnisolatedValidation) { LastError = "검증 실행에는 격리된 저장 경로가 필요합니다."; return false; }
            if (!Validate(record, out var reason)) { LastError = reason; return false; }
            string temporary = target + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                var saved = record.Copy();
                saved.version = Version; saved.savedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                saved.saveId = Guid.NewGuid().ToString("N");
                string payload = JsonUtility.ToJson(saved);
                var envelope = new Envelope { magic = Magic, payload = payload, sha256 = Digest(payload) };
                var bytes = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(envelope, true));
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (File.Exists(target))
                {
                    // A corrupt primary must never overwrite the good recovery copy.
                    bool validPrimary = Read(target, out _, out _);
                    File.Replace(temporary, target, validPrimary ? backup : null);
                }
                else File.Move(temporary, target);
                LastError = null; RecoveredBackup = false;
                return true;
            }
            catch (Exception error)
            {
                LastError = "진행을 저장하지 못했습니다: " + error.Message;
                return false;
            }
            finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch { } }
        }
    }
}
