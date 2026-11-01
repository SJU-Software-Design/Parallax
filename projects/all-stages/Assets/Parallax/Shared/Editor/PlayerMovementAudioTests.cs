using System;
using UnityEngine;

namespace ShadeLink.Editor
{
    public static class PlayerMovementAudioTests
    {
        public static void Run()
        {
            var type=typeof(PlayerControls).Assembly.GetType("ShadeLink.PlayerMovementAudio");
            Require(type!=null,"PlayerMovementAudio is missing");
            var gameObject=new GameObject("Movement sound fixture");
            try
            {
                var sound=gameObject.AddComponent(type);
                Func<Vector3,bool,bool,float,string> tick=(v,g,m,dt)=>type.GetMethod("Tick").Invoke(sound,new object[]{v,g,m,dt}).ToString();
                Require(tick(Vector3.forward*4,true,true,.42f)=="None","Footstep played before its interval");
                Require(tick(Vector3.forward*4,true,true,.02f)=="Footstep","Walking did not produce a .43-second footstep");
                Require(tick(Vector3.forward*4,true,false,.5f)=="None","Support drift without movement input played footsteps");
                Require(tick(Vector3.up*6,false,false,.01f)=="Jump","Takeoff did not produce jump feedback");
                Require(tick(Vector3.down*8,false,false,.1f)=="None","Airborne fall played a walking sound");
                Require(tick(Vector3.zero,true,false,.01f)=="Land","Landing did not produce feedback");
                Require(tick(Vector3.forward*4,true,true,0)=="None","Paused simulation produced a cue");
                var source=gameObject.GetComponent<AudioSource>();
                Require(source!=null&&!source.playOnAwake&&!source.loop&&source.volume<=.3f,"Player audio source is missing or excessive");
                Debug.Log("PLAYER_MOVEMENT_AUDIO_TESTS_OK 8");
            }
            finally{UnityEngine.Object.DestroyImmediate(gameObject);}
        }
        static void Require(bool condition,string message){if(!condition)throw new Exception("PLAYER_MOVEMENT_AUDIO: "+message);}
    }
}
