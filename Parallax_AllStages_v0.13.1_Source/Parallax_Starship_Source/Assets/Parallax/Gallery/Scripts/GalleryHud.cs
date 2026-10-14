using UnityEngine;

namespace ShadeLink.Gallery
{
    public sealed class GalleryHud : MonoBehaviour
    {
        public GalleryGame game;
        GUIStyle text,title,small,button;
        readonly Color mint=new Color(.32f,.95f,.79f), paper=new Color(.94f,.93f,.86f);
        void Panel(Rect rect,Color color){GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=Color.white;}
        void OnGUI()
        {
            if(game==null)return;
            if(text==null)
            {
                text=new GUIStyle(GUI.skin.label){font=game.font,fontSize=21,wordWrap=true};text.normal.textColor=paper;
                title=new GUIStyle(text){fontSize=35,fontStyle=FontStyle.Bold};
                small=new GUIStyle(text){fontSize=17};
                button=new GUIStyle(GUI.skin.button){font=game.font,fontSize=22};
            }
            GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(Screen.width/1600f,Screen.height/900f,1));
            Panel(new Rect(28,24,555,game.checkpoint==1?139:102),new Color(.035f,.065f,.08f,.9f));
            GUI.Label(new Rect(48,35,510,34),"PARALLAX v0.12.0  /  빛을 꺾는 미술관",text);
            GUI.Label(new Rect(48,75,510,34),GalleryWorld.Titles[game.checkpoint],small);
            if(game.checkpoint==1&&game.lightGate!=null)GUI.Label(new Rect(48,112,510,34),game.lightGate.illuminated?"센서: 빛 감지 · 문 열림 · 그림자로 이동하세요":"센서: 빛 없음 · 거울의 각도를 바꾸세요",small);
            if(game.active&&!game.won)
            {
                Panel(new Rect(798,448,4,4),game.held!=null?(game.invalid?new Color(1,.3f,.1f):mint):paper);
                Panel(new Rect(1195,25,377,88),new Color(.035f,.065f,.08f,.9f));
                GUI.Label(new Rect(1215,34,338,33),game.safe?"그늘 · 안전":"햇빛 · 그늘로 이동하세요",text);
                Panel(new Rect(1215,79,333,9),new Color(.14f,.22f,.25f));
                Panel(new Rect(1215,79,333*game.exposure,9),new Color(1,.56f,.25f));
                if(game.held!=null)
                {
                    Panel(new Rect(405,590,790,175),new Color(.035f,.065f,.08f,.94f));
                    GUI.Label(new Rect(427,605,746,32),game.held.title+"  ·  거리 "+game.holdDistance.ToString("0.00")+"m  ·  크기 ×"+game.RequestedScale.ToString("0.00"),text);
                    GUI.Label(new Rect(427,644,746,63),"WASD: 들고 이동 · 시선: 배치 표면과 크기 조절\nQ/E: 회전  ·  Z/X: 기울기  ·  C: 바로 세우기",small);
                    GUI.Label(new Rect(427,711,746,40),game.invalid?game.invalidReason:game.PlacementBlocked?"장애물 앞에서 멈춤 · 다른 방향으로 이동하거나 클릭해 놓으세요":"왼쪽 클릭: 놓기 (중력·회전 적용)  ·  오른쪽 클릭: 잡기 취소",small);
                }
                else if(game.adjustingMirror&&game.mirror!=null)
                {
                    Panel(new Rect(405,622,790,135),new Color(.035f,.065f,.08f,.94f));
                    GUI.Label(new Rect(427,635,746,34),"거울 조작 · 각도 "+game.mirror.angle.ToString("0.0")+"°",text);
                    GUI.Label(new Rect(427,678,746,64),"Q/E: 천천히 회전 · WASD: 이동 · 클릭 / 오른쪽 클릭: 종료\n센서가 초록색이면 문이 열립니다. 통로의 반사광은 차광판으로 가리세요.",small);
                }
                else if(game.aimedMirror!=null)
                    GUI.Label(new Rect(550,475,570,42),"[왼쪽 클릭]  거울 각도 조절",text);
                else if(game.aimed!=null)
                    GUI.Label(new Rect(570,475,520,42),"[왼쪽 클릭]  "+game.aimed.title,text);
                if(game.showHints){Panel(new Rect(28,180,555,205),new Color(.035f,.065f,.08f,.93f));GUI.Label(new Rect(48,195,510,180),GalleryWorld.Hints[game.checkpoint],text);}
                if(Time.unscaledTime<game.noticeUntil&&!game.showHints)
                    GUI.Label(new Rect(380,793,900,36),game.notice,small);
                Panel(new Rect(0,851,1600,49),new Color(.035f,.065f,.08f,.93f));
                GUI.Label(new Rect(30,861,1550,36),"WASD 이동   ·   마우스 시점   ·   Space 점프   ·   청록색 표시 전시물 잡기   ·   R 구역 초기화   ·   H 힌트 · T 설치 연습   ·   Esc 메뉴",small);
            }
            if(!game.active||game.won)
            {
                Panel(new Rect(0,0,1600,900),new Color(.025f,.045f,.055f,.7f));
                Panel(new Rect(362,160,876,590),new Color(.04f,.075f,.09f,.96f));
                GUI.Label(new Rect(412,200,770,66),game.won?"전시의 끝에 도착했습니다":game.started?"잠시 쉬어 가기":"PARALLAX  /  GALLERY v0.12.0",title);
                GUI.Label(new Rect(412,280,770,235),game.won?
                    "높이와 그늘을 함께 설계해 출구에 도착했습니다.\n\n클리어 시간  "+System.TimeSpan.FromSeconds(game.elapsed).ToString(@"mm\:ss")+"   /   재시도  "+game.respawns+"회\n\n전시물: Art Gallery — ALLrounder18 (CGTrader)":
                    "햇빛과 반사광을 피해, 그림자로 출구까지 이동하세요.\n전시물을 잡은 상태에서도 WASD 이동과 점프가 가능합니다.\n시선이 향한 표면의 거리에 따라 실제 크기가 바뀝니다.\n\n두 번째 전시실: 거울 클릭 → Q/E로 빛의 방향 변경.\n빛으로 센서를 켜 문을 열고, 차광판으로 통로를 가리세요.\n\n왼쪽 클릭 잡기 / 놓기 · 오른쪽 클릭 취소\nR 현재 구역 초기화 · Shift+R 전체 초기화 · H 힌트 · T 설치 연습",text);
                if(GUI.Button(new Rect(412,572,370,62),game.won?"처음부터 다시 전시 보기":game.started?"계속하기":"미술관 입장",button))game.Resume();
                if(GUI.Button(new Rect(812,572,370,62),"종료",button))Application.Quit();
                if(GUI.Button(new Rect(412,644,770,48),"거울 전시실 바로 체험",button))game.StartMirrorDemo();
                GUI.Label(new Rect(412,708,770,32),"v0.12.0 · 거울 반사 퍼즐 · 사물을 들고 이동 · 4개 전시실",small);
            }
        }
    }
}
