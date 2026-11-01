using UnityEngine;

namespace ShadeLink.Starship
{
    public sealed class StarshipAmbience:MonoBehaviour
    {
        public StarshipGame game;
        AudioSource source;AudioClip clip;
        void Start()
        {
            int rate=22050;var data=new float[rate*4];
            for(int i=0;i<data.Length;i++){float t=i/(float)rate;data[i]=Mathf.Sin(t*2*Mathf.PI*48)*.10f+Mathf.Sin(t*2*Mathf.PI*72)*.045f+Mathf.Sin(t*2*Mathf.PI*120)*.014f;}
            clip=AudioClip.Create("HELIOS ventilation",data.Length,1,rate,false);clip.SetData(data,0);
            source=gameObject.AddComponent<AudioSource>();source.clip=clip;source.loop=true;source.volume=.12f;source.Play();
        }
        void Update(){if(source!=null)source.volume=game.ending.phase>=StarshipEnding.Phase.Drifting?Mathf.Max(0,.12f-game.ending.driftTime*.02f):.12f;}
        void OnDestroy(){if(clip!=null)Destroy(clip);}
    }
}
