using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadeLink
{
    // Explicit developer launch only. Ordinary player startup never enters here.
    // It uses the same guarded admin-section loader as the visible pause menu.
    public sealed class AdminStageStartup : MonoBehaviour
    {
        public const string Flag = "-admin-start-section";
        public static bool Requested { get; private set; }
        public static bool Ready { get; private set; }
        public static string Scene { get; private set; }
        public static int Section { get; private set; } = -1;
        public static string Failure { get; private set; }
        static bool claimed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (claimed) return;
            claimed = true;
            var args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, Flag);
            if (at < 0) return;
            if (at + 2 >= args.Length || !int.TryParse(args[at + 2], out int section))
            { Failure = "관리자 시작 구간 인자가 올바르지 않습니다."; Debug.LogWarning(Failure); return; }
            string scene = null;
            foreach (string name in new[] { "Tutorial", "Gallery", "Beach", "Eclipse" })
                if (string.Equals(name, args[at + 1], StringComparison.OrdinalIgnoreCase)) scene = name;
            if (!AdminStageSections.IsValid(scene, section))
            { Failure = "관리자 시작 구간을 찾을 수 없습니다."; Debug.LogWarning(Failure); return; }
            Requested = true; Scene = scene; Section = section;
            var host = new GameObject("Explicit administrator section startup");
            DontDestroyOnLoad(host);
            host.AddComponent<AdminStageStartup>();
        }

        IEnumerator Start()
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 120;
            while (global::ParallaxGameShell.Instance == null && Time.realtimeSinceStartupAsDouble < deadline)
                yield return null;
            var shell = global::ParallaxGameShell.Instance;
            if (shell == null || !shell.AuthorizeAdminSectionStartup() ||
                !GameProgressRuntime.AdminTravel(Scene, Section))
            { Fail(GameProgressRuntime.LastError ?? "관리자 구간 시작을 준비하지 못했습니다."); yield break; }
            while (GameProgressRuntime.Transitioning && Time.realtimeSinceStartupAsDouble < deadline)
                yield return null;
            if (GameProgressRuntime.Transitioning || SceneManager.GetActiveScene().name != Scene ||
                !GameProgressRuntime.TestSession || !global::ParallaxGameShell.Administrator)
            { Fail(GameProgressRuntime.LastError ?? "관리자 구간 이동을 마치지 못했습니다."); yield break; }
            IGameProgressStage stage = Scene == "Tutorial" ? FindFirstObjectByType<Tutorial.TutorialGame>() :
                Scene == "Gallery" ? FindFirstObjectByType<Gallery.GalleryGame>() :
                Scene == "Beach" ? FindFirstObjectByType<Beach.BeachGame>() :
                FindFirstObjectByType<Eclipse.EclipseFinaleGame>();
            if (stage == null || !stage.ProgressReady)
            { Fail("선택한 구간의 플레이어가 준비되지 않았습니다."); yield break; }
            Ready = true;
            var record = stage.CaptureProgress();
            Debug.Log("ADMIN_STAGE_START_READY scene=" + Scene + " section=" + Section +
                " label=" + AdminStageSections.ForStage(Scene)[Section].Label +
                " checkpoint=" + record.checkpoint + " galleryRoom=" + record.galleryRoom +
                " test=" + GameProgressRuntime.TestSession + " paused=" + stage.ProgressPaused +
                " capture=false");
            Destroy(gameObject);
        }
        void Fail(string message)
        {
            Failure = message;
            Debug.LogError("ADMIN_STAGE_START_FAILED " + message);
            global::ParallaxGameShell.Instance?.Notify(message);
            Destroy(gameObject);
        }
    }
}

public partial class ParallaxGameShell
{
    internal bool AuthorizeAdminSectionStartup()
    {
        if (!ShadeLink.AdminStageStartup.Requested ||
            !ShadeLink.AdminStageSections.IsValid(ShadeLink.AdminStageStartup.Scene, ShadeLink.AdminStageStartup.Section))
            return false;
        AdminMode = true;
        return true;
    }
}
