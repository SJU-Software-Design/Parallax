#if UNITY_EDITOR
using UnityEditor;

namespace ShadeLink.Eclipse.Editor
{
    [InitializeOnLoad]
    internal static class EclipseFinaleMenu
    {
        const string PlayRequest = "Parallax.Eclipse.PlayRequested";

        static EclipseFinaleMenu()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        [MenuItem("Parallax/Eclipse/Play Finale")]
        static void PlayFinale()
        {
            if (EditorApplication.isPlaying)
            {
                EclipseFinaleGame.Bootstrap();
                return;
            }
            SessionState.SetBool(PlayRequest, true);
            EditorApplication.isPlaying = true;
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PlayRequest, false)) return;
            SessionState.SetBool(PlayRequest, false);
            EclipseFinaleGame.Bootstrap();
        }
    }
}
#endif
