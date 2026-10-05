using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace WaveLab
{
    [ExecuteAlways]
    public sealed partial class WaveLabController : MonoBehaviour
    {
        [Header("Reference timing / one shared clock")]
        [Range(5,16)] public float period=9.4f;
        [Range(.5f,3.6f)] public float runupReach=2.45f;
        [Range(0,2)] public float currentStrength=1f;
        [Range(.3f,2)] public float foamAmount=1f;
        [Range(.1f,2)] public float playbackSpeed=1f;
        public bool paused;
        public bool showWater=true, showVeil=true, showFoam=true, showBreaker=true, moveObjects=true;
        public bool showInterface=true;
        [Header("Object depth / slope of the seabed")]
        public bool showObjectImmersion=true;
        [Range(.05f,.4f)] public float beachSlope=.18f;
        [Range(0,2)] public float objectFoamAmount=1f;
        [Header("Feel / relative phase durations")]
        [Range(.25f,3)] public float approachTime=1,advanceTime=1,holdTime=1,recedeTime=1,restTime=1;
        [Header("Feel / object transport")]
        [Range(0,.6f)] public float lateralDrift=.18f;
        [Range(0,2)] public float breakerPush=.95f,swashPush=.55f;
        [Range(1,30)] public float acceleration=8,settleDamping=12;
        [Range(0,1)] public float rockSteering=.40f,objectSteering=.25f;
        [Range(0,25)] public float turnStrength=7,turnLimit=7;
        [Header("Feel / water and emergence")]
        [Range(.5f,2)] public float objectHeight=1;
        [Range(.03f,.4f)] public float waterlineSoftness=.20f;
        [Range(0,2)] public float waterlineCurve=1,waterlineRoughness=1,submergedTint=1;
        [Range(.15f,1.25f)] public float submergedOpacity=1;
        [Range(.25f,2)] public float crestThickness=1;
        [Range(0,2)] public float crestRoughness=1;
        [Header("Feel / foam and water appearance")]
        [Range(.25f,2.5f)] public float shoreFoamWidth=1,foamScale=1,contactFoamWidth=1;
        [Range(0,3)] public float foamFlow=1,contactFoamSpeed=1;
        [Range(.65f,.98f)] public float foamFadeEnd=.89f;
        [Range(0,1)] public float contactFoamBreakup=1;
        [Range(0,2)] public float waterOpacity=1,veilOpacity=1,reflections=1,wetSandStrength=1;
        [SerializeField] List<ShoreObjectState> immersionObjects=new List<ShoreObjectState>();
        public int seed=913;
        [SerializeField] float clock;
        [SerializeField] Shader surfShader;
        [SerializeField] Shader objectShader;
        [SerializeField] Camera sceneCamera;
        [SerializeField] Transform generatedRoot;
        [SerializeField] List<Body> bodies=new List<Body>();
        [SerializeField] List<MeshRenderer> layers=new List<MeshRenderer>();
        [SerializeField] Vector4[] rocks=new Vector4[8];
        readonly ShoreObjectState[] rockStates=new ShoreObjectState[8];
        readonly float[] rockExposure=new float[8];
        [NonSerialized] readonly List<UnityEngine.Object> generatedResources=new List<UnityEngine.Object>();
        float accumulator;
        bool runtimeReady;
        Body dragged;
        Vector2 lastPointer;
        int activePointerId=int.MinValue;
        bool touchPointer;
        public float Clock => clock;
        public float CycleProgress => Mathf.Repeat(clock/Mathf.Max(.1f,period),1f);
        public float Phase => SurfMath.RemapPhase(CycleProgress,approachTime,advanceTime,holdTime,recedeTime,restTime);
        public void SetPeriod(float value) {clock=clock/Mathf.Max(.1f,period)*value;period=value;}
        public Camera SceneCamera => sceneCamera;
        public int BodyCount => bodies.Count;
        public IReadOnlyList<Body> Bodies => bodies;
        public IReadOnlyList<ShoreObjectState> ImmersionObjects=>immersionObjects;
        public void RegisterImmersionObjects()
        {
            immersionObjects.Clear();
            if(generatedRoot)immersionObjects.AddRange(generatedRoot.GetComponentsInChildren<ShoreObjectState>());
            CacheRockStates();
            ApplyGlobals();
        }
        void CacheRockStates()
        {
            Array.Clear(rockStates,0,rockStates.Length);
            foreach(var item in immersionObjects)
                if(item&&item.name.StartsWith("Rock ")&&int.TryParse(item.name.Substring(5),out int number)&&number>0&&number<=8)
                    rockStates[number-1]=item;
        }
        public int CountImmersion(ImmersionState state)
        {
            int count=0;foreach(var item in immersionObjects)if(item&&item.State==state)count++;
            return count;
        }

        [Serializable] public sealed class Body
        {
            public Transform transform;
            public MeshRenderer renderer;
            public Vector2 initial, position, velocity;
            public float initialRotation, rotation, spin, radius, buoyancy, bobSeed;
        }

        public void Configure(Shader surf,Shader objects,Camera camera)
        { surfShader=surf;objectShader=objects;sceneCamera=camera; }
        void OnEnable() { CacheRockStates();ApplyGlobals(); }
        void Start() { if(Application.isPlaying) InitializeRuntime(); }
        void InitializeRuntime()
        {
            if(runtimeReady) return;
            runtimeReady=true;Application.runInBackground=true;
            if(generatedRoot==null) Rebuild();
            ResetSimulation();
            if(showInterface && !UnityEngine.Object.FindAnyObjectByType<WaveLabHUD>()) WaveLabHUD.Create(this);
        }
        void OnValidate() { if(isActiveAndEnabled) ApplyGlobals(); }
        void Update()
        {
            if(!Application.isPlaying) { runtimeReady=false;ApplyGlobals();return; }
            if(!runtimeReady) InitializeRuntime();
            if(!IsSeeking)HandlePointer();
            if(IsSeeking)AdvanceSeek(8,3);
            else if(!paused)
            {
                accumulator+=Mathf.Min(Time.unscaledDeltaTime,.10f)*playbackSpeed;
                while(accumulator>=1f/60f) { Step(1f/60f);accumulator-=1f/60f; }
            }
            ApplyGlobals();
        }
        void HandlePointer()
        {
#if ENABLE_INPUT_SYSTEM
            bool typing=WaveLabConfigPanel.IsTyping;
            if(Keyboard.current!=null&&!typing)
            {
                if(Keyboard.current.spaceKey.wasPressedThisFrame) paused=!paused;
                if(Keyboard.current.rKey.wasPressedThisFrame) ResetSimulation();
            }
            if(sceneCamera==null)return;
            // Track the finger that began the drag; other fingers cannot take it over.
            if(Touchscreen.current!=null)
            {
                foreach(var touch in Touchscreen.current.touches)
                {
                    int id=touch.touchId.ReadValue();
                    if(touchPointer&&activePointerId==id)
                    {
                        UpdatePointer(touch.position.ReadValue(),false,!touch.press.isPressed,id);
                        return;
                    }
                    if(dragged==null&&touch.press.wasPressedThisFrame)
                    {
                        UpdatePointer(touch.position.ReadValue(),true,false,id);
                        if(dragged!=null){touchPointer=true;activePointerId=id;}
                        return;
                    }
                }
            }
            if(touchPointer){dragged=null;touchPointer=false;activePointerId=int.MinValue;return;}
            if(Mouse.current!=null)
                UpdatePointer(Mouse.current.position.ReadValue(),Mouse.current.leftButton.wasPressedThisFrame,
                    Mouse.current.leftButton.wasReleasedThisFrame,-1);
#endif
        }
        void UpdatePointer(Vector2 screen,bool pressed,bool released,int pointerId)
        {
            Vector2 p=sceneCamera.ScreenToWorldPoint(new Vector3(screen.x,screen.y,20));
            bool overUI=WaveLabConfigPanel.BlocksPointer(screen);
            // Raycast at the current position: Input System UI state can lag Update by a frame.
            var events=UnityEngine.EventSystems.EventSystem.current;
            if(events!=null&&pressed)
            {
                var hits=new List<UnityEngine.EventSystems.RaycastResult>();
                events.RaycastAll(new UnityEngine.EventSystems.PointerEventData(events){position=screen,pointerId=pointerId},hits);
                overUI|=hits.Count>0;
            }
            if(pressed&&!overUI)
            {
                float best=100;
                foreach(Body b in bodies)
                {
                    float dist=Vector2.Distance(p,b.position);
                    if(dist<b.radius*1.4f&&dist<best) { dragged=b;best=dist; }
                }
                lastPointer=p;
            }
            if(dragged!=null)
            {
                dragged.position=p;dragged.velocity=(p-lastPointer)/Mathf.Max(Time.unscaledDeltaTime,.01f)*.15f;
                dragged.transform.position=new Vector3(p.x,p.y,0);lastPointer=p;
                if(released){dragged=null;touchPointer=false;activePointerId=int.MinValue;}
            }
        }
        public void ResetSimulation()
        {
            CancelSeek();
            ResetState();
            ApplyGlobals();
        }
        void ResetState()
        {
            clock=0;accumulator=0;dragged=null;touchPointer=false;activePointerId=int.MinValue;
            foreach(Body b in bodies)
            {
                b.position=b.initial;b.velocity=Vector2.zero;b.rotation=b.initialRotation;b.spin=0;
                if(b.transform) b.transform.SetPositionAndRotation(new Vector3(b.position.x,b.position.y,0),Quaternion.Euler(0,0,b.rotation));
            }
        }
        public void Seek(float seconds)
        {
            BeginSeek(seconds);
            AdvanceSeek(int.MaxValue,0);
        }
        public void Step(float dt)
        {
            clock+=dt;
            if(!moveObjects) return;
            float phase=Phase;
            foreach(Body b in bodies)
            {
                if(b==dragged||!b.transform) continue;
                Vector2 before=b.position;
                float sideBias=Mathf.Sin(b.bobSeed*2.17f);
                Vector2 current=SurfMath.Current(b.position,phase,runupReach,sideBias,breakerPush,swashPush,lateralDrift)*currentStrength*b.buoyancy;
                // Steer gently around rocks while the wave is pushing; no idle repulsion.
                for(int i=0;i<rocks.Length;i++)
                {
                    Vector2 rp=new Vector2(rocks[i].x,rocks[i].y);
                    Vector2 delta=b.position-rp;
                    Vector2 scale=new Vector2(rocks[i].z+b.radius*.65f,rocks[i].w+b.radius*.5f);
                    float length=new Vector2(delta.x/scale.x,delta.y/scale.y).magnitude;
                    if(delta.y>0&&length<1.8f)
                    {
                        float side=Mathf.Abs(delta.x)>.04f?Mathf.Sign(delta.x):Mathf.Sign(sideBias);
                        current.x+=side*(-current.y)*rockSteering*(1-SurfMath.Smooth(.9f,1.8f,length));
                    }
                }
                foreach(Body other in bodies)
                {
                    if(other==b)continue;
                    Vector2 delta=b.position-other.position;
                    float spacing=(b.radius+other.radius)*.80f;
                    float distance=delta.magnitude;
                    if(distance<spacing&&distance>.001f)
                    {
                        float side=Mathf.Abs(delta.x)>.02f?Mathf.Sign(delta.x):Mathf.Sign(sideBias);
                        current.x+=side*(-current.y)*objectSteering*(1-distance/spacing);
                    }
                }
                float response=current.sqrMagnitude>b.velocity.sqrMagnitude?acceleration:settleDamping;
                b.velocity=Vector2.Lerp(b.velocity,current,1-Mathf.Exp(-response*dt));
                b.velocity.y=Mathf.Min(0,b.velocity.y);
                if(b.velocity.sqrMagnitude<.000001f)b.velocity=Vector2.zero;
                b.position+=b.velocity*dt;
                // A rock may stop the shoreward step, but cannot bounce a prop offshore.
                for(int i=0;i<rocks.Length;i++)
                {
                    Vector4 r=rocks[i];float rx=r.z+b.radius*.65f,ry=r.w+b.radius*.5f;
                    float dx=(b.position.x-r.x)/rx;
                    if(Mathf.Abs(dx)<1&&before.y>=r.y)
                    {
                        float edge=r.y+ry*Mathf.Sqrt(1-dx*dx);
                        if(b.position.y<edge)b.position.y=Mathf.Min(before.y,edge);
                    }
                }
                b.position.x=Mathf.Clamp(b.position.x,-4.22f,4.22f);
                b.position.y=Mathf.Clamp(b.position.y,-5.7f,4.95f);
                b.spin=Mathf.Lerp(b.spin,b.velocity.x*turnStrength,1-Mathf.Exp(-dt*5));
                b.rotation=Mathf.Clamp(b.rotation+b.spin*dt,b.initialRotation-turnLimit,b.initialRotation+turnLimit);
                b.transform.SetPositionAndRotation(new Vector3(b.position.x,b.position.y,0),Quaternion.Euler(0,0,b.rotation));
                b.renderer.sortingOrder=500+Mathf.RoundToInt((5-b.position.y)*10);
            }
        }
        public void ApplyGlobals()
        {
            Shader.SetGlobalFloat("_SurfTime",clock);Shader.SetGlobalFloat("_SurfPhase",Phase);
            Shader.SetGlobalFloat("_SurfReach",runupReach);Shader.SetGlobalFloat("_SurfFoam",foamAmount);
            Shader.SetGlobalFloat("_SurfWater",showVeil?1:0);Shader.SetGlobalVectorArray("_SurfRocks",rocks);
            Shader.SetGlobalVector("_SurfCrestFeel",new Vector4(crestThickness,crestRoughness,shoreFoamWidth,foamScale));
            Shader.SetGlobalVector("_SurfFoamFeel",new Vector4(foamFlow,foamFadeEnd,contactFoamSpeed,contactFoamBreakup));
            Shader.SetGlobalVector("_SurfAppearance",new Vector4(waterOpacity,veilOpacity,reflections,wetSandStrength));
            Shader.SetGlobalVector("_ObjectFeel",new Vector4(waterlineCurve,waterlineRoughness,submergedTint,submergedOpacity));
            for(int i=0;i<layers.Count;i++) if(layers[i])
            {
                bool visible=i==0||i==1&&showWater||i==2&&showVeil||i==3&&showFoam||i==4&&showBreaker||i==5&&showWater;
                layers[i].enabled=visible;
            }
            foreach(var item in immersionObjects)if(item)item.Refresh(this);
            // A completely submerged rock must not punch a dry hole in surface foam.
            for(int i=0;i<rockExposure.Length;i++)
                rockExposure[i]=showObjectImmersion&&rockStates[i]?1-SurfMath.Smooth(.65f,.98f,rockStates[i].SubmergedFraction):1;
            Shader.SetGlobalFloatArray("_SurfRockExposure",rockExposure);
        }
        void OnDestroy(){ReleaseGeneratedResources();}
        void ReleaseGeneratedResources()
        {
            foreach(UnityEngine.Object resource in generatedResources)if(resource)
            {
#if UNITY_EDITOR
                if(UnityEditor.AssetDatabase.Contains(resource))continue;
#endif
                if(Application.isPlaying)Destroy(resource);else DestroyImmediate(resource);
            }
            generatedResources.Clear();
        }
        public void Rebuild()
        {
            if(generatedRoot) DestroyImmediate(generatedRoot.gameObject);
            ReleaseGeneratedResources();
            bodies.Clear();layers.Clear();immersionObjects.Clear();
            generatedRoot=new GameObject("Wave layers and beach objects").transform;generatedRoot.SetParent(transform,false);
            if(!surfShader) surfShader=Shader.Find("WaveLab/Surf Layers");
            if(!objectShader) objectShader=Shader.Find("WaveLab/Beach Objects");
            if(!surfShader||!objectShader) throw new InvalidOperationException("WaveLab shaders have not imported.");
            var random=new System.Random(seed);
            float Rand(float a,float b)=>a+(float)random.NextDouble()*(b-a);
            Mesh quad=new Mesh{name="Water field quad"};
            quad.vertices=new[]{new Vector3(-7,-8),new Vector3(7,-8),new Vector3(7,8),new Vector3(-7,8)};
            quad.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};quad.triangles=new[]{0,1,2,0,2,3};quad.RecalculateBounds();generatedResources.Add(quad);
            string[] names={"00 · Sand and sky","01 · Turquoise water / reflection","03 · Submerged water veil","04 · Shoreline lace and foam","05 · Travelling breaking crest","02 · Wet sand memory"};
            int[] sorting={0,10,400,410,420,15};
            for(int i=0;i<6;i++)
            {
                var mat=new Material(surfShader){name=names[i]};mat.SetFloat("_Layer",i);generatedResources.Add(mat);
                layers.Add(Renderer(names[i],quad,mat,sorting[i],Vector2.zero,Vector2.one));
            }
            var objectMat=new Material(objectShader){name="Illustrated objects / shared immersion"};generatedResources.Add(objectMat);
            var dryMat=new Material(objectShader){name="Dry rock and horizon"};dryMat.SetFloat("_Submerge",0);generatedResources.Add(dryMat);
            Mesh mountains=BeachArt.Mountains();generatedResources.Add(mountains);Renderer("Far limestone coast",mountains,dryMat,30,Vector2.zero,Vector2.one);
            Vector2[] rockPos={new Vector2(-2.15f,-.55f),new Vector2(2.8f,.15f),new Vector2(.65f,-1.45f),new Vector2(-2.6f,-3.10f),new Vector2(1.1f,2.3f),new Vector2(-2.3f,3.4f),new Vector2(3.3f,-2.15f),new Vector2(.1f,4.1f)};
            for(int i=0;i<rocks.Length;i++)
            {
                float size=i<4?Rand(.62f,.9f):Rand(.37f,.64f);
                rocks[i]=new Vector4(rockPos[i].x,rockPos[i].y,size*.96f,size*.53f);
                Mesh mesh=BeachArt.Rock(i);generatedResources.Add(mesh);
                Renderer("Rock "+(i+1),mesh,i<4?dryMat:objectMat,500+Mathf.RoundToInt((5-rockPos[i].y)*10),rockPos[i],new Vector2(size,size));
            }
            var meshBank=new Mesh[10];
            for(int i=0;i<10;i++) { meshBank[i]=BeachArt.Prop(i/2,i%2);generatedResources.Add(meshBank[i]); }
            for(int i=0;i<85;i++)
            {
                float y=Rand(-4.65f,4.45f),x=Rand(-4.15f,4.15f);
                if(i<14) { x=-3.9f+(i%7)*1.25f+Rand(-.18f,.18f);y=-3.8f+(i/7)*1.1f; }
                float scale=Mathf.Lerp(.53f,.28f,Mathf.InverseLerp(-4.8f,4.5f,y))*Rand(.82f,1.08f);
                int kind=random.Next(10);
                var render=Renderer("Collectible "+i+" / "+new[]{"Starfish","Fish","Sand dollar","Shell","Coral"}[kind/2],meshBank[kind],objectMat,500+Mathf.RoundToInt((5-y)*10),new Vector2(x,y),Vector2.one*scale);
                float rotation=Rand(-18,18);render.transform.rotation=Quaternion.Euler(0,0,rotation);
                bodies.Add(new Body{transform=render.transform,renderer=render,initial=new Vector2(x,y),position=new Vector2(x,y),initialRotation=rotation,rotation=rotation,radius=scale*.68f,buoyancy=Rand(.65f,1.2f),bobSeed=Rand(0,20)});
            }
            var catalog=Resources.Load<WaveLabImmersionCatalog>(WaveLabImmersionCatalog.ResourcePath);
            if(!catalog)throw new InvalidOperationException("Missing WaveLab immersion catalog. Run Wave Lab > Upgrade Object Immersion.");
            foreach(var renderer in generatedRoot.GetComponentsInChildren<MeshRenderer>())
                if(renderer.name.StartsWith("Rock ")||renderer.name.StartsWith("Collectible "))catalog.Configure(renderer);
            RegisterImmersionObjects();
            ResetSimulation();
        }
        MeshRenderer Renderer(string name,Mesh mesh,Material material,int order,Vector2 pos,Vector2 scale)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(generatedRoot,false);
            go.transform.position=new Vector3(pos.x,pos.y,0);go.transform.localScale=new Vector3(scale.x,scale.y,1);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.sortingOrder=order;
            r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;return r;
        }
    }
}
