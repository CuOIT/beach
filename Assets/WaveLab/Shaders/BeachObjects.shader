Shader "WaveLab/Beach Objects"
{
    Properties
    {
        _Submerge("Submerge",Float)=1 _Fade("Fade",Float)=1
        _WaterPass("0 submerged / 1 exposed",Float)=0
        _StateEnabled("Object state enabled",Float)=0
        _ObjectImmersion("Submerged fraction",Float)=0
        _ObjectSurfaceY("World waterline Y",Float)=0
        _ObjectExcessDepth("Water above object",Float)=0
        _ObjectFeather("Waterline feather",Float)=.02
        _ObjectShape("Center X, half width, height, bottom",Vector)=(0,1,1,0)
        _ObjectSeed("Meniscus variation",Float)=0
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
            #include "UnityCG.cginc"
            #include "SurfCommon.cginc"
            #include "ObjectWater.cginc"
            float _Submerge, _Fade;
            float _WaterPass,_StateEnabled,_ObjectExcessDepth;
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float4 color:COLOR; float2 p:TEXCOORD0; };
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.color=v.color; o.p=mul(unity_ObjectToWorld,v.vertex).xy; return o; }
            float4 frag(v2f i):SV_Target
            {
                if(_StateEnabled>.5)
                {
                    float revealed=objectReveal(i.p);
                    // Keep the whole base underneath. Complementary alpha cuts leave a
                    // transparent horizontal stripe where the two passes meet.
                    float mask=_WaterPass>.5?revealed:1.;
                    clip(mask-.001);
                    float3 col=i.color.rgb;
                    float opacity=1;
                    if(_WaterPass<.5)
                    {
                        float depth=ss(-1.8,4.,i.p.y);
                        float3 tint=lerp(float3(.25,.65,.58),float3(.06,.34,.39),depth);
                        float attenuation=ss(0,.8,_ObjectExcessDepth);
                        float luminance=dot(col,float3(.299,.587,.114));
                        col=lerp(col,luminance.xxx,.2);
                        col=lerp(col,tint,saturate(lerp(.20,.70,attenuation)*_SurfWater*_ObjectFeel.z));
                        opacity=saturate(lerp(.92,.38,attenuation)*_ObjectFeel.w);
                    }
                    return float4(surfColor(col),i.color.a*mask*opacity*_Fade);
                }
                float wet=ss(-.03,.18,signedWater(i.p))*_Submerge*_SurfWater;
                float depth=ss(-1.8,4.0,i.p.y);
                float3 tint=lerp(float3(.28,.67,.60),float3(.08,.38,.43),depth);
                float3 color=lerp(i.color.rgb,tint,wet*lerp(.10,.71,depth));
                return float4(surfColor(color),i.color.a*_Fade);
            }
            ENDHLSL
        }
    }
}
