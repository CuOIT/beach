using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using WaveLab;
using WaveLab.EditorTools;
using Object=UnityEngine.Object;

public static class FoamContourActions
{
    public static string Upgrade()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play before upgrading.");
        if(!Object.FindAnyObjectByType<WaveLabController>())WaveLabSceneBuilder.Open();
        WaveLabSceneBuilder.UpgradeImmersion();
        var c=Object.FindAnyObjectByType<WaveLabController>();
        var texture=c.ImmersionObjects[0].SilhouetteAtlas;
        var tiles=new HashSet<Vector4>();
        foreach(var item in c.ImmersionObjects)
        {
            if(item.SilhouetteAtlas!=texture)throw new Exception("Objects are not sharing the atlas.");
            tiles.Add(item.SilhouetteUV);
        }
        return $"{c.ImmersionObjects.Count} objects; {tiles.Count} unique silhouettes; one {texture.width}x{texture.height} {texture.format} atlas, {texture.GetRawTextureData<byte>().Length} bytes. Scene saved.";
    }
    public static string CaptureAndValidate()
    {
        var c=Object.FindAnyObjectByType<WaveLabController>();
        c.paused=true;c.ResetSimulation();
        var camera=c.SceneCamera;var oldPosition=camera.transform.position;float oldSize=camera.orthographicSize;
        Color oldBackground=camera.backgroundColor;
        var objects=c.ImmersionObjects;
        var active=new bool[objects.Count];
        for(int i=0;i<objects.Count;i++){active[i]=objects[i].gameObject.activeSelf;objects[i].gameObject.SetActive(false);}
        string folder="Screenshots/WaveLab/Contour";Directory.CreateDirectory(folder);
        const int size=400;
        var sheet=new Texture2D(size*3,size*3,TextureFormat.RGB24,false);
        string[] kinds={"Shell","Starfish","Coral"};float[] depths={.75f,.5f,.25f};
        int tested=0,totalFoamPixels=0;float maxOutside=0;
        try
        {
            foreach(var name in new[]{"WaveLab/Beach Objects","WaveLab/Object Contact Foam"})
                if(ShaderUtil.ShaderHasError(Shader.Find(name)))throw new Exception("Shader failed: "+name);
            for(int row=0;row<3;row++)
            {
                ShoreObjectState target=null;
                foreach(var item in objects)if(item.name.Contains(kinds[row])){target=item;break;}
                if(!target)throw new Exception("Missing art: "+kinds[row]);
                Vector3 original=target.transform.position;Quaternion rotation=target.transform.rotation;
                target.gameObject.SetActive(true);
                try
                {
                    for(int column=0;column<3;column++)
                    {
                        // The middle view also verifies that a rotated sprite's mask follows it.
                        target.transform.rotation=Quaternion.Euler(0,0,column==1?23:0);
                        float lo=-4.5f,hi=4.5f;
                        for(int iteration=0;iteration<20;iteration++)
                        {
                            float y=(lo+hi)*.5f;target.transform.position=new Vector3(0,y,0);c.ApplyGlobals();
                            if(target.SubmergedFraction<depths[column])lo=y;else hi=y;
                        }
                        Bounds b=target.SubmergedRenderer.bounds;
                        camera.transform.position=new Vector3(b.center.x,b.center.y,oldPosition.z);
                        camera.orthographicSize=Mathf.Max(b.extents.x,b.extents.y)*1.55f;
                        string path=$"{folder}/{kinds[row]}-{depths[column]:F2}.png";
                        WaveLabSceneBuilder.Render(camera,size,size,path);
                        var cell=new Texture2D(2,2,TextureFormat.RGB24,false);
                        cell.LoadImage(File.ReadAllBytes(path));sheet.SetPixels(column*size,(2-row)*size,size,size,cell.GetPixels());Object.DestroyImmediate(cell);

                        // Render only contact foam against black, then check every visible
                        // pixel against the actual silhouette SDF. A detached ellipse fails
                        // this geometric condition even if its material/shader compiles.
                        var renderers=c.GetComponentsInChildren<MeshRenderer>(true);
                        var enabled=new bool[renderers.Length];
                        for(int i=0;i<renderers.Length;i++){enabled[i]=renderers[i].enabled;renderers[i].enabled=false;}
                        target.ContactFoam.enabled=true;camera.backgroundColor=Color.black;
                        try
                        {
                            string maskPath=$"{folder}/{kinds[row]}-{depths[column]:F2}-foam.png";
                            WaveLabSceneBuilder.Render(camera,size,size,maskPath);
                            var pixels=new Texture2D(2,2,TextureFormat.RGB24,false);pixels.LoadImage(File.ReadAllBytes(maskPath));
                            var colors=pixels.GetPixels32();Object.DestroyImmediate(pixels);
                            var properties=new MaterialPropertyBlock();target.ContactFoam.GetPropertyBlock(properties);
                            float width=properties.GetFloat("_FoamWidth");
                            float range=properties.GetFloat("_SdfRange");Vector4 uv=target.SilhouetteUV;
                            float pixelWorld=camera.orthographicSize*2/size;
                            var inverse=target.ContactFoam.transform.worldToLocalMatrix;
                            int count=0;
                            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                            {
                                Color32 color=colors[y*size+x];if(color.r<26&&color.g<26&&color.b<26)continue;
                                count++;
                                Vector3 world=new Vector3(camera.transform.position.x+(x+.5f-size*.5f)*pixelWorld,
                                    camera.transform.position.y+(y+.5f-size*.5f)*pixelWorld,0);
                                Vector3 local=inverse.MultiplyPoint3x4(world);
                                float u=local.x*.5f+.5f,v=local.y*.5f+.5f;
                                float outside=(target.SilhouetteAtlas.GetPixelBilinear(uv.x+u*uv.z,uv.y+v*uv.w).r-.5f)*2*range;
                                maxOutside=Mathf.Max(maxOutside,outside);
                                if(outside>width*1.4f+pixelWorld*2)throw new Exception("Detached foam outside silhouette: "+outside);
                            }
                            if(count<8)throw new Exception("Contact foam missing: "+kinds[row]+" / "+depths[column]);
                            tested++;totalFoamPixels+=count;
                        }
                        finally
                        {
                            for(int i=0;i<renderers.Length;i++)renderers[i].enabled=enabled[i];
                            camera.backgroundColor=oldBackground;
                        }
                    }
                }
                finally {target.transform.position=original;target.transform.rotation=rotation;target.gameObject.SetActive(false);}
            }
            sheet.Apply();File.WriteAllBytes("Screenshots/WaveLab/09-sprite-contour-depths.png",sheet.EncodeToPNG());
            string result=$"PASS: {tested} GPU views of shell/starfish/coral at 75/50/25% depth, including 23-degree rotation; {totalFoamPixels} foam pixels; all within the sprite contact boundary; max outside distance {maxOutside:F4} world units.";
            File.WriteAllText("Screenshots/WaveLab/contour-validation.txt",result);return result;
        }
        finally
        {
            for(int i=0;i<objects.Count;i++)objects[i].gameObject.SetActive(active[i]);
            camera.transform.position=oldPosition;camera.orthographicSize=oldSize;camera.backgroundColor=oldBackground;
            Object.DestroyImmediate(sheet);c.ResetSimulation();c.paused=false;
        }
    }
}
