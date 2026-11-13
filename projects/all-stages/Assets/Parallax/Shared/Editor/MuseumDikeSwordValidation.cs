using System;
using System.IO;
using ShadeLink.Gallery;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadeLink.Editor
{
    // Batch-only saved-scene Play Mode wrapper. Never saves scene or lighting.
    [InitializeOnLoad]
    public static class MuseumDikeSwordValidation
    {
        const string SessionKey="Parallax.DikeSwordValidation";
        const string ScenePath="Assets/Parallax/Gallery/Scenes/Gallery.unity";
        [Serializable] sealed class Result { public bool passed=false; public string failure=null,phase=null; }

        static MuseumDikeSwordValidation()
        {
            if(SessionState.GetBool(SessionKey,false))EditorApplication.update+=Poll;
        }

        // Unity -batchmode -projectPath ... -executeMethod
        // ShadeLink.Editor.MuseumDikeSwordValidation.Run -gallery-bottle-balance
        // -gallery-dike-sword-test -validationDir <fresh output directory>
        public static void Run()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("This entry point is batch-only.");
            string[] args=Environment.GetCommandLineArgs();
            if(Array.IndexOf(args,"-gallery-dike-sword-test")<0||Array.IndexOf(args,"-gallery-bottle-balance")<0)
                throw new InvalidOperationException("Both -gallery-bottle-balance and -gallery-dike-sword-test are required.");
            if(Array.IndexOf(args,"-gallery-bottle-balance-test")>=0)
                throw new InvalidOperationException("Do not run the older key validator at the same time.");
            int at=Array.IndexOf(args,"-validationDir");
            if(at<0||at+1>=args.Length)throw new InvalidOperationException("A fresh -validationDir is required.");
            string directory=Path.GetFullPath(args[at+1]);Directory.CreateDirectory(directory);
            string result=Path.Combine(directory,"dike-sword-validation-results.json");
            if(File.Exists(result))throw new InvalidOperationException("Refusing to reuse an existing validation report.");
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Editor is already in Play Mode.");
            for(int i=0;i<SceneManager.sceneCount;i++)
                if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save editor work before this batch validation.");
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            SessionState.SetBool(SessionKey,true);
            SessionState.SetString(SessionKey+".result",result);
            SessionState.SetString(SessionKey+".deadline",DateTime.UtcNow.AddMinutes(5).Ticks.ToString());
            EditorApplication.update-=Poll;EditorApplication.update+=Poll;
            EditorApplication.EnterPlaymode();
        }

        static void Poll()
        {
            if(!SessionState.GetBool(SessionKey,false)){EditorApplication.update-=Poll;return;}
            string path=SessionState.GetString(SessionKey+".result","");
            if(File.Exists(path))
            {
                try
                {
                    var result=JsonUtility.FromJson<Result>(File.ReadAllText(path));
                    Debug.Log("DIKE_EDITOR_VALIDATION_RESULT passed="+result.passed+" phase="+result.phase+" "+result.failure);
                    Finish(result.passed?0:1);return;
                }
                catch(IOException){return;}
                catch(Exception error){Debug.LogException(error);Finish(1);return;}
            }
            if(long.TryParse(SessionState.GetString(SessionKey+".deadline","0"),out long deadline)&&DateTime.UtcNow.Ticks>deadline)
            {Debug.LogError("Dike runtime validation did not produce a report before its deadline.");Finish(1);}
        }

        static void Finish(int code)
        {
            SessionState.SetBool(SessionKey,false);EditorApplication.update-=Poll;
            EditorApplication.Exit(code);
        }
    }
}
