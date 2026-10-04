using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WaveLab.EditorTools
{
    // Authoring-time only. Rasterize visible art alpha, ignoring translucent shadows.
    // Sprite art can supply the same binary mask from its texture alpha.
    public static class FoamSilhouetteBaker
    {
        public const int TileSize=128;
        const float RangePixels=12;
        const string Path="Assets/WaveLab/Generated/Object-silhouette-SDF.asset";
        public sealed class Slice
        {
            public Texture2D atlas;
            public Vector4 uvRect,localRect;
            public float distanceRange;
            public int tile;
        }
        sealed class Shape { public byte[] mask;public Vector4 rect; }
        public static Dictionary<Mesh,Slice> Bake(IEnumerable<Mesh> sources)
        {
            var shapes=new List<Shape>();var mappings=new Dictionary<Mesh,int>();
            foreach(var mesh in sources)
            {
                if(!mesh||mappings.ContainsKey(mesh))continue;
                var shape=Rasterize(mesh);int index=-1;
                for(int i=0;i<shapes.Count;i++)
                    if(shapes[i].rect==shape.rect&&Equal(shapes[i].mask,shape.mask)){index=i;break;}
                if(index<0){index=shapes.Count;shapes.Add(shape);}
                mappings.Add(mesh,index);
            }
            if(shapes.Count==0)throw new System.Exception("No visible silhouettes to bake.");
            int columns=Mathf.NextPowerOfTwo(Mathf.CeilToInt(Mathf.Sqrt(shapes.Count)));
            int rows=Mathf.NextPowerOfTwo(Mathf.CeilToInt(shapes.Count/(float)columns));
            int width=columns*TileSize,height=rows*TileSize;
            var pixels=new byte[width*height];
            for(int i=0;i<pixels.Length;i++)pixels[i]=255;
            for(int i=0;i<shapes.Count;i++)
            {
                var sdf=SignedDistance(shapes[i].mask);
                int ox=i%columns*TileSize,oy=i/columns*TileSize;
                for(int y=0;y<TileSize;y++)System.Array.Copy(sdf,y*TileSize,pixels,(oy+y)*width+ox,TileSize);
            }
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Path);bool create=!texture;
            if(create)texture=new Texture2D(width,height,TextureFormat.R8,false,true);
            else texture.Reinitialize(width,height,TextureFormat.R8,false);
            texture.name="Shared silhouette SDF - "+shapes.Count+" shapes";
            texture.wrapMode=TextureWrapMode.Clamp;texture.filterMode=FilterMode.Bilinear;texture.anisoLevel=0;
            texture.SetPixelData(pixels,0);texture.Apply(false,false);
            if(create)AssetDatabase.CreateAsset(texture,Path);else EditorUtility.SetDirty(texture);
            var result=new Dictionary<Mesh,Slice>();
            foreach(var mapping in mappings)
            {
                int i=mapping.Value;var rect=shapes[i].rect;
                result.Add(mapping.Key,new Slice{atlas=texture,tile=i,localRect=rect,
                    uvRect=new Vector4((float)(i%columns)/columns,(float)(i/columns)/rows,1f/columns,1f/rows),
                    distanceRange=RangePixels*rect.z/TileSize});
            }
            return result;
        }
        static bool Equal(byte[] a,byte[] b)
        {for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
        static Shape Rasterize(Mesh mesh)
        {
            var vertices=mesh.vertices;var colors=mesh.colors;var triangles=mesh.triangles;
            float Alpha(int i)=>colors.Length==vertices.Length?colors[i].a:1;
            Vector2 min=new Vector2(float.PositiveInfinity,float.PositiveInfinity);
            Vector2 max=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
            for(int i=0;i<vertices.Length;i++)if(Alpha(i)>=.5f)
            {min=Vector2.Min(min,vertices[i]);max=Vector2.Max(max,vertices[i]);}
            if(float.IsInfinity(min.x))throw new System.Exception("No opaque silhouette: "+mesh.name);
            Vector2 centre=(min+max)*.5f;
            float size=Mathf.Max(max.x-min.x,max.y-min.y)*1.24f;
            Vector2 origin=centre-Vector2.one*size*.5f;var mask=new byte[TileSize*TileSize];
            for(int t=0;t<triangles.Length;t+=3)
            {
                int ia=triangles[t],ib=triangles[t+1],ic=triangles[t+2];
                if(Mathf.Max(Alpha(ia),Mathf.Max(Alpha(ib),Alpha(ic)))<.5f)continue;
                Vector2 a=((Vector2)vertices[ia]-origin)*(TileSize/size);
                Vector2 b=((Vector2)vertices[ib]-origin)*(TileSize/size);
                Vector2 c=((Vector2)vertices[ic]-origin)*(TileSize/size);
                float denominator=(b.y-c.y)*(a.x-c.x)+(c.x-b.x)*(a.y-c.y);
                if(Mathf.Abs(denominator)<.000001f)continue;
                int x0=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x,Mathf.Min(b.x,c.x))),0,TileSize-1);
                int x1=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x,Mathf.Max(b.x,c.x))),0,TileSize-1);
                int y0=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y,Mathf.Min(b.y,c.y))),0,TileSize-1);
                int y1=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y,Mathf.Max(b.y,c.y))),0,TileSize-1);
                for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
                {
                    float px=x+.5f,py=y+.5f;
                    float u=((b.y-c.y)*(px-c.x)+(c.x-b.x)*(py-c.y))/denominator;
                    float v=((c.y-a.y)*(px-c.x)+(a.x-c.x)*(py-c.y))/denominator;
                    float w=1-u-v;
                    if(u>=0&&v>=0&&w>=0&&u*Alpha(ia)+v*Alpha(ib)+w*Alpha(ic)>=.5f)mask[y*TileSize+x]=255;
                }
            }
            return new Shape{mask=mask,rect=new Vector4(centre.x,centre.y,size,size)};
        }
        static byte[] SignedDistance(byte[] mask)
        {
            var toInside=Distance(mask,true);var toOutside=Distance(mask,false);var result=new byte[mask.Length];
            for(int i=0;i<result.Length;i++)
            {
                float d=mask[i]>0?-(toOutside[i]-.5f):toInside[i]-.5f;
                result[i]=(byte)Mathf.RoundToInt(Mathf.Clamp01(.5f+d/(2*RangePixels))*255);
            }
            return result;
        }
        static float[] Distance(byte[] mask,bool inside)
        {
            // Chamfer transform, run once in the Editor; no runtime contour extraction.
            var d=new float[mask.Length];
            for(int i=0;i<d.Length;i++)d[i]=(mask[i]>0)==inside?0:10000;
            for(int y=0;y<TileSize;y++)for(int x=0;x<TileSize;x++)
            {
                int i=y*TileSize+x;
                if(x>0)d[i]=Mathf.Min(d[i],d[i-1]+1);
                if(y>0){d[i]=Mathf.Min(d[i],d[i-TileSize]+1);
                    if(x>0)d[i]=Mathf.Min(d[i],d[i-TileSize-1]+1.414214f);
                    if(x<TileSize-1)d[i]=Mathf.Min(d[i],d[i-TileSize+1]+1.414214f);}
            }
            for(int y=TileSize-1;y>=0;y--)for(int x=TileSize-1;x>=0;x--)
            {
                int i=y*TileSize+x;
                if(x<TileSize-1)d[i]=Mathf.Min(d[i],d[i+1]+1);
                if(y<TileSize-1){d[i]=Mathf.Min(d[i],d[i+TileSize]+1);
                    if(x>0)d[i]=Mathf.Min(d[i],d[i+TileSize-1]+1.414214f);
                    if(x<TileSize-1)d[i]=Mathf.Min(d[i],d[i+TileSize+1]+1.414214f);}
            }
            return d;
        }
    }
}
