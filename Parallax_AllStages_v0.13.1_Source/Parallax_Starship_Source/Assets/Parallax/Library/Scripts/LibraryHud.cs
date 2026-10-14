using UnityEngine;

namespace ShadeLink.Library
{
    public sealed class LibraryHud : MonoBehaviour
    {
        public LibraryGame game;
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
            Panel(new Rect(28,24,555,102),new Color(.035f,.065f,.08f,.9f));
            GUI.Label(new Rect(48,35,510,34),"PARALLAX v0.12.0  /  그림자 서고",text);
            GUI.Label(new Rect(48,75,510,34),LibraryWorld.Titles[game.checkpoint],small);
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
                    GUI.Label(new Rect(427,644,746,63),"시선: 배치 표면 선택  ·  거리와 크기 자동 반영\nQ/E: 회전  ·  Z/X: 기울기  ·  C: 바로 세우기",small);
                    GUI.Label(new Rect(427,711,746,40),game.invalid?game.invalidReason:game.PlacementBlocked?"장애물 앞에서 멈춤 · 다른 방향으로 이동하거나 클릭해 놓으세요":"왼쪽 클릭: 놓기 (중력·회전 적용)  ·  오른쪽 클릭: 잡기 취소",small);
                }
                else if(game.aimed!=null)
                    GUI.Label(new Rect(570,475,520,42),"[왼쪽 클릭]  "+game.aimed.title,text);
                if(game.showHints){Panel(new Rect(28,149,530,230),new Color(.035f,.065f,.08f,.93f));GUI.Label(new Rect(48,164,490,205),LibraryWorld.Hints[game.checkpoint],text);}
                if(Time.unscaledTime<game.noticeUntil&&!game.showHints)
                    GUI.Label(new Rect(380,793,900,36),game.notice,small);
                Panel(new Rect(0,851,1600,49),new Color(.035f,.065f,.08f,.93f));
                GUI.Label(new Rect(30,861,1550,36),"WASD 이동   ·   마우스 시점   ·   Space 점프   ·   청록색 표시 책·가구 잡기   ·   R 구역 초기화   ·   H 힌트 · T 설치 연습   ·   Esc 메뉴",small);
            }
            if(!game.active||game.won)
            {
                Panel(new Rect(0,0,1600,900),new Color(.025f,.045f,.055f,.7f));
                Panel(new Rect(362,160,876,590),new Color(.04f,.075f,.09f,.96f));
                GUI.Label(new Rect(412,200,770,66),game.won?"서고의 출구에 도착했습니다":game.started?"잠시 쉬어 가기":"PARALLAX  /  LIBRARY v0.12.0",title);
                GUI.Label(new Rect(412,280,770,235),game.won?
                    "책과 가구의 그늘을 이어 출구에 도착했습니다.\n\n클리어 시간  "+System.TimeSpan.FromSeconds(game.elapsed).ToString(@"mm\:ss")+"   /   재시도  "+game.respawns+"회\n\nMedieval Library Kit · Rinku Choudhary":
                    "햇빛을 피해, 책과 가구의 그림자로 길을 만드세요.\n책장 속 청록색 표시 책도 한 권씩 잡을 수 있습니다.\n\n잡고 걸으며 가까운 면·먼 면을 시선으로 조준하세요.\n멀수록 실제 크기가 커지고 화면 크기는 비슷합니다.\n파란 빗금은 예상 그늘입니다. 놓고 안착해야 안전합니다.\n책장 위의 책은 먼저 꺼낸 뒤 책장을 옮겨 주세요.\n\nWASD 이동  ·  Space 점프  ·  R 현재 구역 초기화",text);
                if(GUI.Button(new Rect(412,572,370,62),game.won?"도서관 처음부터 다시":game.started?"계속하기":"도서관 입장",button))game.Resume();
                if(GUI.Button(new Rect(812,572,370,62),"종료",button))Application.Quit();
                GUI.Label(new Rect(412,676,770,46),"v0.12.0  /  거리 기반 원근  /  4개 서고 구역  /  1,512㎡",small);
            }
        }
    }
}
