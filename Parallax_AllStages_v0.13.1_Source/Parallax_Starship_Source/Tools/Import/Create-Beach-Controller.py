"""Initialise a separate final-stage controller from the verified Gallery controller."""
from pathlib import Path
root=Path(__file__).resolve().parents[2]/'Assets/Parallax'
out=root/'Beach/Scripts';out.mkdir(parents=True,exist_ok=True)
if (out/'BeachGame.cs').exists():
    raise SystemExit('One-time bootstrap only: BeachGame.cs already exists. Edit the maintained controller directly; do not overwrite it with this generator.')
s=(root/'Gallery/Scripts/GalleryGame.cs').read_text(encoding='utf-8').replace('Gallery','Beach')
s=s.replace('public BeachLightGate lightGate;','public BeachLightGate lightGate, waterGate;\n        public bool castleRest;')
a=s.index('            bool mirrorTest=');b=s.index('            Application.runInBackground=',a)
s=s[:a]+'            automated=Array.IndexOf(arguments,"-beach-test")>=0;\n'+s[b:]
a=s.index('            if(mirrorTest)');b=s.index('\n        }',a)
s=s[:a]+'            if(automated)gameObject.AddComponent<BeachValidation>().game=this;'+s[b:]
s=s.replace('if(mirror!=null)mirror.Restore();if(lightGate!=null)lightGate.Restore();','if(lightGate!=null)lightGate.Restore();if(waterGate!=null)waterGate.Restore();castleRest=false;')
s=s.replace('if(checkpoint==1){if(mirror!=null)mirror.Restore();if(lightGate!=null)lightGate.Restore();}','if(checkpoint==0&&lightGate!=null)lightGate.Restore();if(checkpoint==2&&waterGate!=null)waterGate.Restore();if(checkpoint==3)castleRest=false;')
s=s.replace('ResetAll();checkpoint=1;PlacePlayer(BeachWorld.Checkpoints[1]);Resume();','ResetAll();Resume();')
s=s.replace('거울 전시실 체험 · 거울 클릭 후 Q/E로 회전 · H 힌트','작은 거울을 잡고 멀리 배치해 키우세요 · Q/E 회전 · H 힌트')
s=s.replace('void PlacePlayer(Vector3 point)','public void PlacePlayer(Vector3 point)')
s=s.replace('var spawn=BeachWorld.Checkpoints[checkpoint];','var spawn=castleRest&&checkpoint==3?BeachWorld.CastleRest:BeachWorld.Checkpoints[checkpoint];')
s=s.replace('햇빛에 노출되어 현재 구역의 쉼터로 돌아왔습니다.','빛·물 위험에서 현재 쉼터로 복귀했습니다. 놓은 사물 배치는 유지됩니다.')
s=s.replace('aimedMirror=aimHit.collider.GetComponentInParent<BeachMirror>();','aimedMirror=null;')
s=s.replace('if(mirror!=null)mirror.Highlight(adjustingMirror||aimedMirror==mirror);','')
s=s.replace('float zoneNear=new[]{.18f,14.3f,27.3f,40.3f}[held.zone],zoneFar=new[]{13.7f,26.7f,39.75f,53.8f}[held.zone];','float zoneNear=BeachWorld.ZoneNear[held.zone],zoneFar=BeachWorld.ZoneFar[held.zone];')
s=s.replace('float lo=new[]{.18f,14.3f,27.3f,40.3f}[prop.zone],hi=new[]{13.7f,26.7f,39.75f,53.8f}[prop.zone];','float lo=BeachWorld.ZoneNear[prop.zone],hi=BeachWorld.ZoneFar[prop.zone];')
s=s.replace('ClipDepth(eye.y,velocity.y,.006f,9.74f','ClipDepth(eye.y,velocity.y,-.65f,15.74f')
s=s.replace('b.min.y<-.012f||b.max.y>9.75f','b.min.y<-.68f||b.max.y>15.75f')
s=s.replace('벽·천장 또는 현재 전시 구역을 벗어납니다.','해변 경계 또는 현재 구역을 벗어납니다.')
s=s.replace('전시물','사물').replace('전시대','발판').replace('전시 구역','구역')
s=s.replace('~0,QueryTriggerInteraction.Ignore','BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore')
s=s.replace('out var support,.3f)','out var support,.3f,BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore)')
s=s.replace('out var below,.14f)','out var below,.14f,BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore)')
s=s.replace('if(lightGate!=null){lighting.Synchronize();lightGate.Tick(dt,lighting,controller);lighting.Synchronize();}','lighting.Synchronize();if(lightGate!=null)lightGate.Tick(dt,lighting,controller);if(waterGate!=null)waterGate.Tick(dt,lighting,controller);')
a=s.index('            if(player.feet.z>36');b=s.index('            SyncView();',a)
s=s[:a]+'''            int next=player.feet.z>73&&waterGate.unlocked?3:player.feet.z>48?2:player.feet.z>24&&lightGate.unlocked?1:0;
            if(next>checkpoint&&safe&&player.grounded){checkpoint=next;Tell("0"+(checkpoint+1)+" 구역 저장 · R 구역 재시도",6);}
            if(checkpoint==3&&!castleRest&&Vector3.Distance(player.feet,BeachWorld.CastleRest)<1.6f&&safe&&player.grounded)
            {castleRest=true;Tell("모래성 중간 쉼터 저장 · 실패해도 여기서 다시 시작합니다.",6);}
            if(checkpoint==3&&lightGate.unlocked&&waterGate.unlocked&&Vector3.Distance(player.feet,BeachWorld.Exit)<1.35f&&player.grounded&&safe)
            {won=true;Pause();}
'''+s[b:]
s=s.replace('safe=FootSafe(player.feet);','safe=FootSafe(player.feet)&&!BeachWater.InHazard(player.feet);')
s=s.replace('safe?-1.7f:2.2f','safe?-1.2f:.72f')
s=s.replace('if(exposure>=1||player.feet.y< -3)Respawn();','if(exposure>=1||player.feet.y< -3||BeachWater.InHazard(player.feet))Respawn();')
s=s.replace('CollisionFlags flags=controller.Move(new Vector3(move.x*Speed,verticalVelocity,move.y*Speed)*dt);','''Vector3 supportMotion=Vector3.zero;
            if(grounded&&Physics.Raycast(controller.transform.position+Vector3.up*.18f,Vector3.down,out var platformHit,.3f,BeachWorld.PhysicalMask,QueryTriggerInteraction.Ignore))
            {var platform=platformHit.collider.GetComponentInParent<BeachProp>();if(platform!=null&&platform.floating&&!platform.body.isKinematic)supportMotion=Vector3.ProjectOnPlane(platform.body.linearVelocity,Vector3.up);}
            CollisionFlags flags=controller.Move((new Vector3(move.x*Speed,verticalVelocity,move.y*Speed)+supportMotion)*dt);''')
(out/'BeachGame.cs').write_text(s,encoding='utf-8')
s=(root/'Gallery/Scripts/GalleryProp.cs').read_text(encoding='utf-8').replace('Gallery','Beach')
s=s.replace('public bool platform;','public bool platform, buoyant, isBall, upright, floating;\n        public float driftDistance;\n        public Vector3 lastDriftPosition;\n        public SphereCollider sphere;')
s=s.replace('InitializePhysics();\n            StopMotion();','InitializePhysics();\n            floating=false;driftDistance=0;\n            StopMotion();')
s=s.replace('foreach (var p in parts) p.enabled = true;','foreach (var p in parts) p.enabled = true;\n            if(sphere!=null)sphere.enabled=false;')
s=s.replace('body.constraints = RigidbodyConstraints.None;\n            body.isKinematic = false;','body.constraints = upright?RigidbodyConstraints.FreezeRotation: RigidbodyConstraints.None;\n            if(isBall&&sphere!=null){foreach(var part in parts)part.enabled=false;sphere.enabled=true;}\n            body.isKinematic = false;')
s=s.replace('properties.SetColor("_Selection",','properties.SetColor("_EmissionColor",')
s=s.replace('new Color(.03f, .35f, .28f)','new Color(.025f, .16f, .24f)')
(out/'BeachProp.cs').write_text(s,encoding='utf-8')
s=(root/'Gallery/Scripts/GalleryLabel.cs').read_text(encoding='utf-8').replace('Gallery','Beach')
(out/'BeachLabel.cs').write_text(s,encoding='utf-8')
print('Separate BeachGame, BeachProp, BeachLabel generated from v0.10.0 controller')
