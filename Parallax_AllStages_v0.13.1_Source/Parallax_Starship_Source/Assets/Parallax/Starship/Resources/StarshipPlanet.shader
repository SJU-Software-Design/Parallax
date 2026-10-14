Shader "Parallax/StarshipPlanet"
{
    Properties {_Color("Ocean",Color)=(.015,.16,.30,1)}
    SubShader
    {
        CGPROGRAM
        #pragma surface surf Standard
        #pragma target 3.0
        fixed4 _Color;
        struct Input{float3 worldNormal;};
        float hash(float3 p){return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453);}
        float noise(float3 p)
        {
            float3 a=floor(p),f=frac(p);f=f*f*(3-2*f);
            return lerp(lerp(lerp(hash(a),hash(a+float3(1,0,0)),f.x),lerp(hash(a+float3(0,1,0)),hash(a+float3(1,1,0)),f.x),f.y),
                lerp(lerp(hash(a+float3(0,0,1)),hash(a+float3(1,0,1)),f.x),lerp(hash(a+float3(0,1,1)),hash(a+float3(1,1,1)),f.x),f.y),f.z);
        }
        float fbm(float3 p)
        {
            float v=0,weight=.5;
            for(int j=0;j<4;j++){v+=noise(p)*weight;p=p*2.03+float3(11.7,3.1,9.2);weight*=.5;}
            return v;
        }
        void surf(Input i,inout SurfaceOutputStandard o)
        {
            float3 n=normalize(i.worldNormal);
            float terrain=fbm(n*3.8+7.1),land=smoothstep(.46,.50,terrain);
            float3 surface=lerp(_Color.rgb,lerp(float3(.08,.19,.13),float3(.30,.34,.22),smoothstep(.53,.65,terrain)),land);
            float cloud=smoothstep(.51,.64,fbm(n*12.5+21.3))*.85;
            surface=lerp(surface,float3(.82,.89,.94),cloud);
            surface=lerp(surface,float3(.76,.86,.94),smoothstep(.91,.99,abs(n.y)));
            o.Albedo=surface;o.Smoothness=.12;o.Emission=_Color.rgb*.055;o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
