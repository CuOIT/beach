using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace WaveLab
{
    public sealed class WaveLabConfigPanel : MonoBehaviour
    {
        public WaveLabFeelConfig Config {get;private set;}
        public bool IsOpen=>panel&&panel.gameObject.activeSelf;
        public RectTransform PanelRect=>panel;
        public int CurrentTab=>tab;
        static WaveLabConfigPanel active;
        RectTransform panel,toggleRect;
        UnityEngine.UI.ScrollRect scroll;
        readonly List<RectTransform> pages=new List<RectTransform>();
        readonly List<TextMeshProUGUI> tabs=new List<TextMeshProUGUI>();
        readonly Dictionary<string,Binding> bindings=new Dictionary<string,Binding>();
        TextMeshProUGUI toggleLabel,status;
        int tab;
        float nextRefresh;
        static readonly Color Ink=new Color(.025f,.09f,.11f,.94f),Pale=new Color(.91f,.96f,.94f),Teal=new Color(.27f,.85f,.73f);
        sealed class Binding
        { public FeelParameter parameter;public UnityEngine.UI.Slider slider;public TMP_InputField input;public TextMeshProUGUI toggle;public UnityEngine.UI.Button button; }
        public static bool IsTyping
        {
            get {var current=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;var input=current?current.GetComponent<TMP_InputField>():null;return input&&input.isFocused;}
        }
        public static bool BlocksPointer(Vector2 point)
        {
            if(!active)return false;
            var canvas=active.GetComponentInParent<Canvas>();var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            return RectTransformUtility.RectangleContainsScreenPoint(active.toggleRect,point,camera)
                ||active.IsOpen&&RectTransformUtility.RectangleContainsScreenPoint(active.panel,point,camera);
        }
        public static WaveLabConfigPanel Create(Transform parent,WaveLabController target)
        {
            var root=R("Realtime feel config",parent,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            var result=root.gameObject.AddComponent<WaveLabConfigPanel>();result.Config=new WaveLabFeelConfig(target);
            active=result;result.Build();return result;
        }
        void OnDestroy(){if(active==this)active=null;}
        void Build()
        {
            toggleRect=R("CONFIG",transform,Vector2.one,Vector2.one,new Vector2(-112,-42),new Vector2(-12,-12));
            toggleLabel=MakeButton(toggleRect,"CONFIG  F1",()=>SetOpen(!IsOpen));
            panel=R("Feel panel",transform,Vector2.one,Vector2.one,new Vector2(-290,-426),new Vector2(-12,-50));
            panel.pivot=Vector2.one;
            panel.sizeDelta=new Vector2(278,376);panel.anchoredPosition=new Vector2(-12,-50);
            var bg=panel.gameObject.AddComponent<UnityEngine.UI.Image>();bg.color=Ink;bg.raycastTarget=true;
            var header=R("Drag panel",panel,new Vector2(0,1),Vector2.one,new Vector2(10,-37),new Vector2(-42,-9));
            var hit=header.gameObject.AddComponent<UnityEngine.UI.Image>();hit.color=new Color(0,0,0,0);hit.raycastTarget=true;
            T(header,"FEEL CONFIG  /  drag",13,true).color=Teal;
            var drag=header.gameObject.AddComponent<WaveLabPanelDrag>();drag.target=panel;drag.canvas=(RectTransform)transform;
            MakeButton(R("Close",panel,Vector2.one,Vector2.one,new Vector2(-36,-35),new Vector2(-9,-9)),"X",()=>SetOpen(false));
            var tabRow=R("Tabs",panel,new Vector2(0,1),Vector2.one,new Vector2(10,-68),new Vector2(-10,-43));
            for(int i=0;i<WaveLabFeelConfig.Groups.Length;i++)
            {
                int index=i;float w=1f/WaveLabFeelConfig.Groups.Length;
                var r=R("Tab "+WaveLabFeelConfig.Groups[i],tabRow,new Vector2(i*w,0),new Vector2((i+1)*w,1),Vector2.zero,new Vector2(-3,0));
                tabs.Add(MakeButton(r,WaveLabFeelConfig.Groups[i],()=>SelectTab(index),11));
            }
            var scrollRoot=R("Settings scroll",panel,Vector2.zero,Vector2.one,new Vector2(10,65),new Vector2(-10,-77));
            scroll=scrollRoot.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();scroll.horizontal=false;scroll.vertical=true;
            scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=25;scroll.inertia=false;
            var viewport=R("Viewport",scrollRoot,Vector2.zero,Vector2.one,Vector2.zero,new Vector2(-10,0));
            viewport.gameObject.AddComponent<UnityEngine.UI.Image>().color=Color.white;
            viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic=false;scroll.viewport=viewport;
            for(int group=0;group<WaveLabFeelConfig.Groups.Length;group++)
            {
                var page=R(WaveLabFeelConfig.Groups[group]+" settings",viewport,new Vector2(0,1),Vector2.one,Vector2.zero,Vector2.zero);
                page.pivot=new Vector2(.5f,1);page.sizeDelta=Vector2.zero;
                var layout=page.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
                layout.spacing=5;layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandHeight=false;
                var fitter=page.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();fitter.verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
                pages.Add(page);
                foreach(var parameter in Config.parameters)if(parameter.group==group)BuildParameter(page,parameter);
            }
            var bar=R("Scrollbar",scrollRoot,new Vector2(1,0),Vector2.one,new Vector2(-5,0),Vector2.zero);
            bar.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(.18f,.28f,.29f);
            var handle=R("Handle",bar,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            var handleImage=handle.gameObject.AddComponent<UnityEngine.UI.Image>();handleImage.color=Teal;
            var scrollbar=bar.gameObject.AddComponent<UnityEngine.UI.Scrollbar>();scrollbar.direction=UnityEngine.UI.Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect=handle;scrollbar.targetGraphic=handleImage;scroll.verticalScrollbar=scrollbar;
            scroll.verticalScrollbarVisibility=UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide;
            var actions=R("Preset actions",panel,Vector2.zero,new Vector2(1,0),new Vector2(10,10),new Vector2(-10,36));
            string[] names={"Reset tab","Default","Save","Load"};
            Action[] callbacks={()=>{Config.Reset(tab);RefreshValues();SetStatus("This tab reset");},()=>{Config.Reset();RefreshValues();SetStatus("Reference defaults restored");},
                ()=>Attempt(()=>Config.Save(),"Saved feel-config.json"),()=>Attempt(()=>Config.Load(),"Preset loaded")};
            for(int i=0;i<4;i++)MakeButton(R(names[i],actions,new Vector2(i*.25f,0),new Vector2((i+1)*.25f,1),Vector2.zero,new Vector2(-3,0)),names[i],callbacks[i],10);
            status=T(R("Status",panel,Vector2.zero,new Vector2(1,0),new Vector2(10,39),new Vector2(-10,58)),"Live updates · scroll for more",10);
            status.color=new Color(.64f,.79f,.77f);status.overflowMode=TextOverflowModes.Ellipsis;
            SelectTab(0);SetOpen(false);
        }
        void BuildParameter(Transform parent,FeelParameter p)
        {
            var row=R(p.key,parent,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            var size=row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();size.minHeight=p.toggle?28:43;size.preferredHeight=size.minHeight;
            var binding=new Binding{parameter=p};bindings.Add(p.key,binding);
            T(R("Label",row,new Vector2(0,1),Vector2.one,new Vector2(0,-19),new Vector2(-68,0)),p.label+(string.IsNullOrEmpty(p.unit)?"":" ("+p.unit+")"),12.5f);
            if(p.toggle)
            {
                var button=R("Toggle "+p.key,row,new Vector2(1,1),Vector2.one,new Vector2(-58,-24),Vector2.zero);
                binding.toggle=MakeButton(button,"ON",()=>{Config.Set(p,p.read()>.5f?0:1);RefreshValues();});
                binding.button=button.GetComponent<UnityEngine.UI.Button>();return;
            }
            var value=R("Value "+p.key,row,Vector2.one,Vector2.one,new Vector2(-63,-21),Vector2.zero);
            var bg=value.gameObject.AddComponent<UnityEngine.UI.Image>();bg.color=new Color(.14f,.23f,.25f);
            var textArea=R("Text area",value,Vector2.zero,Vector2.one,new Vector2(4,0),new Vector2(-4,0));textArea.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var text=T(textArea,"",12);text.alignment=TextAlignmentOptions.MidlineRight;
            binding.input=value.gameObject.AddComponent<TMP_InputField>();binding.input.textViewport=textArea;binding.input.textComponent=text;
            binding.input.targetGraphic=bg;binding.input.contentType=TMP_InputField.ContentType.DecimalNumber;binding.input.characterLimit=8;
            binding.input.onEndEdit.AddListener(s=>
            {
                if(float.TryParse(s,NumberStyles.Float,CultureInfo.InvariantCulture,out float number))Config.Set(p,number);
                RefreshValues();
            });
            var sliderRoot=R("Slider "+p.key,row,Vector2.zero,new Vector2(1,0),new Vector2(5,0),new Vector2(-5,20));
            var hit=sliderRoot.gameObject.AddComponent<UnityEngine.UI.Image>();hit.color=Color.clear;
            var slider=sliderRoot.gameObject.AddComponent<UnityEngine.UI.Slider>();binding.slider=slider;
            slider.minValue=p.min;slider.maxValue=p.max;
            var track=R("Track",sliderRoot,new Vector2(0,.40f),new Vector2(1,.60f),Vector2.zero,Vector2.zero);
            track.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(.20f,.33f,.34f);
            var fill=R("Fill",track,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);fill.gameObject.AddComponent<UnityEngine.UI.Image>().color=Teal;slider.fillRect=fill;
            var handleArea=R("Handle area",sliderRoot,new Vector2(0,.15f),new Vector2(1,.85f),Vector2.zero,Vector2.zero);
            var thumb=R("Handle",handleArea,Vector2.zero,Vector2.up,new Vector2(-5,0),new Vector2(5,0));
            var thumbImage=thumb.gameObject.AddComponent<UnityEngine.UI.Image>();thumbImage.color=Pale;slider.handleRect=thumb;slider.targetGraphic=thumbImage;
            slider.onValueChanged.AddListener(v=>{Config.Set(p,v);binding.input.SetTextWithoutNotify(v.ToString("0.##",CultureInfo.InvariantCulture));});
        }
        public UnityEngine.UI.Slider GetSlider(string key)=>bindings[key].slider;
        public TMP_InputField GetInput(string key)=>bindings[key].input;
        public UnityEngine.UI.Button GetToggle(string key)=>bindings[key].button;
        public void SelectTab(int index)
        {
            tab=Mathf.Clamp(index,0,pages.Count-1);
            for(int i=0;i<pages.Count;i++){pages[i].gameObject.SetActive(i==tab);tabs[i].color=i==tab?Teal:Pale;}
            scroll.content=pages[tab];Canvas.ForceUpdateCanvases();UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(pages[tab]);
            scroll.StopMovement();scroll.verticalNormalizedPosition=1;RefreshValues();
            if(status)SetStatus(tab==0?"Time multipliers share the wave period":"Live updates · scroll for more");
        }
        public void SetOpen(bool open)
        {
            if(!open&&IsTyping&&EventSystem.current)EventSystem.current.SetSelectedGameObject(null);
            panel.gameObject.SetActive(open);toggleLabel.text=open?"HIDE  F1":"CONFIG  F1";
            if(open){RefreshValues();Canvas.ForceUpdateCanvases();WaveLabPanelDrag.Clamp(panel,(RectTransform)transform);}
        }
        public void RefreshValues()
        {
            foreach(var binding in bindings.Values)
            {
                if(binding.parameter.group!=tab)continue;float value=binding.parameter.read();
                if(binding.slider)binding.slider.SetValueWithoutNotify(value);
                if(binding.input&&!binding.input.isFocused)binding.input.SetTextWithoutNotify(value.ToString("0.##",CultureInfo.InvariantCulture));
                if(binding.toggle){binding.toggle.text=value>.5f?"ON":"OFF";binding.toggle.color=value>.5f?Teal:Color.gray;}
            }
        }
        void Attempt(Action operation,string success)
        {try{operation();RefreshValues();SetStatus(success);}catch(Exception e){SetStatus(e.Message);}}
        void SetStatus(string text){if(status)status.text=text;}
        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if(Keyboard.current!=null)
            {
                if(Keyboard.current.f1Key.wasPressedThisFrame)SetOpen(!IsOpen);
                else if(Keyboard.current.escapeKey.wasPressedThisFrame&&IsOpen)SetOpen(false);
            }
#endif
            if(IsOpen&&Time.unscaledTime>=nextRefresh){nextRefresh=Time.unscaledTime+.15f;RefreshValues();}
        }
        static RectTransform R(string name,Transform parent,Vector2 min,Vector2 max,Vector2 from,Vector2 to)
        {
            var r=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;r.SetParent(parent,false);
            r.anchorMin=min;r.anchorMax=max;r.offsetMin=from;r.offsetMax=to;return r;
        }
        static TextMeshProUGUI T(Transform parent,string text,float size,bool bold=false)
        {
            var r=R("Text",parent,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);var t=r.gameObject.AddComponent<TextMeshProUGUI>();
            t.text=text;t.fontSize=size;t.color=Pale;t.fontStyle=bold?FontStyles.Bold:FontStyles.Normal;t.raycastTarget=false;
            t.alignment=TextAlignmentOptions.MidlineLeft;t.textWrappingMode=TextWrappingModes.NoWrap;return t;
        }
        static TextMeshProUGUI MakeButton(RectTransform r,string title,Action click,float size=11)
        {
            var image=r.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=new Color(.13f,.26f,.29f);
            var button=r.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=image;button.onClick.AddListener(()=>click());
            var t=T(r,title,size,true);t.alignment=TextAlignmentOptions.Center;return t;
        }
    }
}
