using UnityEngine;

namespace ShadeLink.Starship
{
    public sealed class StarshipHud:MonoBehaviour
    {
        public StarshipGame game;
        GUIStyle text,title,small,button,center;
        readonly Color paper=new Color(.88f,.94f,1),accent=new Color(.25f,.82f,1),ink=new Color(.018f,.038f,.068f,.90f);
        void Panel(Rect r,Color c){GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=Color.white;}
        void OnGUI()
        {
            if(game==null)return;
            if(text==null)
            {
                text=new GUIStyle(GUI.skin.label){font=game.font,fontSize=21,wordWrap=true};text.normal.textColor=paper;
                title=new GUIStyle(text){fontSize=38,fontStyle=FontStyle.Bold};small=new GUIStyle(text){fontSize=17};
                button=new GUIStyle(GUI.skin.button){font=game.font,fontSize=22};
                center=new GUIStyle(title){alignment=TextAnchor.MiddleCenter,fontSize=54};
            }
            GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(Screen.width/1600f,Screen.height/900f,1));
            if(game.ending.ShowEnd)
            {
                GUI.color=new Color(1,1,1,Mathf.Clamp01((game.ending.driftTime-9)/2));
                GUI.Label(new Rect(400,345,800,100),"THE END",center);GUI.color=Color.white;
                if(game.won)
                {
                    if(GUI.Button(new Rect(570,710,220,52),"처음부터 다시",button))game.Resume();
                    if(GUI.Button(new Rect(810,710,220,52),"종료",button))Application.Quit();
                }
                return;
            }
            bool hidden=game.ending.phase!=StarshipEnding.Phase.None;
            if(game.active&&!game.won&&!hidden)
            {
                if(game.checkpoint!=4)
                {
                    Panel(new Rect(24,22,445,95),ink);GUI.Label(new Rect(44,33,400,30),"PARALLAX / HELIOS",text);GUI.Label(new Rect(44,73,400,30),StarshipWorld.Titles[game.checkpoint],small);
                    Panel(new Rect(1235,22,341,80),ink);GUI.Label(new Rect(1254,31,308,31),game.safe?"그늘 · 열 차단":"실험광 노출 · 과열",text);
                    Panel(new Rect(1254,80,300,7),new Color(.12f,.23f,.31f));Panel(new Rect(1254,80,300*game.exposure,7),new Color(1,.43f,.18f));
                }
                if(game.checkpoint==1)
                {
                    var gate=game.lightGate;
                    Panel(new Rect(495,22,715,95),ink);
                    GUI.Label(new Rect(515,32,675,30),gate.unlocked?"두 수신기 충전 완료 · 문 열림":"수신기 "+gate.LitSensors+" / 2 · 충전 "+Mathf.RoundToInt(gate.charge*100)+"%",text);
                    GUI.Label(new Rect(515,65,675,27),gate.unlocked?"빛을 옮겨도 문은 열린 상태로 유지됩니다.":game.held==game.mirror.prop?"반사판을 내려놓고 안착시켜야 충전됩니다.":"두 수신기에 동시에 빛을 맞추면 0.8초에 충전됩니다.",small);
                    Panel(new Rect(515,102,675,5),new Color(.12f,.23f,.31f));
                    Panel(new Rect(515,102,675*gate.charge,5),accent);
                }
                PlayerControls.DrawCursor(new Vector2(800,450),game.aimed!=null,game.held!=null,game.invalid||game.PlacementBlocked,accent,paper);
                if(game.held!=null)
                {
                    Panel(new Rect(380,646,840,143),ink);
                    GUI.Label(new Rect(400,659,800,32),game.held.title+" · 목표 ×"+game.RequestedScale.ToString("0.00")+" / 현재 ×"+game.held.transform.localScale.x.ToString("0.00"),text);
                    GUI.Label(new Rect(400,701,800,55),"오른쪽 버튼 + 마우스 회전 · Q/E 회전 · Z/X 기울기 · C 세우기\n시선으로 크기 조절 · 클릭 놓기 · Backspace 취소 · 안착 뒤 그늘 활성",small);
                    GUI.Label(new Rect(400,760,800,26),game.invalid?game.invalidReason:game.PlacementBlocked?"접촉 중 · 조준을 바꾸면 우회 경로를 찾습니다":"놓기 전에는 안전한 그늘이 생기지 않습니다",small);
                }
                else if(game.aimed!=null)GUI.Label(new Rect(580,482,690,40),"[클릭] "+game.aimed.title,text);
                if(game.showHints)
                {
                    Panel(new Rect(24,135,445,225),ink);
                    GUI.Label(new Rect(44,151,403,187),game.hintLevel==1?StarshipWorld.Hints[game.checkpoint]:StarshipWorld.DetailedHints[game.checkpoint],text);
                    GUI.Label(new Rect(44,331,405,24),"H : 단서 → 자세한 힌트 → 숨기기",small);
                }
                if(Time.unscaledTime<game.noticeUntil){Panel(new Rect(270,805,1060,36),ink);GUI.Label(new Rect(286,809,1025,30),game.notice,small);}
                if(game.checkpoint!=4||game.held!=null)
                {
                    Panel(new Rect(0,855,1600,45),ink);
                    GUI.Label(new Rect(24,865,1550,30),"WASD 이동 · 마우스 시점 · Space 점프 · 클릭 잡기/놓기 · H 힌트 · R 구역 재시도 · Shift+R 전체 재시도 · Esc 메뉴",small);
                }
            }
            if(!game.active&&!game.won)
            {
                Panel(new Rect(0,0,1600,900),new Color(.008f,.017f,.033f,.66f));Panel(new Rect(350,55,900,790),ink);
                GUI.Label(new Rect(393,95,816,63),game.started?"잠시 쉬어 가기":"PARALLAX / HELIOS",title);
                GUI.Label(new Rect(393,180,816,235),game.started?"실험선의 장비와 배치는 그대로 유지됩니다.\n\nH를 누르면 단서를 확인할 수 있습니다.":
                    "빛과 원근을 연구하던 실험용 우주선.\n남겨진 장비를 연결해 마지막 출구를 찾으세요.\n\n화물 · 반사판 · 보관 랙 · 냉각수 · 상층 정비 구역\n여러 기믹을 함께 사용하는 종합 퍼즐입니다.\n\nWASD 이동 / Space 점프 / 클릭 잡기·놓기\n먼 표면을 보면 커지고 가까운 표면을 보면 작아집니다.",text);
                PlayerControls.DrawOptions(new Rect(393,440,816,143),game.font);
                if(GUI.Button(new Rect(393,615,387,62),game.started?"계속하기":"실험선 입장",button))game.Resume();
                if(GUI.Button(new Rect(814,615,387,62),"종료",button))Application.Quit();
                GUI.Label(new Rect(393,785,816,30),"v"+StarshipGame.Version+" · 힌트 기본 숨김 · 구역별 복귀 · Kenney / CC0",small);
            }
        }
    }
}
