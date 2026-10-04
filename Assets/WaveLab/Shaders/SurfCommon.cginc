#ifndef WAVELAB_SURF_COMMON
#define WAVELAB_SURF_COMMON
float _SurfTime, _SurfPhase, _SurfReach, _SurfFoam, _SurfWater;
float4 _SurfCrestFeel,_SurfFoamFeel,_SurfAppearance,_ObjectFeel;
float4 _SurfRocks[8];
float _SurfRockExposure[8];
float3 surfColor(float3 c)
{
    #if defined(UNITY_COLORSPACE_GAMMA)
    return c;
    #else
    return GammaToLinearSpace(c);
    #endif
}
float ss(float a, float b, float x) { float v=saturate((x-a)/(b-a)); return v*v*(3.-2.*v); }
float hash21(float2 p) { p=frac(p*float2(123.34,456.21)); p+=dot(p,p+45.32); return frac(p.x*p.y); }
float noise2(float2 p)
{
    float2 i=floor(p), f=frac(p); f=f*f*(3.-2.*f);
    return lerp(lerp(hash21(i),hash21(i+float2(1,0)),f.x),lerp(hash21(i+float2(0,1)),hash21(i+1),f.x),f.y);
}
float fbm(float2 p) { return noise2(p)*.58+noise2(p*2.03+7.1)*.27+noise2(p*4.11+2.3)*.15; }
float runup(float phase) { return ss(.26,.47,phase)*(1.-ss(.54,.96,phase)); }
float shore(float x) { return -1.25+.23*sin(x*.85+.4)+.12*sin(x*2.3)+x*.045-_SurfReach*runup(_SurfPhase); }
float crest() { return lerp(5.15,-1.72,saturate(_SurfPhase/.31)); }
float signedWater(float2 p)
{
    float rip=.035*sin(p.x*13.+_SurfTime*2.1)+.025*sin(p.x*24.-_SurfTime*3.2);
    return p.y-shore(p.x)+rip;
}
float rockMask(float2 p)
{
    float mask=1.;
    [unroll] for(int k=0;k<8;k++)
    {
        float4 r=_SurfRocks[k];
        float d=length((p-r.xy)/max(r.zw,float2(.001,.001)));
        mask*=lerp(1.,ss(.70,1.04,d),_SurfRockExposure[k]);
    }
    return mask;
}
float cells(float2 p)
{
    float2 g=floor(p), f=frac(p);
    float a=9.,b=9.;
    [unroll] for(int j=-1;j<=1;j++) [unroll] for(int i=-1;i<=1;i++)
    {
        float2 cell=float2(i,j), id=g+cell;
        float2 q=cell+float2(hash21(id),hash21(id+19.2))-f;
        float d=dot(q,q);
        if(d<a){b=a;a=d;} else {b=min(b,d);}
    }
    return sqrt(b)-sqrt(a);
}
#endif
