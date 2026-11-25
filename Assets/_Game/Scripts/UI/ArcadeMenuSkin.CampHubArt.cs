using System.Collections.Generic;
using UnityEngine;

// Menu-only low-poly art. Geometry shares materials and is baked into the
// camp batches; nothing below participates in navigation or combat.
public sealed partial class ArcadeMenuSkin
{
    private readonly Dictionary<int,Mesh> hubFacetMeshes=new Dictionary<int,Mesh>();
    private readonly List<Transform> hubFlames=new List<Transform>();
    private Light hubSun;
    private Color hubSunColor;
    private float hubSunIntensity,hubSunShadow;
    private Quaternion hubSunRotation;
    private LightShadows hubSunShadowMode;
    private Light hubFireLight;

    private void BuildHubClearing(GameMap map,EnvironmentTheme theme)
    {
        Transform parent=campScenery.transform;
        bool forest=map.langKey=="les";
        bool winter=map.langKey=="gory",urban=map.langKey=="gorod";
        int outer=map.langKey switch { "plyazh"=>0xB8AC75,"gory"=>0xB6CED8,"pustynya"=>0xA87E4F,"gorod"=>0x555D60,"kosmos"=>0x4D5363,_=>0x637B48 };
        int inner=map.langKey switch { "plyazh"=>0xD8C08A,"gory"=>0xE0E8E9,"pustynya"=>0xD3A26D,"gorod"=>0x90999A,"kosmos"=>0x828493,_=>0xA3A16C };
        Material moss=HubMat(outer), soil=HubMat(inner);
        Material path=HubMat(winter?0xA9C0CC:urban?0x777E80:map.langKey=="kosmos"?0x9698A4:0xC5AA7B);
        Material pathEdge=HubMat(winter?0xCCDCE1:urban?0xB3B9B7:map.langKey=="kosmos"?0x656A7B:0xA99467);
        // A broad irregular patch and a lighter clearing make the stations
        // read as one place, with an actual entrance and branching paths.
        HubPatch(parent,"Camp ground border",new Vector3(0,.009f,.70f),6.8f,5.65f,moss,1);
        HubPatch(parent,"Sheltered ground",new Vector3(0,.021f,.45f),5.50f,4.40f,soil,2);
        if(urban)
        {
            // A paved emergency outpost, not a dirt clearing surrounded by buildings.
            HubBox(parent,"Asphalt yard",new Vector3(0,.025f,.3f),new Vector3(10.5f,.05f,8.2f),path);
            for(int x=-4;x<=4;x++)
                for(int z=-3;z<=3;z++)
                    HubBox(parent,"Concrete paving slab",new Vector3(x*1.12f,.062f,z*1.08f+.3f),
                        new Vector3(1.09f,.035f,1.05f),HubMat((x+z)%3==0?0x969E9D:0xA2AAA8));
            for(int i=-1;i<=1;i+=2)
                for(int j=0;j<5;j++)
                    HubBox(parent,"Safety marking",new Vector3(i*4.94f,.092f,-2.7f+j*1.35f),
                        new Vector3(.12f,.015f,.65f),HubMat(0xCCAF62));
        }
        else
        {
        HubPatch(parent,"Courtyard earth",new Vector3(0,.031f,.10f),2.15f,1.94f,pathEdge,3);
        HubPatch(parent,"Courtyard center",new Vector3(0,.039f,.10f),1.92f,1.72f,path,4);
        HubTrail(parent,"Entrance path",pathEdge,.042f,.95f,new Vector2(0,-5.5f),new Vector2(-.45f,-3.9f),new Vector2(-.32f,-2.7f),new Vector2(0,-1.4f));
        HubTrail(parent,"Entrance track",path,.049f,.74f,new Vector2(0,-5.5f),new Vector2(-.45f,-3.9f),new Vector2(-.32f,-2.7f),new Vector2(0,-1.4f));
        HubTrail(parent,"Armory path",pathEdge,.042f,.65f,new Vector2(-.9f,.55f),new Vector2(-2.0f,.42f),new Vector2(-3.2f,.9f));
        HubTrail(parent,"Armory track",path,.049f,.48f,new Vector2(-.9f,.55f),new Vector2(-2.0f,.42f),new Vector2(-3.2f,.9f));
        HubTrail(parent,"Workshop path",pathEdge,.042f,.66f,new Vector2(1,.62f),new Vector2(2,.65f),new Vector2(3.2f,1.1f));
        HubTrail(parent,"Workshop track",path,.049f,.48f,new Vector2(1,.62f),new Vector2(2,.65f),new Vector2(3.2f,1.1f));
        HubTrail(parent,"Planning path",pathEdge,.042f,.60f,new Vector2(.75f,-.9f),new Vector2(1.45f,-1.6f),new Vector2(2.75f,-1.8f));
        HubTrail(parent,"Planning track",path,.049f,.44f,new Vector2(.75f,-.9f),new Vector2(1.45f,-1.6f),new Vector2(2.75f,-1.8f));
        HubTrail(parent,"Podium path",pathEdge,.042f,.62f,new Vector2(0,.9f),new Vector2(-.08f,1.75f),new Vector2(0,2.65f));
        HubTrail(parent,"Podium track",path,.049f,.43f,new Vector2(0,.9f),new Vector2(-.08f,1.75f),new Vector2(0,2.65f));
        }

        Material rock=HubMat(forest?0x888C76:0x97968B), paleRock=HubMat(forest?0xB2B09A:0xB7B8B2);
        // A low ring frames the real player without moving their gameplay root.
        for(int i=0;i<12;i++)
        {
            if(urban) break;
            if(i==6||i==0) continue;
            float a=(i+0.5f)*Mathf.PI*2/12;
            HubFaceted(parent,"Courtyard paver",new Vector3(Mathf.Sin(a)*1.18f,.052f,Mathf.Cos(a)*1.18f),new Vector3(.52f,.055f,.34f),i%3==0?paleRock:rock,6,.80f,Quaternion.Euler(0,a*Mathf.Rad2Deg,0));
        }

        if(forest || winter)
        {
            // Uneven silhouettes and several shades provide a forest perimeter,
            // covering the unrelated gameplay walls and horizon naturally.
            Vector3[] trees =
            {
                new(-5.9f,0,-2.45f),new(-6.2f,0,-.65f),new(-5.65f,0,1.08f),
                new(-5.55f,0,3.25f),new(-4.7f,0,5.0f),new(-3.4f,0,5.65f),
                new(-1.9f,0,5.25f),new(-.65f,0,6.1f),new(.8f,0,5.4f),
                new(2.25f,0,5.75f),new(3.85f,0,5.15f),new(5.10f,0,4.4f),
                new(5.88f,0,2.9f),new(6.1f,0,.85f),new(6.35f,0,-1.7f),
                new(-7.0f,0,4.7f),new(6.6f,0,5.8f),new(-1.25f,0,7.2f),new(2.3f,0,7.1f)
            };
            for(int i=0;i<trees.Length;i++)
                HubTree(parent,trees[i],2.6f+((i*23)%11)*.16f,!winter&&(i%4==1||i%5==2),i,winter);
            // Low rocks, bushes and roots soften the ground edge but do not
            // interrupt the central camera composition.
            for(int i=0;i<20;i++)
            {
                float a=i*Mathf.PI*2/20+.1f;
                Vector3 p=new Vector3(Mathf.Sin(a)*5.6f,0,.7f+Mathf.Cos(a)*4.4f);
                if(p.z<-3.5f&&Mathf.Abs(p.x)<1.8f) continue;
                if(i%3==0) HubRock(parent,p,new Vector3(.63f,.31f,.47f),i%2==0?paleRock:rock,i);
                else if(winter) HubFaceted(parent,"Snow drift",p+Vector3.up*.055f,new Vector3(.85f,.16f,.7f),HubMat(0xF0F4F4),7,.55f);
                else HubBush(parent,p,.45f+(i%3)*.06f,i);
            }
            for(int i=0;i<12;i++)
            {
                float x=-4.9f+(i*17%19)*.52f,z=-3.55f+(i*7%5)*.20f;
                if(Mathf.Abs(x)<1.0f) continue;
                if(!winter) HubGrass(parent,new Vector3(x,.05f,z),i);
            }
        }
        else if(theme!=null)
        {
            string tall=map.langKey switch { "plyazh"=>"Palm","gory"=>"Fir","pustynya"=>"Cactus","gorod"=>"OfficeBuilding","kosmos"=>"DarkRock",_=>"Tree" };
            for(int i=0;i<(urban?6:12);i++)
            {
                float x=urban?-6.3f+i*2.5f:-6.2f+i*1.15f;
                CampModel(theme,tall,x,urban?6.6f+(i%2)*.5f:4.8f+(i%3)*.65f,2.65f+(i%4)*.52f,urban?0:i*73,2.4f);
            }
            CampModel(theme,tall,-5.8f,-.75f,2.9f,244,2.0f);
            CampModel(theme,tall,5.9f,.65f,3.1f,82,2.1f);
        }

        // Split-rail fence and stone entrance, tucked behind the stations.
        Material timber=HubMat(urban?0x596467:0x70563A), timberTop=HubMat(urban?0xA1AAAA:0x9B794F);
        for(int side=-1;side<=1;side+=2)
        {
            for(int i=0;i<5;i++)
            {
                Vector3 p=new Vector3(side*(2.15f+i*.68f),.30f,4.05f+(i%2)*.08f);
                HubBox(parent,"Camp fence post",p,new Vector3(.14f,.60f,.15f),timber);
                HubFaceted(parent,"Fence cap",p+Vector3.up*.30f,new Vector3(.18f,.11f,.18f),timberTop,4,.25f);
                if(i<4) HubBox(parent,"Fence rail",p+new Vector3(side*.34f,.02f,0),new Vector3(.76f,.12f,.10f),timberTop);
            }
        }
        // Entrance stones, tiny foliage and scattered stepping stones.
        for(int i=0;i<6;i++)
        {
            if(urban) break;
            float z=-3.85f+i*.31f;
            HubFaceted(parent,"Worn stone",new Vector3(-.37f+i*.045f,.052f,z),new Vector3(.45f,.025f,.23f),i%2==0?rock:paleRock,6,.90f,Quaternion.Euler(0,i*19,0));
        }
        ApplyHubLighting();
    }

    private void HubPatch(Transform parent,string name,Vector3 point,float rx,float rz,Material mat,int seed)
    {
        const int sides=26;
        Vector3[] vertices=new Vector3[sides+1];
        int[] triangles=new int[sides*3];
        for(int i=0;i<sides;i++)
        {
            float a=i*Mathf.PI*2/sides;
            float f=1+.055f*Mathf.Sin(i*2.64f+seed)+.045f*Mathf.Cos(i*1.38f-seed);
            vertices[i+1]=new Vector3(Mathf.Cos(a)*rx*f,0,Mathf.Sin(a)*rz*f);
            triangles[i*3]=0;triangles[i*3+1]=(i+1)%sides+1;triangles[i*3+2]=i+1;
        }
        HubCustomMesh(parent,name,point,vertices,triangles,mat);
    }
    private void HubTrail(Transform parent,string name,Material mat,float y,float width,params Vector2[] points)
    {
        Vector3[] vertices=new Vector3[points.Length*2];
        int[] indices=new int[(points.Length-1)*6];
        for(int i=0;i<points.Length;i++)
        {
            Vector2 delta=points[Mathf.Min(i+1,points.Length-1)]-points[Mathf.Max(i-1,0)];
            Vector2 right=new Vector2(delta.y,-delta.x).normalized*width*.5f;
            vertices[2*i]=new Vector3(points[i].x+right.x,y,points[i].y+right.y);
            vertices[2*i+1]=new Vector3(points[i].x-right.x,y,points[i].y-right.y);
            if(i==points.Length-1) continue;
            int n=i*6,b=i*2;
            indices[n]=b;indices[n+1]=b+1;indices[n+2]=b+2;
            indices[n+3]=b+1;indices[n+4]=b+3;indices[n+5]=b+2;
        }
        HubCustomMesh(parent,name,Vector3.zero,vertices,indices,mat);
    }
    private void HubCustomMesh(Transform parent,string name,Vector3 point,Vector3[] vertices,int[] triangles,Material mat)
    {
        Mesh mesh=new Mesh { name=name, vertices=vertices, triangles=triangles };
        mesh.RecalculateNormals();mesh.RecalculateBounds();campMeshes.Add(mesh);
        HubMeshObject(parent,name,point,Vector3.one,Quaternion.identity,mesh,mat,true);
    }
    private MeshRenderer HubMeshObject(Transform parent,string name,Vector3 point,Vector3 size,Quaternion rotation,Mesh mesh,Material mat,bool batch)
    {
        var obj=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));
        obj.transform.SetParent(parent,false);obj.transform.localPosition=point;obj.transform.localRotation=rotation;obj.transform.localScale=size;
        obj.GetComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=obj.GetComponent<MeshRenderer>();renderer.sharedMaterial=mat;
        if(batch) hubStaticRenderers.Add(renderer);
        return renderer;
    }
    // Tapered prism, flat faces, 4-9 sides. Base at y=0; size is full width,
    // height and depth. Reuse each shape throughout the camp and miniatures.
    private MeshRenderer HubFaceted(Transform parent,string name,Vector3 point,Vector3 size,Material mat,int sides,float taper,Quaternion? rotation=null,bool batch=true)
    {
        int key=sides*1000+Mathf.RoundToInt(taper*100);
        if(!hubFacetMeshes.TryGetValue(key,out Mesh mesh))
        {
            var verts=new List<Vector3>();var tris=new List<int>();
            for(int i=0;i<sides;i++)
            {
                float a=i*2*Mathf.PI/sides,b=(i+1)*2*Mathf.PI/sides;
                Vector3 l=new Vector3(Mathf.Cos(a)*.5f,0,Mathf.Sin(a)*.5f),r=new Vector3(Mathf.Cos(b)*.5f,0,Mathf.Sin(b)*.5f);
                Vector3 lt=l*taper+Vector3.up,rt=r*taper+Vector3.up;
                int n=verts.Count;verts.AddRange(new[]{l,lt,rt,r});
                tris.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
                n=verts.Count;verts.AddRange(new[]{Vector3.up,rt,lt});
                tris.AddRange(new[]{n,n+1,n+2});
                n=verts.Count;verts.AddRange(new[]{Vector3.zero,l,r});
                tris.AddRange(new[]{n,n+1,n+2});
            }
            mesh=new Mesh { name="Low-poly tapered prism",vertices=verts.ToArray(),triangles=tris.ToArray() };
            mesh.RecalculateNormals();mesh.RecalculateBounds();
            hubFacetMeshes.Add(key,mesh);campMeshes.Add(mesh);
        }
        return HubMeshObject(parent,name,point,size,rotation??Quaternion.identity,mesh,mat,batch);
    }
    private void HubTree(Transform parent,Vector3 p,float height,bool broad,int seed,bool winter=false)
    {
        Transform t=HubHolder(parent,"Grove tree",p);
        t.localRotation=Quaternion.Euler(0,seed*71,0);
        Material bark=HubMat(0x705238), shadow=HubMat(winter?0x466063:0x35543B), green=HubMat(winter?0x89A9B1:0x4C7045), high=HubMat(winter?0xD6E6E9:0x6E8C50), tip=HubMat(winter?0xEFF6F7:0x859A59);
        HubFaceted(t,"Tree trunk",Vector3.zero,new Vector3(height*.13f,height*.43f,height*.13f),bark,6,.72f);
        if(broad)
        {
            HubFaceted(t,"Crown shadow",new Vector3(-.08f,height*.32f,0),new Vector3(height*.76f,height*.31f,height*.65f),shadow,7,.92f);
            HubFaceted(t,"Leaf canopy",new Vector3(.13f,height*.53f,-.07f),new Vector3(height*.75f,height*.29f,height*.67f),green,7,.58f,Quaternion.Euler(0,20,0));
            HubFaceted(t,"Sunlit crown",new Vector3(-.10f,height*.74f,.07f),new Vector3(height*.46f,height*.23f,height*.46f),high,7,.33f);
        }
        else
        {
            HubFaceted(t,"Lower branches",new Vector3(0,height*.19f,0),new Vector3(height*.76f,height*.40f,height*.70f),shadow,7,.08f);
            HubFaceted(t,"Mid branches",new Vector3(0,height*.39f,0),new Vector3(height*.59f,height*.36f,height*.56f),green,7,.06f,Quaternion.Euler(0,20,0));
            HubFaceted(t,"Upper branches",new Vector3(0,height*.60f,0),new Vector3(height*.42f,height*.42f,height*.40f),seed%3==0?tip:high,7,.025f);
            if(winter)
                HubFaceted(t,"Snow on branches",new Vector3(0,height*.28f,0),new Vector3(height*.62f,height*.32f,height*.57f),high,7,.05f);
        }
    }
    private void HubRock(Transform parent,Vector3 p,Vector3 size,Material mat,int seed)
    {
        HubFaceted(parent,"Granite lower",p,size,mat,6,.70f,Quaternion.Euler(0,seed*31,0));
        HubFaceted(parent,"Granite facet",p+new Vector3(.02f,size.y*.86f,.02f),new Vector3(size.x*.7f,size.y*.32f,size.z*.7f),mat,6,.38f,Quaternion.Euler(0,seed*31,0));
    }
    private void HubBush(Transform parent,Vector3 p,float width,int seed)
    {
        for(int i=0;i<3;i++) HubFaceted(parent,"Understory",p+new Vector3((i-1)*width*.24f,.01f,i%2*.07f),
            new Vector3(width*.7f,width*(i==1?.67f:.49f),width*.6f),HubMat(i==1?0x72944E:0x507846),6,.34f,Quaternion.Euler(0,seed*29+i*33,0));
    }
    private void HubGrass(Transform parent,Vector3 p,int seed)
    {
        for(int i=0;i<3;i++)
            HubFaceted(parent,"Grass blade",p+new Vector3((i-1)*.06f,0,0),new Vector3(.055f,.18f+i*.027f,.035f),
                HubMat(i==1?0x7B9B56:0x607C45),4,.03f,Quaternion.Euler(0,seed*41,i*22-22));
    }
    private void BuildHubShelter(Transform parent,float width,float depth,Material cloth,Material wood,Material trim,bool fabric)
    {
        // A shallow rear canopy preserves a clear view of the display in
        // both the elevated hub view and the station close-up.
        float rear=.90f,front=.23f,edge=width*.5f-.10f;
        for(int i=-1;i<=1;i+=2)
        {
            HubBox(parent,"Shelter timber",new Vector3(i*edge,1.15f,rear),new Vector3(.11f,2.3f,.11f),wood);
            HubBox(parent,"Shelter front timber",new Vector3(i*edge,1.15f,front),new Vector3(.09f,2.3f,.09f),wood);
            HubBox(parent,"Timber brace",new Vector3(i*(edge-.16f),2.02f,front),new Vector3(.07f,.46f,.07f),trim,Quaternion.Euler(0,0,i*-43));
            HubBox(parent,"Eave",new Vector3(i*edge,2.16f,.58f),new Vector3(.10f,.095f,1.10f),trim);
        }
        HubBox(parent,"Front beam",new Vector3(0,2.13f,front),new Vector3(width,.10f,.10f),trim);
        HubBox(parent,"Rear beam",new Vector3(0,2.13f,rear),new Vector3(width,.10f,.10f),trim);
        bool urban=campSceneryMap?.langKey=="gorod",winter=campSceneryMap?.langKey=="gory";
        float angle=urban?0:17, half=width*.5f;
        for(int side=-1;side<=1;side+=2)
        {
            Quaternion tilt=Quaternion.Euler(0,0,side*-angle);
            HubBox(parent,fabric?"Canvas roof":"Wooden roof",new Vector3(side*half*.5f,2.30f,.56f),new Vector3(half*1.055f,.08f,depth*.70f),cloth,tilt);
            if(winter) HubBox(parent,"Roof snow",new Vector3(side*half*.5f,2.365f,.56f),new Vector3(half*1.04f,.07f,depth*.67f),HubMat(0xE8F1F3),tilt);
            for(int i=0;i<4;i++)
                HubBox(parent,"Roof seam",new Vector3(side*half*.5f,2.35f,.14f+i*.28f),new Vector3(half*1.04f,.035f,.025f),urban?HubMat(0x9AA8A8):winter?HubMat(0xBCCDD4):fabric?HubMat(0x8C9D70):HubMat(0x785F42),tilt);
            HubBox(parent,"Roof fascia",new Vector3(side*half*.5f,2.29f,-.025f),new Vector3(half*1.06f,.09f,.05f),urban?HubMat(0x9AA8A8):winter?HubMat(0xBCCDD4):fabric?HubMat(0xC5BD8A):trim,tilt);
        }
    }
    private void HubPostLamp(Transform parent,Vector3 point,Material wood,Material frame)
    {
        HubFaceted(parent,"Lamp footing",point,new Vector3(.29f,.12f,.29f),HubMat(0x989382),6,.9f);
        HubBox(parent,"Lamp post",point+Vector3.up*.56f,new Vector3(.095f,1.06f,.095f),wood);
        HubBox(parent,"Lamp bracket",point+new Vector3(.13f,1.09f,0),new Vector3(.32f,.06f,.065f),wood);
        HubLantern(parent,point+new Vector3(.22f,.89f,0),frame);
    }
    private void BuildHubCampfire(Transform parent,Vector3 p)
    {
        bool urban=campSceneryMap?.langKey=="gorod";
        if(urban)
        {
            HubFaceted(parent,"Steel heater barrel",p+Vector3.up*.08f,new Vector3(.65f,.59f,.65f),HubMat(0x46565C),10,.95f);
            HubFaceted(parent,"Barrel upper rim",p+Vector3.up*.62f,new Vector3(.69f,.07f,.69f),HubMat(0x829094),10,.96f);
            HubFaceted(parent,"Barrel charcoal",p+Vector3.up*.69f,new Vector3(.53f,.025f,.53f),HubMat(0x242B2D),10,.96f);
            p.y+=.57f;
        }
        else
        {
        HubPatch(parent,"Hearth ash",new Vector3(p.x,.055f,p.z),.72f,.53f,HubMat(0x695941),9);
        for(int i=0;i<9;i++)
        {
            float a=i*Mathf.PI*2/9;
            HubRock(parent,p+new Vector3(Mathf.Sin(a)*.55f,.04f,Mathf.Cos(a)*.45f),new Vector3(.23f,.19f,.23f),
                HubMat(i%2==0?0x9E9A83:0x777D71),i);
        }
        for(int i=0;i<3;i++)
            HubBox(parent,"Firewood",p+new Vector3(0,.12f,0),new Vector3(.11f,.12f,.62f),HubMat(0x63432A),Quaternion.Euler(0,i*60,0));
        }
        for(int i=0;i<3;i++)
        {
            var flame=HubFaceted(parent,"Candle flame",p+new Vector3((i-1)*.10f,.17f,0),new Vector3(.22f,i==1?.47f:.32f,.21f),
                CampMat(i==1?0xF7CB64:0xED8D42,1.1f),5,.03f,Quaternion.Euler(0,i*21,0),false);
            hubFlames.Add(flame.transform);
        }
        if(!MobileControls.IsTouchDevice)
        {
            var lamp=new GameObject("Campfire warmth",typeof(Light));
            lamp.transform.SetParent(parent,false);lamp.transform.localPosition=p+Vector3.up*.65f;
            hubFireLight=lamp.GetComponent<Light>();hubFireLight.type=LightType.Point;
            hubFireLight.color=new Color(1,.62f,.28f);hubFireLight.range=2.7f;
            hubFireLight.intensity=1.0f;hubFireLight.shadows=LightShadows.None;
        }
    }
    private void ApplyHubLighting()
    {
        if(hubSun!=null) return;
        foreach(var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if(light.type==LightType.Directional && light.enabled && (hubSun==null||light.intensity>hubSun.intensity)) hubSun=light;
        if(hubSun==null) return;
        hubSunColor=hubSun.color;hubSunIntensity=hubSun.intensity;hubSunRotation=hubSun.transform.rotation;hubSunShadow=hubSun.shadowStrength;
        hubSunShadowMode=hubSun.shadows;
        Color tint=campSceneryMap?.langKey=="gory"?new Color(.83f,.94f,1f):campSceneryMap?.langKey=="gorod"?new Color(.92f,.96f,1f):new Color(1f,.92f,.78f);
        hubSun.color=Color.Lerp(hubSunColor,tint,.66f);
        hubSun.intensity=Mathf.Clamp(hubSunIntensity*1.12f,.7f,1.8f);
        hubSun.transform.rotation=Quaternion.LookRotation(campScenery.transform.rotation*new Vector3(.50f,-.86f,.24f));
        hubSun.shadowStrength=Mathf.Min(hubSunShadow,.72f);
        if(!MobileControls.IsTouchDevice&&hubSun.shadows!=LightShadows.None) hubSun.shadows=LightShadows.Soft;
    }
    private void UpdateHubArt()
    {
        float t=Time.unscaledTime;
        for(int i=0;i<hubFlames.Count;i++)
        {
            Transform flame=hubFlames[i];
            if(flame==null) continue;
            Vector3 scale=flame.localScale;
            scale.y=(i==1?.47f:.32f)*(1+Mathf.Sin(t*4.3f+i*1.7f)*.09f);
            flame.localScale=scale;
        }
        if(hubFireLight!=null) hubFireLight.intensity=1f+Mathf.Sin(t*5.2f)*.08f;
    }
    private void ClearHubArt()
    {
        if(hubSun!=null) { hubSun.color=hubSunColor;hubSun.intensity=hubSunIntensity;hubSun.transform.rotation=hubSunRotation;hubSun.shadowStrength=hubSunShadow;hubSun.shadows=hubSunShadowMode; }
        hubSun=null;hubFireLight=null;hubFlames.Clear();hubFacetMeshes.Clear();
    }
}
