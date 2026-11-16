using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadeLink.Editor
{
    // Opt-in saved-scene wrapper. Does not save scene, lighting or project settings.
    [InitializeOnLoad]
    public static class TutorialElevatorValidation
    {
        const string SessionKey = "Parallax.TutorialElevatorValidation";
        const string ScenePath = "Assets/Parallax/Tutorial/Scenes/Tutorial.unity";
        [Serializable] sealed class Result { public bool passed; public string failure, phase; }

        static TutorialElevatorValidation()
        {
            if (SessionState.GetBool(SessionKey, false)) EditorApplication.update += Poll;
        }

        // -executeMethod ShadeLink.Editor.TutorialElevatorValidation.Run
        // -tutorial-elevator-review -tutorial-elevator-validate
        // -validation-output <fresh absolute JSON path or directory>
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("This entry point is batch-only.");
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-tutorial-elevator-validate") < 0)
                throw new InvalidOperationException("-tutorial-elevator-validate is required.");
            int at = Array.IndexOf(args, "-validation-output");
            if (at < 0 || at + 1 >= args.Length || !Path.IsPathRooted(args[at + 1]))
                throw new InvalidOperationException("-validation-output requires a fresh absolute JSON path or directory.");
            string path = Path.GetFullPath(args[at + 1]);
            if (!string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase))
                path = Path.Combine(path, "tutorial-elevator-validation-results.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (File.Exists(path)) throw new InvalidOperationException("Refusing to reuse an existing validation report.");
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Editor is already in Play Mode.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("A loaded scene has unsaved editor work.");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SessionState.SetBool(SessionKey, true);
            SessionState.SetString(SessionKey + ".result", path);
            SessionState.SetString(SessionKey + ".deadline", DateTime.UtcNow.AddMinutes(5).Ticks.ToString());
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
            EditorApplication.EnterPlaymode();
        }

        static void Poll()
        {
            if (!SessionState.GetBool(SessionKey, false)) { EditorApplication.update -= Poll; return; }
            string path = SessionState.GetString(SessionKey + ".result", "");
            if (File.Exists(path))
            {
                try
                {
                    var result = JsonUtility.FromJson<Result>(File.ReadAllText(path));
                    Debug.Log("TUTORIAL_ELEVATOR_EDITOR_RESULT passed=" + result.passed + " phase=" + result.phase + " " + result.failure);
                    Finish(result.passed ? 0 : 1);
                    return;
                }
                catch (IOException) { return; }
                catch (Exception error) { Debug.LogException(error); Finish(1); return; }
            }
            if (long.TryParse(SessionState.GetString(SessionKey + ".deadline", "0"), out long deadline) && DateTime.UtcNow.Ticks > deadline)
            {
                Debug.LogError("Tutorial elevator validation did not produce a report before its deadline.");
                Finish(1);
            }
        }

        static void Finish(int code)
        {
            SessionState.SetBool(SessionKey, false);
            EditorApplication.update -= Poll;
            EditorApplication.Exit(code);
        }
    }
}
