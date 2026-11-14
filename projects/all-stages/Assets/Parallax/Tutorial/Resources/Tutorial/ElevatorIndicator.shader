Shader "Parallax/Elevator Indicator"
{
    Properties { _Color ("Colour", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            struct VOut { float4 position : SV_POSITION; };
            VOut vert(float4 vertex : POSITION) { VOut o; o.position = UnityObjectToClipPos(vertex); return o; }
            fixed4 frag(VOut i) : SV_Target { return _Color; }
            ENDCG
        }
    }
}
