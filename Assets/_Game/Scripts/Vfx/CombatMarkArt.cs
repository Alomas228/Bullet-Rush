using System.Collections.Generic;
using UnityEngine;

internal enum CombatMarkShape
{
    Wet, IceChipped, IceCracked, IceIntact, Coordinator, ObjectiveCoordinator, Commanded,
    Blocker, Shield, Guard, Cache
}

// Units match the one-metre enemy cube. Facets are painted once, so small details
// stay readable on both the snowy map and the dark forest floor without extra lights.
internal static class CombatMarkArt
{
    internal sealed class Shape
    {
        internal readonly Vector3[] Points;
        internal readonly Color32[] Colors;
        internal Shape(List<Vector3> points, List<Color32> colors)
        {
            Points = points.ToArray();
            Colors = colors.ToArray();
        }
    }

    private static readonly Dictionary<CombatMarkShape, Shape> cache = new Dictionary<CombatMarkShape, Shape>();

    internal static Shape Get(CombatMarkShape kind)
    {
        if (!cache.TryGetValue(kind, out var shape))
        {
            var art = new Sculptor();
            art.Build(kind);
            cache.Add(kind, shape = new Shape(art.Points, art.Colors));
        }
        return shape;
    }

    private sealed class Sculptor
    {
        internal readonly List<Vector3> Points = new List<Vector3>();
        internal readonly List<Color32> Colors = new List<Color32>();
        private const int Plum = 0x48374F, Violet = 0x9463AE, Lilac = 0xD8B4EB;
        private const int Brass = 0x956642, Gold = 0xE1AD64, Cream = 0xFFF0BC;
        private const int Petrol = 0x365F70, Teal = 0x669EA8, Frost = 0xA4DFE4, Snow = 0xE3F8EF;
        private const int Dark = 0x35444A, Copper = 0xA86246, Orange = 0xE69C64;

        private void Tri(int rgb, Vector3 a, Vector3 b, Vector3 c)
        {
            var color = new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
            Points.Add(a); Points.Add(b); Points.Add(c);
            Colors.Add(color); Colors.Add(color); Colors.Add(color);
        }
        private void Quad(int color, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Tri(color, a, b, c); Tri(color, a, c, d);
        }
        private static Vector3 P(float x, float y, float z = 0) => new Vector3(x,y,z);

        // A little cut stone / enamel insert with four independently legible faces.
        private void Jewel(float x, float y, float w, float h, float z, int shade, int body, int shine)
        {
            var l = P(x-w,y,z); var r = P(x+w,y,z);
            var top = P(x,y+h,z); var foot = P(x,y-h,z); var ridge = P(x-w*.14f,y+h*.1f,z-.075f);
            Tri(shine,l,top,ridge); Tri(body,top,r,ridge);
            Tri(shade,r,foot,ridge); Tri(body,foot,l,ridge);
        }

        internal void Build(CombatMarkShape kind)
        {
            switch (kind)
            {
                case CombatMarkShape.Wet: Water(); break;
                case CombatMarkShape.IceChipped: Ice(5); break;
                case CombatMarkShape.IceCracked: Ice(9); break;
                case CombatMarkShape.IceIntact: Ice(12); break;
                case CombatMarkShape.Coordinator: Crown(false); break;
                case CombatMarkShape.ObjectiveCoordinator: Crown(true); break;
                case CombatMarkShape.Commanded:
                    Jewel(0,0,.23f,.25f,0,Plum,Violet,Lilac);
                    Jewel(0,.03f,.09f,.11f,-.09f,Violet,Lilac,0xEDD9F7); break;
                case CombatMarkShape.Blocker: Barricade(); break;
                case CombatMarkShape.Shield: Pavise(); break;
                case CombatMarkShape.Guard: GuardBadge(); break;
                case CombatMarkShape.Cache: Chest(); break;
                default: throw new System.ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private void Water()
        {
            // Asymmetric splash and one detached splashlet, sitting just above the floor.
            var edge = new[] { P(-.92f,-.46f,-.20f),P(-.75f,-.46f,-.58f),P(-.34f,-.46f,-.68f),
                P(-.06f,-.46f,-.58f),P(.34f,-.46f,-.76f),P(.80f,-.46f,-.55f),P(.87f,-.46f,-.20f),
                P(.67f,-.46f,.1f),P(.7f,-.46f,.44f),P(.34f,-.46f,.66f),P(-.23f,-.46f,.60f),P(-.72f,-.46f,.30f) };
            for(int i=0;i<edge.Length;i++)
            {
                Vector3 a = edge[i], b = edge[(i+1)%edge.Length];
                Vector3 innerA = P(a.x*.88f,-.448f,a.z*.85f), innerB = P(b.x*.88f,-.448f,b.z*.85f);
                Quad(0x3E717A,a,b,innerB,innerA); Tri(i%4 == 0 ? 0x70ADAD : 0x649CA3,P(0,-.447f,0),innerA,innerB);
            }
            Drop(-.61f,-.13f,-.34f,.205f,.53f);
            Drop(.60f,-.26f,-.39f,.135f,.33f);
            Quad(0xBBE6DA,P(.31f,-.436f,-.59f),P(.58f,-.436f,-.55f),P(.48f,-.436f,-.63f),P(.32f,-.436f,-.65f));
        }

        private void Drop(float x,float y,float z,float r,float h)
        {
            var tip = P(x+r*.30f,y+h,z);
            var foot = P(x,y-h*.35f,z);
            var belly = new Vector3[8];
            var neck = new Vector3[8];
            for (int i=0;i<belly.Length;i++)
            {
                float angle = i*Mathf.PI/4;
                float dx=Mathf.Cos(angle)*r, dz=Mathf.Sin(angle)*r;
                belly[i]=P(x+dx,y,z+dz);
                neck[i]=P(x+.04f*r+dx*.82f,y+.34f*h,z+dz*.82f);
            }
            for(int i=0;i<belly.Length;i++)
            {
                int j=(i+1)%belly.Length;
                int body=i==4 || i==5 ? 0x5BB1B8 : i==6 ? 0x4D99A8 : 0x367A89;
                Tri(body,neck[i],neck[j],tip);
                Quad(i==5 ? 0x99DBD3 : body,belly[i],belly[j],neck[j],neck[i]);
                Tri(i==5 ? 0x5896A0 : Petrol,belly[j],belly[i],foot);
            }
        }

        private void Ice(int sectors)
        {
            // One fitted shell. The lower edge runs over the four sides, with distinct
            // drips; successive hits remove contiguous sections all the way to the cap.
            var rim = new[] {
                P(-.46f,.56f,-.53f), P(-.10f,.60f,-.545f), P(.32f,.565f,-.54f),
                P(.535f,.58f,-.44f), P(.55f,.605f,-.07f), P(.53f,.56f,.36f),
                P(.41f,.59f,.535f), P(.035f,.61f,.55f), P(-.36f,.58f,.535f),
                P(-.535f,.55f,.41f), P(-.55f,.595f,.03f), P(-.53f,.58f,-.34f)
            };
            var hem = new[] { .27f,.34f,.20f,.30f,.40f,.23f,.32f,.38f,.29f,.17f,.37f,.31f };
            var top = P(-.09f,.74f,.045f);
            var shelf = new Vector3[rim.Length];
            for (int i=0;i<rim.Length;i++)
                shelf[i] = P(rim[i].x*.64f-.035f,.67f+(i%3)*.017f,rim[i].z*.63f+.018f);

            for (int i=0;i<sectors;i++)
            {
                int j=(i+1)%rim.Length;
                Vector3 a=rim[i], b=rim[j], inner=shelf[i], next=shelf[j];
                Tri(i%4==1 ? 0xCEE9EB : i%3==0 ? 0xDFF4F0 : 0xC5E6EC,top,inner,next);
                Tri(i%3==1 ? 0xC2E3EA : 0xA4CBDD,inner,a,b);
                Tri(i%4==0 ? 0xE5F4EE : 0xB9DCE5,inner,b,next);

                // The hanging lip lies outside the cube until its tip meets the side face.
                var lowA=P(a.x,hem[i],a.z); var lowB=P(b.x,hem[j],b.z);
                bool longTip = i==0 || i==4 || i==8;
                var tooth=P(a.x*.44f+b.x*.56f, longTip ? .055f : Mathf.Min(hem[i],hem[j])-.035f, a.z*.44f+b.z*.56f);
                Tri(i%3==0 ? 0x83B1C9 : 0x92C5D8, a,lowA,tooth);
                Tri(i%3==1 ? 0xC0E1E7 : 0xA8D4E0, a,tooth,b);
                Tri(i%3==0 ? 0xBEDDE5 : 0x81B1C9, b,tooth,lowB);
            }
            if(sectors < rim.Length)
            {
                // Exposed thickness along both new fracture edges.
                foreach(int i in new[] {0,sectors})
                {
                    var floorTop=P(top.x,.505f,top.z);
                    var floorInner=P(shelf[i].x,.505f,shelf[i].z);
                    var floorRim=P(rim[i].x,.505f,rim[i].z);
                    Quad(0x79A9C2,top,shelf[i],floorInner,floorTop);
                    Quad(0x92BCCE,shelf[i],rim[i],floorRim,floorInner);
                }
            }
        }

        private void Crown(bool objective)
        {
            // An octagonal coronet with a velvet cap and cast points, fitted around
            // the top of the unit. The large jewel identifies the objective from any azimuth.
            var corners = new[] { P(-.32f,0,-.525f),P(.32f,0,-.525f),P(.525f,0,-.32f),P(.525f,0,.32f),
                P(.32f,0,.525f),P(-.32f,0,.525f),P(-.525f,0,.32f),P(-.525f,0,-.32f) };
            for (int i=0;i<corners.Length;i++)
            {
                Vector3 a=corners[i], b=corners[(i+1)%corners.Length];
                Vector3 Low(Vector3 v,float y,float scale=1) => P(v.x*scale,y,v.z*scale);
                Vector3 footA=Low(a,.435f), footB=Low(b,.435f);
                Vector3 beltA=Low(a,.52f,1.025f), beltB=Low(b,.52f,1.025f);
                Vector3 rimA=Low(a,.65f), rimB=Low(b,.65f);
                bool side=(i%2)==0;
                Quad(i<3 ? Plum : 0x574158,footA,footB,beltB,beltA);
                Quad(objective ? (side ? Gold : Brass) : (side ? Violet : 0x73548F),beltA,beltB,rimB,rimA);
                var innerA=Low(a,.67f,.81f); var innerB=Low(b,.67f,.81f);
                Quad(objective ? 0xF0CE90 : Lilac,rimA,rimB,innerB,innerA);
                Tri(i%3==0 ? 0x8D638D : i%3==1 ? 0x795674 : 0x694B6B,innerA,innerB,P(0,.78f,0));
                var mid=(a+b)*.5f;
                // Broad triangular castings, with a thicker folded tip on the four short faces.
                var tip=Low(mid, side ? .83f : (objective ? .99f : .92f),1.11f);
                var ridge=Low(mid,.69f,1.08f);
                Tri(side ? 0xB69BC4 : Lilac,rimA,tip,ridge);
                Tri(side ? 0x896399 : 0x9C79AC,ridge,tip,rimB);
                Tri(Plum,innerA,innerB,tip);
            }
            if (objective)
            {
                // Amber cut stone on a low gold setting at the front of the cap.
                var foot=P(0,.745f,.16f); var peak=P(0,1.12f,.16f);
                for(int i=0;i<6;i++)
                {
                    float t=i*Mathf.PI/3f, u=(i+1)*Mathf.PI/3f;
                    Vector3 a=P(Mathf.Cos(t)*.19f,.90f,.16f+Mathf.Sin(t)*.18f);
                    Vector3 b=P(Mathf.Cos(u)*.19f,.90f,.16f+Mathf.Sin(u)*.18f);
                    Tri(i%2==0 ? Brass : Gold,foot,b,a);
                    Tri(i%3==0 ? Cream : i%3==1 ? Gold : 0xF3CA87,a,b,peak);
                }
            }
        }

        private void Barricade()
        {
            for(int side=-1;side<=1;side+=2)
                Box(side*.33f,.46f,.12f,.15f,.48f,.21f,Gold,Brass,Dark);
            Box(0,.65f,.06f,.98f,.19f,.20f,Orange,Brass,0xBB7E53);
            for(int i=-1;i<=1;i++)
                Quad(Cream,P(i*.25f-.12f,.66f,-.045f),P(i*.25f-.04f,.66f,-.045f),
                    P(i*.25f+.09f,.83f,-.045f),P(i*.25f+.01f,.83f,-.045f));
        }
        private void Pavise()
        {
            // Local +Z is also the direction used by ResolveDirectionalDamage.
            var tip=P(0,-.39f,.67f); var ridge=P(0,.13f,.80f); var top=P(0,.63f,.67f);
            for(int side=-1;side<=1;side+=2)
            {
                var wing=P(side*.55f,.43f,.53f); var foot=P(side*.47f,-.15f,.53f);
                Tri(side<0?Frost:Teal,top,wing,ridge); Quad(side<0?Teal:Petrol,wing,foot,tip,ridge);
            }
        }
        private void GuardBadge()
        {
            Quad(Brass,P(-.29f,.28f),P(.29f,.28f),P(.25f,-.06f),P(-.25f,-.06f));
            Tri(Brass,P(-.25f,-.06f),P(.25f,-.06f),P(0,-.29f));
            Jewel(0,.015f,.175f,.205f,-.035f,Brass,Gold,Cream);
        }

        private void Box(float x,float y,float z,float w,float h,float d,int top,int side,int front)
        {
            float l=x-w/2, r=x+w/2, near=z-d/2, far=z+d/2, roof=y+h;
            Quad(top,P(l,roof,near),P(r,roof,near),P(r,roof,far),P(l,roof,far));
            Quad(front,P(l,y,near),P(r,y,near),P(r,roof,near),P(l,roof,near));
            Quad(side,P(l,y,far),P(l,roof,far),P(r,roof,far),P(r,y,far));
            Quad(side,P(r,y,near),P(r,y,far),P(r,roof,far),P(r,roof,near));
            Quad(front,P(l,y,far),P(l,y,near),P(l,roof,near),P(l,roof,far));
        }
        private void Chest()
        {
            Box(0,0,0,1.42f,.54f,.91f,Brass,0x684D40,0x957053);
            Box(0,.54f,0,1.48f,.20f,.98f,0xCCA77A,0x87664F,0xAC8560);
            for(int i=-1;i<=1;i+=2) Box(i*.46f,0,0,.14f,.76f,1,Gold,Brass,0xC99152);
            Box(0,.32f,-.51f,.23f,.27f,.10f,Cream,Brass,Gold);
        }

    }
}
