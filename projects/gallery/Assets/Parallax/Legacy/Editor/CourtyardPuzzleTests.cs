using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ShadeLink.Editor
{
    public static class CourtyardPuzzleTests
    {
        static readonly List<string> checks=new List<string>();
        static void Require(bool condition,string message){if(!condition)throw new Exception("INTEGRATION_TEST_FAILED: "+message);}
        static void Check(string name,Action body){body();checks.Add(name);Debug.Log("INTEGRATION_PASS "+name);}
        static ShadowPiece[] World(ShadowPiece[] pieces)
        {
            var result=new ShadowPiece[pieces.Length];
            for(int i=0;i<pieces.Length;i++)result[i]=new ShadowPiece(pieces[i].shape,CourtyardShadowPuzzle.World(pieces[i].position),pieces[i].scale*CourtyardShadowPuzzle.Unit);
            return result;
        }
        public static void Run()
        {
            checks.Clear();
            Check("Seven initial and solved shapes fit inside the original starting shade without solid overlaps",()=>
            {
                foreach(var pieces in new[]{World(CourtyardShadowPuzzle.InitialPieces()),World(ShadowPuzzleRules.SolvedPieces())})
                    for(int i=0;i<pieces.Length;i++)
                    {
                        foreach(Vector2 p in ShadowPuzzleRules.Shadow(pieces[i]))Require(Rules.Entry.Contains(p),"Shadow leaves starting shelter: "+i+" "+p);
                        foreach(Bounds b in Rules.Solids(Rules.InitialBlocks()))Require(!Rules.Overlap(pieces[i].Bounds,b),"Original object overlap "+i);
                        for(int j=0;j<i;j++)Require(!Rules.Overlap(pieces[i].Bounds,pieces[j].Bounds,.012f),"Plate overlap "+i+" / "+j);
                    }
            });
            Check("Original raised exit requires both the shade checkpoint and restored picture",()=>
            {
                var s=new GameState{pictureRequired=true,shadeSolved=true};s.player.feet=new Vector3(4.7f,2.7f,-15.05f);
                Require(!s.CanFinish,"Unrestored frame allowed escape");s.pictureRestored=true;Require(s.CanFinish,"Fully solved exit rejected");
                s.shadeSolved=false;Require(!s.CanFinish,"Picture bypassed original shade checkpoint");
                s.shadeSolved=true;s.held=new Grab();Require(!s.CanFinish,"Held original object allowed escape");
                s.held=null;s.checkpoint=new Vector2(6.6f,-14.3f);s.Respawn();Require(s.pictureRestored&&s.shadeSolved,"Respawn erased saved picture progress");
            });
            Check("Original movement collides with added plates and respawn avoids an occupied starting point",()=>
            {
                var piece=World(CourtyardShadowPuzzle.InitialPieces())[0];var p=new Player(new Vector2(piece.position.x,15.6f));
                Rules.Simulate(p,new Vector2(0,-3),1,Rules.InitialBlocks(),false,new[]{piece.Bounds});
                Require(p.feet.z>piece.Bounds.max.z,"Walked through new solid");
                var obstruction=new Bounds(new Vector3(Rules.Start.x,.25f,Rules.Start.y),new Vector3(1,.5f,1));
                var s=new GameState();s.Respawn(new[]{obstruction});
                Require(Rules.Entry.Contains(s.player.Point)&&Rules.CanOccupy(s.player.Point,s.blocks,new[]{obstruction}),"Respawn inside a new solid");
            });
            Check("Added visible floor shadows protect feet but cannot protect above their light occluders",()=>
            {
                var piece=new ShadowPiece(0,new Vector2(0,6),.55f);var poly=ShadowPuzzleRules.Shadow(piece);Vector2 point=Vector2.zero;
                foreach(Vector2 p in poly)point+=p;point/=poly.Count;
                var player=new Player(point);Require(Rules.SafeAtHeight(player,new Block[0],new[]{poly}),"Visible floor shadow not safe");
                player.feet.y=2;Require(!ShadowPuzzleRules.RayPiece(player.feet,-Rules.Sun,piece,out _),"Low plate protects above itself");
            });
            Check("Picture pieces do not skip the original two-shadow bridge requirement",()=>
            {
                var pieces=World(CourtyardShadowPuzzle.InitialPieces());var extra=new Bounds[pieces.Length];
                for(int i=0;i<pieces.Length;i++)extra[i]=pieces[i].Bounds;
                var blocks=Rules.InitialBlocks();var shadows=new List<List<Vector2>>(Rules.Shadows(blocks));shadows.AddRange(ShadowPuzzleRules.Shadows(pieces));
                Require(Rules.Route(blocks,shadows.ToArray(),null,null,extra).Count==0,"Initial picture pieces provide full escape route");
                var p=new Player(Rules.Start);var grab=Rules.Capture(p,0,-.14f,0,blocks);
                Require(Rules.Placement(p,0,grab,14.4f,blocks,out blocks[0],out _),"Pillar placement");
                p=new Player(Rules.Staging);grab=Rules.Capture(p,0,-.18f,1,blocks);
                Require(Rules.Placement(p,0,grab,13.05f,blocks,out blocks[1],out _),"Screen placement");
                shadows=new List<List<Vector2>>(Rules.Shadows(blocks));shadows.AddRange(ShadowPuzzleRules.Shadows(pieces));
                Require(Rules.Route(blocks,shadows.ToArray(),null,null,extra).Count>100,"Original connected route obstructed by picture pieces");
            });
            Directory.CreateDirectory("Validation");File.WriteAllText("Validation/integrated-rules-results.txt","Unity "+Application.unityVersion+"\nPassed: "+checks.Count+"\n"+string.Join("\n",checks));
        }
    }
}
