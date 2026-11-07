Shader "Parallax/TutorialDoorPortal"
{
    Properties { _MainTex ("Corridor", 2D) = "black" {} }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Back ZWrite On
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float4 screen : TEXCOORD0; };
            v2f vert(appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.screen = ComputeScreenPos(o.pos); return o; }
            fixed4 frag(v2f i) : SV_Target { return tex2Dproj(_MainTex, UNITY_PROJ_COORD(i.screen)); }
            ENDCG
        }
    }
}
