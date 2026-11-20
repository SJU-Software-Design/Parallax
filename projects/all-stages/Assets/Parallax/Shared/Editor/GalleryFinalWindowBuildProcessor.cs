using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;

// Scene processing changes the build copy only; the authored scene is not saved.
public sealed class GalleryFinalWindowBuildProcessor : IProcessSceneWithReport
{
    public int callbackOrder => 0;
    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (scene.name == "Gallery") PlayerShellBuild.ConfigureFinalWindowBatching(scene);
    }
}