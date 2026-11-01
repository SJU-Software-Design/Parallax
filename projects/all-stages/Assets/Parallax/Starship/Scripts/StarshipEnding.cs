using UnityEngine;

namespace ShadeLink.Starship
{
    public sealed class StarshipEnding:MonoBehaviour
    {
        public enum Phase {None,Corridor,Airlock,Drifting,Complete}
        public StarshipGame game;
        public StarshipProp exitSign;
        public Phase phase;
        public float opening,driftTime;
        public Transform corridor,leftDoor,rightDoor;
        Vector3 leftHome,rightHome,driftStart;
        Quaternion driftRotation;
        public bool ShowEnd=>driftTime>=9;
        public void BuildCorridor(Transform root,Material dark,Material black,Material cyan,Material white)
        {
            corridor=new GameObject("Hidden black corridor / airlock").transform;corridor.SetParent(root,false);
            corridor.position=StarshipWorld.Point(4,new Vector3(0,0,15));corridor.rotation=Quaternion.Euler(0,180,0);
            StarshipWorld.Box("Black corridor floor",new Vector3(0,-.15f,13),new Vector3(3.2f,.3f,28),dark,corridor);
            StarshipWorld.Box("Black corridor ceiling",new Vector3(0,3.4f,12),new Vector3(3.5f,.25f,26),black,corridor);
            foreach(int side in new[]{-1,1})
            {
                StarshipWorld.Box("Black corridor wall",new Vector3(side*1.65f,1.7f,12),new Vector3(.3f,3.4f,26),black,corridor);
                var dim=StarshipWorld.Mat("Dim airlock guide "+side,new Color(.015f,.09f,.13f),true);
                StarshipWorld.Box("Dim floor guide",new Vector3(side*1.25f,.016f,11),new Vector3(.018f,.015f,23),dim,corridor,false,false);
            }
            leftDoor=StarshipWorld.Box("Airlock left",new Vector3(-.8f,1.7f,22),new Vector3(1.6f,3.4f,.3f),dark,corridor).transform;
            rightDoor=StarshipWorld.Box("Airlock right",new Vector3(.8f,1.7f,22),new Vector3(1.6f,3.4f,.3f),dark,corridor).transform;
            leftHome=leftDoor.localPosition;rightHome=rightDoor.localPosition;
            var light=new GameObject("Airlock blue status light").AddComponent<Light>();light.transform.SetParent(corridor,false);light.transform.localPosition=new Vector3(0,2.8f,20);light.type=LightType.Point;light.range=9;light.intensity=.35f;light.color=new Color(.2f,.5f,.7f);
            StarshipWorld.Box("Airlock collar",new Vector3(0,3.6f,23),new Vector3(5,.4f,3),white,corridor);
        }
        public void Restore()
        {
            phase=Phase.None;opening=driftTime=0;if(game.controller!=null)game.controller.enabled=true;if(game.view!=null)game.view.fieldOfView=70;
            if(leftDoor!=null){leftDoor.localPosition=leftHome;rightDoor.localPosition=rightHome;}
        }
        public void CheckEntrance()
        {
            if(phase!=Phase.None||exitSign==null||!exitSign.placement.Ready||game.held!=null)return;
            float scale=exitSign.transform.localScale.x;
            // Clearance is geometry-derived: the capsule must fit the enlarged doorway.
            if(.24f*scale<game.controller.radius*2+.12f||.366f*scale<game.controller.height+.12f)return;
            Vector3 p=exitSign.transform.InverseTransformPoint(game.player.feet+Vector3.up*.4f);
            if(Mathf.Abs(p.x)>.12f-game.controller.radius/scale||p.z<-.17f||p.z>.21f)return;
            BeginCorridor();
        }
        public void BeginCorridor()
        {
            if(game.held!=null)game.Cancel(true);
            phase=Phase.Corridor;game.showHints=false;game.hintLevel=0;game.noticeUntil=0;
            game.PlacePlayer(corridor.TransformPoint(new Vector3(0,0,1)));
            game.yaw=Mathf.PI;game.pitch=0;game.exposure=0;game.safe=true;game.SyncView();
        }
        public void Tick(float dt)
        {
            if(phase==Phase.None||phase==Phase.Complete)return;
            if(phase==Phase.Corridor||phase==Phase.Airlock)
            {
                Vector3 local=corridor.InverseTransformPoint(game.player.feet);
                if(local.z>16)phase=Phase.Airlock;
                if(phase==Phase.Airlock)
                {
                    opening=Mathf.MoveTowards(opening,1,dt*.5f);
                    float doorEase=opening*opening*(3-2*opening);
                    leftDoor.localPosition=leftHome+Vector3.left*1.8f*doorEase;rightDoor.localPosition=rightHome+Vector3.right*1.8f*doorEase;
                    if(opening>.98f&&local.z>24)BeginDrift();
                }
                return;
            }
            driftTime+=dt;
            float t=Mathf.Clamp01(driftTime/14),ease=t*t*(3-2*t);
            Vector3 destination=new Vector3(-92,42,-138);
            game.view.transform.position=Vector3.Lerp(driftStart,destination,ease);
            Quaternion look=Quaternion.LookRotation(new Vector3(-7,1,0)-game.view.transform.position);
            float turn=Mathf.SmoothStep(0,1,Mathf.Clamp01((driftTime-1.5f)/5.5f));
            game.view.transform.rotation=Quaternion.Slerp(driftRotation,look,turn);
            game.view.fieldOfView=Mathf.Lerp(70,76,t);
            if(driftTime>=14){phase=Phase.Complete;game.won=true;game.Pause();}
        }
        public void BeginDrift()
        {
            phase=Phase.Drifting;driftTime=0;driftStart=game.view.transform.position;driftRotation=game.view.transform.rotation;
            game.controller.enabled=false;game.noticeUntil=0;game.exposure=0;game.safe=true;
        }
    }
}
