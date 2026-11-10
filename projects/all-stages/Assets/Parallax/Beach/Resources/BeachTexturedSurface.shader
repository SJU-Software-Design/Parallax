Shader "Parallax/BeachTexturedSurface"
{
    Properties {
        _Color("Tint",Color)=(1,1,1,1)
        _EmissionColor("Emission",Color)=(0,0,0,1)
        _MainTex("Albedo",2D)="white"{}
        _BumpMap("Normal",2D)="bump"{}
        _BumpScale("Normal strength",Range(0,2))=1
        _OcclusionMap("Occlusion",2D)="white"{}
        _MetallicGlossMap("Metallic (R), Smoothness (A)",2D)="white"{}
        _Metallic("Metallic scale",Range(0,1))=0
        _Glossiness("Smoothness scale",Range(0,1))=.2
        _Tiling("Repeats per metre",Float)=.5
    }
    SubShader {
        Tags {"RenderType"="Opaque"}
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert finalcolor:Preview
        #pragma target 3.0
        #include "UnityStandardUtils.cginc"
        sampler2D _MainTex,_BumpMap,_OcclusionMap,_MetallicGlossMap;
        fixed4 _Color,_EmissionColor;half _BumpScale,_Metallic,_Glossiness;float _Tiling;
        struct Input {float3 projectedPos;float3 projectedSize;float3 objectNormal;float3 worldPos;float3 worldNormal;INTERNAL_DATA};
        void vert(inout appdata_full v,out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input,o);
            float3 scale=float3(length(unity_ObjectToWorld._m00_m10_m20),length(unity_ObjectToWorld._m01_m11_m21),length(unity_ObjectToWorld._m02_m12_m22));
            o.projectedPos=v.vertex.xyz*scale;o.projectedSize=scale;o.objectNormal=v.normal;
            // Imported beach meshes have no UV tangents; give the normal map a stable basis.
            if(dot(v.tangent.xyz,v.tangent.xyz)<.01)
                v.tangent=float4(normalize(cross(abs(v.normal.y)>.9?float3(0,0,1):float3(0,1,0),v.normal)),1);
        }
        void surf(Input i,inout SurfaceOutputStandard o)
        {
            float3 weight=pow(abs(normalize(i.objectNormal)),4);weight/=max(weight.x+weight.y+weight.z,.0001);
            float3 p=i.projectedPos*_Tiling;
            float3 ux=i.projectedSize.y>i.projectedSize.z?float3(0,1,0):float3(0,0,1),vx=float3(0,1,1)-ux;
            float3 uy=i.projectedSize.x>i.projectedSize.z?float3(1,0,0):float3(0,0,1),vy=float3(1,0,1)-uy;
            float3 uz=i.projectedSize.x>i.projectedSize.y?float3(1,0,0):float3(0,1,0),vz=float3(1,1,0)-uz;
            float2 x=float2(dot(p,ux),dot(p,vx)),y=float2(dot(p,uy),dot(p,vy)),z=float2(dot(p,uz),dot(p,vz));
            fixed4 albedo=tex2D(_MainTex,x)*weight.x+tex2D(_MainTex,y)*weight.y+tex2D(_MainTex,z)*weight.z;
            half3 nx=UnpackScaleNormal(tex2D(_BumpMap,x),_BumpScale),ny=UnpackScaleNormal(tex2D(_BumpMap,y),_BumpScale),nz=UnpackScaleNormal(tex2D(_BumpMap,z),_BumpScale);
            half3 bump=(ux*nx.x+vx*nx.y)*weight.x+(uy*ny.x+vy*ny.y)*weight.y+(uz*nz.x+vz*nz.y)*weight.z;
            half3 normal=normalize(WorldNormalVector(i,half3(0,0,1))+
                normalize(unity_ObjectToWorld._m00_m10_m20)*bump.x+
                normalize(unity_ObjectToWorld._m01_m11_m21)*bump.y+
                normalize(unity_ObjectToWorld._m02_m12_m22)*bump.z);
            o.Normal=half3(dot(normal,WorldNormalVector(i,half3(1,0,0))),dot(normal,WorldNormalVector(i,half3(0,1,0))),dot(normal,WorldNormalVector(i,half3(0,0,1))));
            half4 metal=tex2D(_MetallicGlossMap,x)*weight.x+tex2D(_MetallicGlossMap,y)*weight.y+tex2D(_MetallicGlossMap,z)*weight.z;
            o.Albedo=albedo.rgb*_Color.rgb;o.Alpha=1;o.Metallic=metal.r*_Metallic;o.Smoothness=metal.a*_Glossiness;o.Emission=_EmissionColor.rgb;
            o.Occlusion=tex2D(_OcclusionMap,x).g*weight.x+tex2D(_OcclusionMap,y).g*weight.y+tex2D(_OcclusionMap,z).g*weight.z;
        }
        #include "BeachPlacementPreview.cginc"
        void Preview(Input i,SurfaceOutputStandard o,inout fixed4 color){BeachPreviewColor(i.worldPos,color);}
        ENDCG
    }
    Fallback "Standard"
}
