using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace WaveLab.EditorTools
{
    public static class WaveLabSceneBuilder
    {
        public const string ScenePath="Assets/WaveLab/Scenes/ShorelineWaveLab.unity";
        [MenuItem("Wave Lab/Create or Open Wave Test")]
        public static void Open()
        {
            if(File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath);return; }
            Create();
        }
        public static string Create()
        {
            if(EditorApplication.isPlaying) return "Stop Play mode before creating the scene.";
            var previous=SceneManager.GetActiveScene();
            // Preserve an unsaved user's scene in memory, otherwise open the dedicated demo by itself.
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,previous.isDirty&&previous.path!=ScenePath?NewSceneMode.Additive:NewSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            var cameraObject=new GameObject("Wave Lab Camera",typeof(Camera),typeof(AudioListener));
            cameraObject.tag="MainCamera";var camera=cameraObject.GetComponent<Camera>();
            camera.orthographic=true;camera.orthographicSize=8;camera.nearClipPlane=.1f;camera.farClipPlane=100;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.10f,.12f);
            camera.transform.position=new Vector3(0,0,-20);camera.allowHDR=false;camera.allowMSAA=true;
            var root=new GameObject("WAVE LAB · select to tune parameters");var controller=root.AddComponent<WaveLabController>();
            controller.Configure(Shader.Find("WaveLab/Surf Layers"),Shader.Find("WaveLab/Beach Objects"),camera);
            controller.Rebuild();
            SaveGeneratedAssets(root);
            UpgradeImmersion();
            Directory.CreateDirectory("Assets/WaveLab/Scenes");
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
            Selection.activeGameObject=root;
            SetPortraitGameView();
            return ScenePath+" | "+controller.BodyCount+" objects | six independently switchable water layers";
        }
        [MenuItem("Wave Lab/Upgrade Object Immersion")]
        public static void UpgradeImmersion()
        {
            var c=Object.FindAnyObjectByType<WaveLabController>();
            if(!c)throw new System.Exception("Open the Wave Lab scene before upgrading.");
            if(EditorApplication.isPlaying)throw new System.Exception("Stop Play mode before upgrading the scene.");
            c.ResetSimulation();
            Directory.CreateDirectory("Assets/WaveLab/Generated");AssetDatabase.Refresh();
            Material MaterialAsset(string filename,string shaderName)
            {
                string path="Assets/WaveLab/Generated/"+filename+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(!material)
                {
                    Shader shader=Shader.Find(shaderName);if(!shader)throw new System.Exception("Shader not imported: "+shaderName);
                    material=new Material(shader){name=filename};AssetDatabase.CreateAsset(material,path);
                }
                return material;
            }
            Material below=MaterialAsset("Object-underwater","WaveLab/Beach Objects");
            Material above=MaterialAsset("Object-exposed","WaveLab/Beach Objects");
            Material foam=MaterialAsset("Object-contact-foam","WaveLab/Object Contact Foam");
            below.SetFloat("_StateEnabled",1);below.SetFloat("_WaterPass",0);
            above.SetFloat("_StateEnabled",1);above.SetFloat("_WaterPass",1);
            string meshPath="Assets/WaveLab/Generated/Contact-foam-quad.asset";
            Mesh quad=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(!quad)
            {
                quad=new Mesh{name="Sprite silhouette sampling quad"};
                quad.vertices=new[]{new Vector3(-1,-1),new Vector3(1,-1),new Vector3(1,1),new Vector3(-1,1)};
                quad.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};quad.triangles=new[]{0,1,2,0,2,3};quad.RecalculateBounds();
                AssetDatabase.CreateAsset(quad,meshPath);
            }
            var targets=new List<MeshRenderer>();var meshes=new List<Mesh>();
            foreach(var renderer in c.GetComponentsInChildren<MeshRenderer>())
                if(renderer.name.StartsWith("Collectible ")||renderer.name.StartsWith("Rock "))
                {targets.Add(renderer);meshes.Add(renderer.GetComponent<MeshFilter>().sharedMesh);}
            var silhouettes=FoamSilhouetteBaker.Bake(meshes);
            foreach(var renderer in targets)
            {
                var state=renderer.GetComponent<ShoreObjectState>();if(!state)state=renderer.gameObject.AddComponent<ShoreObjectState>();
                state.Configure(renderer,below,above,foam,quad);
                var slice=silhouettes[renderer.GetComponent<MeshFilter>().sharedMesh];
                state.ConfigureSilhouette(slice.atlas,slice.uvRect,slice.localRect,slice.distanceRange);
            }
            c.RegisterImmersionObjects();
            EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
            if(!string.IsNullOrEmpty(c.gameObject.scene.path))EditorSceneManager.SaveScene(c.gameObject.scene);
            AssetDatabase.SaveAssets();
        }
        static void SaveGeneratedAssets(GameObject root)
        {
            Directory.CreateDirectory("Assets/WaveLab/Generated");AssetDatabase.Refresh();
            var meshes=new Dictionary<Mesh,Mesh>();var materials=new Dictionary<Material,Material>();
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                Mesh source=filter.sharedMesh;
                if(!meshes.TryGetValue(source,out Mesh saved))
                {
                    string path="Assets/WaveLab/Generated/Mesh-"+meshes.Count.ToString("D2")+".asset";
                    saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if(saved)EditorUtility.CopySerialized(source,saved);else{saved=source;AssetDatabase.CreateAsset(saved,path);}
                    meshes[source]=saved;
                }
                filter.sharedMesh=saved;
            }
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                Material source=renderer.sharedMaterial;
                if(!materials.TryGetValue(source,out Material saved))
                {
                    string path="Assets/WaveLab/Generated/Material-"+materials.Count.ToString("D2")+".mat";
                    saved=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(saved)EditorUtility.CopySerialized(source,saved);else{saved=source;AssetDatabase.CreateAsset(saved,path);}
                    materials[source]=saved;
                }
                renderer.sharedMaterial=saved;
            }
        }
        public static void SetPortraitGameView()
        {
            var assembly=typeof(UnityEditor.Editor).Assembly;
            var gameViewType=assembly.GetType("UnityEditor.GameView");
            EditorWindow window=EditorWindow.GetWindow(gameViewType);
            var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
            var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            object sizes=singleton.GetProperty("instance").GetValue(null);
            object group=sizesType.GetMethod("GetGroup").Invoke(sizes,new object[]{0});
            var sizeType=assembly.GetType("UnityEditor.GameViewSize");var kind=assembly.GetType("UnityEditor.GameViewSizeType");
            int count=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);
            var newSize=System.Activator.CreateInstance(sizeType,new object[]{System.Enum.ToObject(kind,1),540,960,"Wave Lab 540x960"});
            group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{newSize});
            gameViewType.GetProperty("selectedSizeIndex",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic).SetValue(window,count);
            window.Show();window.Focus();
        }
        public static string Capture(float seconds,string name)
        {
            var c=Object.FindAnyObjectByType<WaveLabController>();if(!c)throw new System.Exception("Open the Wave Lab scene first.");
            bool paused=c.paused;c.paused=true;c.Seek(seconds);c.ApplyGlobals();
            Directory.CreateDirectory("Screenshots/WaveLab");
            string path="Screenshots/WaveLab/"+name+".png";
            Render(c.SceneCamera,540,960,path);c.paused=paused;return Path.GetFullPath(path);
        }
        public static void Render(Camera camera,int width,int height,string path,bool includeInterface=false)
        {
            var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);
            var old=camera.targetTexture;var active=RenderTexture.active;
            var oldRect=camera.rect;camera.rect=new Rect(0,0,1,1);camera.targetTexture=rt;
            Canvas ui=null;RenderMode oldMode=RenderMode.ScreenSpaceOverlay;Camera oldCamera=null;float oldPlane=0;
            if(includeInterface)
            {
                var hud=Object.FindAnyObjectByType<WaveLabHUD>();
                if(hud)
                {
                    hud.Refresh();
                    ui=hud.GetComponent<Canvas>();oldMode=ui.renderMode;oldCamera=ui.worldCamera;oldPlane=ui.planeDistance;
                    ui.renderMode=RenderMode.ScreenSpaceCamera;ui.worldCamera=camera;ui.planeDistance=1;Canvas.ForceUpdateCanvases();
                }
            }
            var request=new RenderPipeline.StandardRequest{destination=rt};
            if(GraphicsSettings.currentRenderPipeline!=null&&RenderPipeline.SupportsRenderRequest(camera,request))camera.SubmitRenderRequest(request);else camera.Render();
            RenderTexture.active=rt;var image=new Texture2D(width,height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
            camera.targetTexture=old;camera.rect=oldRect;RenderTexture.active=active;Object.DestroyImmediate(image);Object.DestroyImmediate(rt);
            if(ui){ui.renderMode=oldMode;ui.worldCamera=oldCamera;ui.planeDistance=oldPlane;}
        }
        public static string Validate()
        {
            var c=Object.FindAnyObjectByType<WaveLabController>();if(!c)throw new System.Exception("Missing wave controller.");
            foreach(var shader in new[]{Shader.Find("WaveLab/Surf Layers"),Shader.Find("WaveLab/Beach Objects")})
                if(!shader||ShaderUtil.ShaderHasError(shader))throw new System.Exception("Shader compile errors: "+shader);
            c.Seek(0);Vector2 start=c.Bodies[20].position;c.Seek(4.4f);Vector2 end=c.Bodies[20].position;
            float maxMove=0;
            foreach(var b in c.Bodies)
            {
                if(float.IsNaN(b.position.x)||float.IsNaN(b.position.y))throw new System.Exception("Invalid simulation state");
                maxMove=Mathf.Max(maxMove,Vector2.Distance(b.initial,b.position));
            }
            c.Seek(4.4f);if(Vector2.Distance(end,c.Bodies[20].position)>.0001f)throw new System.Exception("Seek is not deterministic.");
            if(maxMove<.1f)throw new System.Exception("Objects do not respond to the wave.");
            c.Seek(0);return "PASS: shader compilation, "+c.BodyCount+" objects, finite positions, deterministic seek, max displacement "+maxMove.ToString("F2");
        }
    }
}
