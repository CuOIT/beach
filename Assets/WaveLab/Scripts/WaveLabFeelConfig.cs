using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace WaveLab
{
    public sealed class FeelParameter
    {
        public string key,label,unit;
        public int group;
        public float min,max,baseline;
        public bool toggle;
        public Func<float> read;
        public Action<float> write;
    }
    public sealed class WaveLabFeelConfig
    {
        public static readonly string[] Groups={"Wave","Drift","Depth","Foam","View"};
        public readonly List<FeelParameter> parameters=new List<FeelParameter>();
        readonly WaveLabController target;
        public string SavePath=>Path.Combine(Application.persistentDataPath,"WaveLab","feel-config.json");
        public WaveLabFeelConfig(WaveLabController c)
        {
            target=c;
            void P(int g,string key,string label,float min,float max,float baseline,Func<float> get,Action<float> set,string unit="")
            {parameters.Add(new FeelParameter{group=g,key=key,label=label,min=min,max=max,baseline=baseline,read=get,write=set,unit=unit});}
            void B(int g,string key,string label,Func<bool> get,Action<bool> set)
            {parameters.Add(new FeelParameter{group=g,key=key,label=label,min=0,max=1,baseline=1,toggle=true,read=()=>get()?1:0,write=v=>set(v>.5f)});}
            P(0,"period","Wave period",5,16,9.4f,()=>c.period,v=>c.SetPeriod(v),"s");
            P(0,"playbackSpeed","Playback speed",.1f,2,1,()=>c.playbackSpeed,v=>c.playbackSpeed=v,"x");
            P(0,"runupReach","Run-up distance",.5f,3.6f,2.45f,()=>c.runupReach,v=>c.runupReach=v);
            P(0,"approachTime","Approach time",.25f,3,1,()=>c.approachTime,v=>c.approachTime=v,"x");
            P(0,"advanceTime","Advance time",.25f,3,1,()=>c.advanceTime,v=>c.advanceTime=v,"x");
            P(0,"holdTime","Peak hold time",.25f,3,1,()=>c.holdTime,v=>c.holdTime=v,"x");
            P(0,"recedeTime","Recede time",.25f,3,1,()=>c.recedeTime,v=>c.recedeTime=v,"x");
            P(0,"restTime","Rest time",.25f,3,1,()=>c.restTime,v=>c.restTime=v,"x");
            P(0,"crestThickness","Breaker thickness",.25f,2,1,()=>c.crestThickness,v=>c.crestThickness=v,"x");
            P(0,"crestRoughness","Breaker roughness",0,2,1,()=>c.crestRoughness,v=>c.crestRoughness=v,"x");
            P(1,"currentStrength","Overall drift",0,2,1,()=>c.currentStrength,v=>c.currentStrength=v,"x");
            P(1,"breakerPush","Breaker push",0,2,.95f,()=>c.breakerPush,v=>c.breakerPush=v);
            P(1,"swashPush","Swash push",0,2,.55f,()=>c.swashPush,v=>c.swashPush=v);
            P(1,"lateralDrift","Left / right drift",0,.6f,.18f,()=>c.lateralDrift,v=>c.lateralDrift=v);
            P(1,"acceleration","Push response",1,30,8,()=>c.acceleration,v=>c.acceleration=v);
            P(1,"settleDamping","Settling damping",1,30,12,()=>c.settleDamping,v=>c.settleDamping=v);
            P(1,"rockSteering","Steer around rocks",0,1,.4f,()=>c.rockSteering,v=>c.rockSteering=v);
            P(1,"objectSteering","Object separation",0,1,.25f,()=>c.objectSteering,v=>c.objectSteering=v);
            P(1,"turnStrength","Turn strength",0,25,7,()=>c.turnStrength,v=>c.turnStrength=v);
            P(1,"turnLimit","Turn limit",0,25,7,()=>c.turnLimit,v=>c.turnLimit=v,"deg");
            P(2,"beachSlope","Beach depth slope",.05f,.4f,.18f,()=>c.beachSlope,v=>c.beachSlope=v);
            P(2,"objectHeight","Object height",.5f,2,1,()=>c.objectHeight,v=>c.objectHeight=v,"x");
            P(2,"waterlineSoftness","Waterline softness",.03f,.4f,.20f,()=>c.waterlineSoftness,v=>c.waterlineSoftness=v);
            P(2,"waterlineCurve","Meniscus curve",0,2,1,()=>c.waterlineCurve,v=>c.waterlineCurve=v,"x");
            P(2,"waterlineRoughness","Meniscus roughness",0,2,1,()=>c.waterlineRoughness,v=>c.waterlineRoughness=v,"x");
            P(2,"submergedTint","Underwater tint",0,2,1,()=>c.submergedTint,v=>c.submergedTint=v,"x");
            P(2,"submergedOpacity","Underwater opacity",.15f,1.25f,1,()=>c.submergedOpacity,v=>c.submergedOpacity=v,"x");
            P(3,"foamAmount","Surface foam",0,2,1,()=>c.foamAmount,v=>c.foamAmount=v,"x");
            P(3,"shoreFoamWidth","Shore foam width",.25f,2.5f,1,()=>c.shoreFoamWidth,v=>c.shoreFoamWidth=v,"x");
            P(3,"foamScale","Foam pattern scale",.25f,2.5f,1,()=>c.foamScale,v=>c.foamScale=v,"x");
            P(3,"foamFlow","Foam flow speed",0,3,1,()=>c.foamFlow,v=>c.foamFlow=v,"x");
            P(3,"foamFadeEnd","Foam fade phase",.65f,.98f,.89f,()=>c.foamFadeEnd,v=>c.foamFadeEnd=v);
            P(3,"objectFoamAmount","Sprite foam amount",0,2,1,()=>c.objectFoamAmount,v=>c.objectFoamAmount=v,"x");
            P(3,"contactFoamWidth","Sprite foam width",.25f,2.5f,1,()=>c.contactFoamWidth,v=>c.contactFoamWidth=v,"x");
            P(3,"contactFoamBreakup","Sprite foam breakup",0,1,1,()=>c.contactFoamBreakup,v=>c.contactFoamBreakup=v);
            P(3,"contactFoamSpeed","Sprite foam speed",0,3,1,()=>c.contactFoamSpeed,v=>c.contactFoamSpeed=v,"x");
            B(4,"showWater","Water layer",()=>c.showWater,v=>c.showWater=v);
            B(4,"showVeil","Underwater veil",()=>c.showVeil,v=>c.showVeil=v);
            B(4,"showFoam","Foam layer",()=>c.showFoam,v=>c.showFoam=v);
            B(4,"showBreaker","Breaking crest",()=>c.showBreaker,v=>c.showBreaker=v);
            B(4,"showObjectImmersion","Depth / immersion",()=>c.showObjectImmersion,v=>c.showObjectImmersion=v);
            B(4,"moveObjects","Object motion",()=>c.moveObjects,v=>c.moveObjects=v);
            P(4,"waterOpacity","Water opacity",0,2,1,()=>c.waterOpacity,v=>c.waterOpacity=v,"x");
            P(4,"veilOpacity","Veil opacity",0,2,1,()=>c.veilOpacity,v=>c.veilOpacity=v,"x");
            P(4,"reflections","Water reflections",0,2,1,()=>c.reflections,v=>c.reflections=v,"x");
            P(4,"wetSandStrength","Wet sand trail",0,2,1,()=>c.wetSandStrength,v=>c.wetSandStrength=v,"x");
            P(4,"zoom","Camera view size",5,11,8,()=>c.SceneCamera.orthographicSize,v=>c.SceneCamera.orthographicSize=v);
        }
        public void Set(FeelParameter parameter,float value)
        {
            if(float.IsNaN(value)||float.IsInfinity(value))return;
            parameter.write(Mathf.Clamp(value,parameter.min,parameter.max));target.ApplyGlobals();
        }
        public void Reset(int group=-1)
        {foreach(var p in parameters)if(group<0||p.group==group)p.write(p.baseline);target.ApplyGlobals();}
        [Serializable] public sealed class Entry {public string key;public float value;}
        [Serializable] public sealed class Preset {public int version=1;public List<Entry> values=new List<Entry>();}
        public void Save(string path=null)
        {
            path=path??SavePath;var data=new Preset();
            foreach(var p in parameters)data.values.Add(new Entry{key=p.key,value=p.read()});
            Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,JsonUtility.ToJson(data,true));
        }
        public void Load(string path=null)
        {
            path=path??SavePath;
            if(!File.Exists(path))throw new IOException("No saved preset yet");
            var data=JsonUtility.FromJson<Preset>(File.ReadAllText(path));
            if(data==null||data.version!=1||data.values==null)throw new IOException("Invalid preset format");
            foreach(var e in data.values)if(e==null||float.IsNaN(e.value)||float.IsInfinity(e.value))throw new IOException("Invalid preset value");
            foreach(var e in data.values)foreach(var p in parameters)if(e.key==p.key)p.write(Mathf.Clamp(e.value,p.min,p.max));
            target.ApplyGlobals();
        }
    }
}
