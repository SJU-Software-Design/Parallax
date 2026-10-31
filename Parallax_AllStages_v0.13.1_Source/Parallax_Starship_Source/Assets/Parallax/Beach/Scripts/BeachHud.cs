using UnityEngine;

namespace ShadeLink.Beach
{
    public sealed class BeachHud:MonoBehaviour
    {
        public BeachGame game;
        GUIStyle text,title,small,button;
        readonly Color paper=new Color(.98f,.97f,.91f),accent=new Color(.34f,.82f,1),ink=new Color(.035f,.12f,.17f,.94f);
        void Panel(Rect r,Color c){GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=Color.white;}
        void OnGUI()
        {
            if(game==null)return;
            if(text==null){text=new GUIStyle(GUI.skin.label){font=game.font,fontSize=22,wordWrap=true};text.normal.textColor=paper;title=new GUIStyle(text){fontSize=38,fontStyle=FontStyle.Bold};small=new GUIStyle(text){fontSize=18};button=new GUIStyle(GUI.skin.button){font=game.font,fontSize=24};}
            GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(Screen.width/1600f,Screen.height/900f,1));
            Panel(new Rect(25,22,630,127),ink);GUI.Label(new Rect(47,32,590,34),"PARALLAX  /  마지막 해변  v"+BeachGame.Version,text);GUI.Label(new Rect(47,72,590,34),BeachWorld.Titles[game.checkpoint],text);
            string status=game.checkpoint==0?(game.lightGate.unlocked?"센서 충전 완료 · 열린 문까지 그늘을 만드세요":game.mirror.WorldWidth<2.35f?"작은 거울을 확대하세요 · 두 센서에 빛 연결":"반사 방향을 맞추세요 · 센서 충전 "+Mathf.RoundToInt(game.lightGate.charge*100)+"%"):game.checkpoint==2?(game.waterGate.unlocked?"물살 운반 완료 · 수문 열림":"공·판자를 물살로 반대편에 보내세요"):game.checkpoint==3?(game.castleRest?"성벽 체크포인트 저장 · 정상까지 그늘과 발판":"발판 + 점프 + 높은 곳의 그늘") : "판자는 통로 · 파라솔은 그늘";
            GUI.Label(new Rect(47,113,590,30),status,small);
            if(game.active&&!game.won)
            {
                PlayerControls.DrawCursor(new Vector2(800,450),game.aimed!=null,game.held!=null,game.invalid||game.PlacementBlocked,accent,paper);
                Panel(new Rect(1195,22,380,91),ink);GUI.Label(new Rect(1215,32,335,33),game.safe?"그늘 · 안전":"빛 노출 · 그늘로 이동",text);Panel(new Rect(1215,82,333,9),new Color(.19f,.3f,.35f));Panel(new Rect(1215,82,333*game.exposure,9),new Color(1,.46f,.2f));
                if(game.held!=null)
                {
                    Panel(new Rect(360,611,880,154),ink);GUI.Label(new Rect(381,624,840,33),game.held.title+"  ·  크기 ×"+game.RequestedScale.ToString("0.00")+"  ·  거리 "+game.holdDistance.ToString("0.0")+"m",text);
                    GUI.Label(new Rect(381,664,840,60),"WASD 들고 이동 · 오른쪽 버튼 + 마우스 회전 · Backspace 취소\n시선으로 크기 조절 · Q/E 회전 · Z/X 기울기 · C 바로 세우기",small);
                    GUI.Label(new Rect(381,725,840,31),game.invalid?game.invalidReason:game.PlacementBlocked?"충돌 때문에 멈췄습니다 · 방향을 바꾸거나 취소할 수 있습니다":"왼쪽 클릭으로 놓기 · 놓은 뒤 물리와 부력 적용",small);
                }
                else if(game.aimed!=null)GUI.Label(new Rect(540,475,630,36),"[왼쪽 클릭] "+game.aimed.title,text);
                if(game.showHints){Panel(new Rect(25,166,630,230),ink);GUI.Label(new Rect(47,183,587,204),BeachWorld.Hints[game.checkpoint],text);}
                if(Time.unscaledTime<game.noticeUntil)GUI.Label(new Rect(300,789,1090,50),game.notice,small);
                Panel(new Rect(0,851,1600,49),ink);GUI.Label(new Rect(25,861,1550,34),"WASD 이동 · 마우스 시점 · Space 점프 · 클릭 잡기/놓기 · R 구역 초기화 · Shift+R 전체 초기화 · H 힌트 · T 설치 연습 · Esc 메뉴",small);
            }
            if(!game.active||game.won)
            {
                Panel(new Rect(0,0,1600,900),new Color(.025f,.07f,.10f,.52f));Panel(new Rect(320,55,960,790),ink);
                GUI.Label(new Rect(366,95,870,62),game.won?"모래성 정상에서 탈출했습니다":game.started?"잠시 쉬어 가기":"PARALLAX / 마지막 해변",title);
                GUI.Label(new Rect(366,180,865,235),game.won?"원근·반사광·그늘·물살·점프를 연결해 마지막 스테이지를 완료했습니다.\n\n기록  "+System.TimeSpan.FromSeconds(game.elapsed).ToString(@"mm\:ss")+"  ·  복귀 "+game.respawns+"회\n\n3D 소품: Beach Surf and Paddle Kit / 3DAssets.dev / CC0":
                    "한낮의 피서장에는 스스로 만든 그늘이 필요합니다.\n\n01  작은 거울을 키워 반사광 센서를 켜세요.\n02  판자로 물길을 잇고 파라솔로 그 위를 가리세요.\n03  물살에 사물을 보내 반대편 수문을 여세요.\n04  사물을 발판과 차광막으로 재사용해 모래성을 오르세요.\n\n사물을 잡은 채 이동·점프 가능 · H로 구역별 힌트",text);
                PlayerControls.DrawOptions(new Rect(366,440,865,143),game.font);
                if(GUI.Button(new Rect(366,615,414,63),game.won?"처음부터 다시":game.started?"계속하기":"해변 입장",button))game.Resume();
                if(GUI.Button(new Rect(816,615,414,63),"종료",button))Application.Quit();
                game.showHints=GUI.Toggle(new Rect(366,710,800,35),game.showHints,"구역별 힌트 표시 (플레이 중 H로 변경)",small);
                GUI.Label(new Rect(366,785,870,29),"v"+BeachGame.Version+" · 4개 구역 · 물 접촉 시 복귀 · 성벽 중간 체크포인트",small);
            }
        }
    }
}
