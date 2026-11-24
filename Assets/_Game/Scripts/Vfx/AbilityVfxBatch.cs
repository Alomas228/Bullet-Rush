using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Two reusable meshes: faceted cores and soft additive halos. Limited pooled
// point lights illuminate the environment on desktop, with no shadow passes.
[DefaultExecutionOrder(310)]
public sealed class AbilityVfxBatch : MonoBehaviour
{
    private enum Shape { Ring, Arc, Beam, Star, Thunder, Frost, Gravity, Flame, Lightning, Lance }
    private struct Mark
    {
        public Shape Shape;
        public Vector3 Center, Direction;
        public float Radius, Start, Duration;
        public Color Color;
    }
    private const int MaxMarks = 48;
    private const int DesktopVertexBudget = 42000, MobileVertexBudget = 24000;
    private const int DesktopGlowBudget = 9000, MobileGlowBudget = 4500;
    private const int LightCapacity = 2;
    private static AbilityVfxBatch instance;
    private static bool unavailable;
    private readonly List<Mark> marks = new(MaxMarks);
    private readonly List<Vector3> vertices = new(8192);
    private readonly List<Color> colors = new(8192);
    private readonly List<Vector3> normals = new(8192);
    private readonly List<int> indices = new(8192);
    private readonly List<Vector3> glowVertices = new(4096);
    private readonly List<Color> glowColors = new(4096);
    private readonly List<Vector2> glowUv = new(4096);
    private readonly List<int> glowIndices = new(4096);
    private static readonly Vector3 FaceLight = new Vector3(-.45f,.9f,-.45f).normalized;
    private Mesh mesh, glowMesh;
    private Material material, glowMaterial;
    private MeshRenderer surface, glowSurface;
    private Camera view;
    private Vector3 viewRight=Vector3.right, viewUp=Vector3.up, viewForward=Vector3.forward;
    private int glowBudget;
    private float glowScale;
    private bool useLights;
    private struct LightFlash { public Vector3 Position; public Color Color; public float Range, Intensity, Weight; }
    private readonly LightFlash[] lightRequests = new LightFlash[LightCapacity];
    private readonly Light[] flashLights = new Light[LightCapacity];
    private Transform owner;
    private float orbitRadius;
    private bool orbit, familiar;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; unavailable = false; }

    private static AbilityVfxBatch Get()
    {
        if (unavailable) return null;
        if (instance == null) instance = new GameObject("Ability effect batch").AddComponent<AbilityVfxBatch>();
        return instance;
    }
    public static void Prepare() => Get();
    public static void Clear()
    {
        if (instance == null) return;
        instance.marks.Clear(); instance.owner = null; instance.orbit = instance.familiar = false;
        if (instance.surface != null) instance.surface.enabled = false;
        if (instance.glowSurface != null) instance.glowSurface.enabled = false;
        instance.HideLights();
    }
    public static void SetCompanions(Transform player, bool orbitActive, float radius, bool familiarActive)
    {
        if (!orbitActive && !familiarActive && instance == null) return;
        var batch = Get(); if (batch == null) return;
        batch.owner = player; batch.orbit = orbitActive; batch.orbitRadius = radius; batch.familiar = familiarActive;
    }
    public static Vector3 FamiliarPosition(Transform owner) => owner.position + new Vector3(Mathf.Cos(Time.time * 1.7f) * 1.45f,
        1.08f + Mathf.Sin(Time.time*4f)*.12f, Mathf.Sin(Time.time * 1.7f) * 1.45f);
    public static void Ring(Vector3 p, float r, Color color, float seconds = .34f) => Add(Shape.Ring, p, Vector3.forward, r, color, seconds);
    public static void Arc(Vector3 p, Vector3 direction, float r, Color color) => Add(Shape.Arc, p, direction, r, color, .43f);
    public static void Beam(Vector3 from, Vector3 to, Color color, float seconds = .20f) => Add(Shape.Beam, from, to, .14f, color, seconds);
    public static void Star(Vector3 p, float r, Color color) => Add(Shape.Star, p, Vector3.forward, r, color, .54f);
    public static void Thunder(Vector3 p,float r,Color color) => Add(Shape.Thunder,p,Vector3.forward,r,color,.45f);
    public static void Frost(Vector3 p,float r,Color color) => Add(Shape.Frost,p,Vector3.forward,r,color,.54f);
    public static void Gravity(Vector3 p,float r,Color color) => Add(Shape.Gravity,p,Vector3.forward,r,color,.61f);
    public static void Flame(Vector3 p,float r,Color color) => Add(Shape.Flame,p,Vector3.forward,r,color,.48f);
    public static void Lightning(Vector3 from,Vector3 to,Color color) => Add(Shape.Lightning,from,to,.14f,color,.23f);
    public static void Lance(Vector3 from,Vector3 to,Color color) => Add(Shape.Lance,from,to,.14f,color,.27f);

    private static void Add(Shape shape, Vector3 pos, Vector3 direction, float radius, Color color, float seconds)
    {
        var batch = Get(); if (batch == null || batch.surface == null || radius <= 0f) return;
        if (batch.marks.Count >= MaxMarks) batch.marks.RemoveAt(0);
        batch.marks.Add(new Mark { Shape = shape, Center = pos, Direction = direction, Radius = radius, Color = color, Start = Time.time, Duration = seconds });
    }

    private void Awake()
    {
        instance = this;
        var shader = Resources.Load<Shader>("AbilityCore");
        if (shader == null || !shader.isSupported) shader = Resources.Load<Shader>("CombatInsignia");
        if (shader == null) { unavailable = true; enabled = false; return; }
        material = new Material(shader) { name = "Ability effects" };
        mesh = new Mesh { name = "Batched ability shapes" }; mesh.MarkDynamic();
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        surface = gameObject.AddComponent<MeshRenderer>(); surface.sharedMaterial = material;
        surface.shadowCastingMode = ShadowCastingMode.Off; surface.receiveShadows = false;
        surface.lightProbeUsage = LightProbeUsage.Off; surface.reflectionProbeUsage = ReflectionProbeUsage.Off; surface.enabled = false;
        var glowShader = Resources.Load<Shader>("AbilityGlow");
        if (glowShader != null && glowShader.isSupported)
        {
            glowMaterial = new Material(glowShader) { name = "Ability soft glow" };
            glowMesh = new Mesh { name = "Batched ability halos" }; glowMesh.MarkDynamic();
            var child = new GameObject("Ability halos"); child.transform.SetParent(transform,false);
            child.AddComponent<MeshFilter>().sharedMesh = glowMesh;
            glowSurface = child.AddComponent<MeshRenderer>(); glowSurface.sharedMaterial = glowMaterial;
            glowSurface.shadowCastingMode=ShadowCastingMode.Off; glowSurface.receiveShadows=false;
            glowSurface.lightProbeUsage=LightProbeUsage.Off; glowSurface.reflectionProbeUsage=ReflectionProbeUsage.Off; glowSurface.enabled=false;
        }
    }
    private static Vector3 Floor(Vector3 p) { p.y = Mathf.Max(.07f, p.y - .32f); return p; }
    private void LateUpdate()
    {
        if (surface == null) return;
        bool playing = GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameState.Playing && !GameModeManager.IsPvp;
        if (!playing)
        {
            surface.enabled = false;
            if(glowSurface!=null) glowSurface.enabled=false;
            HideLights(); return;
        }
        vertices.Clear(); colors.Clear(); normals.Clear();
        glowVertices.Clear(); glowColors.Clear(); glowUv.Clear();
        bool mobile=MobileControls.IsTouchDevice;
        glowBudget=mobile?MobileGlowBudget:DesktopGlowBudget; useLights=!mobile&&Time.timeScale>0f;
        // Prevent pale halos from accumulating into a solid white screen
        // when several automatic abilities trigger in the same moment.
        glowScale=(mobile ? .83f : 1f)*Mathf.Lerp(1f,.55f,Mathf.Clamp01((marks.Count-4)/12f));
        System.Array.Clear(lightRequests,0,lightRequests.Length);
        if(view==null||!view.isActiveAndEnabled) view=Camera.main;
        if(view!=null) { viewRight=view.transform.right; viewUp=view.transform.up; viewForward=view.transform.forward; }
        // Keep all new effects in a 16-bit mesh even when the player has a
        // full automatic loadout; prioritize the newest effects at the limit.
        int budget=mobile?MobileVertexBudget:DesktopVertexBudget;
        int segments=mobile||marks.Count>16?16:24;
        for (int i = marks.Count - 1; i >= 0; i--)
        {
            Mark m = marks[i]; float t = (Time.time - m.Start) / Mathf.Max(.01f, m.Duration);
            if (t >= 1f) { marks.RemoveAt(i); continue; }
            if (vertices.Count > budget-2200) continue;
            float life=1f-t, width=Mathf.Lerp(.29f,.04f,t);
            float swell=Mathf.Sin(Mathf.PI*Mathf.Clamp01(t));
            Vector3 center = Floor(m.Center);
            switch (m.Shape)
            {
                case Shape.Ring:
                    DrawRaisedArc(center,Vector3.forward,m.Radius*Mathf.Lerp(.52f,1f,t),width,.24f*life+.04f,360f,m.Color,segments,false);
                    DrawArc(center,Vector3.forward,m.Radius,.035f,360f,Tint(m.Color,.65f),segments); break;
                case Shape.Arc:
                    // A rising crescent with a bevelled bright edge and a
                    // smaller trailing arc, rather than a flat sector decal.
                    Vector3 slash=center+Vector3.up*(.22f+.22f*swell);
                    float slashRadius=m.Radius*Mathf.Lerp(.65f,1f,t);
                    DrawRaisedArc(slash,m.Direction,slashRadius,width*2.7f,.55f*life+.08f,120f,m.Color,segments,true);
                    DrawRaisedArc(slash+Vector3.up*.06f,m.Direction,Mathf.Max(.12f,slashRadius-width*2.9f),width*.65f,.12f*life+.03f,106f,Tint(m.Color,.82f),12,true);
                    DrawArc(center,m.Direction,m.Radius,.045f,120f,Tint(m.Color,.58f),14); break;
                case Shape.Beam:
                    Vector3 beamStart=m.Center+Vector3.up*.08f,beamEnd=m.Direction+Vector3.up*.12f;
                    DrawTube(beamStart,beamEnd,width*.27f,width*.10f,m.Color,4);
                    DrawGem(Vector3.Lerp(beamStart,beamEnd,Mathf.Clamp01(t*1.6f)),beamEnd-beamStart,.21f*life+.11f,.12f*life+.04f,m.Color,4);
                    DrawGem(beamEnd,Vector3.up,.24f*life+.06f,.18f*life+.04f,m.Color,4); break;
                case Shape.Star:
                    float fall=Mathf.Clamp01(t*3f),impact=Mathf.Clamp01((t-.22f)*1.4f);
                    if(t<.57f)
                    {
                        Vector3 comet=center+Vector3.up*Mathf.Lerp(3.3f,.30f,fall*fall);
                        DrawGem(comet,Vector3.up,.50f,.32f,GoldCore(m.Color),5);
                        DrawTube(comet+Vector3.up*.20f,comet+Vector3.up*(1.08f+.6f*life),.22f,.015f,Tint(m.Color,.83f),5);
                    }
                    DrawRaisedArc(center,Vector3.forward,Mathf.Max(.16f,m.Radius*Mathf.Lerp(.28f,1f,impact)),width,.26f*life+.04f,360f,m.Color,segments,false);
                    for(int j=0;j<6;j++)
                    {
                        Vector3 d=Direction(j*60f+17f);
                        DrawGem(center+d*(m.Radius*(.22f+.52f*impact))+Vector3.up*(.11f+.46f*swell),Vector3.up+d*.30f,.24f*life+.05f,.12f*life+.025f,m.Color,4);
                    }
                    break;
                case Shape.Thunder:
                    float shock=m.Radius*Mathf.Lerp(.3f,1f,t);
                    DrawRaisedArc(center,Vector3.forward,shock,width*1.75f,.58f*life+.05f,360,m.Color,segments,false);
                    DrawArc(center,Vector3.forward,m.Radius,.045f,360,Tint(m.Color,.62f),segments);
                    for(int j=0;j<8;j++)
                    {
                        Vector3 d=Direction(j*45f);
                        Vector3 p=center+d*Mathf.Min(m.Radius,shock*.95f)+Vector3.up*(.08f+swell*.26f);
                        DrawTube(p,p+Vector3.up*(.34f*life+.04f)+d*.24f,width*.42f,.012f,GoldCore(m.Color),4);
                    }
                    break;
                case Shape.Frost:
                    float cold=m.Radius*Mathf.Lerp(.50f,1f,t);
                    DrawRaisedArc(center,Vector3.forward,cold,.17f*life+.03f,.22f*life+.04f,360,m.Color,segments,false);
                    float growth=Mathf.Clamp01(t*6f)*Mathf.Pow(life,.60f);
                    int crystals=mobile?8:11;
                    for(int j=0;j<crystals;j++)
                    {
                        Vector3 d=Direction(j*360f/crystals+9f);
                        Vector3 p=center+d*(cold-.16f);
                        float height=(.65f+.28f*Mathf.Sin(j*2.7f))*growth+.04f;
                        DrawCrystal(p,(Vector3.up-d*.22f).normalized,height,.18f*growth+.035f,m.Color,5);
                        if(!mobile&&j%2==0) DrawCrystal(p-d*.23f+Vector3.up*.02f,(Vector3.up+d*.33f).normalized,height*.60f,.11f*growth+.02f,GoldCore(m.Color),4);
                    }
                    break;
                case Shape.Gravity:
                    float gravity=m.Radius*Mathf.Lerp(1f,.24f,t);
                    Vector3 core=center+Vector3.up*(.54f+.24f*swell);
                    DrawArc(center,Vector3.forward,m.Radius,.045f,360,Tint(m.Color,.56f),segments);
                    for(int j=0;j<4;j++)
                    {
                        Vector3 d=Direction(j*90f+t*160f);
                        DrawRaisedArc(center+Vector3.up*(.09f+j*.10f),d,gravity,width*.62f,.23f*life+.04f,68f,Tint(m.Color,.78f+j*.10f),8,true);
                        DrawGem(center+d*gravity+Vector3.up*(.28f+.25f*swell),Vector3.up+d*.25f,.22f*life+.06f,.13f*life+.03f,m.Color,4);
                    }
                    DrawGem(core,Vector3.up,.42f*life+.12f,.36f*life+.08f,Tint(m.Color,.36f),6);
                    DrawRaisedArc(core-Vector3.up*.10f,Direction(t*300),.55f*life+.14f,.095f,.10f,290f,GoldCore(m.Color),14,true);
                    break;
                case Shape.Flame:
                    float flame=m.Radius*Mathf.Lerp(.5f,1f,t);
                    DrawRaisedArc(center,Vector3.forward,flame,.15f*life+.03f,.16f*life+.03f,360,Tint(m.Color,.77f),segments,false);
                    int flames=mobile?8:12;
                    for(int j=0;j<flames;j++)
                    {
                        Vector3 d=Direction(j*360f/flames+t*12f);
                        float flameHeight=(.48f+.26f*Mathf.Sin(j*2.4f+t*11f))*life+.06f;
                        Vector3 p=center+d*(flame-.12f), axis=(Vector3.up+d*.22f).normalized;
                        DrawCrystal(p,axis,flameHeight,.21f*life+.035f,m.Color,4);
                        DrawCrystal(p+Vector3.up*.045f,axis,flameHeight*.62f,.095f*life+.018f,new Color32(255,211,113,255),4);
                    }
                    break;
                case Shape.Lightning:
                    DrawBolt(m.Center+Vector3.up*.17f,m.Direction+Vector3.up*.17f,Mathf.Max(.025f,width*.30f),m.Color,t); break;
                case Shape.Lance:
                    Vector3 start=m.Center+Vector3.up*.20f,end=m.Direction+Vector3.up*.20f;
                    Vector3 travel=(end-start).normalized;
                    Vector3 tip=Vector3.Lerp(start,end,Mathf.Clamp01(t*4.8f));
                    DrawTube(start,end,width*.17f,width*.08f,Tint(m.Color,.71f),4);
                    DrawTube(Vector3.Lerp(start,tip,.62f),tip,.075f*life+.025f,.12f*life+.035f,m.Color,4);
                    DrawGem(tip,travel,.48f*life+.14f,.18f*life+.05f,m.Color,5);
                    break;
            }
            DrawAtmosphere(m,center,t,mobile,segments);
        }
        if (owner != null)
        {
            if (orbit)
            {
                Vector3 c = Floor(owner.position) + Vector3.up * .56f;
                float phase = Time.time * 2.9f;
                for (int i = 0; i < 2; i++)
                {
                    float a = phase + i * Mathf.PI;
                    Vector3 radial = new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                    float lengthDegrees=Mathf.Clamp(1.05f/Mathf.Max(.25f,orbitRadius)*Mathf.Rad2Deg,13f,38f);
                    Vector3 bladeCenter=c+Vector3.up*(Mathf.Sin(phase+i*Mathf.PI)*.07f);
                    DrawRaisedArc(bladeCenter,radial,orbitRadius+.16f,.33f,.20f,lengthDegrees,new Color32(239,222,167,255),8,true);
                    DrawRaisedArc(c-Vector3.up*.04f,Direction(a*Mathf.Rad2Deg-24f),orbitRadius+.035f,.095f,.05f,30f,new Color32(214,158,75,255),7,true);
                    GlowArc(bladeCenter+Vector3.up*.12f,radial,orbitRadius+.03f,.25f,lengthDegrees+9f,new Color32(246,208,128,255),.14f,7);
                }
            }
            if (familiar)
            {
                Vector3 spirit=FamiliarPosition(owner);
                DrawGem(spirit,Vector3.up,.38f,.27f,new Color32(178,145,222,255),6);
                DrawGem(spirit+Vector3.up*.05f,Direction(Time.time*75f),.19f,.10f,new Color32(232,217,246,255),4);
                GlowBillboard(spirit,.65f,new Color32(187,153,239,255),.15f);
                for(int j=0;j<2;j++) DrawGem(spirit+Direction(Time.time*100f+j*180f)*.43f-Vector3.up*.09f,Vector3.up,.095f,.065f,new Color32(201,175,239,255),4);
            }
        }
        surface.enabled = vertices.Count > 0;
        if (surface.enabled)
        {
            mesh.Clear();
            for (int i = indices.Count; i < vertices.Count; i++) indices.Add(i);
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetNormals(normals); mesh.SetTriangles(indices, 0, vertices.Count, 0, true);
        }
        if(glowSurface!=null)
        {
            glowSurface.enabled=glowVertices.Count>0;
            if(glowSurface.enabled)
            {
                glowMesh.Clear();
                for(int i=glowIndices.Count;i<glowVertices.Count;i++) glowIndices.Add(i);
                glowMesh.SetVertices(glowVertices); glowMesh.SetColors(glowColors); glowMesh.SetUVs(0,glowUv);
                glowMesh.SetTriangles(glowIndices,0,glowVertices.Count,0,true);
            }
        }
        UpdateLights();
    }

    private void DrawAtmosphere(Mark m,Vector3 center,float t,bool mobile,int segments)
    {
        float life=Mathf.Clamp01(1f-t), glow=life*life*.75f+.25f*life;
        float swell=Mathf.Sin(Mathf.PI*t);
        Color c=m.Color;
        switch(m.Shape)
        {
            case Shape.Ring:
                GlowArc(center+Vector3.up*.09f,Vector3.forward,m.Radius*Mathf.Lerp(.52f,1f,t),.26f,360,c,.22f*glow,segments); break;
            case Shape.Arc:
            {
                float r=m.Radius*Mathf.Lerp(.65f,1f,t);
                Vector3 pos=center+Vector3.up*(.32f+.22f*swell);
                GlowArc(pos,m.Direction,r-.12f,.51f,120f,c,.48f*glow,segments);
                GlowArc(center+Vector3.up*.025f,m.Direction,r,.45f,120f,c,.16f*glow,segments);
                Sparks(center,m.Direction,r,120f,mobile?3:7,t,c,false);
                Vector3 flash=center+m.Direction.normalized*(r*.72f)+Vector3.up*.90f;
                RequestLight(flash,c,3.2f,1.5f*glow,1.5f); break;
            }
            case Shape.Beam:
            {
                Vector3 from=m.Center+Vector3.up*.12f,to=m.Direction+Vector3.up*.12f;
                GlowLine(from,to,.22f,c,.40f*glow);
                GlowBillboard(to,.62f,c,.34f*glow);
                RequestLight(to,c,2.0f,.58f*glow,.5f); break;
            }
            case Shape.Star:
            {
                float fall=Mathf.Clamp01(t*3f),impact=Mathf.Clamp01((t-.22f)*1.4f);
                if(t<.57f)
                {
                    Vector3 comet=center+Vector3.up*Mathf.Lerp(3.3f,.30f,fall*fall);
                    GlowBillboard(comet,.78f,c,.56f*glow);
                    GlowLine(comet,comet+Vector3.up*(1.08f+.6f*life),.34f,c,.34f*glow);
                }
                GlowDisc(center+Vector3.up*.036f,Mathf.Min(2.3f,m.Radius*.8f),c,.30f*glow);
                GlowArc(center+Vector3.up*.18f,m.Direction,m.Radius*Mathf.Lerp(.28f,1f,impact),.46f,360f,c,.32f*glow,segments);
                Sparks(center,Vector3.forward,m.Radius,360f,mobile?4:9,t,c,false);
                RequestLight(center+Vector3.up*.85f,c,3.6f,1.65f*glow,1.3f); break;
            }
            case Shape.Thunder:
            {
                float r=m.Radius*Mathf.Lerp(.3f,1f,t);
                GlowDisc(center+Vector3.up*.038f,Mathf.Min(2.5f,m.Radius*.5f),c,.25f*glow);
                GlowArc(center+Vector3.up*.22f,m.Direction,r,.50f,360f,c,.43f*glow,segments);
                Sparks(center,Vector3.forward,r,360f,mobile?4:8,t,c,false);
                RequestLight(center+Vector3.up*.77f,c,3.5f,1.75f*glow,1.5f); break;
            }
            case Shape.Frost:
            {
                float r=m.Radius*Mathf.Lerp(.5f,1f,t);
                GlowArc(center+Vector3.up*.09f,Vector3.forward,r,.37f,360f,c,.30f*glow,segments);
                GlowDisc(center+Vector3.up*.035f,Mathf.Min(2.0f,m.Radius*.45f),c,.15f*glow);
                Sparks(center,Vector3.forward,r,360f,mobile?3:6,t,c,false);
                RequestLight(center+Vector3.up*.9f,c,3.1f,1.22f*glow,1.15f); break;
            }
            case Shape.Gravity:
            {
                float r=m.Radius*Mathf.Lerp(1f,.24f,t);
                Vector3 pos=center+Vector3.up*(.54f+.24f*swell);
                GlowBillboard(pos,.96f,c,.33f*glow);
                GlowDisc(center+Vector3.up*.037f,Mathf.Min(2.0f,r),c,.20f*glow);
                for(int j=0;j<3;j++) GlowArc(center+Vector3.up*(.13f+j*.13f),Direction(j*120f+t*160f),r,.34f,72f,c,.24f*glow,8);
                Sparks(center,Vector3.forward,m.Radius,360f,mobile?3:7,t,c,true);
                RequestLight(pos,c,2.9f,1.14f*glow,1.1f); break;
            }
            case Shape.Flame:
            {
                float r=m.Radius*Mathf.Lerp(.5f,1f,t);
                GlowArc(center+Vector3.up*.17f,Vector3.forward,r,.43f,360f,c,.32f*glow,segments);
                Sparks(center,Vector3.forward,r,360f,mobile?3:6,t,new Color32(255,182,83,255),false);
                RequestLight(center+Vector3.up*.7f,c,2.8f,.95f*glow,.8f); break;
            }
            case Shape.Lightning:
            {
                Vector3 to=m.Direction+Vector3.up*.17f;
                GlowBillboard(to,.64f,c,.48f*glow);
                RequestLight(to,c,2.2f,.95f*glow,.75f); break;
            }
            case Shape.Lance:
            {
                Vector3 start=m.Center+Vector3.up*.20f,end=m.Direction+Vector3.up*.20f;
                Vector3 tip=Vector3.Lerp(start,end,Mathf.Clamp01(t*4.8f));
                GlowLine(start,end,.22f,c,.18f*glow);
                GlowLine(Vector3.Lerp(start,tip,.62f),tip,.33f,c,.36f*glow);
                GlowBillboard(tip,.62f,c,.40f*glow);
                RequestLight(tip,c,2.2f,.65f*glow,.6f); break;
            }
        }
    }

    private void GlowQuad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color,float intensity,bool strip)
    {
        if(glowSurface==null||intensity<=.005f||glowVertices.Count+6>glowBudget) return;
        color.a=Mathf.Clamp01(intensity*glowScale);
        glowVertices.Add(a); glowVertices.Add(b); glowVertices.Add(c); glowVertices.Add(a); glowVertices.Add(c); glowVertices.Add(d);
        for(int j=0;j<6;j++) glowColors.Add(color);
        if(strip)
        {
            Vector2 lo=new Vector2(.5f,0),hi=new Vector2(.5f,1);
            glowUv.Add(lo); glowUv.Add(hi); glowUv.Add(hi); glowUv.Add(lo); glowUv.Add(hi); glowUv.Add(lo);
        }
        else
        {
            glowUv.Add(Vector2.zero); glowUv.Add(Vector2.right); glowUv.Add(Vector2.one);
            glowUv.Add(Vector2.zero); glowUv.Add(Vector2.one); glowUv.Add(Vector2.up);
        }
    }
    private void GlowDisc(Vector3 center,float radius,Color color,float intensity)
    {
        if(radius<=.02f) return;
        Vector3 x=Vector3.right*radius,z=Vector3.forward*radius;
        GlowQuad(center-x-z,center+x-z,center+x+z,center-x+z,color,intensity,false);
    }
    private void GlowBillboard(Vector3 center,float radius,Color color,float intensity)
    {
        if(radius<=.02f) return;
        Vector3 x=viewRight*radius,y=viewUp*radius;
        GlowQuad(center-x-y,center+x-y,center+x+y,center-x+y,color,intensity,false);
    }
    private void GlowLine(Vector3 from,Vector3 to,float width,Color color,float intensity)
    {
        if((to-from).sqrMagnitude<.0001f) return;
        Vector3 side=Vector3.Cross(to-from,viewForward);
        if(side.sqrMagnitude<.0001f) side=viewRight;
        side=side.normalized*width;
        GlowQuad(from-side,from+side,to+side,to-side,color,intensity,true);
    }
    private void GlowArc(Vector3 center,Vector3 direction,float radius,float width,float degrees,Color color,float intensity,int segments)
    {
        if(glowSurface==null||radius<=.02f||intensity<=.005f||glowVertices.Count>=glowBudget) return;
        direction.y=0; if(direction.sqrMagnitude<.001f) direction=Vector3.forward;
        float start=Mathf.Atan2(direction.x,direction.z)-degrees*Mathf.Deg2Rad*.5f;
        float step=degrees*Mathf.Deg2Rad/segments; float inner=Mathf.Max(.01f,radius-width),outer=radius+width;
        for(int i=0;i<segments;i++)
        {
            float a=start+i*step,b=a+step;
            Vector3 da=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a)),db=new Vector3(Mathf.Sin(b),0,Mathf.Cos(b));
            GlowQuad(center+da*inner,center+da*outer,center+db*outer,center+db*inner,color,intensity,true);
        }
    }
    private void Sparks(Vector3 center,Vector3 direction,float radius,float degrees,int count,float t,Color color,bool inward)
    {
        float life=Mathf.Clamp01(1f-t);
        if(life<.05f) return;
        float start=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg-degrees*.5f;
        for(int i=0;i<count;i++)
        {
            float variation=Mathf.Sin(i*9.13f+1.7f)*.5f+.5f;
            Vector3 radial=Direction(start+(i+.5f)*degrees/count+Mathf.Sin(i*5.8f)*13f);
            float distance=radius*(inward?Mathf.Lerp(.84f,.16f,t):Mathf.Lerp(.56f,.93f,t));
            distance*=.83f+variation*.17f;
            float height=.15f+Mathf.Sin(Mathf.PI*t)*(.36f+variation*.76f);
            Vector3 point=center+radial*distance+Vector3.up*height;
            float size=(.045f+.045f*variation)*life+.018f;
            Vector3 axis=(Vector3.up+radial*(inward?-.7f:.7f)).normalized;
            DrawGem(point,axis,size*1.9f,size,GoldCore(color),4);
            if(i%2==0) GlowBillboard(point,size*4.6f,color,.20f*life);
        }
    }

    private void RequestLight(Vector3 position,Color color,float range,float intensity,float priority)
    {
        if(!useLights||intensity<.07f) return;
        var request=new LightFlash { Position=position,Color=color,Range=range,Intensity=intensity,Weight=intensity*priority };
        for(int i=0;i<lightRequests.Length;i++)
        {
            if(lightRequests[i].Weight<=0f||(lightRequests[i].Position-position).sqrMagnitude>2.25f) continue;
            if(request.Weight>lightRequests[i].Weight) lightRequests[i]=request;
            if(i>0&&lightRequests[i].Weight>lightRequests[i-1].Weight)
            { var previous=lightRequests[i-1]; lightRequests[i-1]=lightRequests[i]; lightRequests[i]=previous; }
            return;
        }
        for(int i=0;i<lightRequests.Length;i++)
        {
            if(request.Weight<=lightRequests[i].Weight) continue;
            for(int j=lightRequests.Length-1;j>i;j--) lightRequests[j]=lightRequests[j-1];
            lightRequests[i]=request; return;
        }
    }
    private void UpdateLights()
    {
        if(!useLights) { HideLights(); return; }
        for(int i=0;i<flashLights.Length;i++)
        {
            LightFlash request=lightRequests[i]; Light light=flashLights[i];
            if(request.Weight<=0f)
            {
                if(light!=null) light.enabled=false;
                continue;
            }
            if(light==null)
            {
                var child=new GameObject("Pooled ability flash "+i); child.transform.SetParent(transform,false);
                light=child.AddComponent<Light>(); flashLights[i]=light;
                light.type=LightType.Point; light.shadows=LightShadows.None; light.bounceIntensity=0f;
            }
            light.transform.position=request.Position; light.color=request.Color;
            light.range=request.Range; light.intensity=request.Intensity;
            if(!light.enabled) light.enabled=true;
        }
    }
    private void HideLights()
    {
        foreach(Light light in flashLights) if(light!=null) light.enabled=false;
    }
    private void DrawArc(Vector3 center, Vector3 direction, float r, float width, float degrees, Color color, int segments)
    {
        direction.y = 0; if (direction.sqrMagnitude < .001f) direction = Vector3.forward;
        direction.Normalize();
        float start = Mathf.Atan2(direction.x, direction.z) - degrees * Mathf.Deg2Rad * .5f;
        float step = degrees * Mathf.Deg2Rad / segments;
        float inner = Mathf.Max(0f, r - width);
        for (int i = 0; i < segments; i++)
        {
            float a = start + i * step, b = a + step;
            Vector3 da = new Vector3(Mathf.Sin(a),0,Mathf.Cos(a)), db = new Vector3(Mathf.Sin(b),0,Mathf.Cos(b));
            Quad(center+da*inner,center+da*r,center+db*r,center+db*inner,color);
        }
    }
    private static Vector3 Direction(float angle) => new Vector3(Mathf.Sin(angle*Mathf.Deg2Rad),0,Mathf.Cos(angle*Mathf.Deg2Rad));
    private static Color Tint(Color c,float brightness) => new Color(c.r*brightness,c.g*brightness,c.b*brightness,c.a);
    private static Color GoldCore(Color c) => Color.Lerp(c,Color.white,.32f);

    // Four independently coloured surfaces of a bevelled curved blade/ring.
    // All points share the same batch, with no objects along the arc.
    private void DrawRaisedArc(Vector3 center,Vector3 direction,float radius,float width,float height,float degrees,Color color,int segments,bool taper)
    {
        if(radius<=.01f||width<=.001f||height<=.001f||segments<1) return;
        direction.y=0f; if(direction.sqrMagnitude<.001f) direction=Vector3.forward;
        float start=Mathf.Atan2(direction.x,direction.z)-degrees*Mathf.Deg2Rad*.5f;
        float step=degrees*Mathf.Deg2Rad/segments;
        Color outside=Tint(color,.62f), inside=Tint(color,.78f), bevel=GoldCore(color), ridge=color;
        for(int i=0;i<segments;i++)
        {
            float a=start+i*step,b=a+step;
            Vector3 da=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a)),db=new Vector3(Mathf.Sin(b),0,Mathf.Cos(b));
            float ta=taper?Mathf.Max(.08f,Mathf.Sin(Mathf.PI*i/segments)):1f;
            float tb=taper?Mathf.Max(.08f,Mathf.Sin(Mathf.PI*(i+1)/segments)):1f;
            float wa=Mathf.Min(radius*.94f,width*ta),wb=Mathf.Min(radius*.94f,width*tb);
            float ha=height*ta,hb=height*tb;
            Vector3 a0=center+da*(radius-wa),b0=center+db*(radius-wb);
            Vector3 a1=center+da*(radius-wa*.78f)+Vector3.up*(ha*.45f),b1=center+db*(radius-wb*.78f)+Vector3.up*(hb*.45f);
            Vector3 a2=center+da*(radius-wa*.36f)+Vector3.up*ha,b2=center+db*(radius-wb*.36f)+Vector3.up*hb;
            Vector3 a3=center+da*radius+Vector3.up*(ha*.22f),b3=center+db*radius+Vector3.up*(hb*.22f);
            Vector3 a4=center+da*radius,b4=center+db*radius;
            Quad(a0,a1,b1,b0,inside); Quad(a1,a2,b2,b1,ridge);
            Quad(a2,a3,b3,b2,bevel); Quad(a3,a4,b4,b3,outside);
            if(taper&&i==0) { Triangle(a0,a1,a2,outside);Triangle(a0,a2,a4,outside);Triangle(a2,a3,a4,outside); }
            if(taper&&i==segments-1) { Triangle(b0,b2,b1,outside);Triangle(b0,b4,b2,outside);Triangle(b2,b4,b3,outside); }
        }
    }

    // A small faceted solid from two cross-sections. Very low polygon count
    // and directed face shading, useful for projectiles, ice and sparks.
    private void DrawTube(Vector3 from,Vector3 to,float radiusFrom,float radiusTo,Color color,int sides,bool capStart=true,bool capEnd=true)
    {
        Vector3 axis=to-from; if(axis.sqrMagnitude<.00001f) return;
        axis.Normalize();
        Vector3 side=Vector3.Cross(axis,Vector3.up);
        if(side.sqrMagnitude<.001f) side=Vector3.Cross(axis,Vector3.right);
        side.Normalize(); Vector3 other=Vector3.Cross(axis,side).normalized;
        float step=Mathf.PI*2f/Mathf.Max(3,sides);
        for(int i=0;i<sides;i++)
        {
            float a=i*step,b=(i+1)*step;
            Vector3 da=side*Mathf.Cos(a)+other*Mathf.Sin(a),db=side*Mathf.Cos(b)+other*Mathf.Sin(b);
            Vector3 f0=from+da*radiusFrom,f1=from+db*radiusFrom,t0=to+da*radiusTo,t1=to+db*radiusTo;
            Vector3 n=(da+db).normalized;
            float illumination=Mathf.Clamp01(Vector3.Dot(n,FaceLight)*.5f+.5f);
            Color face=Color.Lerp(Tint(color,.62f),GoldCore(color),illumination);
            if(radiusFrom<.001f) Triangle(from,t0,t1,face);
            else if(radiusTo<.001f) Triangle(f0,to,f1,face);
            else Quad(f0,t0,t1,f1,face);
            if(capStart&&radiusFrom>=.001f) Triangle(from,f0,f1,Tint(color,.73f));
            if(capEnd&&radiusTo>=.001f) Triangle(to,t1,t0,GoldCore(color));
        }
    }
    private void DrawGem(Vector3 center,Vector3 axis,float length,float radius,Color color,int sides)
    {
        if(axis.sqrMagnitude<.001f) axis=Vector3.up;
        axis.Normalize();
        DrawTube(center-axis*length,center,0f,radius,Tint(color,.86f),sides,false,false);
        DrawTube(center,center+axis*length,radius,0f,color,sides,false,false);
    }
    private void DrawCrystal(Vector3 root,Vector3 axis,float height,float radius,Color color,int sides)
    {
        if(axis.sqrMagnitude<.001f) axis=Vector3.up;
        axis.Normalize();
        Vector3 shoulder=root+axis*height*.43f;
        DrawTube(root,shoulder,radius*.64f,radius,Tint(color,.81f),sides,true,false);
        DrawTube(shoulder,root+axis*height,radius,0f,color,sides,false,false);
    }
    private void DrawBolt(Vector3 from,Vector3 to,float width,Color color,float time)
    {
        Vector3 side=Vector3.Cross(to-from,Vector3.up).normalized;
        if(side.sqrMagnitude<.001f) side=Vector3.right;
        float wiggle=.19f+.06f*Mathf.Sin(time*20f);
        Vector3 p1=Vector3.Lerp(from,to,.30f)+side*wiggle+Vector3.up*.11f;
        Vector3 p2=Vector3.Lerp(from,to,.63f)-side*wiggle+Vector3.up*.18f;
        Color bright=GoldCore(color);
        DrawTube(from,p1,width*.72f,width,bright,4); DrawTube(p1,p2,width,width,bright,4,false,false);
        DrawTube(p2,to,width,width*.60f,bright,4,false,true);
        DrawTube(p1,p1+side*.29f+Vector3.up*.20f,width*.58f,0f,color,4,false,false);
        DrawGem(to,Vector3.up,width*2.1f,width*1.2f,color,4);
        float glow=.42f*Mathf.Clamp01(1f-time), halo=width*2.6f+.13f;
        GlowLine(from,p1,halo,color,glow); GlowLine(p1,p2,halo,color,glow); GlowLine(p2,to,halo,color,glow);
    }
    private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
    {
        vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(a); vertices.Add(c); vertices.Add(d);
        Vector3 normal=FaceNormal(a,b,c);
        for (int i = 0; i < 6; i++) { colors.Add(color); normals.Add(normal); }
    }
    private void Triangle(Vector3 a,Vector3 b,Vector3 c,Color color)
    {
        vertices.Add(a); vertices.Add(b); vertices.Add(c);
        Vector3 normal=FaceNormal(a,b,c);
        for(int i=0;i<3;i++) { colors.Add(color); normals.Add(normal); }
    }
    private static Vector3 FaceNormal(Vector3 a,Vector3 b,Vector3 c)
    {
        Vector3 n=Vector3.Cross(b-a,c-a);
        return n.sqrMagnitude>.0000001f?n.normalized:Vector3.up;
    }
    private void OnDestroy()
    {
        if (mesh != null) Destroy(mesh); if (material != null) Destroy(material);
        if(glowMesh!=null) Destroy(glowMesh); if(glowMaterial!=null) Destroy(glowMaterial);
        HideLights();
        if (instance == this) instance = null;
    }
}
