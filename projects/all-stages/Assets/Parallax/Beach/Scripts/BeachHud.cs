using UnityEngine;

namespace ShadeLink.Beach
{
    public sealed class BeachHud:MonoBehaviour
    {
        public BeachGame game;
        GUIStyle text,title,small,button,goal;
        readonly Color paper=new Color(.98f,.97f,.91f),accent=new Color(.34f,.82f,1),ink=new Color(.035f,.12f,.17f,.94f);
        void Panel(Rect r,Color c){GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=Color.white;}
        void OnGUI()
        {
            if(game==null)return;
            if(text==null){text=new GUIStyle(GUI.skin.label){font=game.font,fontSize=22,wordWrap=true};text.normal.textColor=paper;title=new GUIStyle(text){fontSize=38,fontStyle=FontStyle.Bold};small=new GUIStyle(text){fontSize=18};button=new GUIStyle(GUI.skin.button){font=game.font,fontSize=24};goal=new GUIStyle(small){fontStyle=FontStyle.Bold};goal.normal.textColor=new Color(1,.78f,.38f);}
            GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(Screen.width/1600f,Screen.height/900f,1));
            Panel(new Rect(25,22,690,168),ink);GUI.Label(new Rect(47,32,646,34),"PARALLAX  /  마지막 해변  v"+BeachGame.Version,text);GUI.Label(new Rect(47,70,646,32),BeachWorld.Titles[game.checkpoint],text);
            GUI.Label(new Rect(47,106,646,29),"최종 목표  ·  깃발 있는 모래성 정상",goal);
            string status=game.checkpoint==0?FirstGateStatus():game.checkpoint==2?HarborStatus():game.checkpoint==3?(game.castleRoute!=null?game.castleRoute.Status:"작은 성벽 조각을 찾아보세요") : "판자는 통로 · 파라솔은 그늘";
            GUI.Label(new Rect(47,143,646,31),status,small);
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
                if(game.showHints){Panel(new Rect(25,207,630,230),ink);GUI.Label(new Rect(47,224,587,204),BeachWorld.Hints[game.checkpoint],text);}
                if(Time.unscaledTime<game.noticeUntil)GUI.Label(new Rect(300,789,1090,50),game.notice,small);
                Panel(new Rect(0,851,1600,49),ink);GUI.Label(new Rect(25,861,1550,34),"WASD 이동 · 마우스 시점 · Space 점프 · 클릭 잡기/놓기 · R 구역 초기화 · Shift+R 전체 초기화 · H 힌트 · T 설치 연습 · Esc 메뉴",small);
            }
            if(!game.active||game.won)
            {
                Panel(new Rect(0,0,1600,900),new Color(.025f,.07f,.10f,.52f));Panel(new Rect(320,55,960,790),ink);
                GUI.Label(new Rect(366,95,870,62),game.won?"모래성 정상에서 탈출했습니다":game.started?"잠시 쉬어 가기":"PARALLAX / 마지막 해변",title);
                GUI.Label(new Rect(366,180,865,235),game.won?"원근·반사광·그늘·물살·점프를 연결해 마지막 스테이지를 완료했습니다.\n\n기록  "+System.TimeSpan.FromSeconds(game.elapsed).ToString(@"mm\:ss")+"  ·  복귀 "+game.respawns+"회\n\n3D 소품: Beach Surf and Paddle Kit / 3DAssets.dev / CC0":
                    "목표는 멀리 깃발이 보이는 모래성 정상입니다.\n한낮의 해변에서는 스스로 만든 그늘로 이동하세요.\n\n01  태양빛을 거울로 반사해 두 수신기를 연결하세요.\n02  판자로 물길을 잇고 파라솔로 그 위를 가리세요.\n03  그림자에서 배를 꺼내 키우고 그늘에 승선하세요.\n04  작은 성벽 조각을 키워 끊긴 성벽 두 곳을 복구하세요.\n\n배의 그늘 안에서 E 출항 · H로 구역별 힌트",text);
                PlayerControls.DrawOptions(new Rect(366,440,865,143),game.font);
                if(GUI.Button(new Rect(366,615,414,63),game.won?"처음부터 다시":game.started?"계속하기":"해변 입장",button))game.Resume();
                if(GUI.Button(new Rect(816,615,414,63),"종료",button))Application.Quit();
                game.showHints=GUI.Toggle(new Rect(366,710,800,35),game.showHints,"구역별 힌트 표시 (플레이 중 H로 변경)",small);
                GUI.Label(new Rect(366,785,870,29),"v"+BeachGame.Version+" · 4개 구역 · 물 접촉 시 복귀 · 성벽 중간 체크포인트",small);
            }
        }
        string HarborStatus()
        {
            var harbor=game.shadowHarbor;
            if(harbor!=null&&!harbor.Solved)
                return "선체 "+Mathf.RoundToInt(harbor.HullCoverage*100)+"% · 차양 "+Mathf.RoundToInt(harbor.CanopyCoverage*100)+
                    "% · 바깥 빛 "+Mathf.RoundToInt(harbor.Clearance*100)+"% · 완성 "+Mathf.RoundToInt(harbor.Charge*100)+"%";
            if(harbor!=null&&harbor.Materializing)return harbor.Dropping?"작은 배가 보드 앞 받침으로 떨어집니다":"그림자 완성 · 보드의 배가 작아지고 있습니다";
            var boat=game.boat;
            if(boat==null)return "세워진 제작 보드에서 그림자 배를 완성하세요";
            if(boat.Arrived)return "그늘 배 도착 완료 · 열린 길을 따라 모래성으로";
            if(boat.Departed)return "배의 실제 그늘 안에서 항해 중";
            if(boat.IsAboard)return "승선 완료 · 그늘 안에서 E 출항";
            if(game.held==boat.Prop)return "손에 든 배를 출항 부두 앞 물에 크게 놓으세요";
            if(BeachWater.Supports(boat.Prop))return "물에 놓인 배의 그늘진 갑판에 올라타세요";
            return "보드 앞에 떨어진 작은 배를 직접 잡아 물가로 가져가세요";
        }
        string FirstGateStatus()
        {
            var gate=game.lightGate;
            if(gate==null)return "태양빛 → 거울 → 두 수신기";
            if(gate.unlocked)return "두 수신기 연결 완료 · 열린 게이트 너머로 그늘을 이으세요";
            if(game.mirror!=null&&game.held==game.mirror.prop)
                return "거울을 내려놓으세요 · 안착한 거울만 빛을 연결합니다";
            if(game.mirror!=null&&game.mirror.prop.placement!=null&&!game.mirror.prop.placement.Ready)
                return "거울이 안착하면 반사광이 연결됩니다";
            int total=gate.sensors==null?2:gate.sensors.Length;
            return "빛 → 거울 → 수신기  "+gate.LitSensors+" / "+total+"  ·  충전 "+Mathf.RoundToInt(gate.charge*100)+"%";
        }
    }
}
