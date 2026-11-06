using UnityEngine;

namespace ShadeLink.Gallery
{
    // These sources exist for the offline bounce calculation. Runtime direct
    // light and moving-object shadows are still drawn by the museum shader.
    [ExecuteAlways,DefaultExecutionOrder(-200)]
    public sealed class GalleryBakedLighting : MonoBehaviour
    {
        public Light[] bakeSources;
        void OnEnable()
        {
            if(Application.isPlaying&&bakeSources!=null)foreach(var source in bakeSources)if(source!=null)source.enabled=false;
            Shader.SetGlobalFloat("_MuseumBakedGI",LightmapSettings.lightmaps.Length>0?1:0);
        }
        #if UNITY_EDITOR
        void Update()
        {
            if(!Application.isPlaying)Shader.SetGlobalFloat("_MuseumBakedGI",LightmapSettings.lightmaps.Length>0?1:0);
        }
        #endif
        void OnDisable(){Shader.SetGlobalFloat("_MuseumBakedGI",0);}
    }
}
