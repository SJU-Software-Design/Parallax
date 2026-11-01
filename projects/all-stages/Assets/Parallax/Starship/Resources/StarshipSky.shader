Shader "Parallax/StarshipSky"
{
    SubShader
    {
        Tags {"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"}
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct v2f{float4 pos:SV_POSITION;float3 dir:TEXCOORD0;};
            v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.dir=v.vertex.xyz;return o;}
            float hash(float3 p){return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453);}
            fixed4 frag(v2f i):SV_Target
            {
                float3 d=normalize(i.dir),p=d*240;
                float h=hash(floor(p)),s=step(.989,h)*pow(saturate(1-length(frac(p)-.5)*2),7)*4;
                float sun=dot(d,normalize(float3(.24,1,-.42)));
                float disc=smoothstep(.99975,.9999,sun);
                float glow=pow(saturate(sun),500)*.5;
                float neb=pow(saturate(1-abs(d.y*.75+d.x*.25-.12)),14)*.012;
                return fixed4(float3(.004,.007,.018)+float3(.08,.15,.35)*neb+s*float3(.75,.85,1)+float3(1,.82,.53)*(disc+glow),1);
            }
            ENDCG
        }
    }
}
