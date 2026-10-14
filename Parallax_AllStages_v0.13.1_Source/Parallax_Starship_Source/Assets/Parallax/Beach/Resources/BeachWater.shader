Shader "Parallax/BeachWater" {
 Properties { _Flow("Flow direction", Vector)=(0,1,.3,0) }
 SubShader { Tags { "RenderType"="Opaque" } LOD 100
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct input {float4 vertex:POSITION;};struct output {float4 pos:SV_POSITION;float3 world:TEXCOORD0;};
 float4 _Flow;
 output vert(input v){output o;o.pos=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;return o;}
 fixed4 frag(output i):SV_Target {
 float2 d=normalize(_Flow.xy+float2(.001,0));float t=_Time.y*_Flow.z;
 float stripe=sin(dot(i.world.xz,d)*5-t*4+sin(i.world.x*.9)*.65);
 float cross=sin(i.world.x*2.4+i.world.z*1.9+t)*.5+.5;
 float foam=pow(saturate(stripe),18)*.5;
 float3 c=lerp(float3(.03,.35,.49),float3(.12,.62,.73),cross*.4+.35)+foam*float3(.6,.8,.85);
 return float4(c,1); }
 ENDCG }
 }
}
