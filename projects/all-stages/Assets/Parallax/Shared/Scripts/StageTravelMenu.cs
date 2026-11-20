using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using ShadeLink.Tutorial;
using ShadeLink.Gallery;
using ShadeLink.Beach;
using ShadeLink.Eclipse;

namespace ShadeLink
{
    // Explicit pause-menu travel always creates a fresh stage. Ordinary elevator
    // and museum-exit routes retain their own transition runners.
    public sealed class StageTravelMenu : MonoBehaviour
    {
        static StageTravelMenu instance;
        static bool pauseRequested;
        static string requestedScene;
        public static bool ShouldPauseOnArrival(string sceneName) => Loading && pauseRequested && requestedScene == sceneName;
        void Awake() { SceneManager.sceneLoaded += PauseLoadedDestination; }
        void OnDestroy() { SceneManager.sceneLoaded -= PauseLoadedDestination; }
        void PauseLoadedDestination(Scene loaded, LoadSceneMode mode)
        {
            if (!ShouldPauseOnArrival(loaded.name)) return;
            IGameProgressStage stage = loaded.name == "Tutorial" ? FindFirstObjectByType<TutorialGame>() :
                loaded.name == "Gallery" ? FindFirstObjectByType<GalleryGame>() :
                loaded.name == "Beach" ? FindFirstObjectByType<BeachGame>() :
                loaded.name == "Eclipse" ? FindFirstObjectByType<EclipseFinaleGame>() : null;
            stage?.SetProgressPaused(true);
            Time.timeScale = 0;
        }
        public static bool Loading { get; private set; }
        public static float SceneLoadProgress { get; private set; }
        public static double SceneLoadCalledAt { get; private set; } = -1;
        public static bool DirectGalleryEntryRequested { get; private set; }
        public static string LastFailure { get; private set; }
        public static string LastCompletedStage { get; private set; }

        public static bool LoadStage(string sceneName, bool pauseOnArrival = false)
        {
            if (Loading || (sceneName != "Tutorial" && sceneName != "Gallery" && sceneName != "Beach" && sceneName != "Eclipse"))
                return false;
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                LastFailure = "선택한 맵을 실행본에서 찾을 수 없습니다: " + sceneName;
                return false;
            }
            // Never interrupt an automatic route in the middle of its scene swap.
            if (TutorialMuseumElevatorFlow.ArrivalPending) return false;
            foreach (var passage in FindObjectsByType<GalleryStageTransition>(FindObjectsSortMode.None))
                if (passage.IsTransitioning) return false;
            if (instance == null)
            {
                var runner = new GameObject("Pause menu · stage travel");
                instance = runner.AddComponent<StageTravelMenu>();
                DontDestroyOnLoad(runner);
            }
            Loading = true; SceneLoadProgress = 0; SceneLoadCalledAt = -1;
            pauseRequested = pauseOnArrival; requestedScene = sceneName;
            DirectGalleryEntryRequested = sceneName == "Gallery";
            LastFailure = null;
            instance.StartCoroutine(instance.Travel(sceneName, pauseOnArrival));
            return true;
        }

        IEnumerator Travel(string sceneName, bool pauseOnArrival)
        {
            // Finish the brief UI confirm on the current scene listener before handoff.
            if (global::ParallaxGameShell.Instance != null) yield return new WaitForSecondsRealtime(.10f);
            Time.timeScale = 1;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            // The arrived elevator owns a persistent cabin/listener. Its normal
            // scene cleanup skips Gallery, so explicitly dispose it for a fresh map.
            if (TutorialMuseumElevatorFlow.Instance != null)
                Destroy(TutorialMuseumElevatorFlow.Instance.gameObject);
            foreach (var source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                if (!global::ParallaxGameShell.IsUiFeedbackSource(source)) source.Stop();
            var departureScene = SceneManager.GetActiveScene();
            var previousListeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            var previousListenerStates = new bool[previousListeners.Length];
            for (int i = 0; i < previousListeners.Length; i++)
            {
                previousListenerStates[i] = previousListeners[i].enabled;
                previousListeners[i].enabled = false;
            }
            yield return null; // Finish old elevator ownership before destination Awake.

            AsyncOperation load = null;
            try { SceneLoadCalledAt = Time.realtimeSinceStartupAsDouble; load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single); }
            catch (Exception error) { LastFailure = error.Message; }
            if (load != null)
            {
                while (!load.isDone)
                {
                    SceneLoadProgress = Mathf.Max(SceneLoadProgress, Mathf.Clamp01(load.progress / .9f));
                    yield return null;
                }
                SceneLoadProgress = 1;
                yield return null; // All destination Awake/Start callbacks have completed.
                if (sceneName == "Gallery")
                {
                    var gallery = FindFirstObjectByType<GalleryGame>();
                    if (gallery == null || gallery.bottleBalance == null)
                        LastFailure = "미술관 시작 구역을 준비하지 못했습니다.";
                    else { gallery.ResetAll(); if (pauseOnArrival) gallery.Pause(); else gallery.Resume(); }
                }
                else if (sceneName == "Beach")
                {
                    var beach = FindFirstObjectByType<BeachGame>();
                    if (beach == null) LastFailure = "해변 시작 구역을 준비하지 못했습니다.";
                    else { beach.ResetAll(); if (pauseOnArrival) beach.Pause(); else beach.Resume(); }
                }
                else if (sceneName == "Eclipse")
                {
                    var eclipse = FindFirstObjectByType<EclipseFinaleGame>();
                    if (eclipse == null || !eclipse.ProgressReady) LastFailure = "일식 시작 구역을 준비하지 못했습니다.";
                    else { eclipse.ResetGame(!pauseOnArrival); eclipse.SetProgressPaused(pauseOnArrival); }
                }
                else
                {
                    var tutorial = FindFirstObjectByType<TutorialGame>();
                    if (tutorial == null) LastFailure = "튜토리얼 시작 구역을 준비하지 못했습니다.";
                    else if (pauseOnArrival) tutorial.SetProgressPaused(true);
                }
                // Fresh Tutorial Awake already starts lesson 1 and its music.
                if (LastFailure == null)
                {
                    LastCompletedStage = sceneName;
                    if (!pauseOnArrival) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
                }
            }
            if (load == null)
            {
                // A rejected scene request leaves the departure scene alive.
                // Restore only its surviving listeners with their original states.
                if (SceneManager.GetActiveScene().handle == departureScene.handle)
                    for (int i = 0; i < previousListeners.Length; i++)
                        if (previousListeners[i] != null) previousListeners[i].enabled = previousListenerStates[i];
                if (LastFailure == null) LastFailure = "맵 로딩 작업을 만들지 못했습니다.";
            }
            DirectGalleryEntryRequested = false;
            pauseRequested = false; requestedScene = null;
            Loading = false;
            if (LastFailure != null) Debug.LogError("STAGE_TRAVEL: " + LastFailure);
            else Debug.Log("STAGE_TRAVEL_READY " + sceneName);
        }

        public static bool DrawPauseTabs(Rect area, Font font, ref bool travelTab)
        {
            var style = new GUIStyle(GUI.skin.button) { font = font, fontSize = 21 };
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && !Loading;
            float half = (area.width - 8) * .5f;
            if (GUI.Button(new Rect(area.x, area.y, half, area.height),
                travelTab ? "일시정지" : "● 일시정지", style)) travelTab = false;
            if (GUI.Button(new Rect(area.x + half + 8, area.y, half, area.height),
                travelTab ? "● 맵 이동" : "맵 이동", style)) travelTab = true;
            GUI.enabled = enabled;
            return travelTab;
        }

        public static void DrawDestinations(Rect area, Font font)
        {
            var text = new GUIStyle(GUI.skin.label)
                { font = font, fontSize = 21, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            text.normal.textColor = Color.white;
            var button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 24 };
            GUI.Label(new Rect(area.x, area.y, area.width, 80),
                "선택한 맵의 처음부터 시작합니다.\n현재 맵의 진행은 초기화됩니다.", text);
            string[] scenes = { "Tutorial", "Gallery", "Beach", "Eclipse" };
            string[] labels = { "튜토리얼", "미술관", "해변", "일식" };
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && !Loading;
            for (int i = 0; i < scenes.Length; i++)
            {
                string label = labels[i] + (SceneManager.GetActiveScene().name == scenes[i] ? " · 처음부터" : "");
                if (GUI.Button(new Rect(area.x + 20, area.y + 102 + i * 75, area.width - 40, 60), label, button))
                    LoadStage(scenes[i]);
            }
            GUI.enabled = enabled;
            if (Loading || LastFailure != null)
                GUI.Label(new Rect(area.x, area.y + 412, area.width, 65),
                    Loading ? "맵을 불러오는 중…" : LastFailure, text);
        }
    }
}
