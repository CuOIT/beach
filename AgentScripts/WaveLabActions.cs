using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using WaveLab;
using WaveLab.EditorTools;

public static class WaveLabActions
{
    public static string Build() => WaveLabSceneBuilder.Create();
    public static string Verify() => WaveLabSceneBuilder.Validate();
    public static string UpgradeImmersion()
    {
        WaveLabSceneBuilder.UpgradeImmersion();
        var c=Object.FindAnyObjectByType<WaveLabController>();
        return "Installed split-water rendering and contact foam on "+c.ImmersionObjects.Count+" objects.";
    }
    public static string VerifyImmersion()
    {
        var c=Object.FindAnyObjectByType<WaveLabController>();c.paused=true;c.Seek(0);
        if(c.ImmersionObjects.Count!=93)throw new System.Exception("Expected 85 props and 8 rocks with object states.");
        foreach(var name in new[]{"WaveLab/Beach Objects","WaveLab/Object Contact Foam"})
            if(ShaderUtil.ShaderHasError(Shader.Find(name)))throw new System.Exception("Shader error: "+name);
        foreach(var value in new[]{ImmersionState.Exposed,ImmersionState.PartiallySubmerged,ImmersionState.FullySubmerged})
            if(c.CountImmersion(value)==0)throw new System.Exception("Baseline does not demonstrate "+value);
        var sample=c.ImmersionObjects[12];Vector3 original=sample.transform.position;
        sample.transform.position=new Vector3(0,4,0);c.ApplyGlobals();
        if(sample.State!=ImmersionState.FullySubmerged||sample.FoamStrength>.01f)throw new System.Exception("Deep state or foam gating failed.");
        sample.transform.position=new Vector3(0,-4.5f,0);c.ApplyGlobals();
        if(sample.State!=ImmersionState.Exposed||sample.FoamStrength>.01f)throw new System.Exception("Exposed state or foam gating failed.");
        bool foundPartial=false;
        for(float y=-1;y<2;y+=.05f)
        {
            sample.transform.position=new Vector3(0,y,0);c.ApplyGlobals();
            if(sample.SubmergedFraction>.45f&&sample.SubmergedFraction<.55f)
            {
                if(sample.FoamStrength<.8f)throw new System.Exception("No contact foam at half immersion.");
                if(!(sample.SubmergedRenderer.sortingOrder<400&&sample.ExposedRenderer.sortingOrder>420))throw new System.Exception("Water layers are not between the two portions.");
                foundPartial=true;break;
            }
        }
        if(!foundPartial)throw new System.Exception("Could not reach half immersion.");
        sample.transform.position=original;c.ApplyGlobals();
        float nearDepth=SurfMath.ObjectWaterDepth(new Vector2(0,-2.5f),c.beachSlope);
        float farDepth=SurfMath.ObjectWaterDepth(new Vector2(0,2.5f),c.beachSlope);
        if(farDepth<=nearDepth)throw new System.Exception("Depth does not increase offshore.");
        c.Seek(0);c.ApplyGlobals();
        string result=$"PASS: 93 objects; all three states; half-immersion foam; full-state foam off; lower/upper layer order; shore-based depth. Baseline: {c.CountImmersion(ImmersionState.FullySubmerged)} underwater, {c.CountImmersion(ImmersionState.PartiallySubmerged)} partial, {c.CountImmersion(ImmersionState.Exposed)} exposed.";
        File.WriteAllText("Screenshots/WaveLab/immersion-validation.txt",result);
        c.paused=false;return result;
    }
    public static string VerifyShorewardMotion()
    {
        var c=Object.FindAnyObjectByType<WaveLabController>();
        bool oldPause=c.paused,oldMove=c.moveObjects;
        c.paused=true;c.moveObjects=false;c.ResetSimulation();
        try
        {
            var baseline=new float[c.ImmersionObjects.Count];
            for(int i=0;i<baseline.Length;i++)baseline[i]=c.ImmersionObjects[i].SubmergedFraction;
            float stationaryDepthChange=0;
            for(int frame=0;frame<570;frame++)
            {
                c.Step(1f/60);c.ApplyGlobals();
                for(int i=0;i<baseline.Length;i++)stationaryDepthChange=Mathf.Max(stationaryDepthChange,Mathf.Abs(baseline[i]-c.ImmersionObjects[i].SubmergedFraction));
            }
            if(stationaryDepthChange>.00001f)throw new System.Exception("A passing wave changed stationary object depth: "+stationaryDepthChange);
            c.moveObjects=true;c.ResetSimulation();
            var previous=new Vector2[c.Bodies.Count];
            float backtrack=0,activeTravel=0,idleTravel=0,maxDepthStep=0;
            for(int frame=0;frame<1692;frame++)
            {
                for(int i=0;i<previous.Length;i++)previous[i]=c.Bodies[i].position;
                for(int i=0;i<baseline.Length;i++)baseline[i]=c.ImmersionObjects[i].SubmergedFraction;
                c.Step(1f/60);c.ApplyGlobals();
                for(int i=0;i<previous.Length;i++)
                {
                    Vector2 delta=c.Bodies[i].position-previous[i];
                    if(!float.IsFinite(delta.x)||!float.IsFinite(delta.y))throw new System.Exception("Invalid movement");
                    backtrack=Mathf.Max(backtrack,delta.y);
                    if(c.Phase>.66f)idleTravel+=delta.magnitude;else activeTravel+=delta.magnitude;
                }
                for(int i=0;i<baseline.Length;i++)maxDepthStep=Mathf.Max(maxDepthStep,Mathf.Abs(baseline[i]-c.ImmersionObjects[i].SubmergedFraction));
            }
            if(backtrack>.00001f)throw new System.Exception("Prop moved offshore: "+backtrack);
            if(activeTravel<1||idleTravel>.01f)throw new System.Exception("Motion is not synchronized with advancing waves: "+activeTravel+" / "+idleTravel);
            if(maxDepthStep>.025f)throw new System.Exception("Immersion changed too abruptly: "+maxDepthStep);
            int left=0,right=0;float retained=0;
            foreach(var b in c.Bodies)
            {
                float dx=b.position.x-b.initial.x;
                if(dx<-.03f)left++;if(dx>.03f)right++;
                retained+=b.initial.y-b.position.y;
            }
            if(left<3||right<3)throw new System.Exception("Missing gentle drift to both sides");
            string result=$"PASS: three waves; max offshore step {backtrack:F6}; stationary depth change {stationaryDepthChange:F6}; max per-frame immersion change {maxDepthStep:F4}; late-backwash travel {idleTravel:F6}; retained advance {retained:F2}; left/right {left}/{right}.";
            File.WriteAllText("Screenshots/WaveLab/motion-validation.txt",result);return result;
        }
        finally {c.moveObjects=oldMove;c.ResetSimulation();c.paused=oldPause;}
    }
    public static string CaptureMeniscus()
    {
        var c=Object.FindAnyObjectByType<WaveLabController>();c.paused=true;c.Seek(0);
        var camera=c.SceneCamera;Vector3 oldPosition=camera.transform.position;float oldSize=camera.orthographicSize;
        try
        {
            foreach(var item in c.ImmersionObjects)
                if(item.name.Contains("Fish")&&item.SubmergedFraction>.30f&&item.SubmergedFraction<.70f)
                {
                    Vector3 p=item.transform.position;camera.transform.position=new Vector3(p.x,p.y,oldPosition.z);camera.orthographicSize=.72f;
                    WaveLabSceneBuilder.Render(camera,900,600,"Screenshots/WaveLab/08-soft-emergence-detail.png");
                    return item.name+" | immersion "+item.SubmergedFraction;
                }
            throw new System.Exception("No partial fish available for the detail capture");
        }
        finally {camera.transform.position=oldPosition;camera.orthographicSize=oldSize;c.ResetSimulation();c.paused=false;}
    }
    public static string CaptureImmersion()
    {
        var c=Object.FindAnyObjectByType<WaveLabController>();c.paused=true;c.Seek(0);
        WaveLabSceneBuilder.Render(c.SceneCamera,720,1280,"Screenshots/WaveLab/06-immersion-states.png",true);
        c.Seek(4.6f);
        WaveLabSceneBuilder.Render(c.SceneCamera,720,1280,"Screenshots/WaveLab/07-immersion-runup.png",true);
        c.ResetSimulation();c.paused=false;return "Captured calm and peak-water object states.";
    }
    public static string VerifyRuntime()
    {
        var c=Object.FindAnyObjectByType<WaveLabController>();
        if(!Application.isPlaying)throw new System.Exception("Must be in Play mode");
        var hud=Object.FindAnyObjectByType<WaveLabHUD>();
        if(!hud)throw new System.Exception("Runtime controls were not initialized");
        var buttons=hud.GetComponentsInChildren<UnityEngine.UI.Button>();
        var events=Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>();
        if(events.Length!=1)throw new System.Exception("Expected exactly one EventSystem");
        int checkedButtons=0;
        foreach(var b in buttons)
        {
            if(b.name=="FOAM") {bool before=c.showFoam;b.onClick.Invoke();if(c.showFoam==before)throw new System.Exception("Foam toggle failed");b.onClick.Invoke();checkedButtons++;}
            if(b.name=="PAUSE") {bool before=c.paused;b.onClick.Invoke();if(c.paused==before)throw new System.Exception("Pause failed");b.onClick.Invoke();checkedButtons++;}
            if(b.name=="DEPTH")
            {
                bool before=c.showObjectImmersion;b.onClick.Invoke();c.ApplyGlobals();
                if(c.showObjectImmersion==before)throw new System.Exception("Depth toggle failed");
                foreach(var item in c.ImmersionObjects)
                    if(item.ExposedRenderer.enabled!=c.showObjectImmersion)throw new System.Exception("Depth toggle did not update the split passes");
                b.onClick.Invoke();c.ApplyGlobals();checkedButtons++;
            }
        }
        c.paused=true;c.Seek(56.4f);
        float minY=99,maxY=-99;
        foreach(var body in c.Bodies)
        {
            if(float.IsNaN(body.position.x)||float.IsInfinity(body.position.y))throw new System.Exception("Invalid drift position");
            minY=Mathf.Min(minY,body.position.y);maxY=Mathf.Max(maxY,body.position.y);
        }
        c.Seek(4.6f);c.ApplyGlobals();
        WaveLabSceneBuilder.Render(c.SceneCamera,540,960,"Screenshots/WaveLab/05-live-controls.png",true);
        c.ResetSimulation();c.paused=false;
        return "PASS: "+buttons.Length+" buttons, "+checkedButtons+" callbacks checked, one EventSystem; six cycles stable, Y range "+minY.ToString("F2")+".."+maxY.ToString("F2");
    }
    public static string CapturePhases()
    {
        WaveLabSceneBuilder.Capture(.3f,"01-calm");
        WaveLabSceneBuilder.Capture(1.65f,"02-breaker");
        WaveLabSceneBuilder.Capture(4.6f,"03-runup");
        WaveLabSceneBuilder.Capture(6.8f,"04-backwash");
        return "Captured four reference phases.";
    }
    public static string SaveBaseline()
    {
        if(Application.isPlaying)throw new System.Exception("Stop Play mode before saving the baseline.");
        var c=Object.FindAnyObjectByType<WaveLabController>();c.ResetSimulation();c.paused=false;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(c.gameObject.scene);
        return c.gameObject.scene.path;
    }
    public static async Task<string> Record()
    {
        var c=Object.FindAnyObjectByType<WaveLabController>();
        c.paused=true;c.ResetSimulation();
        string directory=Path.GetFullPath("Screenshots/WaveLab/Frames-refined");Directory.CreateDirectory(directory);
        for(int i=0;i<600;i++)
        {
            c.ApplyGlobals();
            WaveLabSceneBuilder.Render(c.SceneCamera,540,960,Path.Combine(directory,i.ToString("D4")+".png"));
            c.Step(1f/60);c.Step(1f/60);
            if(i%4==0)await Task.Delay(1);
        }
        c.ResetSimulation();c.paused=false;
        return directory;
    }
}
