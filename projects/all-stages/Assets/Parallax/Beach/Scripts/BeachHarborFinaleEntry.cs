using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ShadeLink.Beach
{
    // Review entry starts the selected final puzzle; regular three-stage play is unchanged.
    public sealed class BeachHarborFinaleEntry : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            var args=Environment.GetCommandLineArgs();
            if(Array.IndexOf(args,"-beach-counterweight-review")<0&&Array.IndexOf(args,"-beach-harbor-finale-review")<0)return;
            if(FindFirstObjectByType<BeachHarborFinaleEntry>()!=null)return;
            var node=new GameObject("Harbor finale review entry");DontDestroyOnLoad(node);node.AddComponent<BeachHarborFinaleEntry>();
        }
        IEnumerator Start()
        {
            if(SceneManager.GetActiveScene().name!="Beach")yield return SceneManager.LoadSceneAsync("Beach",LoadSceneMode.Single);
            yield return null;
            var game=FindFirstObjectByType<BeachGame>();
            if(game!=null){game.StartHarborFinaleDemo();game.Resume();}
            Destroy(gameObject);
        }
    }
}