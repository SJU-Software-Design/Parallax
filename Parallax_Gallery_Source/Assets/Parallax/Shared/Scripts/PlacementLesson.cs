using System;
using UnityEngine;

namespace ShadeLink
{
    public sealed class PlacementLesson:MonoBehaviour
    {
        Font font;Func<bool> active,safe;Func<PlacementSupport> held;Func<Vector3> feet;Func<int> drops;
        Vector3 sun,origin,originalScale;PlacementSupport tracked;int previousDrops;
        public int Step {get;private set;}
        public bool Visible {get;private set;}=true;
        static readonly string[] Titles={"1 / 4  ·  잡고 이동하기","2 / 4  ·  크기를 바꿔 놓기","3 / 4  ·  안착 확인하기","4 / 4  ·  설치한 그늘로 이동","연습 완료"};
        static readonly string[] Details={"안전한 쉼터에서 사물을 잡고 걸어 보세요. 운반 중에도 WASD와 점프를 사용할 수 있습니다.","시선을 먼 바닥으로 옮겨 크기를 바꾼 뒤 놓으세요. 파란 빗금은 예상 그늘이며 아직 보호되지 않습니다.","바닥·다른 설치된 사물·물 위의 지지가 필요합니다. 0.35초 동안 안정되면 실제 그늘과 발판이 활성화됩니다.","놓은 사물의 실제 그늘로 이동해 보세요. 발밑의 안전 표시를 확인하세요. 다시 잡으면 보호가 즉시 사라집니다.","이제 그늘을 설치하고 다음 쉼터로 이동하세요. T를 누르면 안내를 접거나 다시 볼 수 있습니다."};
        public void Configure(Font value,Func<bool> running,Func<PlacementSupport> carrying,Func<Vector3> position,Func<int> released,Func<bool> protectedNow,Vector3 toSun)
        {font=value;active=running;held=carrying;feet=position;drops=released;safe=protectedNow;sun=toSun;Restart();}
        public void Restart(){Step=0;Visible=true;tracked=null;previousDrops=drops==null?0:drops();}
        public void Tick(float dt)
        {
            if(active==null||!active())return;var current=held();
            if(Step==0&&current!=null)
            {
                if(tracked!=current){tracked=current;origin=feet();originalScale=current.transform.localScale;previousDrops=drops();}
                if(Vector3.Distance(feet(),origin)>.4f)Step=1;
            }
            if(Step==1&&current!=null&&current!=tracked){tracked=current;originalScale=current.transform.localScale;previousDrops=drops();}
            if(Step==1&&current==null&&tracked!=null&&drops()>previousDrops)
            {if(Vector3.Distance(tracked.transform.localScale,originalScale)>.05f){Step=2;origin=feet();}else previousDrops=drops();}
            if(Step==2&&tracked!=null&&tracked.Ready)Step=3;
            if(Step==3&&tracked!=null&&tracked.Ready&&safe()&&Vector3.Distance(feet(),origin)>.4f)
                foreach(var shape in tracked.GetComponentsInChildren<Collider>())
                    if(shape.enabled&&shape.Raycast(new Ray(feet()+Vector3.up*.04f,sun),out var hit,100)){Step=4;break;}
        }
        void Update(){if(active!=null&&active()&&Input.GetKeyDown(KeyCode.T))Visible=!Visible;}
        void OnGUI()
        {
            if(active==null||!active())return;var current=held();if(!Visible&&current==null)return;
            var old=GUI.matrix;GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(Screen.width/1600f,Screen.height/900f,1));
            var panel=new Rect(1195,130,377,Visible?260:132);GUI.color=new Color(.035f,.08f,.14f,.94f);GUI.DrawTexture(panel,Texture2D.whiteTexture);GUI.color=Color.white;
            var title=new GUIStyle(GUI.skin.label){font=font,fontSize=22,fontStyle=FontStyle.Bold,wordWrap=true};title.normal.textColor=new Color(.55f,.84f,1);
            var body=new GUIStyle(GUI.skin.label){font=font,fontSize=18,wordWrap=true};body.normal.textColor=Color.white;
            GUI.Label(new Rect(1212,144,341,55),Visible?Titles[Step]:"운반 중 · 예상 그늘",title);
            if(Visible)GUI.Label(new Rect(1212,197,341,134),Details[Step],body);
            var state=current!=null?current:tracked;
            GUI.Label(new Rect(1212,Visible?328:197,341,52),state!=null?state.Status:"T 안내 접기 / 펼치기",body);
            GUI.matrix=old;
        }
    }
}
