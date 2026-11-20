using System.Collections;
using UnityEngine;
using ShadeLink;
using ShadeLink.Tutorial;
using ShadeLink.Gallery;
using ShadeLink.Beach;

public sealed partial class PlayerShellValidation
{
    // These two guarded scene probes can safely finish after a failed check.
    // Keep every failure in the final report while collecting both diagnostics.
    void RecordPolishResult(bool passed, string name, string detail)
    {
        checks.Add(new CheckResult { passed = passed, name = name, detail = detail });
        Debug.Log((passed ? "PLAYER_SHELL_PASS " : "PLAYER_SHELL_FAIL ") + name + " " + detail);
    }
    IEnumerator PolishSequence()
    {
        // Validation deliberately skips the shell's live-settings side effects.
        // Explicitly install the real mixer before checking production routing.
        GameShellSettingsRuntime.Apply(ParallaxGameShell.Instance.appliedConfig.DeepCopy());
        yield return null;
        Check(GameShellSettingsRuntime.AudioRoutingReady, "Polish validation explicitly initializes the actual settings mixer");
        GameProgressRuntime.NewGame();
        yield return WaitForStage("Tutorial");
        var tutorial = FindFirstObjectByType<TutorialGame>();
        yield return TutorialSunlightGaugeValidation.Run(tutorial, Check);
        yield return PickupDeniedFeedbackValidation.Run(tutorial, Check);
        VerifyOwnership("Tutorial");
        yield return VerifyPause("Tutorial");
        GameProgressRuntime.ReturnToTitle();
        yield return WaitForStage("MainMenu");

        SetAdmin(true);
        GameProgressRuntime.AdminTravel("Beach");
        yield return WaitForStage("Beach");
        yield return PickupDeniedFeedbackValidation.Run(FindFirstObjectByType<BeachGame>(), Check);
        VerifyOwnership("Beach");
        yield return VerifyPause("Beach");
        Check(GameProgressRuntime.TestSession, "Beach polish checks preserve the admin sandbox separation");
        GameProgressRuntime.ReturnToTitle();
        yield return WaitForStage("MainMenu");
        SetAdmin(false);

        var fixture = new GameProgressRecord { stage = "Gallery", checkpoint = 1, galleryRoom = 2, milestones = 3 };
        Check(GameProgress.TrySave(fixture), "Polish checks use an explicit unsolved Gallery fixture", GameProgress.LastError);
        GameProgressRuntime.ContinueGame();
        yield return WaitForStage("Gallery", true);
        var gallery = FindFirstObjectByType<GalleryGame>();
        yield return PickupDeniedFeedbackValidation.Run(gallery, Check);
        yield return GalleryFinalRoomAppearanceValidation.Run(gallery, RecordPolishResult);
        yield return GalleryPlayerContactValidation.Run(gallery, RecordPolishResult);
        VerifyOwnership("Gallery");
        yield return VerifyPause("Gallery");
        Check(GameProgressRuntime.SaveCurrent(), "Gallery polish checks leave a valid saveable checkpoint", GameProgressRuntime.LastError);
        Check(!GameProgressRuntime.TestSession, "Gallery contact checks stay in the normal isolated session");
        GameProgressRuntime.ReturnToTitle();
        yield return WaitForStage("MainMenu");
        Check(GameProgressRuntime.HasSave && GameProgressRuntime.PeekSavedRecord().stage == "Gallery",
            "Polish run preserves a normal Gallery slot after admin testing");
    }
}