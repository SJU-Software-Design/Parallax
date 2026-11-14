using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using ShadeLink.Tutorial;
using ShadeLink.Gallery;
using ShadeLink.Beach;

namespace ShadeLink
{
    // Explicit pause-menu travel always creates a fresh stage. Ordinary elevator
    // and museum-exit routes retain their own transition runners.
    public sealed class StageTravelMenu : MonoBehaviour
    {
        static StageTravelMenu instance;
        public static bool Loading { get; private set; }
        public static bool DirectGalleryEntryRequested { get; private set; }
        public static string LastFailure { get; private set; }
        public static string LastCompletedStage { get; private set; }

        public static bool LoadStage(string sceneName)
        {
            if (Loading || (sceneName != "Tutorial" && sceneName != "Gallery" && sceneName != "Beach"))
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
            Loading = true;
            DirectGalleryEntryRequested = sceneName == "Gallery";
            LastFailure = null;
            instance.StartCoroutine(instance.Travel(sceneName));
            return true;
        }

        IEnumerator Travel(string sceneName)
        {
            Time.timeScale = 1;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            // The arrived elevator owns a persistent cabin/listener. Its normal
            // scene cleanup skips Gallery, so explicitly dispose it for a fresh map.
            if (TutorialMuseumElevatorFlow.Instance != null)
                Destroy(TutorialMuseumElevatorFlow.Instance.gameObject);
            foreach (var source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                source.Stop();
            foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                listener.enabled = false;
            yield return null; // Finish old elevator ownership before destination Awake.

            AsyncOperation load = null;
            try { load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single); }
            catch (Exception error) { LastFailure = error.Message; }
            if (load != null)
            {
                while (!load.isDone) yield return null;
                yield return null; // All destination Awake/Start callbacks have completed.
                if (sceneName == "Gallery")
                {
                    var gallery = FindFirstObjectByType<GalleryGame>();
                    if (gallery == null || gallery.bottleBalance == null)
                        LastFailure = "미술관 시작 구역을 준비하지 못했습니다.";
                    else { gallery.ResetAll(); gallery.Resume(); }
                }
                else if (sceneName == "Beach")
                {
                    var beach = FindFirstObjectByType<BeachGame>();
                    if (beach == null) LastFailure = "해변 시작 구역을 준비하지 못했습니다.";
                    else { beach.ResetAll(); beach.Resume(); }
                }
                else if (FindFirstObjectByType<TutorialGame>() == null)
                    LastFailure = "튜토리얼 시작 구역을 준비하지 못했습니다.";
                // Fresh Tutorial Awake already starts lesson 1 and its music.
                if (LastFailure == null)
                {
                    LastCompletedStage = sceneName;
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
            DirectGalleryEntryRequested = false;
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
            string[] scenes = { "Tutorial", "Gallery", "Beach" };
            string[] labels = { "튜토리얼", "미술관", "해변" };
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
                GUI.Label(new Rect(area.x, area.y + 337, area.width, 65),
                    Loading ? "맵을 불러오는 중…" : LastFailure, text);
        }
    }
}
