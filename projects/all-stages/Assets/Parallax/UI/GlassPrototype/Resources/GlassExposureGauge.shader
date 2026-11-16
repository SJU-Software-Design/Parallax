Shader "Hidden/GlassTrial/ExposureGauge"
{
 Properties { _MainTex("Texture", 2D) = "white" {} _Fill("Fill", Range(0, 1)) = 0 _PuzzleMode("Puzzle mode", Float) = 0 _Color("Color", Color) = (1,1,1,1) }
 SubShader
 {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
  Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode] Blend SrcAlpha OneMinusSrcAlpha
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
   struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
   float _Fill, _PuzzleMode; fixed4 _Color;
   v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color*_Color; return o; }
   fixed4 frag(v2f i) : SV_Target
   {
    float2 p = float2(i.uv.x*16.0, i.uv.y);
    float2 q = abs(p-float2(8.0,0.5))-float2(7.5,0.0);
    float d = length(max(q,0.0)) + min(max(q.x,q.y),0.0)-0.5;
    float track = 1.0-smoothstep(-0.05,0.05,d);
    float endX = saturate(_Fill)*16.0;
    float2 fq = abs(p-float2(endX*0.5,0.5))-float2(max(endX*0.5-0.42,0.0),0.0);
    float fd = length(max(fq,0.0))+min(max(fq.x,fq.y),0.0)-0.42;
    float fill = (1.0-smoothstep(-0.05,0.05,fd))*step(0.001,_Fill)*track;
    float wave = 0.5+0.5*sin(i.uv.x*7.0-_Time.y*1.0472);
    float wave2 = 0.5+0.5*sin(i.uv.x*11.0+_Time.y*0.7+i.uv.y*2.0);
    float3 red=float3(0.95,0.06,0.08), scarlet=float3(1.0,0.15,0.12), vermilion=float3(1.0,0.29,0.13);
    float3 redColor=lerp(lerp(red,scarlet,wave),vermilion,wave2*0.18);
    float3 mint=float3(.32,.95,.79), turquoise=float3(.18,.78,.68), paleMint=float3(.58,1.0,.86);
    float3 mintColor=lerp(lerp(mint,turquoise,wave),paleMint,wave2*.22);
    float3 color=lerp(redColor,mintColor,step(.5,_PuzzleMode));
    color += 0.08*smoothstep(0.70,0.97,i.uv.y);
    float alpha=lerp(0.18,0.94,fill)*track*i.color.a;
    return fixed4(lerp(float3(1,1,1),color,fill)*i.color.rgb,alpha);
   }
   ENDCG
  }
 }
}
