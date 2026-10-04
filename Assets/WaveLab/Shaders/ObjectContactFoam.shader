Shader "WaveLab/Object Contact Foam"
{
    Properties
    {
        _SilhouetteAtlas("Baked sprite silhouette SDF",2D)="white"{}
        _SilhouetteUV("Atlas rect",Vector)=(0,0,1,1)
        _SdfRange("SDF distance range in world units",Float)=.1
        _FoamWidth("Contact width in world units",Float)=.01
        _ContactStrength("Contact strength",Float)=0
        _ContactSeed("Phase offset",Float)=0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "SurfCommon.cginc"
            #include "ObjectWater.cginc"
            sampler2D _SilhouetteAtlas;
            float4 _SilhouetteUV;
            float _SdfRange,_FoamWidth,_ContactStrength,_ContactSeed;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float2 p:TEXCOORD1; };
            v2f vert(appdata v)
            {
                v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;
                o.p=mul(unity_ObjectToWorld,v.vertex).xy;return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float2 atlasUV=_SilhouetteUV.xy+i.uv*_SilhouetteUV.zw;
                float silhouette=(tex2D(_SilhouetteAtlas,atlasUV).r-.5)*2.*_SdfRange;
                float water=objectWaterDistance(i.p);
                // Actual silhouette intersected with the region above water. Concave
                // shapes and separated arms retain their contours; no ellipse is used.
                float exposedDistance=max(silhouette,-water);
                float grain=noise2(i.uv*37.+float2(_SurfTime*.22*_SurfFoamFeel.z,_ContactSeed));
                float width=_FoamWidth*lerp(.60,1.25,grain);
                float aa=max(fwidth(exposedDistance)*.65,_FoamWidth*.15);
                float rim=ss(-aa,aa,exposedDistance+width*.20)
                    *(1-ss(width-aa,width+aa,exposedDistance));
                float contactZone=1-ss(_FoamWidth*2.,_FoamWidth*8.,abs(water));
                float breakup=lerp(1.,lerp(.30,1.,ss(.20,.68,grain)),_SurfFoamFeel.w);
                float alpha=rim*contactZone*breakup*_ContactStrength;
                return float4(surfColor(lerp(float3(.75,.89,.82),float3(.99,.98,.90),grain)),alpha);
            }
            ENDHLSL
        }
    }
}
