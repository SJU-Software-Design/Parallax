using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ShadeLink.Gallery;
using UnityEngine;

namespace ShadeLink.Tutorial
{
    // Opt-in native QA helper. Setup uses the production lesson selector; prop
    // checks call production BeginHold/Release, and stage-two drops use normal
    // FixedUpdate/PhysX. No completion/arrival flags or audio Play test doubles.
    public static class TutorialAudioBaselineValidation
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static readonly string[] LegacyFiles = {
            "Footstep81", "Land72", "PropGrab128", "PropRelease156", "MetalMedium14", "MetalHeavy157",
            "Door026", "PuzzleComplete036", "UIConfirm42", "UICancel142", "UIError145", "BgmB23"
        };

        static readonly string[] RefreshFiles = {
            "T02_Pickup", "T04_DieSmall", "T07_DieMedium", "T26_DieLarge", "T19_JumpLand",
            "T14_Footstep01", "T14_Footstep02", "T14_Footstep03", "T14_Footstep04", "BgmTB01_000_179"
        };

        public static IEnumerator Run(TutorialGame tutorial, Action<bool, string, string> check)
        {
            check(tutorial != null && tutorial.Controller != null, "Tutorial audio baseline has actual saved-scene player", "Production TutorialGame");
            bool originalEnabled = tutorial.enabled;
            TutorialAudio audio = null;
            TutorialLandingFeedback[] feedback = null;
            Action<float>[] originals = null;
            try
            {
                tutorial.enabled = true;
                Time.timeScale = 1;
                Call(tutorial, "StartTutorialStage", 1);
                Call(tutorial, "SetPaused", false);
                yield return null;
                audio = TutorialAudio.Instance;
                check(audio != null && TutorialAudio.Available, "User-selected TutorialAudio refresh bank is actually installed", "T02/T04/T07/T26/T14/T19 + TB01; PR41 controls retained");
                audio.CommonEffectsEnabled = true;
                check(!Get<bool>(tutorial, "inClimbRoom") && !Get<bool>(tutorial, "inSunlightRoom") && Get<int>(tutorial, "step") == 0 &&
                    Vector3.Distance(tutorial.Controller.transform.position, new Vector3(0, .03f, 0)) < .2f,
                    "Baseline starts in the first tutorial lesson", tutorial.Controller.transform.position.ToString("F3"));
                foreach (string file in LegacyFiles)
                {
                    var clip = Resources.Load<AudioClip>("TutorialAudio/Selected/" + file);
                    check(clip != null && clip.samples > 0 && clip.length > 0,
                        "Previous local tutorial resource remains included for compatibility", file);
                }
                foreach (string file in RefreshFiles)
                {
                    var clip = Resources.Load<AudioClip>("TutorialAudio/Refresh/" + file);
                    check(clip != null && clip.samples > 0 && clip.length > 0,
                        "User-selected refresh resource is actually included", file);
                }
                var steps = Get<AudioClip[]>(audio, "footstepClips");
                bool shortSteps = steps != null && steps.Length == 4;
                if (steps != null) foreach (var clip in steps)
                    shortSteps &= clip != null && clip.length >= .25f && clip.length <= .35f;
                check(shortSteps, "One footstep uses a short individual contact rather than the whole T14 recording", "Four 0.30-0.32 second source slices");
                var clips = Get<Dictionary<string, AudioClip>>(audio, "clips");
                check(clips != null && clips.Count == 12, "Twelve tutorial cue routes include three impact sizes", "Small/medium/large are distinct; unchanged UI/release/door routes retained");
                yield return WaitUntil(() => CurrentMusic(audio) != null && CurrentMusic(audio).isPlaying, 4,
                    "Selected TB01 tutorial BGM must actually start");
                var music = CurrentMusic(audio);
                int beforeMusic = music.timeSamples;
                yield return Wait(.2f);
                check(music.isPlaying && music.clip != null && music.clip.name == "BgmTB01_000_179" && Mathf.Abs(music.clip.length - 179f) < .01f && music.timeSamples > beforeMusic,
                    "Selected TB01 0:00-2:59 is playing and advancing samples", "Current sample=" + music.timeSamples);

                Call(tutorial, "SetPaused", true);
                yield return null;
                int pausedSample = music.timeSamples;
                yield return Wait(.25f);
                check(!music.isPlaying && Mathf.Abs(music.timeSamples - pausedSample) < 2048,
                    "Original tutorial pause preserves BGM sample position", pausedSample + "→" + music.timeSamples);
                Call(tutorial, "SetPaused", false);
                yield return Wait(.2f);
                check(music.isPlaying && music.timeSamples > pausedSample,
                    "Original tutorial BGM resumes without restarting", "Sample=" + music.timeSamples);

                // Seek only the actual AudioSource transport, then let normal
                // TutorialAudio.Update perform its repeat crossfade (no 179s wait).
                var endingMusic = CurrentMusic(audio);
                endingMusic.timeSamples = Mathf.RoundToInt((endingMusic.clip.length - 1.55f) * endingMusic.clip.frequency);
                yield return WaitUntil(() => CurrentMusic(audio) != endingMusic, 4,
                    "Selected TB01 must wrap through the production transport");
                music = CurrentMusic(audio);
                check(music.isPlaying && music.clip.name == "BgmTB01_000_179" && music.time < 4f,
                    "TB01 naturally crossfades back inside the authorized excerpt", "Playback-position fixture near 2:57; production Update owns the transition");

                var movement = PlayerMovementAudio.Attach(tutorial.Controller);
                var heardSteps = new HashSet<string>();
                for (int i = 0; i < 4; i++)
                {
                    movement.Reset();
                    movement.Tick(new Vector3(1.5f, 0, 0), true, true, .44f);
                    foreach (string file in new[] { "T14_Footstep01", "T14_Footstep02", "T14_Footstep03", "T14_Footstep04" })
                        if (Playing(audio, "movement.footstep", file)) heardSteps.Add(file);
                    yield return Wait(.36f);
                }
                check(heardSteps.Count == 4, "Production movement dispatch actually plays all four short T14 contacts", "PlayerMovementAudio.Tick walking-input fixture; not an actual walking-distance claim");
                movement.Reset();
                movement.Tick(Vector3.up, false, false, .02f);
                movement.Tick(Vector3.down * 4, false, false, .02f);
                movement.Tick(Vector3.zero, true, false, .02f);
                check(Playing(audio, "movement.land", "T19_JumpLand"),
                    "Production airborne-to-grounded dispatch actually plays selected T19", "PlayerMovementAudio.Tick landing-state fixture; no direct audio Play call");
                movement.Reset();

                // The same production prop methods used by ObjectInteraction
                // exercise the bank routing without supplying a fake initial cue.
                var practice = Get<GalleryProp>(tutorial, "die");
                check(practice != null && practice.GetComponent<TutorialLandingFeedback>() != null,
                    "Practice prop opts into original tutorial audio routing", practice == null ? "missing" : practice.title);
                practice.BeginHold();
                check(Playing(audio, "prop.grab", "T02_Pickup"),
                    "Actual production grab uses selected T02", "GalleryProp.BeginHold → PropReleasePhysics → TutorialAudio");
                // Raise the actual practice die above its existing support and
                // release through the normal prop path for a real small landing.
                practice.transform.position += Vector3.up * .65f;
                practice.body.position = practice.transform.position;
                Physics.SyncTransforms();
                yield return Wait(.04f);
                practice.Release();
                check(Playing(audio, "prop.release", "PropRelease156"),
                    "Actual production release retains PropRelease156", "GalleryProp.Release → PropReleasePhysics → TutorialAudio");
                yield return WaitUntil(() => Playing(audio, "impact.small", "T04_DieSmall"), 4,
                    "Actual practice die must land with selected small T04");
                check(Playing(audio, "impact.small", "T04_DieSmall"),
                    "Actual gravity-driven small die contact uses T04", "No PlayImpact or fake audio event supplied");
                int firstImpactVoices = PlayingCount(audio, "impact.small");
                var releasePhysics = practice.GetComponent<PropReleasePhysics>();
                bool extraContactsRejected = !releasePhysics.PlayImpact(3f) && !releasePhysics.PlayImpact(4f);
                check(firstImpactVoices == 1 && extraContactsRejected && PlayingCount(audio, "impact.small") == firstImpactVoices,
                    "Repeated contacts within the selected clip do not stack impact voices", "First contact came from PhysX; two immediate production contact-method probes must be suppressed");
                // A genuinely new hold/release must reset that cooldown. The
                // short real fall lands while the first contact tail still plays.
                practice.BeginHold();
                practice.transform.position += Vector3.up * .08f;
                practice.body.position = practice.transform.position;
                Physics.SyncTransforms();
                practice.Release();
                yield return WaitUntil(() => PlayingCount(audio, "impact.small") > firstImpactVoices, 2,
                    "Fresh regrab/release must permit its first actual impact");
                check(PlayingCount(audio, "impact.small") > firstImpactVoices,
                    "Fresh regrab resets suppression for a new gravity-driven drop", "0.08m physical drop; no manual impact/audio event supplied");
                audio.CommonEffectsEnabled = false;
                practice.BeginHold();
                practice.Release();
                yield return Wait(.08f);
                check(!HasCommonVoice(audio) && practice.GetComponent<AudioSource>() == null && CurrentMusic(audio).isPlaying,
                    "Legacy common-effects switch suppresses shared cues without a fallback", "Grab/release consumed; BGM remains active");
                audio.CommonEffectsEnabled = true;

                check(Physics.simulationMode == SimulationMode.FixedUpdate,
                    "Original two-dice arrival uses actual FixedUpdate physics", Physics.simulationMode.ToString());
                var dice = Get<GalleryProp[]>(tutorial, "climbDice");
                check(dice != null && dice.Length == 2, "Original stage two has two automatic-drop dice", "Medium and large");
                feedback = new TutorialLandingFeedback[2];
                originals = new Action<float>[2];
                int mediumLandings = 0, largeLandings = 0;
                for (int i = 0; i < 2; i++)
                {
                    feedback[i] = dice[i].GetComponent<TutorialLandingFeedback>();
                    originals[i] = Get<Action<float>>(feedback[i], "onImpact");
                    int index = i;
                    Action<float> observe = strength => { if (index == 0) mediumLandings++; else largeLandings++; };
                    feedback[i].Configure(originals[i] + observe);
                }
                Call(tutorial, "StartTutorialStage", 2);
                check(Get<bool>(tutorial, "climbArrivalPlaying") && !tutorial.CanInteract,
                    "Production stage selection starts the normal automatic arrival", "No body-release or completion flags supplied");
                bool mediumReleased = false, largeReleased = false, sequential = false;
                bool mediumSound = false, largeSound = false, noLocalSources = true, noDuplicateImpacts = true;
                double end = Time.realtimeSinceStartupAsDouble + 30;
                double stableSince = -1;
                while (Time.realtimeSinceStartupAsDouble < end)
                {
                    if (!dice[0].body.isKinematic) mediumReleased = true;
                    if (!largeReleased && !dice[1].body.isKinematic)
                    {
                        largeReleased = true;
                        sequential = mediumLandings == 1;
                    }
                    mediumSound |= Playing(audio, "impact.medium", "T07_DieMedium");
                    largeSound |= Playing(audio, "impact.large", "T26_DieLarge");
                    noDuplicateImpacts &= PlayingCount(audio, "impact.medium") <= 1 && PlayingCount(audio, "impact.large") <= 1;
                    noLocalSources &= dice[0].GetComponent<AudioSource>() == null && dice[1].GetComponent<AudioSource>() == null;
                    bool ready = mediumLandings == 1 && largeLandings == 1 && !Get<bool>(tutorial, "climbArrivalPlaying") &&
                        tutorial.CanInteract && dice[0].placement.Ready && dice[1].placement.Ready;
                    if (ready)
                    {
                        if (stableSince < 0) stableSince = Time.realtimeSinceStartupAsDouble;
                        if (Time.realtimeSinceStartupAsDouble - stableSince > .3) break;
                    }
                    else stableSince = -1;
                    yield return null;
                }
                check(mediumReleased && largeReleased && sequential && mediumLandings == 1 && largeLandings == 1,
                    "Both original dice release sequentially and land once", "medium=" + mediumLandings + ", large=" + largeLandings);
                check(mediumSound && largeSound, "Normal PhysX drops play distinct selected medium and large clips", "T07=" + mediumSound + ", T26=" + largeSound);
                check(noDuplicateImpacts, "Each normal automatic drop retains at most one impact voice per size", "No overlapping duplicate T07/T26 during the actual two-drop arrival");
                check(noLocalSources, "Pooled tutorial drops retain the PR41 null-source fix", "No synthesized local AudioSource on either die");
                check(!Get<bool>(tutorial, "climbArrivalPlaying") && tutorial.CanInteract && dice[0].placement.Ready && dice[1].placement.Ready,
                    "Original two-dice arrival completes and returns interaction", "Normal FixedUpdate; stable placements");
                check(CurrentMusic(audio) != null && CurrentMusic(audio).isPlaying,
                    "Selected BGM remains active throughout physical arrival", "TB01 0:00-2:59");
                Debug.Log("TUTORIAL_AUDIO_BASELINE_OK retainedResources=12 refreshResources=10 actualPropHooks=1 threeImpactSizes=1 twoNormalDrops=1 pauseResume=1 commonSwitch=1");
            }
            finally
            {
                if (feedback != null && originals != null)
                    for (int i = 0; i < feedback.Length; i++) if (feedback[i] != null) feedback[i].Configure(originals[i]);
                if (audio != null) audio.CommonEffectsEnabled = true;
                if (tutorial != null)
                {
                    Call(tutorial, "StartTutorialStage", 1);
                    Call(tutorial, "SetPaused", false);
                    tutorial.enabled = originalEnabled;
                }
                Time.timeScale = 1;
            }
        }

        static AudioSource CurrentMusic(TutorialAudio audio) => Get<AudioSource>(audio, "currentMusic");

        static bool Playing(TutorialAudio audio, string cue, string file)
        {
            foreach (object voice in Get<IList>(audio, "voices"))
            {
                var source = Get<AudioSource>(voice, "source");
                if (Get<string>(voice, "cue") == cue && source != null && source.isPlaying && source.clip != null && source.clip.name == file)
                    return true;
            }
            return false;
        }

        static int PlayingCount(TutorialAudio audio, string cue)
        {
            int count = 0;
            foreach (object voice in Get<IList>(audio, "voices"))
            {
                var source = Get<AudioSource>(voice, "source");
                if (Get<string>(voice, "cue") == cue && source != null && source.isPlaying) count++;
            }
            return count;
        }

        static bool HasCommonVoice(TutorialAudio audio)
        {
            foreach (object voice in Get<IList>(audio, "voices"))
            {
                string cue = Get<string>(voice, "cue");
                var source = Get<AudioSource>(voice, "source");
                if (cue != null && source != null && source.isPlaying &&
                    (cue.StartsWith("movement.", StringComparison.Ordinal) || cue.StartsWith("prop.", StringComparison.Ordinal) || cue.StartsWith("impact.", StringComparison.Ordinal))) return true;
            }
            return false;
        }

        static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Flags).GetValue(target);
        static void Call(object target, string name, params object[] arguments) => target.GetType().GetMethod(name, Flags).Invoke(target, arguments);
        static IEnumerator Wait(float seconds)
        {
            double end = Time.realtimeSinceStartupAsDouble + seconds;
            while (Time.realtimeSinceStartupAsDouble < end) yield return null;
        }
        static IEnumerator WaitUntil(Func<bool> ready, float seconds, string message)
        {
            double end = Time.realtimeSinceStartupAsDouble + seconds;
            while (!ready())
            {
                if (Time.realtimeSinceStartupAsDouble >= end) throw new TimeoutException(message);
                yield return null;
            }
        }
    }
}
