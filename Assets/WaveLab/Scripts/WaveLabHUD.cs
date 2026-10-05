using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace WaveLab
{
    public sealed class WaveLabHUD : MonoBehaviour
    {
        WaveLabController target;
        TextMeshProUGUI phaseLabel, timingLabel, pauseLabel;
        UnityEngine.UI.Slider scrub;
        bool scrubbing;
        static readonly Color Ink=new Color(.035f,.12f,.15f,.94f);
        static readonly Color Pale=new Color(.89f,.95f,.91f,1);
        static readonly Color Teal=new Color(.22f,.71f,.64f,1);
        public static WaveLabHUD Create(WaveLabController controller)
        {
            var go=new GameObject("Wave Lab · live controls",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
            var hud=go.AddComponent<WaveLabHUD>();hud.target=controller;
            Canvas canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=1000;
            var scaler=go.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(540,960);scaler.matchWidthOrHeight=.5f;
            if(EventSystem.current==null)
            {
                var events=new GameObject("Wave Lab EventSystem",typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
                events.AddComponent<InputSystemUIInputModule>();
#else
                events.AddComponent<StandaloneInputModule>();
#endif
            }
            hud.Build();return hud;
        }
        void Build()
        {
            var heading=Rect("Title",transform,new Vector2(0,1),new Vector2(1,1),new Vector2(14,-31),new Vector2(-125,-10));
            Text(heading,"SHORELINE / WAVE LAB",15,FontStyles.Bold);
            phaseLabel=Text(Rect("Phase",transform,new Vector2(0,1),new Vector2(1,1),new Vector2(14,-49),new Vector2(-125,-32)),"",10);
            phaseLabel.color=Teal;
            var footer=Rect("Controls",transform,Vector2.zero,new Vector2(1,0),new Vector2(12,12),new Vector2(-12,70));
            var bg=footer.gameObject.AddComponent<UnityEngine.UI.Image>();bg.color=Ink;bg.raycastTarget=true;
            var content=Rect("Rows",footer,Vector2.zero,Vector2.one,new Vector2(9,6),new Vector2(-9,-6));
            var layout=content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing=2;layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandHeight=false;
            var row=Row(content,25);
            pauseLabel=Button(row,"PAUSE",()=>target.paused=!target.paused);
            Button(row,"RESTART",()=>target.ResetSimulation());
            Button(row,"0.5x / 1x",()=>target.playbackSpeed=target.playbackSpeed<.8f?1:.5f);
            timingLabel=Label(row,"",10,25);
            scrub=Slider(content,0,1,0,value=>target.RequestSeek(value*target.period));
            WaveLabConfigPanel.Create(transform,target);
        }
        void Update() => Refresh();
        public void Refresh()
        {
            if(!target)return;
            phaseLabel.text=SurfMath.PhaseName(target.Phase);
            timingLabel.text=$"{target.CycleProgress*target.period:0.0} / {target.period:0.0}s";
            pauseLabel.text=target.paused?"PLAY":"PAUSE";
            if(!scrubbing&&!target.IsSeeking) scrub.SetValueWithoutNotify(target.CycleProgress);
        }
        static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max,Vector2 offsetMin,Vector2 offsetMax)
        {
            var go=new GameObject(name,typeof(RectTransform));var rect=(RectTransform)go.transform;rect.SetParent(parent,false);
            rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=offsetMin;rect.offsetMax=offsetMax;return rect;
        }
        static RectTransform Item(string name,Transform parent,float height)
        {
            var r=Rect(name,parent,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            var e=r.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();e.minHeight=height;e.preferredHeight=height;e.flexibleWidth=1;return r;
        }
        static RectTransform Row(Transform parent,float height)
        {
            var r=Item("Control row",parent,height);var layout=r.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing=7;layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandWidth=true;return r;
        }
        static TextMeshProUGUI Text(Transform parent,string value,float size,FontStyles style=FontStyles.Normal)
        {
            var r=Rect(value,parent,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);var text=r.gameObject.AddComponent<TextMeshProUGUI>();
            text.text=value;text.fontSize=size;text.fontStyle=style;text.color=Pale;text.alignment=TextAlignmentOptions.MidlineLeft;text.raycastTarget=false;
            text.textWrappingMode=TextWrappingModes.NoWrap;return text;
        }
        static TextMeshProUGUI Label(Transform parent,string value,float size,float height)=>Text(Item("Label",parent,height),value,size);
        static TextMeshProUGUI Button(Transform parent,string title,Action onClick)
        {
            var r=Item(title,parent,25);var image=r.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=new Color(.14f,.29f,.31f,1);
            var b=r.gameObject.AddComponent<UnityEngine.UI.Button>();b.targetGraphic=image;b.onClick.AddListener(()=>onClick());
            var text=Text(r,title,11,FontStyles.Bold);text.alignment=TextAlignmentOptions.Center;return text;
        }
        static void LayerButton(Transform parent,string title,Func<bool> get,Action<bool> set)
        {
            TextMeshProUGUI text=null;
            text=Button(parent,title,()=>{set(!get());text.color=get()?Teal:new Color(.6f,.65f,.65f);});text.color=Teal;
        }
        UnityEngine.UI.Slider Slider(Transform parent,float min,float max,float value,Action<float> change)
        {
            var r=Item("Cycle scrubber",parent,19);var slider=r.gameObject.AddComponent<UnityEngine.UI.Slider>();
            slider.minValue=min;slider.maxValue=max;slider.value=value;
            var bg=Rect("Track",r,new Vector2(0,.3f),new Vector2(1,.7f),Vector2.zero,Vector2.zero);bg.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(.23f,.36f,.36f,1);
            var fill=Rect("Fill",bg,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);fill.gameObject.AddComponent<UnityEngine.UI.Image>().color=Teal;slider.fillRect=fill;
            var handleArea=Rect("Handle area",r,new Vector2(0,.15f),new Vector2(1,.85f),Vector2.zero,Vector2.zero);
            var handle=Rect("Handle",handleArea,Vector2.zero,Vector2.up,new Vector2(-5,0),new Vector2(5,0));
            var image=handle.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=Pale;slider.handleRect=handle;slider.targetGraphic=image;
            slider.onValueChanged.AddListener(v=>change(v));
            var events=r.gameObject.AddComponent<EventTrigger>();
            var down=new EventTrigger.Entry{eventID=EventTriggerType.PointerDown};down.callback.AddListener(_=>scrubbing=true);events.triggers.Add(down);
            var up=new EventTrigger.Entry{eventID=EventTriggerType.PointerUp};up.callback.AddListener(_=>scrubbing=false);events.triggers.Add(up);
            return slider;
        }
    }
}
