using UnityEngine;

namespace WaveLab
{
    // Shared contract with SurfCommon.cginc. Y grows toward the horizon.
    public static class SurfMath
    {
        public const float RestShore = -1.25f;
        public static float RemapPhase(float p,float approach,float advance,float hold,float recede,float rest)
        {
            float a=.26f*Mathf.Max(.01f,approach),b=.21f*Mathf.Max(.01f,advance),c=.07f*Mathf.Max(.01f,hold);
            float d=.42f*Mathf.Max(.01f,recede),e=.04f*Mathf.Max(.01f,rest);
            float time=p*(a+b+c+d+e);
            if(time<a)return time/a*.26f;time-=a;
            if(time<b)return .26f+time/b*.21f;time-=b;
            if(time<c)return .47f+time/c*.07f;time-=c;
            if(time<d)return .54f+time/d*.42f;time-=d;
            return .96f+time/e*.04f;
        }
        public static float Smooth(float a, float b, float x)
        {
            float v = Mathf.Clamp01((x - a) / (b - a));
            return v * v * (3f - 2f * v);
        }
        public static float Runup(float phase)
        {
            return Smooth(.26f, .47f, phase) * (1f - Smooth(.54f, .96f, phase));
        }
        public static float Shore(float x, float phase, float reach)
        {
            return RestShore + .23f * Mathf.Sin(x * .85f + .4f)
                + .12f * Mathf.Sin(x * 2.3f) + x*.045f - reach * Runup(phase);
        }
        public static float Crest(float phase)
        {
            return Mathf.Lerp(5.15f, -1.72f, Mathf.Clamp01(phase / .31f));
        }
        public static float Wetness(Vector2 p, float phase, float reach)
        {
            return Smooth(-.07f, .25f, p.y - Shore(p.x, phase, reach));
        }
        public static float WetSandMemory(Vector2 p,float reach)
        {
            return Smooth(0,.05f,p.y-Shore(p.x,.5f,reach));
        }
        // Object progression belongs to the beach layout, not the oscillating wave surface.
        public static float ObjectWaterDepth(Vector2 p,float slope)
        {
            return Mathf.Max(0,(p.y-Shore(p.x,0,0))*slope);
        }
        public static float ContactFoam(float submergedFraction)
        {
            return Smooth(.06f,.28f,submergedFraction)*(1-Smooth(.72f,.97f,submergedFraction));
        }
        public static Vector2 Current(Vector2 p, float phase, float reach, float sideBias,float breakerForce=.95f,float swashForce=.55f,float sideways=.18f)
        {
            float wet = Wetness(p, phase, reach);
            float crestPush = Mathf.Exp(-Mathf.Pow((p.y - Crest(phase)) / .78f, 2f))
                * (1f - Smooth(.29f, .36f, phase));
            float flood = Smooth(.26f, .31f, phase) * (1f - Smooth(.44f, .50f, phase));
            float frontDistance=p.y-Shore(p.x,phase,reach);
            float advancingFront=Mathf.Exp(-Mathf.Pow((frontDistance-.30f)/1.1f,2));
            float shoreward=(crestPush*breakerForce+flood*advancingFront*swashForce)*wet;
            // A retained step when the breaker/swash reaches the object. The receding
            // water moves visually, but does not pull the collectibles offshore.
            return new Vector2(shoreward*sideBias*sideways,-shoreward);
        }
        public static string PhaseName(float p)
        {
            if (p < .26f) return "01  /  INCOMING BREAKER";
            if (p < .47f) return "02  /  SWASH · WATER ADVANCES";
            if (p < .54f) return "03  /  MAXIMUM RUN-UP";
            if (p < .96f) return "04  /  BACKWASH · FOAM DISSOLVES";
            return "05  /  SETTLING";
        }
    }
}
