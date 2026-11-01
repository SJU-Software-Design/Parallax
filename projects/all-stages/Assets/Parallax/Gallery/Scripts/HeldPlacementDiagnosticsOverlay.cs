using System.Text;
using UnityEngine;

namespace ShadeLink.Gallery
{
    // Reads the exact result used by placement; performs no additional physics queries.
    public sealed class HeldPlacementDiagnosticsOverlay : MonoBehaviour
    {
        public GalleryGame game;
        bool visible=true;
        GUIStyle text,small,title,marker;
        static readonly Color Yellow=new Color(1,.86f,.24f);
        void Update() { if(Input.GetKeyDown(KeyCode.F3))visible=!visible; }
        void OnGUI()
        {
            if(game==null||!game.active||game.won||game.held==null)return;
            if(text==null)
            {
                text=new GUIStyle(GUI.skin.label){font=game.font,fontSize=19,wordWrap=true};text.normal.textColor=Color.white;
                small=new GUIStyle(text){fontSize=16};title=new GUIStyle(text){fontSize=21,fontStyle=FontStyle.Bold};title.normal.textColor=Yellow;
                marker=new GUIStyle(small){alignment=TextAnchor.MiddleCenter};marker.normal.textColor=Yellow;
            }
            var matrix=GUI.matrix;var color=GUI.color;GUI.depth=-100;
            GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(Screen.width/1600f,Screen.height/900f,1));
            if(!visible){GUI.Label(new Rect(610,26,560,30),"거리 제한 진단 숨김 · F3 표시",small);GUI.matrix=matrix;GUI.color=color;return;}
            var d=game.PlacementDiagnostic;
            Fill(new Rect(600,24,580,285),new Color(.025f,.045f,.055f,.95f));
            GUI.Label(new Rect(616,35,548,30),"거리 제한: "+Reason(d.Limiter)+"  [F3 숨김]",title);
            string source=d.Source!=null?d.Source.name+"  (#"+d.Source.GetInstanceID()+")":d.HasPoint?"공간 경계 · "+d.Boundary:"충돌체 없음";
            GUI.Label(new Rect(616,70,548,30),source,text);
            GUI.Label(new Rect(616,104,548,45),d.Source!=null?Hierarchy(d.Source.transform):d.HasPoint?"제한 지점 "+d.Point.ToString("F2"):"아래 수치는 각 조건이 허용하는 잡기점 거리입니다.",small);
            float applied=Vector3.Distance(game.held.transform.TransformPoint(game.GrabLocalPoint),game.view.transform.position);
            GUI.Label(new Rect(616,153,548,28),"계산 "+Metres(d.Depth)+"  / 적용 "+Metres(applied)+"  / 크기 ×"+game.held.transform.localScale.x.ToString("0.000"),small);
            GUI.Label(new Rect(616,182,548,28),"표면 "+Metres(d.SurfaceDepth)+"  · 경계 "+Metres(d.BoundaryDepth)+"  · 크기 상한 "+Metres(d.SizeDepth),small);
            GUI.Label(new Rect(616,211,548,28),"의도 상한 "+Metres(d.IntentDepth)+"  · 플레이어 겹침 배율 "+game.PlayerOverlapReduction.ToString("0.000"),small);
            string status=!d.Feasible?"배치 해 없음 · 마지막 안전 자세/보정 자세 유지":!game.ProjectionApplied?"계산 자세 적용 실패 · 마지막 안전 자세/보정 자세 유지":"계산 자세 적용 중";
            GUI.Label(new Rect(616,242,548,55),status+(d.HasPoint?" · 노란 + 는 제한 지점":" · 정확한 접촉 지점 없음"),small);
            if(d.HasPoint)DrawPoint(d.Point);
            else if(d.Source!=null)DrawBounds(d.Source.bounds);
            GUI.matrix=matrix;GUI.color=color;
        }
        static string Reason(PerspectivePlacement.LimitKind kind)
        {
            switch(kind)
            {
                case PerspectivePlacement.LimitKind.Surface:return "실제 표면 충돌";
                case PerspectivePlacement.LimitKind.Boundary:return "공간 경계";
                case PerspectivePlacement.LimitKind.Size:return "물체 최대 크기";
                case PerspectivePlacement.LimitKind.Intent:return "조작 의도 거리 상한";
                case PerspectivePlacement.LimitKind.Volume:return "물체 전체 부피 겹침";
                case PerspectivePlacement.LimitKind.OriginInside:return "카메라가 충돌체 내부";
                case PerspectivePlacement.LimitKind.Minimum:return "최소 거리/경계 조건 충돌";
                default:return "표면 없음 · 기본 거리";
            }
        }
        static string Metres(float value) { return float.IsInfinity(value)?"제한 없음":value.ToString("0.00")+"m"; }
        static string Hierarchy(Transform node)
        {
            var path=new StringBuilder(node.name);
            while(node.parent!=null){node=node.parent;path.Insert(0,node.name+" / ");}
            return path.ToString();
        }
        static void Fill(Rect rect,Color color){GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=Color.white;}
        void DrawPoint(Vector3 point)
        {
            Vector3 p=game.view.WorldToViewportPoint(point);
            bool outside=p.z<=0||p.x<0||p.x>1||p.y<0||p.y>1;
            if(p.z<=0){p.x=1-p.x;p.y=1-p.y;}
            float x=Mathf.Clamp(p.x*1600,30,1570),y=Mathf.Clamp((1-p.y)*900,330,820);
            // Keep the marker's actual screen position when it is on-screen;
            // only off-screen points are moved to an edge and labelled as such.
            if(!outside){x=p.x*1600;y=(1-p.y)*900;}
            Fill(new Rect(x-12,y-1,24,2),Yellow);Fill(new Rect(x-1,y-12,2,24),Yellow);
            Rect label=new Rect(Mathf.Clamp(x-180,8,1232),Mathf.Clamp(y+15,312,815),360,28);
            Fill(label,new Color(.02f,.03f,.04f,.9f));GUI.Label(label,outside?"제한 지점 · 화면 밖":"실제 제한 지점",marker);
        }
        void DrawBounds(Bounds bounds)
        {
            Vector2 lo=new Vector2(float.PositiveInfinity,float.PositiveInfinity),hi=-new Vector2(float.PositiveInfinity,float.PositiveInfinity);
            for(int i=0;i<8;i++)
            {
                Vector3 corner=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                Vector3 p=game.view.WorldToViewportPoint(corner);if(p.z<=game.view.nearClipPlane)return;
                Vector2 screen=new Vector2(p.x*1600,(1-p.y)*900);lo=Vector2.Min(lo,screen);hi=Vector2.Max(hi,screen);
            }
            if(hi.x<0||lo.x>1600||hi.y<0||lo.y>900)return;
            lo=Vector2.Max(lo,Vector2.zero);hi=Vector2.Min(hi,new Vector2(1600,900));
            Fill(new Rect(lo.x,lo.y,hi.x-lo.x,2),Yellow);Fill(new Rect(lo.x,hi.y-2,hi.x-lo.x,2),Yellow);
            Fill(new Rect(lo.x,lo.y,2,hi.y-lo.y),Yellow);Fill(new Rect(hi.x-2,lo.y,2,hi.y-lo.y),Yellow);
            GUI.Label(new Rect(lo.x,Mathf.Clamp(hi.y+4,312,815),500,28),"충돌체 외곽 범위 (접촉점 아님)",small);
        }
    }
}
