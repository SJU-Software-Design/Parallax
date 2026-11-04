using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadeLink.Gallery
{
    // Captures the actual player camera and its OnRenderImage stack. These are
    // rendered game views, never synthetic replacements for the environment.
    public sealed class GalleryConceptCapture : MonoBehaviour
    {
        public GalleryGame game;
        readonly List<string> errors=new List<string>();
        readonly List<Shot> shots=new List<Shot>();
        string output;

        [Serializable] sealed class Shot
        {
            public string file;
            public Vector3 cameraPosition,lookTarget;
            public float fieldOfView;
        }
        [Serializable] sealed class Manifest
        {
            public bool passed;
            public string version,scene,unity,gpu,scope,failure;
            public int width=1600,height=900;
            public Shot[] shots;
            public string[] errors;
        }
        void Log(string message,string stack,LogType kind)
        {
            if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert)
                errors.Add(message+"\n"+stack);
        }
        IEnumerator Start()
        {
            Application.logMessageReceived+=Log;
            string failure=null;
            var sequence=Sequence();
            while(true)
            {
                object next=null;bool more;
                try{more=sequence.MoveNext();if(more)next=sequence.Current;}
                catch(Exception exception){failure=exception.ToString();more=false;}
                if(!more)break;yield return next;
            }
            if(output==null)
            {
                Debug.LogError("CONCEPT_CAPTURE_FAIL "+failure);
                Application.logMessageReceived-=Log;Application.Quit(1);yield break;
            }
            var manifest=new Manifest{passed=failure==null&&errors.Count==0,version=GalleryGame.Version,
                scene=SceneManager.GetActiveScene().name,unity=Application.unityVersion,gpu=SystemInfo.graphicsDeviceName,
                scope="Actual 1600x900 player-camera views with the camera post-processing stack. This is visual QA, not a puzzle-completion test.",
                failure=failure,shots=shots.ToArray(),errors=errors.ToArray()};
            File.WriteAllText(Path.Combine(output,"capture-manifest.json"),JsonUtility.ToJson(manifest,true));
            Application.logMessageReceived-=Log;
            if(manifest.passed)Debug.Log("CONCEPT_CAPTURE_OK "+shots.Count+" actual camera views saved to "+output);
            else Debug.LogError("CONCEPT_CAPTURE_FAIL "+failure+" errors="+errors.Count);
            Application.Quit(manifest.passed?0:1);
        }
        IEnumerator Sequence()
        {
            string[] args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-captureDir");
            if(at<0||at+1>=args.Length||!Path.IsPathRooted(args[at+1]))
                throw new ArgumentException("-captureDir requires an absolute output directory");
            output=Path.GetFullPath(args[at+1]);Directory.CreateDirectory(output);
            if(game==null||game.view==null)throw new InvalidOperationException("Concept capture needs the actual GalleryGame camera");
            game.Resume();
            yield return null;yield return null;yield return null;
            for(int frame=0;frame<120&&Shader.GetGlobalFloat("_MuseumProbeReady")<.5f;frame++)yield return null;
            game.active=false;Time.timeScale=1;
            CheckMaterials();
            var definitions=new[]{
                new Shot{file="01-entry.png",cameraPosition=new Vector3(0,1.65f,2.2f),lookTarget=new Vector3(0,1.65f,14)},
                new Shot{file="02-painting-room.png",cameraPosition=new Vector3(5.6f,2.2f,11.7f),lookTarget=new Vector3(-2,2,5)},
                new Shot{file="03-nike-room.png",cameraPosition=new Vector3(4.8f,2.1f,18),lookTarget=new Vector3(-2,2.2f,22)},
                new Shot{file="04-museum-axis.png",cameraPosition=new Vector3(0,2.2f,12),lookTarget=new Vector3(0,2.2f,45)},
                new Shot{file="05-perspective-room.png",cameraPosition=new Vector3(0,1.55f,30.2f),lookTarget=new Vector3(0,1.55f,42)},
                new Shot{file="06-gallery-hero.png",cameraPosition=new Vector3(5.0f,1.75f,26.9f),lookTarget=new Vector3(-2.1f,2.7f,21)},
                new Shot{file="07-nike-rear.png",cameraPosition=new Vector3(-2,2.1f,28),lookTarget=new Vector3(-2,2.5f,24)},
                new Shot{file="08-nike-side.png",cameraPosition=new Vector3(-6.1f,2.1f,22),lookTarget=new Vector3(-2,2.5f,24)},
                new Shot{file="09-paintings-oblique.png",cameraPosition=new Vector3(-4.8f,1.7f,3.3f),lookTarget=new Vector3(1.3f,2.65f,12.8f)},
                new Shot{file="10-through-the-arch.png",cameraPosition=new Vector3(1.8f,1.7f,8),lookTarget=new Vector3(-2,2.6f,24)}
            };
            foreach(var shot in definitions)
            {
                game.view.transform.SetPositionAndRotation(shot.cameraPosition,Quaternion.LookRotation(shot.lookTarget-shot.cameraPosition));
                game.view.aspect=1600f/900;game.lighting.Synchronize();
                // Let lighting drivers and camera effects see this exact pose.
                yield return null;yield return new WaitForEndOfFrame();
                Render(shot);shots.Add(shot);
            }
        }
        void CheckMaterials()
        {
            foreach(var renderer in game.GetComponentsInChildren<Renderer>(true))
                foreach(var material in renderer.sharedMaterials)
                    if(material==null||material.shader==null||!material.shader.isSupported||material.shader.name=="Hidden/InternalErrorShader")
                        throw new InvalidOperationException("Missing/unsupported render material on "+renderer.name);
        }
        void Render(Shot shot)
        {
            Camera camera=game.view;var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
            var target=new RenderTexture(1600,900,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB){name="Actual museum visual QA"};
            Texture2D image=null;
            try
            {
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();
                File.WriteAllBytes(Path.Combine(output,shot.file),image.EncodeToPNG());shot.fieldOfView=camera.fieldOfView;
            }
            finally
            {
                camera.targetTexture=oldTarget;RenderTexture.active=oldActive;
                target.Release();Destroy(target);if(image!=null)Destroy(image);
            }
        }
        void OnDestroy(){Application.logMessageReceived-=Log;}
    }
}
