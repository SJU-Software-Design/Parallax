using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ShadeLink.Gallery
{
    // Playback uses the real saved museum and DSP clock, without time compression.
    public sealed class GallerySelectedAudioValidation : MonoBehaviour
    {
        public GalleryGame game;
        readonly Report report = new Report();
        readonly List<string> checks = new List<string>(), errors = new List<string>();
        readonly float[] outputSamples = new float[1024];
        MuseumAudio audio;
        GalleryConceptPerspective portrait;
        GalleryPortraitGate gate;
        string output, resultPath;
        double deadline, absorptionWall, redWall;
        int reds, smiles, ticks;
        float redElapsed, smileElapsed;
        bool subscribed;

        [Serializable] sealed class Report
        {
            public bool passed, clipsLoaded, bgmStarted, overlapPreserved, pauseResumePreserved;
            public bool fadeStartedAtAbsorption, fadeOutCompletedBeforeRed, fadePauseResumePreserved;
            public bool silenceThroughOpening, redCueOnce, smileOnce, faceNaturalEndSynchronized, actualCrossingResumed, resetCancels;
            public bool loopSeamPlaybackValidated, listenerPause, listenerPauseObserved;
            public bool loopSourcesFadedTogether;
            public int realTimeTicks, maximumVoices, redEvents, smileEvents, finalCheckpoint, outputSampleFrames;
            public float brushSeconds, faceSeconds, quietSimulationSeconds, quietWallSeconds, outputPeak;
            public float fadeOutSeconds, gainAtAbsorption, gainDuringFade, gainAfterFade;
            public float absorptionToSmileSeconds, absorptionToSmileWallSeconds;
            public float absorptionToRedSeconds, visualSmileCueCursor, faceLastPlayingCursor, faceEndWallSeconds, faceEndSkewSeconds;
            public float listenerVolume, selectedClipPeak;
            public string phase, failure, version, unity, audioDevice;
            public string scope = "Real saved museum, production audio service, AudioSources and wall-clock playback. Explicit published portrait solution poses and a movement-cue state fixture avoid pretending this is input-only puzzle solving. The complete portrait timeline runs through normal game Tick in real time, including a disclosed menu pause during its two-second BGM fade. The selected 151 derivative begins at the 2.5-second reddening milestone, survives the separate 4.5-second visual smile without retriggering, and naturally completes its 9.5-second playback at the 12-second full curtain opening; the final crossing uses its actual CharacterController. A separate disclosed fast-forward fixture checks reset cancellation. A disclosed BGM seek to 1.7 seconds before its end checks real two-source crossfade, a portrait-envelope request during that overlap, and advancing playback after silence and the seam, without waiting the whole song. Clip identity, pitch, playback cursor, natural completion, overlap, pause, fade gains, silence and cue counts are asserted. Per-frame listener peak and first-second brush PCM peak are diagnostics, not human listening, loudness balance or hardware-independent audible output.";
            public string[] checks, errors;
        }

        IEnumerator Start()
        {
            Application.logMessageReceived += Log;
            deadline = Time.realtimeSinceStartupAsDouble + 90;
            report.version = GalleryGame.Version; report.unity = Application.unityVersion;
            report.audioDevice = AudioSettings.driverCapabilities.ToString();
            var sequence = Sequence();
            while (true)
            {
                bool more; object next = null;
                try { Guard(); more = sequence.MoveNext(); if (more) next = sequence.Current; }
                catch (Exception exception) { report.failure = exception.ToString(); break; }
                if (!more) break;
                yield return next;
            }
            report.realTimeTicks = ticks; report.redEvents = reds; report.smileEvents = smiles;
            report.finalCheckpoint = game != null ? game.checkpoint : -1;
            report.checks = checks.ToArray(); report.errors = errors.ToArray();
            report.passed = report.failure == null && errors.Count == 0 && report.clipsLoaded && report.bgmStarted &&
                report.overlapPreserved && report.pauseResumePreserved && report.fadeStartedAtAbsorption &&
                report.fadeOutCompletedBeforeRed && report.fadePauseResumePreserved && report.silenceThroughOpening &&
                report.redCueOnce && report.smileOnce && report.faceNaturalEndSynchronized && report.actualCrossingResumed &&
                report.resetCancels && report.loopSeamPlaybackValidated && report.loopSourcesFadedTogether;
            try
            {
                if (output != null && game != null) Capture(report.passed ? "museum-audio-final" : "museum-audio-failure");
                if (resultPath != null) File.WriteAllText(resultPath, JsonUtility.ToJson(report, true));
            }
            catch (Exception exception) { report.passed = false; report.failure = exception.ToString(); }
            Unsubscribe();
            if (report.passed) Debug.Log("GALLERY_SELECTED_AUDIO_OK " + checks.Count + " checks");
            else Debug.LogError("GALLERY_SELECTED_AUDIO_FAIL phase=" + report.phase + " " + report.failure);
            Application.Quit(report.passed ? 0 : 1);
        }

        IEnumerator Sequence()
        {
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-museumAudioOutput");
            string destination = at >= 0 && at + 1 < args.Length ? Path.GetFullPath(args[at + 1]) :
                Path.Combine(Application.persistentDataPath, "MuseumSelectedAudioValidation");
            resultPath = destination.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? destination :
                Path.Combine(destination, "museum-selected-audio-results.json");
            output = Path.GetDirectoryName(resultPath); Directory.CreateDirectory(output);
            yield return null; yield return null;
            Require(game != null && game.automated && game.conceptLayout, "Real automated concept museum is required");
            audio = MuseumAudio.Instance; gate = game.portraitGate;
            portrait = game.GetComponentInChildren<GalleryConceptPerspective>(true);
            Require(audio != null && audio.gameObject == game.gameObject && gate != null && portrait != null,
                "Saved museum audio installation and actual portrait gate are required");
            gate.AbsorptionStarted += Absorbing; gate.ReddeningStarted += Reddening; gate.SmileStarted += Smiling; subscribed = true;
            foreach (string cue in new[] { "movement.footstep", "movement.land", "prop.grab", "prop.release",
                "nike.paint", "face.smile", "nike.shadow_fit", "nike.door_open", "hands.open", "hands.settle",
                "ui.confirm", "ui.cancel", "ui.error" }) Require(audio.HasClip(cue), "Missing selected clip: " + cue);
            report.clipsLoaded = true; game.conceptCapture = true;

            report.phase = "selected C01 music starts from normal Resume";
            game.ResetAll(); game.Resume(); yield return new WaitForSecondsRealtime(.2f);
            float musicTime = audio.MusicTime; yield return new WaitForSecondsRealtime(.18f);
            Require(audio.GameplayRunning && audio.MusicAudible && audio.MusicTime > musicTime + .08f,
                "Normal Resume did not start advancing museum BGM");
            report.bgmStarted = true; SampleOutput();
            Check("C01 BGM starts and advances through the real game Resume hook");

            report.phase = "brush voice survives simultaneous movement and grab";
            MuseumAudio.TryPlay("nike.paint", this, game.view.transform.position); yield return null;
            AudioSource brush = Playing("nike.paint"); Require(brush != null, "Selected brush source did not play");
            AudioClip brushClip = brush.clip; float brushPitch = brush.pitch, brushTime = brush.time;
            report.brushSeconds = brushClip.length;
            var pcm = new float[Mathf.Min(brushClip.samples, brushClip.frequency) * brushClip.channels];
            if (brushClip.GetData(pcm, 0)) foreach (float sample in pcm) report.selectedClipPeak = Mathf.Max(report.selectedClipPeak, Mathf.Abs(sample));
            var movement = PlayerMovementAudio.Attach(game.controller); movement.Reset();
            Require(movement.Tick(Vector3.forward * 4, true, true, .44f) == PlayerMovementAudio.Cue.Footstep,
                "Movement fixture did not route a production footstep");
            movement.Tick(Vector3.up * 6, false, true, .01f);
            movement.Tick(Vector3.down * 8, false, false, .1f);
            Require(movement.Tick(Vector3.zero, true, false, .01f) == PlayerMovementAudio.Cue.Land,
                "Movement fixture did not route a production landing");
            Require(MuseumAudio.TryPlayPropGrab(game.props[0].body), "Museum prop grab did not use its selected route");
            report.maximumVoices = audio.ActiveVoiceCount;
            Require(audio.ActiveVoiceCount >= 4, "Brush, footstep, landing and grab did not own independent voices");
            yield return new WaitForSecondsRealtime(.3f);
            Require(brush.isPlaying && brush.clip == brushClip && Mathf.Abs(brush.pitch - brushPitch) < .0001f &&
                brush.time > brushTime + .12f, "Movement or prop playback cut/replaced/retuned the brush voice");
            report.overlapPreserved = true;
            Check("Actual movement cue routing and prop grab overlap the brush without changing its source, pitch or cursor");

            report.phase = "pause preserves music and active one-shot cursors";
            game.Pause(); brushTime = brush.time; musicTime = audio.MusicTime;
            yield return new WaitForSecondsRealtime(.18f);
            Require(!audio.GameplayRunning && !audio.MusicAudible && Mathf.Abs(brush.time - brushTime) < .06f &&
                Mathf.Abs(audio.MusicTime - musicTime) < .06f, "Pause did not retain the active playback positions");
            game.Resume(); yield return new WaitForSecondsRealtime(.2f);
            Require(audio.MusicAudible && audio.MusicTime > musicTime + .08f && brush.isPlaying &&
                brush.clip == brushClip && Mathf.Abs(brush.pitch - brushPitch) < .0001f && brush.time > brushTime + .08f,
                "Resume restarted or lost an active music/brush voice");
            report.pauseResumePreserved = true;
            yield return new WaitForSecondsRealtime(Mathf.Max(.1f, brushClip.length / brushPitch - brush.time + .15f));
            Require(!brush.isPlaying, "A completed one-shot brush unexpectedly loops");
            Check("Pause freezes and Resume continues music and the same brush voice; its one-shot then finishes naturally");

            report.phase = "portrait fades BGM, then 151 plays from reddening through full curtain opening";
            game.StartConceptDemo(2); SolveFixture();
            Require(audio.MusicAudible && !audio.PortraitSilenced, "A completed face muted music before absorption");
            while (!gate.Performing) { Step(Vector2.zero); yield return null; }
            Require(gate.Stage == GalleryPortraitGate.RevealStage.Absorbing && audio.PortraitSilenced &&
                !audio.PortraitFadeOutComplete && audio.MusicAudible && audio.MusicGainLevel > .05f,
                "Real absorption start cut the BGM instead of beginning its fade");
            report.fadeStartedAtAbsorption = true; report.gainAtAbsorption = audio.MusicGainLevel;
            int beforeRed = reds, beforeSmile = smiles, requests = audio.RequestCount;
            bool sampledMiddle = false, sampledEnd = false;
            double fadedWall = 0;
            while (reds == beforeRed)
            {
                Require(audio.PortraitSilenced, "Portrait lost its fade/quiet state before reddening");
                if (!report.fadePauseResumePreserved && gate.PerformanceElapsed >= .55f)
                {
                    game.Pause(); float progress = audio.PortraitFadeOutProgress;
                    float pausedGain = audio.MusicGainLevel, pausedTime = audio.MusicTime, pausedPerformance = gate.PerformanceElapsed;
                    yield return new WaitForSecondsRealtime(.22f);
                    Require(!audio.GameplayRunning && Mathf.Abs(audio.PortraitFadeOutProgress - progress) < .001f &&
                        Mathf.Abs(audio.MusicGainLevel - pausedGain) < .001f && Mathf.Abs(audio.MusicTime - pausedTime) < .06f &&
                        Mathf.Abs(gate.PerformanceElapsed - pausedPerformance) < .001f,
                        "Menu pause advanced the portrait fade, BGM cursor or performance timeline");
                    game.Resume(); yield return null;
                    Require(audio.PortraitSilenced && !audio.PortraitFadeOutComplete && audio.MusicAudible &&
                        audio.PortraitFadeOutProgress >= progress && audio.MusicGainLevel <= pausedGain + .001f,
                        "Resume reset or skipped the in-progress portrait fade");
                    report.fadePauseResumePreserved = true;
                }
                if (!sampledMiddle && gate.PerformanceElapsed >= .95f)
                {
                    report.gainDuringFade = audio.MusicGainLevel; sampledMiddle = true;
                    Require(!audio.PortraitFadeOutComplete && audio.MusicAudible &&
                        report.gainDuringFade > report.gainAtAbsorption * .25f && report.gainDuringFade < report.gainAtAbsorption * .75f,
                        "Portrait BGM did not decay gradually through the middle of its two-second fade");
                }
                if (audio.PortraitFadeOutComplete && !sampledEnd)
                {
                    report.fadeOutSeconds = gate.PerformanceElapsed;
                    report.gainAfterFade = audio.MusicGainLevel; sampledEnd = true;
                    fadedWall = Time.realtimeSinceStartupAsDouble; musicTime = audio.MusicTime;
                    Require(report.fadeOutSeconds >= 1.85f && report.fadeOutSeconds <= 2.15f &&
                        !audio.MusicAudible && report.gainAfterFade < .0001f,
                        "Portrait BGM did not reach zero and pause after two gameplay seconds");
                    report.fadeOutCompletedBeforeRed = true;
                }
                if (sampledEnd)
                    Require(audio.PortraitFadeOutComplete && !audio.MusicAudible && audio.MusicGainLevel < .0001f &&
                        Mathf.Abs(audio.MusicTime - musicTime) < .06f,
                        "BGM cursor advanced or music escaped after the fade reached zero");
                Step(Vector2.zero); yield return null;
            }
            Require(sampledMiddle && sampledEnd, "Portrait fade did not expose its initial, middle and zero-gain milestones");
            report.absorptionToRedSeconds = redElapsed;
            report.quietSimulationSeconds = redElapsed - report.fadeOutSeconds;
            report.quietWallSeconds = (float)(Time.realtimeSinceStartupAsDouble - fadedWall);
            AudioSource face = Playing("face.smile");
            Require(reds == beforeRed + 1 && smiles == beforeSmile && Mathf.Abs(redElapsed - 2.5f) < .065f && face != null &&
                face.clip == audio.Clip("face.smile") && Mathf.Abs(face.pitch - 1) < .0001f && !face.loop &&
                audio.RequestCount == requests + 1 && audio.PortraitFadeOutComplete && !audio.MusicAudible,
                "151 did not start exactly once after BGM silence at the 2.5-second reddening milestone");
            report.faceSeconds = face.clip.length;
            Require(Mathf.Abs(face.clip.length - 9.5f) < .08f &&
                Mathf.Abs(face.clip.length - (GalleryPortraitGate.TotalPerformanceSeconds - GalleryPortraitGate.AbsorbSeconds)) < .08f &&
                face.time < .15f && report.quietSimulationSeconds > .3f && report.quietSimulationSeconds < .7f,
                "151 duration does not span reddening to full opening after the half-second quiet interval");
            report.redCueOnce = true; Capture("museum-audio-reddening");
            bool observedVisualSmile = false;
            float lastFaceCursor = face.time;
            while (!gate.Opened)
            {
                Require(audio.PortraitFadeOutComplete && !audio.MusicAudible && Mathf.Abs(audio.MusicTime - musicTime) < .06f,
                    "BGM restarted during smile or curtain opening");
                Require(reds == beforeRed + 1 && audio.RequestCount == requests + 1,
                    "151 retriggered or an unselected curtain cue was added during the portrait performance");
                if (gate.PerformanceElapsed < GalleryPortraitGate.TotalPerformanceSeconds - .2f)
                    Require(face.isPlaying && face.clip == audio.Clip("face.smile") && Mathf.Abs(face.pitch - 1) < .0001f && !face.loop,
                        "151 was cut, replaced, retuned or looped before the curtain finished opening");
                if (face.isPlaying)
                {
                    Require(face.time >= lastFaceCursor - .06f, "151 cursor restarted during the same portrait performance");
                    lastFaceCursor = face.time; report.faceLastPlayingCursor = lastFaceCursor;
                }
                if (!observedVisualSmile && smiles > beforeSmile)
                {
                    report.visualSmileCueCursor = face.time;
                    Require(smiles == beforeSmile + 1 && Mathf.Abs(smileElapsed - 4.5f) < .065f && face.isPlaying &&
                        Mathf.Abs(face.time - (smileElapsed - redElapsed)) < .18f,
                        "The unretimed visual smile did not preserve the already-playing reddening cue");
                    report.absorptionToSmileSeconds = smileElapsed;
                    report.absorptionToSmileWallSeconds = (float)(Time.realtimeSinceStartupAsDouble - absorptionWall);
                    observedVisualSmile = true; Capture("museum-audio-smile");
                }
                Step(Vector2.zero); yield return null;
            }
            Require(audio.PortraitSilenced && !audio.MusicAudible && Mathf.Abs(audio.MusicTime - musicTime) < .06f &&
                reds == beforeRed + 1 && smiles == beforeSmile + 1 && observedVisualSmile && audio.RequestCount == requests + 1,
                "An opened curtain restarted music, repeated 151, or added an unselected curtain cue");
            double faceEndDeadline = Time.realtimeSinceStartupAsDouble + .35;
            while (face.isPlaying)
            {
                Require(Time.realtimeSinceStartupAsDouble < faceEndDeadline, "151 continued beyond full curtain opening");
                Require(face.time >= lastFaceCursor - .06f, "151 restarted at full curtain opening");
                lastFaceCursor = face.time; report.faceLastPlayingCursor = lastFaceCursor;
                yield return null;
            }
            report.faceEndWallSeconds = (float)(Time.realtimeSinceStartupAsDouble - redWall);
            report.faceEndSkewSeconds = report.faceEndWallSeconds - face.clip.length;
            Require(report.faceLastPlayingCursor > face.clip.length - .22f && Mathf.Abs(report.faceEndSkewSeconds) < .3f &&
                Mathf.Abs(gate.PerformanceElapsed - GalleryPortraitGate.TotalPerformanceSeconds) < .001f &&
                audio.RequestCount == requests + 1 && !face.loop,
                "151 did not finish naturally near its full 9.5-second cursor at complete curtain opening");
            report.faceNaturalEndSynchronized = true;
            report.silenceThroughOpening = report.smileOnce = true;
            Check("Production BGM fades over two gameplay seconds, holds half a second of quiet, then 151 plays once from reddening at 2.5 seconds through full curtain opening at 12 seconds; the visual smile at 4.5 seconds does not restart it");

            report.phase = "actual controller crosses open curtain and resumes BGM";
            double crossingDeadline = Time.realtimeSinceStartupAsDouble + 12;
            while (game.player.feet.z < 44.8f || game.checkpoint < 3)
            {
                Require(Time.realtimeSinceStartupAsDouble < crossingDeadline, "Actual controller failed to cross the open curtain");
                Vector3 delta = new Vector3(0, 0, 45.05f) - game.player.feet; delta.y = 0;
                Step(new Vector2(delta.x, delta.z).normalized); yield return null;
            }
            Require(!audio.PortraitSilenced && audio.MusicAudible && reds == beforeRed + 1 && smiles == beforeSmile + 1,
                "Actual checkpoint 3 crossing did not resume BGM without repeating the smile");
            musicTime = audio.MusicTime; yield return new WaitForSecondsRealtime(.18f);
            Require(audio.MusicTime > musicTime + .08f, "Resumed next-room BGM cursor did not advance");
            report.actualCrossingResumed = true;
            Check("Real CharacterController traversal reaches checkpoint 3 and resumes the preserved BGM cursor");

            report.phase = "public room reset cancels a pending or playing portrait cue";
            game.StartConceptDemo(2); SolveFixture();
            gate.Tick(GalleryPortraitGate.HoldSeconds + .01f);
            Require(audio.PortraitSilenced, "Replay did not silence BGM");
            game.ResetZone();
            Require(!audio.PortraitSilenced && audio.MusicAudible && !gate.Performing,
                "Room reset did not cancel absorption and restore normal BGM");
            int resetReds = reds, resetSmiles = smiles, resetRequests = audio.RequestCount;
            SolveFixture(); gate.Tick(GalleryPortraitGate.HoldSeconds + .01f); gate.Tick(4.51f);
            face = Playing("face.smile");
            Require(face != null && reds == resetReds + 1 && smiles == resetSmiles + 1 && audio.RequestCount == resetRequests + 1,
                "Disclosed long-tick reset fixture did not emit one reddening cue across the separate red and smile milestones");
            game.ResetZone();
            Require(!face.isPlaying && !audio.PortraitSilenced && audio.MusicAudible && !gate.Performing,
                "Reset left an old smile voice or silence state alive");
            report.resetCancels = true; game.StartConceptDemo(3);
            Check("Public room reset cancels pending and playing portrait audio and restores ordinary music");

            report.phase = "disclosed end-of-song seek checks fading both real BGM loop sources";
            yield return new WaitForSecondsRealtime(.15f);
            AudioSource outgoing = null, incoming = null;
            foreach (var source in audio.GetComponentsInChildren<AudioSource>(true))
                if (source.name.StartsWith("Museum BGM", StringComparison.Ordinal) && source.isPlaying) outgoing = source;
            Require(outgoing != null && outgoing.clip != null && outgoing.clip.length > 4, "Active full-length C01 source is missing");
            outgoing.time = outgoing.clip.length - 1.7f; // Explicit seek fixture; the production crossfade runs normally.
            double loopDeadline = Time.realtimeSinceStartupAsDouble + 7;
            while (incoming == null)
            {
                Guard(); Require(Time.realtimeSinceStartupAsDouble < loopDeadline, "BGM crossfade did not finish after the end seek");
                foreach (var source in audio.GetComponentsInChildren<AudioSource>(true))
                    if (source != outgoing && source.clip == outgoing.clip && source.isPlaying &&
                        outgoing.isPlaying && source.volume > .001f && outgoing.volume > .001f) incoming = source;
                yield return null;
            }
            float loopGain = audio.MusicGainLevel;
            audio.SetPortraitSilence(true); // Disclosed envelope fixture during the real loop overlap.
            yield return new WaitForSecondsRealtime(.65f);
            Require(outgoing.isPlaying && incoming.isPlaying && audio.PortraitSilenced && !audio.PortraitFadeOutComplete &&
                audio.MusicGainLevel > loopGain * .4f && audio.MusicGainLevel < loopGain * .85f,
                "The shared portrait envelope did not attenuate the two overlapping loop sources");
            report.loopSourcesFadedTogether = true;
            while (!audio.PortraitFadeOutComplete)
            {
                Guard(); Require(Time.realtimeSinceStartupAsDouble < loopDeadline, "BGM overlap fade did not reach zero");
                yield return null;
            }
            musicTime = audio.MusicTime; float outgoingTime = outgoing.time, incomingTime = incoming.time;
            yield return new WaitForSecondsRealtime(.18f);
            Require(!outgoing.isPlaying && !incoming.isPlaying && !audio.MusicAudible && audio.MusicGainLevel < .0001f &&
                Mathf.Abs(audio.MusicTime - musicTime) < .06f && Mathf.Abs(outgoing.time - outgoingTime) < .06f &&
                Mathf.Abs(incoming.time - incomingTime) < .06f, "Fade completion failed to freeze both loop cursors at zero gain");
            audio.SetPortraitSilence(false);
            Require(!audio.PortraitSilenced && audio.MusicGainLevel < .0001f, "Unmute failed to begin the ordinary fade-in at zero gain");
            float afterSeam = incoming.time; yield return new WaitForSecondsRealtime(.6f);
            Require(incoming.isPlaying && incoming.clip == outgoing.clip && incoming.time > afterSeam + .08f &&
                audio.MusicAudible && audio.MusicTime < 4 && audio.MusicGainLevel > loopGain * .9f,
                "Loop handoff failed to restore ordinary gain and advance the preserved new C01 voice");
            report.loopSeamPlaybackValidated = true;
            Check("End seek observes two audible-gain loop sources share the fade, pause both cursors at zero, and resume the preserved next voice after the seam");
        }

        void SolveFixture()
        {
            game.checkpoint = 2;
            foreach (var piece in portrait.pieces) piece.Restore();
            for (int i = 0; i < portrait.pieces.Length; i++)
            {
                var piece = portrait.pieces[i];
                piece.transform.SetPositionAndRotation(portrait.transform.TransformPoint(portrait.solutionPositions[i]), portrait.transform.rotation);
                piece.transform.localScale = Vector3.one * portrait.solutionScales[i];
                piece.body.position = piece.transform.position; piece.body.rotation = piece.transform.rotation;
                piece.RefreshMass(); Physics.SyncTransforms(); piece.placement.ResetPlaced();
                Require(game.ValidPlacement(piece, out var reason) && piece.placement.Ready, "Published solution invalid: " + reason);
            }
            game.PlacePlayer(portrait.viewingPoint.position); Look(portrait.transform.TransformPoint(new Vector3(0, 1.9f, 39)));
            portrait.Evaluate(); Look(portrait.FaceCenter); portrait.Evaluate();
            Require(portrait.Complete && portrait.CanObserve && game.held == null, "Published face pose is not recognizable");
        }
        void Look(Vector3 target)
        {
            Vector3 d = target - game.view.transform.position;
            game.yaw = Mathf.Atan2(d.x, d.z); game.pitch = Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude); game.SyncView();
        }
        void Step(Vector2 move) { Guard(); game.Tick(Time.deltaTime, move, false); ticks++; }
        AudioSource Playing(string cue)
        {
            foreach (var source in audio.GetComponentsInChildren<AudioSource>(true))
                if (source.clip == audio.Clip(cue) && source.isPlaying) return source;
            return null;
        }
        void Absorbing() { absorptionWall = Time.realtimeSinceStartupAsDouble; }
        void Reddening() { reds++; redElapsed = gate.PerformanceElapsed; redWall = Time.realtimeSinceStartupAsDouble; }
        void Smiling() { smiles++; smileElapsed = gate.PerformanceElapsed; }
        void SampleOutput()
        {
            AudioListener.GetOutputData(outputSamples, 0); report.outputSampleFrames++;
            foreach (float sample in outputSamples) report.outputPeak = Mathf.Max(report.outputPeak, Mathf.Abs(sample));
        }
        void Update()
        {
            if (audio == null) return;
            SampleOutput(); report.listenerPause = AudioListener.pause; report.listenerPauseObserved |= AudioListener.pause;
            report.listenerVolume = AudioListener.volume; report.maximumVoices = Mathf.Max(report.maximumVoices, audio.ActiveVoiceCount);
        }
        void Capture(string name)
        {
            var camera = game.view; var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            var target = new RenderTexture(1280, 720, 24); Texture2D texture = null;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
                File.WriteAllBytes(Path.Combine(output, name + ".png"), texture.EncodeToPNG());
            }
            finally
            { camera.targetTexture = oldTarget; RenderTexture.active = oldActive; target.Release(); Destroy(target); if (texture != null) Destroy(texture); }
        }
        void Guard() { Require(Time.realtimeSinceStartupAsDouble <= deadline, "Real-time audio validation exceeded 90 seconds"); }
        void Check(string message) { checks.Add(message); Debug.Log("MUSEUM_AUDIO_PASS " + message); }
        static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("MUSEUM_AUDIO: " + message); }
        void Log(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
        void Unsubscribe()
        {
            Application.logMessageReceived -= Log;
            if (subscribed && gate != null) { gate.AbsorptionStarted -= Absorbing; gate.ReddeningStarted -= Reddening; gate.SmileStarted -= Smiling; }
            subscribed = false;
        }
        void OnDestroy() { Unsubscribe(); }
    }
}
