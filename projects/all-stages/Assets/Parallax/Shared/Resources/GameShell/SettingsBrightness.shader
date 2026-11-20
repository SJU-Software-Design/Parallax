Shader "Hidden/Parallax/SettingsBrightness"
{
    Properties { _MainTex ("Source", 2D) = "white" {} _Brightness ("Brightness", Float) = 1 }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Brightness;
            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed4 pixel = tex2D(_MainTex, i.uv);
                pixel.rgb *= _Brightness;
                return pixel;
            }
            ENDCG
        }
    }
    Fallback Off
}
