#ifndef WAVELAB_OBJECT_WATER
#define WAVELAB_OBJECT_WATER
float _ObjectImmersion, _ObjectSurfaceY, _ObjectFeather, _ObjectSeed;
float4 _ObjectShape;
// The exposed art and its foam share the same intersection profile.
float objectWaterDistance(float2 p)
{
    float nx=(p.x-_ObjectShape.x)/_ObjectShape.y;
    float ny=(p.y-_ObjectShape.w)/_ObjectShape.z;
    float bend=(.055-.11*nx*nx)*_ObjectShape.z*_ObjectFeel.x;
    float irregular=(noise2(float2(nx*3.2+_ObjectSeed,ny*4.))-.5)*_ObjectShape.z*.12*_ObjectFeel.y;
    return p.y-(_ObjectSurfaceY+bend+irregular);
}
float objectReveal(float2 p)
{
    float revealed=ss(-_ObjectFeather,_ObjectFeather,objectWaterDistance(p));
    return lerp(1.,revealed,ss(.02,.17,_ObjectImmersion))*(1-ss(.88,.98,_ObjectImmersion));
}
#endif
