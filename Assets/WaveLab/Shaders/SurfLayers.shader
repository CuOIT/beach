Shader "WaveLab/Surf Layers"
{
    Properties { _Layer("Layer",Float)=0 _Visibility("Visibility",Float)=1 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "SurfCommon.cginc"
            float _Layer, _Visibility;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; float2 p:TEXCOORD0; };
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.p=mul(unity_ObjectToWorld,v.vertex).xy; return o; }
            float4 frag(v2f i):SV_Target
            {
                float2 p=i.p;
                float t=_SurfTime, phase=_SurfPhase, r=runup(phase);
                float d=signedWater(p), water=ss(-.025,.06,d);
                float fine=noise2(p*85.);
                float broad=fbm(p*float2(.7,1.9));
                float sky=ss(5.35,5.5,p.y);
                if(_Layer<.5)
                {
                    float3 sand=lerp(float3(.67,.59,.42),float3(.91,.84,.65),broad*.6+.25);
                    sand+=(fine-.5)*.06;
                    float ridges=pow(saturate(sin(p.y*23.+noise2(p*float2(.8,2))*5.)),13.);
                    sand-=ridges*.017;
                    float wet=ss(-4.5,-3.75,p.y)*(1.-ss(-1.0,.0,p.y));
                    sand=lerp(sand,sand*float3(.75,.83,.83),wet*.56);
                    float3 skyColor=lerp(float3(.55,.75,.76),float3(.27,.48,.59),ss(5.4,8.,p.y));
                    skyColor+=pow(fbm(p*float2(.8,2.2)+float2(t*.007,0)),3.)*.18;
                    return float4(surfColor(lerp(sand,skyColor,sky)),_Visibility);
                }
                if(_Layer<1.5)
                {
                    float depth=ss(-1.8,5.4,p.y);
                    float3 col=lerp(float3(.29,.73,.66),float3(.025,.23,.30),depth);
                    col=lerp(col,float3(.09,.48,.51),exp(-pow((p.y-2.1)/1.8,2.))*.28);
                    float grain=fbm(p*float2(2.5,15.)+float2(t*.08,-t*.17));
                    col+=(grain-.5)*.07;
                    float sheen=pow(saturate(sin(p.y*85.+noise2(p*float2(3.,18.)+t*.05)*8.)),18.);
                    sheen*=ss(.62,.86,noise2(p*float2(4,28)+float2(t*.2,0)));
                    col+=sheen*float3(.43,.69,.66)*ss(1.7,4.,p.y)*.50*_SurfAppearance.z;
                    float caustic=1.-ss(.018,.073,cells(p*float2(3.,4.)+float2(t*.07,t*.04)));
                    col+=caustic*.028*(1.-depth);
                    return float4(surfColor(col),saturate(water*(1.-sky)*lerp(.68,1.,depth)*_Visibility*_SurfAppearance.x));
                }
                if(_Layer<2.5)
                {
                    float depth=ss(-1.8,3.9,p.y);
                    float3 col=lerp(float3(.23,.71,.63),float3(.06,.35,.40),depth);
                    return float4(surfColor(col),saturate(water*(1.-sky)*rockMask(p)*lerp(.05,.25,depth)*_Visibility*_SurfAppearance.y));
                }
                if(_Layer<3.5)
                {
                    // Foam is advected more slowly than the edge. It survives the turn of the tide.
                    float2 q=p*float2(2.8,3.9)*_SurfCrestFeel.w;
                    q.y+=r*1.5-t*.085*_SurfFoamFeel.x;
                    q+=float2(fbm(p*2.4+t*.08),fbm(p*2.1+11.-t*.06))*1.5;
                    q+=float2(noise2(p*9.+t*.13),noise2(p*8.+17.))* .16;
                    float thickness=.025+.115*pow(noise2(q*3.1),2.);
                    float lace=1.-ss(thickness,thickness+.046,cells(q));
                    float islands=ss(.48,.69,fbm(q*2.2+float2(0,t*.08)));
                    float shoreBand=exp(-max(d,0.)*1.25);
                    float floodBand=ss(-1.5,.3,p.y-shore(p.x))*(1.-ss(-.5,2.,p.y));
                    float life=ss(.27,.40,phase)*(1.-ss(.56,_SurfFoamFeel.y,phase));
                    float field=max(shoreBand*.36,life*floodBand);
                    float holes=ss(.22,.54,fbm(p*3.2+float2(0,r*.6-t*.03)));
                    float rim=1.-ss(.045*_SurfCrestFeel.z,(.19+(noise2(p*15.)*.12))*_SurfCrestFeel.z,abs(d));
                    float edgeRagged=ss(.25,.66,fbm(p*float2(16,25)+float2(t*.12,0)));
                    float alpha=(lace*field*.92*holes+islands*field*.48+rim*(.65+edgeRagged*.3));
                    alpha=saturate(alpha*_SurfFoam)*water*(1.-sky)*rockMask(p);
                    float3 foam=lerp(float3(.69,.87,.79),float3(.99,.98,.86),saturate(rim+lace*.8));
                    return float4(surfColor(foam),alpha*_Visibility);
                }
                if(_Layer<4.5)
                {
                    float cy=crest();
                    float growth=ss(0.,.15,phase);
                    float alive=ss(0.,.025,phase)*(1.-ss(.285,.355,phase));
                    float noise=fbm(float2(p.x*5.2,t*.63));
                    float curl=(.065*sin(p.x*3.5+t*.7)+.10*(noise-.5))*_SurfCrestFeel.y;
                    float dist=p.y-cy-curl;
                    float width=lerp(.045,.26,growth)*_SurfCrestFeel.x;
                    float belly=exp(-pow((dist+width*.8)/(width*.67),2.));
                    float lip=1.-ss(width*.35,width*.65+noise*.14,abs(dist));
                    float fleck=ss(.41,.72,fbm(p*float2(24,38)+float2(t*.08,-t*.7)));
                    float spume=exp(-pow((dist-width*.6)/(width*.9),2.))*fleck*.6;
                    float alpha=saturate(belly*.65+lip+spume)*alive*rockMask(p);
                    float aeration=fbm(p*float2(13,19)+float2(0,-t*.9));
                    float3 col=lerp(float3(.045,.28,.29),lerp(float3(.67,.84,.78),float3(1.,.99,.90),aeration*.6+.4),saturate(lip+spume));
                    return float4(surfColor(col),alpha*_Visibility);
                }
                // A thin wet-sand memory remains outside the receding front.
                float recent=ss(-_SurfReach-.25,-_SurfReach+.05,p.y+1.25);
                float drying=(1.-water)*recent*ss(.51,.60,phase)*(1.-ss(.90,1.,phase));
                return float4(surfColor(float3(.22,.42,.36)),drying*.23*_Visibility*_SurfAppearance.w);
            }
            ENDHLSL
        }
    }
}
