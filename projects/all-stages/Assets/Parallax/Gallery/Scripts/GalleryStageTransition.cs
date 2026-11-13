using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using ShadeLink.Beach;

namespace ShadeLink.Gallery
{
    // This small runner survives the scene swap; the museum itself never does.
    public sealed class GalleryStageTransition : MonoBehaviour
    {
        public bool IsTransitioning { get; private set; }
        public float FallDistance { get; private set; }
        public bool DestinationReady { get; private set; }
        float fade;
        bool automatedHandoff;
        GameObject shaft;
        Material stone, sand, sea, foam;

        void Awake() { DontDestroyOnLoad(gameObject); }

        public bool Begin(GalleryGame game, GalleryOrcusExit entrance)
        {
            if(IsTransitioning||game==null||entrance==null||
                !Application.CanStreamedLevelBeLoaded("Beach"))return false;
            IsTransitioning=true;
            automatedHandoff=game.automated;
            if(automatedHandoff)SceneManager.sceneLoaded+=KeepAutomatedHandoffRunning;
            StartCoroutine(Passage(game,entrance));return true;
        }

        void KeepAutomatedHandoffRunning(Scene scene,LoadSceneMode mode)
        {
            if(!automatedHandoff||scene.name!="Beach")return;
            // Beach Awake establishes its normal menu/focus policy. Restore
            // inherited automation in this same frame, before a hidden player
            // can pause and prevent the async passage from ever resuming.
            var beach=Object.FindFirstObjectByType<BeachGame>();
            if(beach!=null)beach.automated=true;
            Application.runInBackground=true;
        }

        IEnumerator Passage(GalleryGame game,GalleryOrcusExit entrance)
        {
            bool automated=game.automated;
            Camera camera=game.view;
            Vector3 start=camera.transform.position;
            Vector3 inward=-entrance.EntranceForward;
            Quaternion original=camera.transform.rotation;
            for(float time=0;time<.28f;time+=Time.unscaledDeltaTime)
            {
                float t=Mathf.Clamp01(time/.28f);
                camera.transform.SetPositionAndRotation(start+inward*t*.6f,original);
                fade=Mathf.SmoothStep(0,1,t);yield return null;
            }
            BuildShaft();
            Vector3 top=new Vector3(0,-30,72);
            camera.farClipPlane=100;
            for(float time=0;time<2.35f;time+=Time.unscaledDeltaTime)
            {
                float t=Mathf.Clamp01(time/2.35f);
                FallDistance=14*t*t;
                camera.transform.position=top+Vector3.down*FallDistance;
                camera.transform.rotation=Quaternion.Euler(Mathf.Lerp(28,58,t),0,Mathf.Sin(t*Mathf.PI)*1.2f);
                camera.fieldOfView=Mathf.Lerp(70,78,Mathf.Sin(t*Mathf.PI));
                fade=1-Mathf.Clamp01(time/.3f);
                if(time>1.95f)fade=Mathf.Clamp01((time-1.95f)/.4f);
                yield return null;
            }
            fade=1;
            AsyncOperation load=SceneManager.LoadSceneAsync("Beach",LoadSceneMode.Single);
            while(!load.isDone)yield return null;
            SceneManager.sceneLoaded-=KeepAutomatedHandoffRunning;
            // Awake has established the next stage's controller and placement state.
            yield return null;
            var beach=Object.FindFirstObjectByType<BeachGame>();
            if(beach==null)
            {
                Debug.LogError("MUSEUM_PASSAGE: Beach scene has no BeachGame");
                yield break;
            }
            beach.automated=automated;Application.runInBackground=automated;
            Application.targetFrameRate=automated?90:-1;QualitySettings.vSyncCount=automated?0:1;
            beach.checkpoint=0;beach.yaw=0;beach.pitch=-.08f;
            beach.PlacePlayer(BeachWorld.Checkpoints[0]+Vector3.up*.35f);
            // Keep the arrival visible behind the fade without accepting a held W
            // key until the player can actually see the destination.
            beach.enabled=false;beach.active=beach.started=true;
            if(!automated){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
            if(shaft!=null)Destroy(shaft);
            for(float time=0;time<.55f;time+=Time.unscaledDeltaTime)
            {fade=1-Mathf.Clamp01(time/.55f);yield return null;}
            fade=0;beach.enabled=true;beach.Resume();DestinationReady=true;
            beach.Tell("두 손 사이의 빛을 지나, 바닷바람이 부는 해변에 내려앉았습니다.",6);
            Debug.Log("MUSEUM_BEACH_HANDOFF_OK");
            IsTransitioning=false;Destroy(gameObject);
        }

        Material Surface(string name,Color color)
        {
            var result=new Material(Shader.Find("Parallax/GalleryTransitionSurface")){name=name,color=color};
            return result;
        }

        void Box(string name,Vector3 position,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;
            go.transform.SetParent(shaft.transform,false);go.transform.position=position;go.transform.localScale=size;
            Object.Destroy(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=material;
        }

        void BuildShaft()
        {
            shaft=new GameObject("Museum passage · descending toward the sunlit shore");
            DontDestroyOnLoad(shaft);
            stone=Surface("Passage charcoal stone",new Color(.065f,.072f,.079f));
            sand=Surface("Sunlit shore sand",new Color(.83f,.72f,.49f));
            sea=Surface("Turquoise water beyond the passage",new Color(.075f,.58f,.66f));
            foam=Surface("White sunlight on the surf",new Color(.86f,.97f,.95f));
            for(int side=-1;side<=1;side+=2)
            {
                Box("Deep stone wall",new Vector3(side*2.7f,-39.5f,72),new Vector3(.35f,23,6.2f),stone);
                Box("Deep stone end",new Vector3(0,-39.5f,72+side*3.0f),new Vector3(5.6f,23,.3f),stone);
            }
            for(int ring=0;ring<8;ring++)
                for(int side=-1;side<=1;side+=2)
                    Box("Sandstone seam",new Vector3(side*2.49f,-29-ring*2.6f,72),new Vector3(.10f,.12f,5.8f),sand);
            Box("Shore sand at the foot of the passage",new Vector3(0,-49.4f,72),new Vector3(5.5f,.3f,5.8f),sand);
            Box("Turquoise sea at the passage mouth",new Vector3(0,-49.20f,73.9f),new Vector3(5.4f,.08f,2),sea);
            for(int wave=0;wave<5;wave++)
            {
                var strip=new Vector3(Mathf.Sin(wave*1.7f)*.18f,-49.147f,73.03f+wave*.36f);
                Box("Soft surf in the arrival light",strip,new Vector3(5.1f,.018f,.035f+wave*.009f),foam);
            }
            Box("Daylight on the sandy arrival",new Vector3(0,-49.235f,71.85f),new Vector3(3.8f,.02f,.15f),foam);
        }

        void OnGUI()
        {
            if(!IsTransitioning||fade<=0)return;
            Color old=GUI.color;int oldDepth=GUI.depth;Matrix4x4 oldMatrix=GUI.matrix;
            GUI.depth=-10000;GUI.matrix=Matrix4x4.identity;GUI.color=new Color(0,0,0,fade);
            GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);
            GUI.color=old;GUI.depth=oldDepth;GUI.matrix=oldMatrix;
        }
        void OnDestroy()
        {
            SceneManager.sceneLoaded-=KeepAutomatedHandoffRunning;
            if(shaft!=null)Destroy(shaft);
            foreach(var material in new[]{stone,sand,sea,foam})if(material!=null)Destroy(material);
        }
    }
}
