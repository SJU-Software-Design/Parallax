using System.Collections.Generic;
using UnityEngine;

namespace ShadeLink
{
    // The picture is an exit requirement inside the original courtyard. Its
    // shape coordinates stay local; player movement/exposure stay in ShadeGame.
    public sealed class CourtyardShadowPuzzle
    {
        public const float Unit = .55f;
        public static readonly Vector2 Origin = new Vector2(-5.2f,13.45f);
        public static readonly Bounds Frame = new Bounds(new Vector3(1.95f,1.8f,-15.87f),new Vector3(2.25f,2.25f,.12f));
        public ShadowPuzzleState state;
        public ShadowPiece[] worldPieces;
        public Bounds[] solids;
        public List<Vector2>[] projected;
        public Vector2 tokenAnchor;
        public bool blocked, hints;
        public CourtyardPuzzleVisuals visuals;
        readonly ShadeGame game;
        float matchClock;
        public bool HoldingPiece => state.held != null;
        public bool Carrying => state.stage == ShadowStage.Carrying;
        public bool Restored => state.stage == ShadowStage.Restored;
        public static Vector2 World(Vector2 local) => Origin + local * Unit;
        public static Vector2 Local(Vector2 world) => (world - Origin) / Unit;
        public static ShadowPiece[] InitialPieces()
        {
            Vector2[] positions = { new Vector2(-5.7f,15),new Vector2(-2.1f,12.1f),new Vector2(-6.25f,12.6f),
                new Vector2(-5.9f,11.8f),new Vector2(-1.9f,13.6f),new Vector2(-2.05f,15.1f),new Vector2(-5.9f,13.9f) };
            var pieces = new ShadowPiece[positions.Length];
            for(int i=0;i<pieces.Length;i++) pieces[i]=new ShadowPiece(i,Local(positions[i]));
            return pieces;
        }
        public CourtyardShadowPuzzle(ShadeGame owner,Font font)
        {
            game=owner; Reset();
            visuals=new GameObject("Courtyard picture puzzle").AddComponent<CourtyardPuzzleVisuals>();
            visuals.Build(this,font,game.visuals.view); visuals.Sync(this);
        }
        public void Reset()
        {
            state=new ShadowPuzzleState { pieces=InitialPieces() }; tokenAnchor=Origin;
            blocked=false; matchClock=0; SyncPlayer(); RefreshGeometry(); state.Evaluate(0);
            if(visuals!=null) visuals.Sync(this);
        }
        public void SyncPlayer()
        {
            var s=game.state; Vector2 point=Local(s.player.Point);
            state.player.feet=new Vector3(point.x,s.player.Eye.y/Unit-Rules.EyeHeight,point.y);
            state.player.grounded=s.player.grounded; state.yaw=s.yaw; state.pitch=s.pitch;
        }
        public void SetFixedSize(bool enabled)
        {
            SyncPlayer(); if(state.fixedSize==enabled) return;
            state.fixedSize=enabled;
            if(HoldingPiece) state.held=ShadowPuzzleRules.Capture(state,state.held.index);
        }
        public void RefreshGeometry()
        {
            worldPieces=new ShadowPiece[state.pieces.Length]; solids=new Bounds[worldPieces.Length];
            for(int i=0;i<worldPieces.Length;i++)
            { var p=state.pieces[i]; worldPieces[i]=new ShadowPiece(p.shape,World(p.position),p.scale*Unit); solids[i]=worldPieces[i].Bounds; }
            projected=ShadowPuzzleRules.Shadows(worldPieces);
        }
        public List<Vector2>[] TokenPolygons()
        {
            if(state.frozen==null) return new List<Vector2>[0];
            var result=new List<Vector2>[state.frozen.Length];
            for(int i=0;i<result.Length;i++)
            { result[i]=new List<Vector2>(); foreach(Vector2 p in state.frozen[i]) result[i].Add(tokenAnchor+p*Unit*state.tokenScale); }
            return result;
        }
        public List<Vector2>[] SafetyShadows(List<Vector2>[] original)
        {
            var all=new List<List<Vector2>>(original);
            if(state.stage==ShadowStage.Arranging) all.AddRange(projected);
            else if(state.stage==ShadowStage.Ready) all.AddRange(TokenPolygons());
            return all.ToArray();
        }
        public bool SafeAboveFloor(Player player)
        {
            if(state.stage!=ShadowStage.Arranging || player.feet.y<=Rules.Epsilon) return false;
            foreach(var piece in worldPieces)
                if(ShadowPuzzleRules.RayPiece(player.feet+Vector3.up*.0001f,-Rules.Sun,piece,out _)) return true;
            return false;
        }
        float OriginalObstacle()
        {
            float nearest=(1f/0f); var s=game.state; Vector3 look=Rules.Look(s.yaw,s.pitch);
            foreach(Bounds b in Rules.Solids(s.blocks))
                if(Rules.RayBox(s.player.Eye,look,b,out float distance)) nearest=Mathf.Min(nearest,distance);
            return nearest;
        }
        public int AimPiece(out float distance)
        {
            SyncPlayer(); int index=ShadowPuzzleRules.Aim(state,out float localDistance); distance=localDistance*Unit;
            return index>=0 && distance<OriginalObstacle()?index:-1;
        }
        public bool BlocksOriginalAim(int index)
        {
            if(index<0) return false;
            SyncPlayer(); int part=ShadowPuzzleRules.Aim(state,out float distance);
            return part>=0 && Rules.RayBox(game.state.player.Eye,Rules.Look(game.state.yaw,game.state.pitch),game.state.blocks[index].Bounds,out float hit) && distance*Unit<hit;
        }
        public bool AimFloor(out Vector2 point,out float distance)
        {
            var s=game.state; Vector3 look=Rules.Look(s.yaw,s.pitch); point=default; distance=0;
            if(look.y>=-.015f) return false;
            distance=(.029f-s.player.Eye.y)/look.y; Vector3 hit=s.player.Eye+look*distance; point=new Vector2(hit.x,hit.z);
            return distance>0 && distance<8.5f && Rules.Room.Contains(point);
        }
        public bool AimToken()
        {
            if(state.stage!=ShadowStage.Ready || !AimFloor(out Vector2 p,out float distance)) return false;
            SyncPlayer(); ShadowPuzzleRules.Aim(state,out float part);
            return distance<OriginalObstacle() && distance<part*Unit && ShadowPuzzleRules.Contains((p-tokenAnchor)/(Unit*state.tokenScale),state.frozen);
        }
        public bool AimFrame()
        {
            var s=game.state;
            return Rules.RayBox(s.player.Eye,Rules.Look(s.yaw,s.pitch),Frame,out float distance) && distance<4.5f && distance<OriginalObstacle();
        }
        public bool ValidPlacement(ShadowPiece local,int index)
        {
            var p=new ShadowPiece(local.shape,World(local.position),local.scale*Unit); Bounds b=p.Bounds;
            if(b.min.x<-6.95f || b.max.x>6.95f || b.min.z<-15.9f || b.max.z>15.9f || Rules.Touches(game.state.player.Point,b,.30f)) return false;
            foreach(Bounds original in Rules.Solids(game.state.blocks)) if(Rules.Overlap(b,original)) return false;
            for(int i=0;i<solids.Length;i++) if(i!=index && Rules.Overlap(b,solids[i],.012f)) return false;
            foreach(Vector2 point in ShadowPuzzleRules.Shadow(p)) if(!Rules.Room.Contains(point)) return false;
            return true;
        }
        public bool BlocksPlacement(Block candidate)
        { foreach(Bounds b in solids) if(Rules.Overlap(candidate.Bounds,b)) return true; return false; }
        public bool Interact()
        {
            SyncPlayer();
            if(HoldingPiece)
            {
                int index=state.held.index; var piece=state.pieces[index]; var aligned=new ShadowPiece(piece.shape,ShadowPuzzleRules.Shapes[piece.shape].solution);
                if((piece.position-aligned.position).magnitude<.28f && Mathf.Abs(piece.scale-1)<.12f && ValidPlacement(aligned,index)) state.pieces[index]=aligned;
                state.held=null; blocked=false; RefreshGeometry(); state.Evaluate(0); return true;
            }
            if(Carrying)
            {
                if(AimFrame())
                {
                    state.Restore(); game.state.pictureRestored=true;
                    game.Tell("액자 복원 완료 · 이제 두 사물을 발판으로 놓고 출구로 올라가세요.",7);
                }
                else if(AimFloor(out Vector2 point,out float distance) && distance<OriginalObstacle())
                {
                    // A small floor token can be safely set down in the narrow
                    // exit shelter; its immutable silhouette is unchanged.
                    Vector2 anchor=new Vector2(Mathf.Clamp(point.x,-6.4f,6.49f),Mathf.Clamp(point.y,-15.40f,15.3f));
                    var footprint=new Bounds(new Vector3(anchor.x,.03f,anchor.y),Vector3.zero);
                    foreach(var poly in state.frozen) foreach(Vector2 v in poly)
                    { Vector2 at=anchor+v*Unit*.4f; footprint.Encapsulate(new Vector3(at.x,.03f,at.y)); }
                    foreach(Bounds obstacle in Rules.Solids(game.state.blocks,solids))
                        if(Rules.Overlap(footprint,obstacle,0))
                        { game.Tell("사물 아래에는 놓을 수 없습니다. 빈 바닥을 조준해주세요."); return true; }
                    state.DropShadow(Vector2.zero); state.tokenScale=.4f; tokenAnchor=anchor;
                    game.Tell("그림자를 내려놓았습니다. 기둥·차광판을 조절한 뒤 다시 집으세요.");
                }
                else game.Tell("액자 가까이에서 넣거나, 가려지지 않은 바닥에 내려놓으세요.");
                return true;
            }
            if(AimToken()) { state.PickShadow(); game.Tell("그림자를 들었습니다 · 운반 중에도 햇빛을 피해 그늘로 이동하세요.",6); return true; }
            int aimed=AimPiece(out _);
            if(aimed>=0)
            {
                if(state.stage!=ShadowStage.Arranging) { game.Tell("완성한 도형 배치입니다. 바닥의 그림자 조각을 집어주세요."); return true; }
                if(!game.state.player.grounded || game.state.player.support>=game.state.blocks.Length+2)
                { game.Tell("바닥에 내려온 뒤 도형을 잡아주세요."); return true; }
                state.held=ShadowPuzzleRules.Capture(state,aimed); state.stable=0; return true;
            }
            if(state.stage==ShadowStage.Arranging && AimFloor(out Vector2 floor,out _) && ShadowPuzzleRules.Contains(Local(floor),ShadowPuzzleRules.Target))
            { game.Tell("미완성 그림자는 집을 수 없습니다. 일곱 도형을 먼저 맞춰주세요."); return true; }
            return false;
        }
        public void Tick(float dt)
        {
            SyncPlayer();
            if(HoldingPiece)
            {
                blocked=!ShadowPuzzleRules.Place(state,out ShadowPiece placed) || !ValidPlacement(placed,state.held.index);
                if(!blocked) { state.pieces[state.held.index]=placed; RefreshGeometry(); }
            }
            matchClock+=dt;
            if(matchClock>=.1f)
            {
                if(state.Evaluate(matchClock)) game.Tell("강아지 그림자 완성 · 빛나는 그림자를 집어 도착 구역의 액자로 운반하세요.",7);
                matchClock=0;
            }
            game.state.pictureRestored=Restored;
        }
        public void Respawn()
        { state.held=null; state.stable=0; blocked=false; SyncPlayer(); }
        public string Prompt()
        {
            if(HoldingPiece) return ShadowPuzzleRules.Shapes[state.held.kind].name+" · 클릭 놓기 · Shift 크기 고정";
            if(Carrying) return AimFrame()?"클릭 · 강아지 그림자를 액자에 넣기":"그림자 운반 중 · 햇빛 주의 · 바닥을 보고 클릭하면 내려놓기";
            if(AimToken()) return "클릭 · 완성된 강아지 그림자 집기";
            int part=AimPiece(out _);
            return part>=0 && state.stage==ShadowStage.Arranging?"클릭 · "+ShadowPuzzleRules.Shapes[part].name+" 잡기":null;
        }
    }
}
