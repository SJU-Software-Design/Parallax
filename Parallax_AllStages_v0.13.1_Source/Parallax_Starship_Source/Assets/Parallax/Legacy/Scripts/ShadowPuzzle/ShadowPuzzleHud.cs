using UnityEngine;

namespace ShadeLink
{
    public sealed class ShadowPuzzleHud : MonoBehaviour
    {
        ShadowPuzzleGame game;
        Font font;
        GUIStyle label,button;
        float width,height;
        readonly Color ink=new Color(.93f,.93f,.86f),muted=new Color(.67f,.74f,.73f),panel=new Color(.035f,.068f,.078f,.95f);
        public void Initialize(ShadowPuzzleGame owner,Font koreanFont) { game=owner; font=koreanFont; }
        void Fill(Rect rect,Color color) { GUI.color=color; GUI.DrawTexture(rect,Texture2D.whiteTexture); GUI.color=Color.white; }
        void Text(float x,float y,float w,float h,string value,int size=20,Color? color=null,bool bold=false,TextAnchor align=TextAnchor.UpperLeft)
        {
            label.fontSize=size; label.fontStyle=bold?FontStyle.Bold:FontStyle.Normal; label.normal.textColor=color??ink; label.alignment=align;
            GUI.Label(new Rect(x,y,w,h),value,label);
        }
        bool Button(float x,float y,float w,float h,string value,bool primary=false)
        {
            var r=new Rect(x,y,w,h); Fill(r,primary?ShadeVisuals.Teal:(r.Contains(Event.current.mousePosition)?new Color(.19f,.30f,.31f):new Color(.12f,.21f,.23f)));
            button.normal.textColor=primary?panel:ink; return GUI.Button(r,value,button);
        }
        void OnGUI()
        {
            if(game==null) return;
            if(label==null) { label=new GUIStyle { font=font,wordWrap=true }; button=new GUIStyle { font=font,fontSize=21,alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold }; }
            float scale=Mathf.Min(Screen.width/1600f,Screen.height/900f); width=Screen.width/scale; height=Screen.height/scale;
            GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
            if(game.state.started) Hud();
            if(!game.state.active) Menu();
            GUI.matrix=Matrix4x4.identity;
        }
        void Hud()
        {
            var s=game.state;
            string[] titles={"01  일곱 그림자로 강아지 만들기","02  완성된 그림자 집기","03  그림자를 액자에 넣기","04  열린 문으로 나가기"};
            string[] details={"도형을 조준하고 클릭해 잡으세요.\n바닥의 흐린 윤곽에 그림자를 맞춰주세요.","강아지 그림자가 하나의 조각으로 굳었습니다.\n바닥에서 빛나는 그림자를 조준하고 클릭하세요.","그림자를 들고 벽의 액자 앞으로 이동하세요.\n가까이에서 액자를 조준하고 클릭하면 들어갑니다.","강아지의 색이 돌아오고 잠금이 풀렸습니다.\n액자 오른쪽의 문으로 걸어가세요."};
            Fill(new Rect(26,25,507,182),panel); Fill(new Rect(26,25,4,182),ShadeVisuals.Teal);
            Text(47,41,460,28,"PARALLAX   /   그림자 액자   0.4",17,muted);
            Text(47,79,470,36,titles[(int)s.stage],24,ink,true);
            Text(47,129,470,60,details[(int)s.stage],18,muted);
            Fill(new Rect(26,220,507,93),panel);
            float progress=s.stage==ShadowStage.Arranging?s.match.iou:1;
            Text(47,235,454,28,s.stage==ShadowStage.Arranging?"그림자 일치도   "+Mathf.RoundToInt(progress*100)+"%":"그림자 완성   ·   "+(s.DoorUnlocked?"액자 복원 완료":"획득 가능"),21,ShadeVisuals.Teal,true);
            Fill(new Rect(47,278,458,8),new Color(.20f,.29f,.30f)); Fill(new Rect(47,278,Mathf.Max(1,458*progress),8),ShadeVisuals.Teal);
            float rx=width-292;
            Fill(new Rect(rx,25,266,303),panel); Text(rx+17,41,232,28,"액자의 빈자리",19,muted);
            GUI.DrawTexture(new Rect(rx+17,81,232,184),game.visuals.dogTexture,ScaleMode.ScaleToFit);
            Text(rx+17,282,232,30,s.DoorUnlocked?"● 액자 복원 · 문 열림":"○ 액자 복원 전 · 문 잠김",17,s.DoorUnlocked?ShadeVisuals.Teal:ShadeVisuals.Amber);
            float cx=width/2,cy=height/2;
            Color cross=s.held!=null||ShadowPuzzleRules.AimToken(s)||ShadowPuzzleRules.AimFrame(s)||game.aimed>=0?ShadeVisuals.Teal:ink;
            Fill(new Rect(cx-6,cy-1,12,2),cross); Fill(new Rect(cx-1,cy-6,2,12),cross);
            string prompt=null;
            if(s.held!=null)
            {
                var piece=s.pieces[s.held.index];
                Fill(new Rect(cx-293,cy+42,586,117),panel);
                Text(cx-273,cy+56,546,31,ShadowPuzzleRules.Shapes[piece.shape].name+"  /  "+piece.scale.ToString("0.00")+"배",21,ShadeVisuals.Teal,true,TextAnchor.UpperCenter);
                Text(cx-273,cy+94,546,57,game.blocked?"사물 또는 벽과 겹칩니다 · 마지막 유효 위치를 유지합니다":s.fixedSize?"크기 고정 중 · 마우스로 배치 · 클릭해서 놓기":"시선을 멀리 두면 커지고, 가까이 두면 작아집니다\nShift를 누르는 동안 크기 고정 · 클릭해서 놓기",17,game.blocked?ShadeVisuals.Amber:ink,false,TextAnchor.UpperCenter);
            }
            else if(s.stage==ShadowStage.Carrying) prompt=ShadowPuzzleRules.AimFrame(s)?"왼쪽 클릭 · 액자에 강아지 그림자 넣기":"액자 앞으로 이동하세요 · 바닥을 보고 클릭하면 내려놓기";
            else if(ShadowPuzzleRules.AimToken(s)) prompt="왼쪽 클릭 · 완성된 강아지 그림자 집기";
            else if(s.stage==ShadowStage.Arranging && game.aimed>=0) prompt="왼쪽 클릭 · "+ShadowPuzzleRules.Shapes[s.pieces[game.aimed].shape].name+" 잡기";
            else if(s.stage==ShadowStage.Arranging && ShadowPuzzleRules.AimFloor(s,out Vector2 p,out _) && ShadowPuzzleRules.Contains(p,ShadowPuzzleRules.Target)) prompt="미완성 그림자는 집을 수 없습니다 · 도형을 먼저 맞춰주세요";
            if(prompt!=null)
            { Fill(new Rect(cx-345,cy+33,690,49),panel); Text(cx-329,cy+45,658,32,prompt,19,ink,true,TextAnchor.UpperCenter); }
            if(game.hints && s.stage==ShadowStage.Arranging)
            {
                Fill(new Rect(26,327,336,304),panel); Text(46,343,293,31,"H  힌트 켜짐 · 도형별 목표 윤곽",18,ShadeVisuals.Teal,true);
                for(int i=0;i<ShadowPuzzleRules.Shapes.Length;i++)
                { var shape=ShadowPuzzleRules.Shapes[i]; Fill(new Rect(47,389+i*30,10,10),shape.color); Text(68,382+i*30,263,28,shape.name,17,ink); }
            }
            if(!string.IsNullOrEmpty(game.notice) && Time.unscaledTime<game.noticeUntil)
            { Fill(new Rect(cx-505,height-157,1010,55),panel); Text(cx-487,height-142,974,37,game.notice,18,ShadeVisuals.Teal,false,TextAnchor.UpperCenter); }
            Fill(new Rect(26,height-79,width-52,53),panel);
            Text(42,height-64,width-84,36,"WASD 이동   ·   마우스 시점   ·   클릭 잡기 / 놓기 / 넣기   ·   Shift 크기 고정   ·   H 힌트   ·   R 초기화   ·   Esc 메뉴",18,ink,false,TextAnchor.UpperCenter);
        }
        void Menu()
        {
            var s=game.state; Fill(new Rect(0,0,width,height),new Color(.02f,.04f,.045f,s.started?.65f:.12f));
            float x=52,y=Mathf.Max(28,(height-816)/2);
            Fill(new Rect(x,y,658,816),panel); Fill(new Rect(x,y,658,4),ShadeVisuals.Teal);
            Text(x+36,y+30,586,29,"PARALLAX    /    UNITY PROTOTYPE  0.4",17,ShadeVisuals.Teal);
            Text(x+34,y+77,588,75,s.won?"강아지가 돌아왔어요":"잃어버린 강아지",s.won?43:49,ink,true);
            Text(x+36,y+165,586,68,s.won?"그림자를 모으고, 액자를 복원하고, 문을 열었습니다.":s.started?"잠시 멈췄습니다. 이어서 그림자를 완성해보세요.":"일곱 도형의 그림자로 강아지를 만들어\n텅 빈 액자에 돌려주세요.",23,muted);
            if(s.won)
            {
                int seconds=Mathf.FloorToInt(s.elapsed);
                Text(x+36,y+270,586,49,"완료 시간   "+seconds/60+"분 "+seconds%60+"초",26,ShadeVisuals.Teal,true);
                Text(x+36,y+349,586,119,"도형을 옮겨 그림자를 조합했습니다.\n완성된 그림자를 집어 액자에 넣었습니다.\n다른 배치와 크기로 다시 시도할 수 있습니다.",22,ink);
            }
            else
            {
                Text(x+36,y+266,586,31,"01  도형을 잡아 그림자 맞추기",23,ShadeVisuals.Teal,true);
                Text(x+36,y+308,586,92,"클릭해서 잡고 마우스로 위치를 바꿉니다.\n가까이 보면 작게, 멀리 보면 크게 배치됩니다.\nShift는 크기 고정, H는 도형별 윤곽 힌트입니다.",20,ink);
                Text(x+36,y+420,586,31,"02  완성한 그림자를 집어 액자로",23,ShadeVisuals.Teal,true);
                Text(x+36,y+462,586,96,"미완성 그림자는 집을 수 없습니다.\n완성되면 빛나는 그림자를 클릭해 들어 올립니다.\n액자 가까이에서 클릭해 넣으면 문이 열립니다.",20,ink);
            }
            if(Button(x+36,y+601,586,59,s.won?"그림자 퍼즐 다시 플레이":s.started?"계속 플레이":"그림자 퍼즐 시작",true)) game.Resume();
            if(Button(x+36,y+674,286,49,"기존 그늘 잇기")) ShadeGame.SwitchMode(true);
            if(Button(x+336,y+674,286,49,"게임 종료")) Application.Quit();
            if(s.started && !s.won)
            { if(Button(x+36,y+737,586,43,"이 퍼즐 초기화")) { game.ResetPuzzle(); game.Resume(); } }
            else Text(x+36,y+748,586,38,"일곱 도형 · 하나의 그림자 · 되살아나는 액자",17,muted,false,TextAnchor.UpperCenter);
        }
    }
}
