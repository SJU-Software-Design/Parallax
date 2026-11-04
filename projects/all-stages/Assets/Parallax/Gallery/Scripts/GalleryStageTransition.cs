using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using ShadeLink.Library;

namespace ShadeLink.Gallery
{
    // This small runner survives the scene swap; the museum itself never does.
    public sealed class GalleryStageTransition : MonoBehaviour
    {
        public bool IsTransitioning { get; private set; }
        public float FallDistance { get; private set; }
        public bool DestinationReady { get; private set; }
        float fade;
        GameObject shaft;
        Material stone, timber, paper, gold;

        void Awake() { DontDestroyOnLoad(gameObject); }

        public bool Begin(GalleryGame game, GalleryOrcusExit entrance)
        {
            if(IsTransitioning||game==null||entrance==null||
                !Application.CanStreamedLevelBeLoaded("Library"))return false;
            IsTransitioning=true;
            StartCoroutine(Passage(game,entrance));return true;
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
            AsyncOperation load=SceneManager.LoadSceneAsync("Library",LoadSceneMode.Single);
            while(!load.isDone)yield return null;
            // Awake has established the next stage's controller and placement state.
            yield return null;
            var library=Object.FindFirstObjectByType<LibraryGame>();
            if(library==null)
            {
                Debug.LogError("MUSEUM_PASSAGE: Library scene has no LibraryGame");
                yield break;
            }
            library.automated=automated;Application.runInBackground=automated;
            Application.targetFrameRate=automated?90:-1;QualitySettings.vSyncCount=automated?0:1;
            library.PlacePlayer(LibraryWorld.Checkpoints[0]+Vector3.up*.35f);
            // Keep the arrival visible behind the fade without accepting a held W
            // key until the player can actually see the destination.
            library.enabled=false;library.active=library.started=true;
            if(!automated){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
            if(shaft!=null)Destroy(shaft);
            for(float time=0;time<.55f;time+=Time.unscaledDeltaTime)
            {fade=1-Mathf.Clamp01(time/.55f);yield return null;}
            fade=0;library.enabled=true;library.Resume();DestinationReady=true;
            library.Tell("두 손 사이의 빛을 지나, 책장 사이에 내려앉았습니다.",6);
            Debug.Log("MUSEUM_LIBRARY_HANDOFF_OK");
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
            shaft=new GameObject("A passage too deep for a small sculpture");
            DontDestroyOnLoad(shaft);
            stone=Surface("Passage charcoal stone",new Color(.065f,.072f,.079f));
            timber=Surface("Bookshelf warm oak",new Color(.26f,.14f,.055f));
            paper=Surface("Book spines in the distance",new Color(.57f,.38f,.15f));
            gold=Surface("Library lamplight",new Color(.90f,.58f,.22f));
            for(int side=-1;side<=1;side+=2)
            {
                Box("Deep stone wall",new Vector3(side*2.7f,-39.5f,72),new Vector3(.35f,23,6.2f),stone);
                Box("Deep stone end",new Vector3(0,-39.5f,72+side*3.0f),new Vector3(5.6f,23,.3f),stone);
            }
            for(int ring=0;ring<8;ring++)
                for(int side=-1;side<=1;side+=2)
                    Box("Stone seam",new Vector3(side*2.49f,-29-ring*2.6f,72),new Vector3(.10f,.12f,5.8f),timber);
            Box("Lower library floor",new Vector3(0,-49.4f,72),new Vector3(5.5f,.3f,5.8f),timber);
            for(int side=-1;side<=1;side+=2)
            {
                Box("Bookcase at the foot of the passage",new Vector3(side*2.35f,-47.6f,72),new Vector3(.35f,3.4f,4.6f),timber);
                for(int shelf=0;shelf<4;shelf++)for(int book=0;book<15;book++)
                    Box("Warm book spine",new Vector3(side*2.11f,-48.8f+shelf*.75f,69.9f+book*.29f),new Vector3(.09f,.40f+(book%3)*.07f,.16f),paper);
            }
            Box("Lower reading table",new Vector3(0,-48.4f,73),new Vector3(1.5f,.10f,.9f),timber);
            Box("A light on the reading table",new Vector3(0,-48.15f,73),new Vector3(.22f,.25f,.22f),gold);
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
            if(shaft!=null)Destroy(shaft);
            foreach(var material in new[]{stone,timber,paper,gold})if(material!=null)Destroy(material);
        }
    }
}
