using UnityEngine;
using UnityEditor;
using WaveLab;
using WaveLab.EditorTools;
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using Object=UnityEngine.Object;
using System.Threading.Tasks;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
public static class PanelActions
{
    public static string Inspect()
    {
        var hud=Object.FindAnyObjectByType<WaveLabHUD>();
        if(!hud)return "No runtime HUD; playing="+Application.isPlaying;
        var canvas=hud.GetComponent<Canvas>();
        return "HUD="+hud.name+"; canvas="+canvas.renderMode+"; buttons="+hud.GetComponentsInChildren<UnityEngine.UI.Button>().Length+
            "; sliders="+hud.GetComponentsInChildren<UnityEngine.UI.Slider>().Length+"; canvas rect="+((RectTransform)hud.transform).rect;
    }
    public static string Verify()
    {
        var panel=Object.FindAnyObjectByType<WaveLabConfigPanel>();var c=Object.FindAnyObjectByType<WaveLabController>();
        if(!Application.isPlaying||!panel)throw new Exception("Run the scene in Play mode first.");
        var baseline=new List<float>();foreach(var p in panel.Config.parameters)baseline.Add(p.read());
        c.paused=true;c.ResetSimulation();int sliders=0,toggles=0;
        try
        {
            if(panel.IsOpen)throw new Exception("Config must start collapsed.");
            var configButton=panel.transform.Find("CONFIG").GetComponent<UnityEngine.UI.Button>();
            configButton.onClick.Invoke();if(!panel.IsOpen)throw new Exception("CONFIG button did not open panel.");
            foreach(var p in panel.Config.parameters)
            {
                panel.SelectTab(p.group);
                if(p.toggle)
                {
                    float before=p.read();panel.GetToggle(p.key).onClick.Invoke();
                    if(p.read()==before)throw new Exception("Toggle is not wired: "+p.key);toggles++;
                }
                else
                {
                    float test=Mathf.Lerp(p.min,p.max,.62f);panel.GetSlider(p.key).value=test;
                    if(Mathf.Abs(p.read()-test)>.0001f)throw new Exception("Slider is not wired: "+p.key);
                    panel.GetInput(p.key).onEndEdit.Invoke(p.baseline.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    if(Mathf.Abs(p.read()-p.baseline)>.0001f)throw new Exception("Numeric input is not wired: "+p.key);sliders++;
                }
            }
            panel.Config.Reset();panel.SelectTab(3);
            panel.GetSlider("contactFoamWidth").value=1.7f;panel.GetSlider("foamFlow").value=.8f;
            var item=c.ImmersionObjects[12];var properties=new MaterialPropertyBlock();item.ContactFoam.GetPropertyBlock(properties);
            float expectedWidth=Mathf.Clamp(item.SubmergedRenderer.bounds.size.y*.022f,.009f,.025f)*1.7f;
            if(Mathf.Abs(properties.GetFloat("_FoamWidth")-expectedWidth)>.00001f)throw new Exception("Foam width did not reach renderer.");
            if(Mathf.Abs(Shader.GetGlobalVector("_SurfFoamFeel").x-.8f)>.0001f)throw new Exception("Foam flow did not reach shader.");
            string path=Path.GetFullPath("Screenshots/WaveLab/config-roundtrip.json");panel.Config.Save(path);
            panel.Config.Reset();panel.Config.Load(path);
            if(Mathf.Abs(c.contactFoamWidth-1.7f)>.00001f||Mathf.Abs(c.foamFlow-.8f)>.00001f)throw new Exception("Save/load did not preserve values.");
            panel.Config.Reset();
            for(int i=0;i<100;i++)if(Mathf.Abs(SurfMath.RemapPhase(i*.01f,1,1,1,1,1)-i*.01f)>.00001f)throw new Exception("Default phase timing changed.");
            panel.SelectTab(0);Canvas.ForceUpdateCanvases();
            var scroll=panel.GetComponentInChildren<UnityEngine.UI.ScrollRect>();
            if(scroll.content.rect.height<=scroll.viewport.rect.height)throw new Exception("Settings cannot scroll.");
            scroll.verticalNormalizedPosition=0;Canvas.ForceUpdateCanvases();
            if(scroll.content.anchoredPosition.y<=0)throw new Exception("Scrolling did not move content.");
            scroll.verticalNormalizedPosition=1;
            var canvas=(RectTransform)panel.transform;var rect=panel.PanelRect;
            float cover=rect.rect.width*rect.rect.height/(canvas.rect.width*canvas.rect.height);
            if(cover>.25f)throw new Exception("Panel covers too much gameplay: "+cover);
            Vector2 position=rect.anchoredPosition;
            panel.GetComponentInChildren<WaveLabPanelDrag>().OnDrag(new PointerEventData(EventSystem.current){delta=new Vector2(-100,25)});
            if(Vector2.Distance(position,rect.anchoredPosition)<1)throw new Exception("Panel header does not drag.");
            rect.anchoredPosition=position;
            var hits=new List<RaycastResult>();var pointer=new PointerEventData(EventSystem.current){position=new Vector2(Screen.width*.15f,Screen.height*.45f)};
            EventSystem.current.RaycastAll(pointer,hits);
            if(hits.Count>0||WaveLabConfigPanel.BlocksPointer(pointer.position))throw new Exception("Config is blocking uncovered gameplay.");
            configButton.onClick.Invoke();if(panel.IsOpen)throw new Exception("CONFIG button did not hide panel.");
            if(Object.FindObjectsByType<EventSystem>().Length!=1)throw new Exception("Duplicate EventSystems.");
            string result=$"PASS: {sliders} live sliders and numeric fields; {toggles} layer toggles; GPU values; Save/Load; scrolling; drag; collapsed default; gameplay raycasts; one EventSystem. Open-panel area {cover:P1}.";
            File.WriteAllText("Screenshots/WaveLab/panel-validation.txt",result);return result;
        }
        finally
        {
            for(int i=0;i<baseline.Count;i++)panel.Config.parameters[i].write(baseline[i]);
            panel.SelectTab(0);panel.SetOpen(false);c.ResetSimulation();c.paused=false;
        }
    }
    public static string Capture()
    {
        var panel=Object.FindAnyObjectByType<WaveLabConfigPanel>();var c=Object.FindAnyObjectByType<WaveLabController>();
        c.paused=true;c.Seek(3.8f);
        panel.SetOpen(false);WaveLabSceneBuilder.Render(c.SceneCamera,540,960,"Screenshots/WaveLab/10-config-closed.png",true);
        panel.SetOpen(true);panel.SelectTab(3);
        WaveLabSceneBuilder.Render(c.SceneCamera,540,960,"Screenshots/WaveLab/11-config-foam.png",true);
        panel.SelectTab(1);WaveLabSceneBuilder.Render(c.SceneCamera,960,540,"Screenshots/WaveLab/12-config-landscape.png",true);
        panel.SelectTab(0);panel.SetOpen(false);c.ResetSimulation();c.paused=false;
        return "Captured closed, foam tab and landscape panel.";
    }
    public static async Task<string> KeyboardCheck()
    {
        var panel=Object.FindAnyObjectByType<WaveLabConfigPanel>();panel.SetOpen(false);
        try
        {
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.F1));await Task.Delay(200);
            if(!panel.IsOpen)throw new Exception("F1 did not open the panel.");
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());await Task.Delay(100);
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.Escape));await Task.Delay(200);
            if(panel.IsOpen)throw new Exception("Escape did not close the panel.");
            return "PASS: F1 opens and Escape closes through the Input System.";
        }
        finally {InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());panel.SetOpen(false);}
    }
}
