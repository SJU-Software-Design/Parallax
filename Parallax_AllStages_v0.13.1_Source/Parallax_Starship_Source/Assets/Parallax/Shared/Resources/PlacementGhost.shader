Shader "Parallax/PlacementGhost" {
 Properties {_MainTex("Texture",2D)="white"{} _Color("Color",Color)=(1,1,1,1) _Selection("Selection",Color)=(0,0,0,0) _EmissionColor("Selection standard",Color)=(0,0,0,0)}
 SubShader {Tags {"Queue"="Transparent" "RenderType"="Transparent"} Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
 Pass {CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 sampler2D _MainTex;float4 _MainTex_ST,_Color,_Selection,_EmissionColor;
 struct v2f{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;};
 v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=TRANSFORM_TEX(v.texcoord,_MainTex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;return o;}
 fixed4 frag(v2f i):SV_Target{float hatch=step(.68,frac((i.world.x+i.world.y+i.world.z)*3));float3 base=tex2D(_MainTex,i.uv).rgb*_Color.rgb;return float4(lerp(base,float3(.08,.69,1),.70)+_Selection.rgb*.2+_EmissionColor.rgb*.1,.29+hatch*.24);}
 ENDCG}
 }
}
