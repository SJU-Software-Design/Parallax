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
            if(game==null||game.Transitioning||game.conceptCapture)return;
            if(text==null)
            {
                text=new GUIStyle(GUI.skin.label){font=game.font,fontSize=21,wordWrap=true};text.normal.textColor=paper;
                title=new GUIStyle(text){fontSize=35,fontStyle=FontStyle.Bold};
                small=new GUIStyle(text){fontSize=17};
                button=new GUIStyle(GUI.skin.button){font=game.font,fontSize=22};
            }
            GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(Screen.width/1600f,Screen.height/900f,1));
            Panel(new Rect(28,24,game.conceptLayout?670:555,game.conceptLayout?150:game.checkpoint==1?139:102),new Color(.035f,.065f,.08f,.9f));
            GUI.Label(new Rect(48,35,630,34),"PARALLAX v"+GalleryGame.Version+"  /  빛과 시선의 미술관",text);
            GUI.Label(new Rect(48,75,620,30),game.RoomTitle,small);
            if(game.conceptLayout)GUI.Label(new Rect(48,108,620,49),game.Objective,small);
            if(game.checkpoint==1&&game.lightGate!=null)GUI.Label(new Rect(48,112,510,34),game.lightGate.illuminated?"센서: 빛 감지 · 문 열림 · 그림자로 이동하세요":"센서: 빛 없음 · 거울의 각도를 바꾸세요",small);
            if(game.active&&!game.won)
            {
                PlayerControls.DrawCursor(new Vector2(800,450),game.aimed!=null,game.held!=null,game.invalid||game.PlacementBlocked,mint,paper);
                Panel(new Rect(1195,25,377,88),new Color(.035f,.065f,.08f,.9f));
                string status=game.exhibitionPreview?"자유 관람":game.conceptLayout&&!game.InShadowCourse?
                    (game.checkpoint==2?"초상 "+(game.portraitGate==null?0:game.portraitGate.portrait.AlignedPieces)+" / 3 · 관람 지점에서 확인":"두 손 사이로 도서관이 보입니다"):
                    game.safe?"그늘 · 안전":"햇빛 · 그늘로 이동하세요";
                GUI.Label(new Rect(1215,34,338,38),status,small);
                Panel(new Rect(1215,79,333,9),new Color(.14f,.22f,.25f));
                float progress=game.conceptLayout&&game.checkpoint==2&&game.portraitGate!=null?game.portraitGate.Progress:game.exposure;
                Panel(new Rect(1215,79,333*progress,9),game.checkpoint==2?mint:new Color(1,.56f,.25f));
                if(game.held!=null)
                {
                    Panel(new Rect(405,590,790,175),new Color(.035f,.065f,.08f,.94f));
                    GUI.Label(new Rect(427,605,746,32),game.held.title+"  ·  거리 "+game.holdDistance.ToString("0.00")+"m  ·  크기 ×"+game.RequestedScale.ToString("0.00"),text);
                    GUI.Label(new Rect(427,644,746,63),"WASD: 들고 이동 · 오른쪽 버튼 + 마우스: 회전\nQ/E: 회전  ·  Z/X: 기울기  ·  C: 바로 세우기",small);
                    GUI.Label(new Rect(427,711,746,40),game.invalid?game.invalidReason:game.PlacementBlocked?"장애물 앞에서 멈춤 · 방향을 바꾸거나 Backspace로 취소":"왼쪽 클릭: 놓기 (중력·회전 적용)  ·  Backspace: 잡기 취소",small);
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
                if(game.showHints){Panel(new Rect(28,190,635,235),new Color(.035f,.065f,.08f,.93f));GUI.Label(new Rect(48,205,590,205),game.conceptLayout?game.RoomHint:GalleryWorld.Hints[game.checkpoint],small);}
                if(Time.unscaledTime<game.noticeUntil&&!game.showHints)
                    GUI.Label(new Rect(380,793,900,36),game.notice,small);
                Panel(new Rect(0,851,1600,49),new Color(.035f,.065f,.08f,.93f));
                GUI.Label(new Rect(30,861,1550,36),"WASD 이동   ·   마우스 시점   ·   Space 점프   ·   클릭 전시물 잡기/놓기   ·   R 구역 초기화   ·   Shift+R 처음부터   ·   H 힌트   ·   Esc 메뉴",small);
            }
            if(!game.active||game.won)
            {
                Panel(new Rect(0,0,1600,900),new Color(.025f,.045f,.055f,.7f));
                Panel(new Rect(362,55,876,790),new Color(.04f,.075f,.09f,.96f));
                GUI.Label(new Rect(412,95,770,66),game.won?"전시의 끝에 도착했습니다":game.started?"잠시 쉬어 가기":"PARALLAX  /  MUSEUM v"+GalleryGame.Version,title);
                GUI.Label(new Rect(412,180,770,235),game.conceptLayout&&!game.exhibitionPreview?
                    "복제작과 전시 패널의 그림자를 이어 햇빛을 건너세요.\n시점의 방에서는 눈·코·입을 하나의 초상으로 맞춥니다.\n관람 지점에서 2초 바라보면 커튼이 열립니다.\n\n로댕의 성당을 확대하고 두 손 사이로 들어가 보세요.\n작은 도서관 풍경 너머로 다음 스테이지가 이어집니다.\n\n앞의 두 전시실은 빛과 그림자 이동 구역입니다.\n시점의 방과 출구 전시실에서는 안전하게 조작할 수 있습니다.":game.exhibitionPreview?
                    "미술관 구상안을 기준으로 새로 구성한 전시 공간입니다.\n실제 명화와 니케의 실물 3D 스캔을 가까이에서 감상하세요.\n\n해변맵과 같은 최신 이동·사물 조작을 사용합니다.\n이젤의 복제작품과 시점 설치물은 크기와 위치를 바꿀 수 있습니다.\n\n현재는 공간 디자인 검토 버전입니다.\n출구 조각과 퍼즐 진행은 제작 중입니다.":game.won?
                    "높이와 그늘을 함께 설계해 출구에 도착했습니다.\n\n클리어 시간  "+System.TimeSpan.FromSeconds(game.elapsed).ToString(@"mm\:ss")+"   /   재시도  "+game.respawns+"회":
                    "명화와 조각 사이에서 그늘을 이어 이동하세요.\n시선이 향한 표면의 거리에 따라 사물의 실제 크기가 바뀝니다.\n\n거울로 빛의 방향을 바꾸고, 시점의 전시를 맞춰 보세요.\n마지막 조각의 바람을 따라, 커진 입 안으로 들어갑니다.\n\n왼쪽 클릭 잡기 / 놓기 · 오른쪽 버튼 + 마우스 회전\nBackspace 취소 · R 구역 초기화 · H 힌트 · T 설치 연습",text);
                PlayerControls.DrawOptions(new Rect(412,440,770,143),game.font);
                if(GUI.Button(new Rect(412,615,370,62),game.won?"처음부터 다시 전시 보기":game.started?"계속하기":"미술관 입장",button))game.Resume();
                if(GUI.Button(new Rect(812,615,370,62),"종료",button))Application.Quit();
                if(game.conceptLayout)
                {
                    if(GUI.Button(new Rect(412,691,245,48),"그림자 구역 체험",button))game.StartConceptDemo(1);
                    if(GUI.Button(new Rect(675,691,245,48),"시점의 방 체험",button))game.StartConceptDemo(2);
                    if(GUI.Button(new Rect(937,691,245,48),"출구 체험",button))game.StartConceptDemo(3);
                }
                else if(game.exhibitionPreview)
                {
                    if(GUI.Button(new Rect(812,691,370,48),"시점의 방으로 이동",button)){game.ResetAll();game.checkpoint=2;game.PlacePlayer(game.SpawnPoints[2]);game.Resume();}
                }
                else if(GUI.Button(new Rect(812,691,370,48),"오르쿠스 전시 바로 체험",button))game.StartOrcusDemo();
                GUI.Label(new Rect(412,760,770,30),game.conceptLayout?"명화·실물 니케 · 그림자 이동 · 시점의 초상 · 도서관 연결": "명화 5점 · 원근 체험 · 오르쿠스 → 도서관 · 최신 공통 조작",small);
                GUI.Label(new Rect(412,794,770,28),game.conceptLayout?"니케: Cosmo Wenman / CC BY 3.0 · 성당 스캔: Migg / CC BY 4.0":"명화: Public domain 자료 / 조각: 게임용 재해석",small);
            }
        }
    }
}
