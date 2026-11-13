using UnityEngine;

namespace ShadeLink.Beach
{
    public sealed class BeachSkyShip : MonoBehaviour
    {
        public const float FlightDuration=24f;
        const float TakeoffDelay=1.1f,OrbitDuration=10.5f;
        public const float DepartureTime=TakeoffDelay+OrbitDuration;
        public BeachGame Game;
        public bool Flying { get; private set; }
        public float FlightTime { get; private set; }
        public bool Departing=>Flying&&FlightTime>=DepartureTime;
        public float EndingFade=>Flying?Mathf.SmoothStep(0,1,(FlightTime-(FlightDuration-4.5f))/3.5f):0;
        public float EndingTitleAlpha=>Flying?Mathf.SmoothStep(0,1,(FlightTime-(FlightDuration-2.4f))/1.8f):0;
        Vector3 departureCameraPosition;
        Quaternion departureCameraRotation;
        float departureYaw,departurePitch,homeFogDensity,homeFarClip;
        Transform ocean;Vector3 homeOceanScale;
        bool cameraDetached;
        public Quaternion ViewRotation=>transform.rotation*Quaternion.Inverse(homeRotation);
        Vector3 homePosition,rider,homeScale;
        Quaternion homeRotation;
        bool initialized;

        public void Initialize(BeachGame game)
        {
            if(initialized)return;
            Game=game;homePosition=transform.position;homeRotation=transform.rotation;homeScale=transform.localScale;homeFogDensity=RenderSettings.fogDensity;homeFarClip=game.view.farClipPlane;
            ocean=game.transform.Find("수평선");if(ocean!=null)homeOceanScale=ocean.localScale;
            initialized=true;
        }
        public bool IsAboard
        {
            get
            {
                if(Game==null||Game.controller==null||!Game.controller.enabled||!Game.player.grounded)return false;
                return Physics.Raycast(Game.player.feet+Vector3.up*.18f,Vector3.down,out var hit,.5f,BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore)
                    &&hit.normal.y>.6f&&hit.collider.GetComponentInParent<BeachSkyShip>()==this;
            }
        }
        public string Status=>Flying?FlightTime<TakeoffDelay?"계류 해제 · 출항 준비 중":Departing?"하늘로 떠나는 마지막 항해":"하늘로 항해 중 · 마우스로 주변을 둘러보세요":
            IsAboard?"승선 완료 · E를 눌러 하늘로 출항":"모래성 오른쪽 길 → 뒤편 선착장 → 갑판에서 E 출항";
        public bool TryLaunch()
        {
            Initialize(Game);
            if(Flying||Game==null||!Game.active||Game.won||Game.held!=null||!IsAboard)return false;
            rider=transform.InverseTransformPoint(Game.controller.transform.position);
            Flying=true;FlightTime=0;Game.verticalVelocity=0;Game.exposure=0;
            // During takeoff the rider follows the deck exactly; the walking capsule
            // must not collide with the same rotating ship carrying it.
            Game.controller.enabled=false;
            Game.Tell("계류를 풀었습니다. 왼쪽 난간 너머로 해변을 바라보세요.",5);
            return true;
        }
        public void Tick(float dt)
        {
            if(!Flying||Game==null||!Game.active||Game.won)return;
            FlightTime=Mathf.Min(FlightDuration,FlightTime+Mathf.Max(0,dt));
            Vector3 position;Quaternion rotation;
            if(FlightTime<TakeoffDelay)
            {
                float t=FlightTime/TakeoffDelay,envelope=Mathf.Pow(Mathf.Sin(t*Mathf.PI),2);
                position=homePosition+homeRotation*new Vector3(0,-.10f*envelope,0);
                rotation=homeRotation*Quaternion.Euler(-1.8f*envelope,0,1.4f*Mathf.Sin(t*Mathf.PI*3)*envelope);
            }
            else
            {
                position=FlightPosition(FlightTime);
                var tangent=FlightPosition(FlightTime+.01f)-FlightPosition(Mathf.Max(TakeoffDelay,FlightTime-.01f));
                float departure=Mathf.SmoothStep(0,1,(FlightTime-TakeoffDelay)/1.2f);
                float pitch=-Mathf.Min(28,Mathf.Atan2(tangent.y,new Vector2(tangent.x,tangent.z).magnitude)*Mathf.Rad2Deg)*departure;
                float yaw=Mathf.Atan2(tangent.x,tangent.z)*Mathf.Rad2Deg;
                float bank=8*departure*Mathf.SmoothStep(0,1,(DepartureTime-FlightTime)/1.8f);
                rotation=Quaternion.Euler(pitch,yaw,bank);
            }
            transform.localScale=homeScale*Mathf.Lerp(1,1.8f,Mathf.SmoothStep(0,1,(FlightTime-DepartureTime-1)/3));
            transform.SetPositionAndRotation(position,rotation);
            Game.controller.transform.position=transform.TransformPoint(rider);
            Game.player.feet=Game.controller.transform.position;Game.player.grounded=true;Game.safe=true;
            Game.SyncView();
            if(FlightTime>=FlightDuration)
            {
                Game.won=true;Game.Pause();Debug.Log("BEACH_SKY_ENDING_COMPLETE height="+transform.position.y);
            }
        }
        Vector3 FlightPosition(float seconds)
        {
            if(seconds<=DepartureTime)return OrbitPosition(Mathf.Clamp01((seconds-TakeoffDelay)/OrbitDuration));
            var exit=OrbitPosition(1);
            var velocity=(exit-OrbitPosition(.999f))/(OrbitDuration*.001f);
            float t=seconds-DepartureTime;
            return exit+velocity*t+Vector3.up*(.45f*t*t);
        }
        Vector3 OrbitPosition(float t)
        {
            // Start at rest and meet the departure tangent at its original speed.
            t=t*t*(2-t);
            const float ramp=.2f;
            float progress;
            // Ease into the half-turn; preserve its tangent speed for the straight departure.
            if(t<ramp)
            {
                float u=t/ramp;progress=ramp*u*u*u*(1-u*.5f);
            }
            else progress=t-ramp*.5f;
            var center=new Vector3(0,homePosition.y,52);var radial=homePosition-center;
            float angle=Mathf.Atan2(radial.x,radial.z)-Mathf.PI*progress/(1-ramp*.5f);
            float radius=radial.magnitude+12*(1-Mathf.Exp(-4*t));
            return center+new Vector3(Mathf.Sin(angle)*radius,34*Mathf.Pow(t,1.3f),Mathf.Cos(angle)*radius);
        }
        public void ApplyDepartureView(Camera view)
        {
            if(!Departing)return;
            if(!cameraDetached)
            {
                departureCameraPosition=view.transform.position;departureCameraRotation=view.transform.rotation;
                departureYaw=Game.yaw;departurePitch=Game.pitch;cameraDetached=true;
                // The elevated wide shot needs sea beyond the original near-ground horizon.
                if(ocean!=null)ocean.localScale=new Vector3(100000,homeOceanScale.y,100000);
            }
            float blend=Mathf.SmoothStep(0,1,(FlightTime-DepartureTime)/4);
            RenderSettings.fogDensity=Mathf.Lerp(homeFogDensity,.00075f,blend);view.farClipPlane=Mathf.Lerp(homeFarClip,60000,blend);
            var position=Vector3.Lerp(departureCameraPosition,new Vector3(-35,26,145),blend);
            var focus=Vector3.Lerp(new Vector3(0,10,52),transform.position+Vector3.up*5,.3f);
            var look=Quaternion.LookRotation(focus-position,Vector3.up);
            var input=Quaternion.Euler(-(Game.pitch-departurePitch)*Mathf.Rad2Deg,(Game.yaw-departureYaw)*Mathf.Rad2Deg,0);
            view.transform.SetPositionAndRotation(position,Quaternion.Slerp(departureCameraRotation,look*input,blend));
        }
        void OnDisable()
        {
            if(cameraDetached)RestoreViewEnvironment();
        }
        void RestoreViewEnvironment()
        {
            RenderSettings.fogDensity=homeFogDensity;
            if(Game!=null&&Game.view!=null)Game.view.farClipPlane=homeFarClip;
            if(ocean!=null)ocean.localScale=homeOceanScale;
        }
        public void ResetFlight()
        {
            if(!initialized)return;
            transform.localScale=homeScale;transform.SetPositionAndRotation(homePosition,homeRotation);
            Flying=false;FlightTime=0;cameraDetached=false;RestoreViewEnvironment();
            if(Game!=null&&Game.controller!=null)Game.controller.enabled=true;
        }
    }
}
