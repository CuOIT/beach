using System.Collections.Generic;
using UnityEngine;

namespace WaveLab
{
    // Small illustrated meshes, generated locally. Each prop is one draw, not a stack of sprites.
    public sealed class BeachArt
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> triangles = new List<int>();
        public static Color C(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out Color c); return c; }
        public void Polygon(Vector2[] p, Color color, Color? centerColor = null)
        {
            Vector2 center = Vector2.zero;
            foreach (Vector2 v in p) center += v;
            center /= p.Length;
            int start = vertices.Count;
            vertices.Add(center); colors.Add(centerColor ?? color);
            for (int i = 0; i < p.Length; i++) { vertices.Add(p[i]); colors.Add(color); }
            for (int i = 0; i < p.Length; i++)
            { triangles.Add(start); triangles.Add(start + 1 + i); triangles.Add(start + 1 + (i + 1) % p.Length); }
        }
        public void Ellipse(Vector2 center, Vector2 size, Color color, Color? inner = null, int count = 28)
        {
            var p = new Vector2[count];
            for (int i = 0; i < count; i++)
            { float a = i * Mathf.PI * 2f / count; p[i] = center + new Vector2(Mathf.Cos(a) * size.x, Mathf.Sin(a) * size.y); }
            Polygon(p, color, inner);
        }
        public void Line(Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 n = new Vector2(-(b-a).y,(b-a).x).normalized * width;
            Polygon(new[] { a-n, b-n, b+n, a+n }, color);
        }
        public Mesh Mesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles,0); mesh.RecalculateBounds();
            return mesh;
        }
        public static Mesh Prop(int kind, int variant)
        {
            var a = new BeachArt();
            Color outline = C("305b59"), shine = C("fff1cc");
            a.Ellipse(new Vector2(.08f,-.25f),new Vector2(.69f,.28f),new Color(.07f,.15f,.13f,0),new Color(.07f,.15f,.13f,.28f));
            if (kind == 0)
            {
                for (int pass=0;pass<2;pass++)
                {
                    var p = new Vector2[40];
                    for(int i=0;i<p.Length;i++)
                    {
                        float angle = i * Mathf.PI * 2 / p.Length + .32f;
                        float radius = (.58f + .30f * Mathf.Cos((angle-.32f)*5)) * (pass==0?1.04f:1f);
                        p[i] = new Vector2(Mathf.Cos(angle),Mathf.Sin(angle)) * radius;
                    }
                    a.Polygon(p,pass==0?outline:C("369cac"),pass==0?outline:C("a7e9e1"));
                }
                for(int i=0;i<5;i++)
                {
                    float angle=i*Mathf.PI*2/5+.32f;
                    a.Line(Vector2.zero,new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*.65f,.018f,C("b6eee2"));
                }
                a.Ellipse(new Vector2(-.09f,.12f),Vector2.one*.055f,shine);
            }
            else if(kind==1)
            {
                Color baseColor=variant%2==0?C("dd713e"):C("558cba");
                Color light=variant%2==0?C("ffc668"):C("b9d6ed");
                a.Polygon(new[]{new Vector2(.43f,0),new Vector2(.95f,.40f),new Vector2(.83f,-.36f)},outline);
                a.Polygon(new[]{new Vector2(.48f,0),new Vector2(.88f,.32f),new Vector2(.79f,-.27f)},baseColor);
                a.Polygon(new[]{new Vector2(-.4f,.22f),new Vector2(-.08f,.62f),new Vector2(.36f,.30f)},baseColor);
                a.Ellipse(Vector2.zero,new Vector2(.71f,.41f),outline);
                a.Ellipse(new Vector2(-.025f,.025f),new Vector2(.65f,.355f),baseColor,light);
                for(int j=0;j<4;j++) for(int k=0;k<3;k++)
                    a.Ellipse(new Vector2(-.15f+j*.16f,-.17f+k*.16f),new Vector2(.068f,.043f),new Color(1,.87f,.63f,.30f));
                a.Ellipse(new Vector2(-.42f,.14f),new Vector2(.105f,.11f),shine);
                a.Ellipse(new Vector2(-.445f,.145f),new Vector2(.049f,.075f),C("163941"));
                a.Line(new Vector2(-.29f,-.19f),new Vector2(-.22f,.18f),.017f,outline);
                a.Ellipse(new Vector2(.02f,-.21f),new Vector2(.22f,.12f),baseColor);
            }
            else if(kind==2)
            {
                a.Ellipse(Vector2.zero,new Vector2(.65f,.56f),C("546798"));
                a.Ellipse(new Vector2(0,.035f),new Vector2(.60f,.51f),C("739bc3"),C("d0ebee"));
                for(int j=0;j<5;j++)
                {
                    float ang=j*Mathf.PI*2/5;
                    a.Ellipse(new Vector2(Mathf.Sin(ang)*.16f,Mathf.Cos(ang)*.16f+.025f),new Vector2(.095f,.11f),shine);
                }
                a.Ellipse(new Vector2(0,.04f),Vector2.one*.07f,C("a0d8dd"));
                a.Ellipse(new Vector2(-.23f,.28f),new Vector2(.12f,.05f),new Color(1,1,1,.7f));
            }
            else if(kind==3)
            {
                Color edge=variant%2==0?C("ad8292"):C("547e79");
                Color fill=variant%2==0?C("f0d9d0"):C("b7cabc");
                a.Ellipse(new Vector2(0,-.07f),new Vector2(.60f,.41f),edge);
                for(int j=0;j<9;j++)
                {
                    float angle=Mathf.Lerp(.15f,Mathf.PI-.15f,j/8f);
                    Vector2 tip=new Vector2(Mathf.Cos(angle)*.59f,Mathf.Sin(angle)*.66f-.13f);
                    a.Ellipse(tip,new Vector2(.15f,.16f),fill,shine,16);
                    a.Polygon(new[]{new Vector2(0,-.39f),tip+new Vector2(-.1f,0),tip+new Vector2(.1f,0)},fill,shine);
                    a.Line(new Vector2(0,-.34f),tip,.012f,edge);
                }
                a.Ellipse(new Vector2(0,-.36f),new Vector2(.17f,.08f),fill);
            }
            else
            {
                Color stem=variant%2==0?C("d7bbb0"):C("ddae56");
                Color tip=variant%2==0?C("fff0de"):C("ecdb74");
                for(int j=0;j<7;j++)
                {
                    float x=(j-3)*.17f, height=.36f+.25f*Mathf.Sin(j*8.4f+variant);
                    a.Line(new Vector2(x*.5f,-.35f),new Vector2(x,height),.10f,stem);
                    a.Line(new Vector2(x,.03f),new Vector2(x+.18f,height*.8f),.06f,stem);
                    a.Ellipse(new Vector2(x,height),Vector2.one*.10f,tip);
                    a.Ellipse(new Vector2(x+.18f,height*.8f),Vector2.one*.072f,tip);
                }
                a.Ellipse(new Vector2(0,-.34f),new Vector2(.5f,.11f),stem);
            }
            return a.Mesh("Beach prop " + kind + "-" + variant);
        }
        public static Mesh Rock(int seed)
        {
            var a=new BeachArt();
            a.Ellipse(new Vector2(.05f,-.28f),new Vector2(1.12f,.40f),new Color(.08f,.12f,.11f,0),new Color(.08f,.12f,.11f,.38f));
            var p=new[]{new Vector2(-.98f,-.2f),new Vector2(-.80f,.23f),new Vector2(-.26f,.61f),new Vector2(.38f,.57f),new Vector2(.96f,-.08f),new Vector2(.58f,-.39f),new Vector2(-.45f,-.42f)};
            a.Polygon(p,C("4b625c"));
            a.Polygon(new[]{p[0],p[1],p[2],new Vector2(.04f,.14f),new Vector2(-.24f,-.29f)},C("b7b79c"),C("d4cfaa"));
            a.Polygon(new[]{p[2],p[3],p[4],new Vector2(.04f,.14f)},C("a5b7a5"),C("c8cfb0"));
            a.Polygon(new[]{new Vector2(.04f,.14f),p[4],p[5],new Vector2(-.24f,-.29f)},C("576960"));
            a.Line(p[2],new Vector2(.04f,.14f),.022f,C("e0dbc1"));
            a.Line(new Vector2(.04f,.14f),p[4],.016f,C("d9d6b9"));
            return a.Mesh("Faceted coastal rock "+seed);
        }
        public static Mesh Mountains()
        {
            var a=new BeachArt();
            for(int layer=0;layer<3;layer++)
            {
                var p=new List<Vector2>{new Vector2(-6,5.32f)};
                for(int j=0;j<40;j++)
                {
                    float x=-6+j*12f/39;
                    float peak=Mathf.PerlinNoise(x*.69f+layer*3.1f,2.5f)*1.6f;
                    float height=5.3f+peak+Mathf.Pow(Mathf.Abs(Mathf.Sin(x*.65f+layer)),6)*1.25f;
                    p.Add(new Vector2(x,height-layer*.18f));
                }
                p.Add(new Vector2(6,5.32f));
                a.Polygon(p.ToArray(),layer==0?C("729295"):layer==1?C("4d777b"):C("2e5a61"));
            }
            return a.Mesh("Distant limestone silhouettes");
        }
    }
}
