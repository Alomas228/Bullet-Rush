using System.Collections.Generic;
using UnityEngine;

public sealed partial class ArcadeMenuSkin
{
    private static readonly Vector3 HubArmoryPosition = new Vector3(-3.25f,0,1.65f);
    private static readonly Vector3 HubHeroPosition = new Vector3(0,0,2.65f);
    private static readonly Vector3 HubWorkshopPosition = new Vector3(3.2f,0,1.85f);
    private static readonly Vector3 HubMapPosition = new Vector3(2.8f,0,-1.45f);
    private Transform hubWeaponPivot, hubHeroPivot, hubMapTable, hubHeroWeaponPoint;
    private GameObject hubWeaponModel, hubHeroModel, hubHeroWeaponModel;
    private WeaponData hubShownWeapon,hubShownHeroWeapon;
    private ClothingData hubShownHero;
    private Material hubHeroMaterial, hubMapNormal, hubMapSelected;
    private readonly Dictionary<GameMap, Renderer> hubMapRims = new Dictionary<GameMap, Renderer>();
    private readonly Dictionary<int, Material> hubPalette = new Dictionary<int, Material>();
    private readonly List<MeshRenderer> hubStaticRenderers = new List<MeshRenderer>();

    public static float GetHubFieldOfView()
    {
        HubView view=Instance!=null ? Instance.hubView : HubView.Home;
        if(ResponsiveUi.Portrait) return view==HubView.Home?57f:53f;
        float aspect=Screen.width/(float)Mathf.Max(1,Screen.height);
        // A portrait lens calms the perspective of the close-ups. At 4:3
        // slightly widen the framing so every station remains in the picture.
        float lens=view==HubView.Home?43f:view==HubView.Maps?40f:39f;
        return lens+Mathf.Clamp((1.77f-aspect)*12f,0,10);
    }

    public static void GetHubCameraPose(Transform player, out Vector3 position, out Quaternion rotation)
    {
        HubView view=Instance!=null ? Instance.hubView : HubView.Home;
        bool portrait=ResponsiveUi.Portrait;
        Vector3 point, camera;
        switch(view)
        {
            case HubView.Armory:
                point=HubArmoryPosition+new Vector3(0,1.02f,.08f);
                camera=HubArmoryPosition+new Vector3(-.58f,2.45f,portrait?-4.0f:-3.65f);
                break;
            case HubView.Character:
            case HubView.Common:
                point=HubHeroPosition+new Vector3(0,1.03f,0);
                camera=HubHeroPosition+new Vector3(-.36f,2.45f,portrait?-3.85f:-3.35f);
                break;
            case HubView.Workbench:
                point=HubWorkshopPosition+new Vector3(0,1.0f,0);
                camera=HubWorkshopPosition+new Vector3(.54f,2.75f,portrait?-4.1f:-3.6f);
                break;
            case HubView.Maps:
                point=HubMapPosition+new Vector3(0,.99f,.08f);
                camera=HubMapPosition+new Vector3(-.32f,portrait?5.8f:5.15f,portrait?-2.7f:-2.95f);
                break;
            default:
                point=new Vector3(.1f,.50f,.65f);
                camera=new Vector3(portrait?0:-.65f,portrait?11.3f:8.1f,portrait?-10.8f:-8.0f);
                break;
        }
        // Leave the lower half of a portrait screen free for the controls, without
        // changing the gameplay camera's offset/FOV.
        if(portrait) point.y-=view==HubView.Home?1.15f:1.0f;
        Vector3 origin=new Vector3(player.position.x,0,player.position.z);
        Quaternion basis=Quaternion.LookRotation(new Vector3(-CampCameraOffset.x,0,-CampCameraOffset.z));
        position=origin+basis*camera;
        rotation=Quaternion.LookRotation(origin+basis*point-position);
    }

    private void BuildHubScenery(GameMap map, EnvironmentTheme theme)
    {
        Transform scenery=campScenery.transform;
        BuildHubClearing(map,theme);
        bool urban=map.langKey=="gorod", winter=map.langKey=="gory";
        Material wood=HubMat(urban?0x647377:winter?0x776452:0xA5794D), darkWood=HubMat(urban?0x39464C:0x65503B), lightWood=HubMat(urban?0x97A5A6:0xC69A69),
            canvas=HubMat(0x879366), metal=HubMat(0x424D4A), brass=HubMat(0xD9B873),
            stone=HubMat(0x89918A), green=HubMat(0x536844), paper=HubMat(0xE6D6B4);

        // Wooden platforms, floorboards and silhouettes keep station placement legible.
        Transform armory=HubHolder(scenery,"Armory display",HubArmoryPosition);
        HubPlatform(armory,3.0f,2.45f,darkWood,wood);
        HubTable(armory,2.18f,1.13f,darkWood,lightWood);
        HubBox(armory,"Rack backing",new Vector3(0,1.42f,.70f),new Vector3(2.22f,1.12f,.10f),darkWood);
        for(int i=0;i<5;i++) HubBox(armory,"Rack board",new Vector3(0,.97f+i*.22f,.64f),new Vector3(2.1f,.19f,.05f),i%2==0?wood:lightWood);
        BuildHubShelter(armory,2.9f,1.66f,HubMat(urban?0x53666D:winter?0x526B79:0x647E53),wood,darkWood,true);
        HubBox(armory,"Turntable base",new Vector3(0,.83f,-.05f),new Vector3(1.48f,.09f,.70f),metal);
        HubBox(armory,"Felt display",new Vector3(0,.889f,-.05f),new Vector3(1.40f,.025f,.62f),green);
        hubWeaponPivot=HubHolder(armory,"Weapon inspection",new Vector3(0,.91f,-.05f));
        if(UpgradeManager.Instance!=null)
        {
            int i=0;
            foreach(var w in UpgradeManager.Instance.GetAvailableWeapons())
            {
                if(w==null||w.WeaponPrefab==null) continue;
                Transform rack=HubHolder(armory,"Rack sample",new Vector3((i-1)*.65f,1.12f,.47f));
                var model=HubWeaponCopy(w,rack,.63f);
                if(model!=null) model.transform.localRotation=Quaternion.Euler(0,90,-23);
                if(++i>=3) break;
            }
        }
        BuildCampCrate(HubArmoryPosition+new Vector3(-1.47f,.29f,-.36f),new Vector3(.58f,.58f,.55f),true);
        BuildCampCrate(HubArmoryPosition+new Vector3(1.37f,.23f,.62f),new Vector3(.5f,.46f,.46f),false);

        Transform hero=HubHolder(scenery,"Fighter's podium",HubHeroPosition);
        HubFaceted(hero,"Stone pedestal",new Vector3(0,.10f,0),new Vector3(1.85f,.32f,1.85f),stone,8,.94f);
        HubFaceted(hero,"Upper stone",new Vector3(0,.42f,0),new Vector3(1.64f,.04f,1.64f),HubMat(0xB7B29D),8,.96f);
        hubHeroPivot=HubHolder(hero,"Fighter inspection",new Vector3(0,.46f,0));
        hubHeroWeaponPoint=HubHolder(hubHeroPivot,"Carried weapon",new Vector3(.53f,.46f,-.46f));
        HubBox(hero,"Banner pole",new Vector3(1.04f,1.15f,.69f),new Vector3(.055f,2.3f,.055f),darkWood);
        HubBox(hero,"Banner",new Vector3(.73f,1.82f,.69f),new Vector3(.63f,.80f,.035f),green);
        HubBox(hero,"Banner stripe",new Vector3(.73f,1.65f,.666f),new Vector3(.60f,.09f,.02f),brass);

        Transform bench=HubHolder(scenery,"Workshop",HubWorkshopPosition);
        HubPlatform(bench,2.85f,2.16f,darkWood,wood);
        HubTable(bench,2.12f,1.0f,darkWood,lightWood);
        HubBox(bench,"Pegboard",new Vector3(0,1.40f,.57f),new Vector3(2.04f,1.15f,.09f),darkWood);
        BuildHubShelter(bench,2.75f,1.64f,HubMat(urban?0x637F85:winter?0x617985:0xB08B58),wood,darkWood,false);
        for(int i=0;i<5;i++)
        {
            HubBox(bench,"Tool peg",new Vector3(-.83f+i*.41f,1.65f,.40f),new Vector3(.06f,.06f,.13f),brass);
            HubBox(bench,"Tool",new Vector3(-.83f+i*.41f,1.43f,.36f),new Vector3(.105f,.41f,.08f),metal,Quaternion.Euler(0,0,(i%3-1)*18));
            HubBox(bench,"Tool head",new Vector3(-.83f+i*.41f,1.54f,.31f),new Vector3(.23f,.13f,.11f),stone);
        }
        HubBox(bench,"Vice body",new Vector3(-.68f,.99f,-.25f),new Vector3(.41f,.35f,.38f),metal);
        HubBox(bench,"Vice jaws",new Vector3(-.68f,1.20f,-.25f),new Vector3(.51f,.09f,.38f),stone);
        HubBox(bench,"Toolbox",new Vector3(.68f,.96f,.08f),new Vector3(.6f,.32f,.34f),green);
        HubBox(bench,"Blueprint",new Vector3(.15f,.85f,-.16f),new Vector3(.65f,.025f,.45f),paper,Quaternion.Euler(0,8,0));
        HubBox(bench,"Blueprint drawing",new Vector3(.15f,.868f,-.16f),new Vector3(.42f,.01f,.20f),HubMat(0xB5BBAD),Quaternion.Euler(0,8,0));
        HubLantern(bench,new Vector3(.91f,.88f,-.30f),metal);

        hubMapTable=HubHolder(scenery,"Planning table",HubMapPosition);
        HubPlatform(hubMapTable,3.35f,2.28f,darkWood,wood);
        HubTable(hubMapTable,3.04f,2.02f,darkWood,lightWood);
        HubBox(hubMapTable,"Table mat",new Vector3(0,.84f,0),new Vector3(2.94f,.035f,1.92f),green);
        hubMapNormal=HubMat(0x5D5646);
        hubMapSelected=HubMat(0xF0C270);
        if(environment!=null)
        {
            int count=Mathf.Min(6,environment.MapsCount);
            for(int i=0;i<count;i++)
            {
                GameMap a=environment.GetMap(i);
                if(a==null) continue;
                Transform tile=HubHolder(hubMapTable,"Arena miniature "+a.langKey,new Vector3((i%3-1)*.96f,.88f,(.5f-i/3)*.90f));
                BuildHubMiniature(tile,a);
            }
        }
        // Campsite details, arranged outside the central player and camera path.
        if(!urban) BuildCampTent(new Vector3(-3.95f,.035f,4.15f));
        BuildCampCrate(new Vector3(-1.8f,.24f,3.85f),new Vector3(.58f,.48f,.55f),false);
        BuildHubCampfire(scenery,new Vector3(-2.7f,0,-1.65f));
        HubPostLamp(scenery,new Vector3(-1.65f,0,.75f),wood,metal);
        HubPostLamp(scenery,new Vector3(1.6f,0,-.25f),wood,metal);
        HubPostLamp(scenery,new Vector3(-1.4f,0,-2.7f),wood,metal);
        BakeHubStatic();
        RefreshHubPreview();
    }

    private Material HubMat(int color)
    {
        if(!hubPalette.TryGetValue(color,out Material m)) { m=CampMat(color); hubPalette[color]=m; }
        return m;
    }
    private static Transform HubHolder(Transform parent,string name,Vector3 point)
    {
        var holder=new GameObject(name).transform; holder.SetParent(parent,false); holder.localPosition=point; return holder;
    }
    private MeshRenderer HubShape(Transform parent,string name,PrimitiveType type,Vector3 point,Vector3 size,Material mat,Quaternion? rotation=null,bool batch=true)
    {
        var obj=GameObject.CreatePrimitive(type);
        obj.name=name; obj.transform.SetParent(parent,false);
        obj.transform.localPosition=point; obj.transform.localRotation=rotation??Quaternion.identity; obj.transform.localScale=size;
        var coll=obj.GetComponent<Collider>(); coll.enabled=false; Destroy(coll);
        var renderer=obj.GetComponent<MeshRenderer>(); renderer.sharedMaterial=mat;
        if(batch) hubStaticRenderers.Add(renderer);
        return renderer;
    }
    private MeshRenderer HubBox(Transform parent,string name,Vector3 point,Vector3 size,Material mat,Quaternion? rotation=null,bool batch=true) =>
        HubShape(parent,name,PrimitiveType.Cube,point,size,mat,rotation,batch);
    private void HubPlatform(Transform parent,float w,float d,Material edge,Material plank)
    {
        HubBox(parent,"Platform frame",new Vector3(0,.025f,0),new Vector3(w,.05f,d),edge);
        for(int i=0;i<7;i++) HubBox(parent,"Floorboards",new Vector3((i-3)*w/7,.065f,0),new Vector3(w/7-.023f,.035f,d-.04f),plank);
    }
    private void HubTable(Transform parent,float w,float d,Material frame,Material plank)
    {
        for(int x=-1;x<=1;x+=2) for(int z=-1;z<=1;z+=2)
            HubBox(parent,"Leg",new Vector3(x*(w/2-.12f),.4f,z*(d/2-.12f)),new Vector3(.13f,.8f,.13f),frame);
        HubBox(parent,"Table frame",new Vector3(0,.72f,0),new Vector3(w-.08f,.15f,d-.08f),frame);
        for(int i=0;i<5;i++) HubBox(parent,"Table planks",new Vector3(0,.80f,(i-2)*d/5),new Vector3(w,.075f,d/5-.012f),plank);
    }
    private void HubLantern(Transform parent,Vector3 point,Material frame)
    {
        var lamp=HubHolder(parent,"Warm lantern",point);
        HubBox(lamp,"Base",Vector3.zero,new Vector3(.26f,.065f,.26f),frame);
        HubBox(lamp,"Glass",new Vector3(0,.14f,0),new Vector3(.17f,.24f,.17f),CampMat(0xEEC279,.85f));
        HubBox(lamp,"Lid",new Vector3(0,.27f,0),new Vector3(.25f,.065f,.25f),frame);
        for(int x=-1;x<=1;x+=2) for(int z=-1;z<=1;z+=2)
            HubBox(lamp,"Frame",new Vector3(x*.1f,.14f,z*.1f),new Vector3(.025f,.24f,.025f),frame);
    }

    private void BuildHubMiniature(Transform tile,GameMap map)
    {
        int color=map.langKey switch { "les"=>0x738B58,"plyazh"=>0xD7B773,"gory"=>0xAABDC6,"pustynya"=>0xBE8253,"gorod"=>0x7D8584,"kosmos"=>0x555B70,_=>0x929779 };
        hubMapRims[map]=HubBox(tile,"Selection rim",Vector3.zero,new Vector3(.91f,.10f,.85f),hubMapNormal,batch:false);
        HubBox(tile,"Terrain",new Vector3(0,.065f,0),new Vector3(.84f,.06f,.78f),HubMat(color));
        Material trunk=HubMat(0x755138), leaves=HubMat(0x476846), lightLeaves=HubMat(0x789450), rock=HubMat(0x7D8484),
            snow=HubMat(0xE1EDF0), water=HubMat(0x6AABB8), building=HubMat(0xB4B5A7);
        switch(map.langKey)
        {
            case "les":
                for(int i=0;i<4;i++) HubMiniTree(tile,new Vector3(-.26f+i*.17f,.1f,.02f+(i%2)*.19f),trunk,i%2==0?leaves:lightLeaves);
                HubBox(tile,"Path",new Vector3(0,.10f,-.24f),new Vector3(.7f,.018f,.13f),HubMat(0xBEA378));
                HubRock(tile,new Vector3(.25f,.10f,-.10f),new Vector3(.19f,.12f,.19f),rock,2);
                break;
            case "plyazh":
                HubBox(tile,"Sea",new Vector3(0,.106f,-.23f),new Vector3(.83f,.025f,.31f),water);
                HubBox(tile,"Foam edge",new Vector3(0,.122f,-.085f),new Vector3(.83f,.01f,.035f),HubMat(0xC2DFD5));
                HubBox(tile,"Palm trunk",new Vector3(-.16f,.25f,.14f),new Vector3(.05f,.30f,.05f),trunk,Quaternion.Euler(0,0,12));
                for(int i=0;i<5;i++) HubFaceted(tile,"Palm frond",new Vector3(-.19f,.40f,.14f),new Vector3(.12f,.20f,.08f),lightLeaves,4,.06f,Quaternion.Euler(66,i*72,0));
                break;
            case "gory":
                for(int i=0;i<3;i++)
                {
                    float x=-.23f+i*.23f, h=i==1?.32f:.22f;
                    HubFaceted(tile,"Mountain peak",new Vector3(x,.10f,.12f),new Vector3(.34f,h,.33f),rock,5,.32f,Quaternion.Euler(0,22+i*20,0));
                    HubFaceted(tile,"Snowcap",new Vector3(x,.10f+h,.12f),new Vector3(.11f,.11f,.11f),snow,5,.025f,Quaternion.Euler(0,22+i*20,0));
                }
                HubMiniTree(tile,new Vector3(-.26f,.1f,-.24f),trunk,leaves,.54f);
                break;
            case "pustynya":
                HubFaceted(tile,"Mesa",new Vector3(-.13f,.10f,.08f),new Vector3(.48f,.25f,.37f),HubMat(0x9D6246),6,.70f);
                HubFaceted(tile,"Mesa top",new Vector3(-.13f,.35f,.08f),new Vector3(.34f,.12f,.26f),HubMat(0xD29562),6,.75f);
                HubBox(tile,"Cactus",new Vector3(.26f,.21f,-.16f),new Vector3(.08f,.25f,.08f),leaves);
                HubBox(tile,"Cactus arm",new Vector3(.31f,.25f,-.16f),new Vector3(.1f,.07f,.08f),leaves);
                break;
            case "gorod":
                HubBox(tile,"Road",new Vector3(0,.105f,-.18f),new Vector3(.83f,.02f,.24f),HubMat(0x414846));
                for(int i=0;i<3;i++)
                {
                    float x=-.25f+i*.25f, h=.26f+i*.08f;
                    HubBox(tile,"Building",new Vector3(x,.10f+h/2,.15f),new Vector3(.19f,h,.28f),building);
                    for(int j=0;j<2;j++) HubBox(tile,"Window strip",new Vector3(x,.18f+j*.082f,.005f),new Vector3(.15f,.045f,.015f),HubMat(0x56747B));
                }
                for(int i=0;i<3;i++) HubBox(tile,"Road stripe",new Vector3(-.24f+i*.24f,.12f,-.18f),new Vector3(.13f,.01f,.025f),snow);
                break;
            case "kosmos":
                HubShape(tile,"Crater",PrimitiveType.Cylinder,new Vector3(-.17f,.11f,-.08f),new Vector3(.31f,.023f,.31f),HubMat(0x3F465B));
                HubBox(tile,"Station",new Vector3(.14f,.21f,.16f),new Vector3(.24f,.22f,.28f),snow);
                HubBox(tile,"Solar array",new Vector3(.14f,.31f,.16f),new Vector3(.62f,.045f,.16f),HubMat(0x536D9C));
                HubBox(tile,"Dish",new Vector3(-.15f,.20f,.23f),new Vector3(.12f,.20f,.12f),rock,Quaternion.Euler(0,0,20));
                HubRock(tile,new Vector3(.30f,.10f,-.20f),new Vector3(.19f,.10f,.15f),rock,5);
                break;
        }
    }
    private void HubMiniTree(Transform parent,Vector3 p,Material trunk,Material leaves,float scale=1)
    {
        HubFaceted(parent,"Tree trunk",p,new Vector3(.055f,.18f,.055f)*scale,trunk,5,.8f);
        HubFaceted(parent,"Tree crown",p+Vector3.up*.10f*scale,new Vector3(.24f,.25f,.24f)*scale,leaves,6,.04f);
        HubFaceted(parent,"Tree top",p+Vector3.up*.24f*scale,new Vector3(.16f,.20f,.16f)*scale,leaves,6,.02f);
    }

    private void BakeHubStatic()
    {
        // Include imported backdrop props, crates, tents and rack weapon meshes,
        // not only the procedural furniture. Keep interactive objects separate.
        var sources = new List<MeshRenderer>();
        var rims = new HashSet<Renderer>(hubMapRims.Values);
        foreach(var renderer in campScenery.GetComponentsInChildren<MeshRenderer>(false))
        {
            if(rims.Contains(renderer) || hubFlames.Contains(renderer.transform) ||
               (hubWeaponPivot!=null && renderer.transform.IsChildOf(hubWeaponPivot)) ||
               (hubHeroPivot!=null && renderer.transform.IsChildOf(hubHeroPivot))) continue;
            sources.Add(renderer);
        }
        ArenaStaticMeshBatcher.BatchStaticRenderers(campScenery.transform,sources);
        hubStaticRenderers.Clear();
    }

    private GameObject HubWeaponCopy(WeaponData weapon,Transform pivot,float span)
    {
        if(weapon?.WeaponPrefab==null) return null;
        GameObject root=new GameObject("Preview "+weapon.WeaponName);
        root.transform.SetParent(pivot,false);
        // Copy only render data, never instantiate Weapon or other gameplay behaviour.
        CopyHubRenderers(weapon.WeaponPrefab.transform,root.transform);
        root.transform.localRotation=Quaternion.Euler(0,90,0);
            NormalizeHubModel(root,pivot.position,span,false);
        return root;
    }
    private static void CopyHubRenderers(Transform source,Transform destination)
    {
        var filter=source.GetComponent<MeshFilter>();
        var renderer=source.GetComponent<MeshRenderer>();
        if(filter!=null&&filter.sharedMesh!=null&&renderer!=null)
        {
            destination.gameObject.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;
            destination.gameObject.AddComponent<MeshRenderer>().sharedMaterials=renderer.sharedMaterials;
        }
        for(int i=0;i<source.childCount;i++)
        {
            Transform from=source.GetChild(i);
            if(!from.gameObject.activeSelf) continue;
            Transform to=HubHolder(destination,from.name,from.localPosition);
            to.localRotation=from.localRotation; to.localScale=from.localScale;
            CopyHubRenderers(from,to);
        }
    }
    private static void NormalizeHubModel(GameObject obj,Vector3 ground,float size,bool height)
    {
        if(!TryCampBounds(obj,out Bounds b)) return;
        float dimension=height?b.size.y:Mathf.Max(b.size.x,b.size.y,b.size.z);
        if(dimension<.001f) return;
        obj.transform.localScale*=size/dimension;
        if(TryCampBounds(obj,out b)) obj.transform.position+=ground-new Vector3(b.center.x,b.min.y,b.center.z);
    }
    private void RefreshHubPreview()
    {
        if(campScenery==null || hubWeaponPivot==null || hubHeroPivot==null) return;
        WeaponData equippedWeapon=null;
        if(UpgradeManager.Instance!=null)
            foreach(var w in UpgradeManager.Instance.GetAvailableWeapons()) if(w!=null&&EquipmentManager.IsEquipped(w)) { equippedWeapon=w; break; }
        WeaponData weapon=hubView==HubView.Armory?HubSelected as WeaponData:equippedWeapon;
        if(weapon!=hubShownWeapon || (weapon!=null&&hubWeaponModel==null))
        {
            if(hubWeaponModel!=null) { hubWeaponModel.SetActive(false); Destroy(hubWeaponModel); }
            hubWeaponPivot.localRotation=Quaternion.identity;
            hubWeaponModel=HubWeaponCopy(weapon,hubWeaponPivot,1.72f);
            hubShownWeapon=weapon;
        }
        ClothingData hero=hubView==HubView.Character ? HubSelected as ClothingData : CharacterBuild.Selected();
        if(hero!=hubShownHero || hubHeroModel==null)
        {
            if(hubHeroModel!=null) { hubHeroModel.SetActive(false); Destroy(hubHeroModel); }
            hubHeroPivot.localRotation=Quaternion.identity;
            hubHeroModel=new GameObject("Fighter model",typeof(MeshFilter),typeof(MeshRenderer));
            hubHeroModel.transform.SetParent(hubHeroPivot,false);
            if(hubHeroMaterial==null) hubHeroMaterial=CampMat(0xFFFFFF);
            var mr=hubHeroModel.GetComponent<MeshRenderer>();
            mr.sharedMaterial=hubHeroMaterial;
            if(hero!=null&&hero.BodyMesh!=null)
            {
                hubHeroModel.GetComponent<MeshFilter>().sharedMesh=hero.BodyMesh;
                var props=new MaterialPropertyBlock();
                props.SetColor("_BaseColor",hero.HasSkinTexture?Color.white:hero.BodyColor);
                props.SetColor("_Color",hero.HasSkinTexture?Color.white:hero.BodyColor);
                if(hero.HasSkinTexture) { props.SetTexture("_BaseMap",hero.SkinTexture); props.SetTexture("_MainTex",hero.SkinTexture); }
                mr.SetPropertyBlock(props);
                hubHeroModel.transform.localRotation=Quaternion.Euler(0,180,0);
                NormalizeHubModel(hubHeroModel,hubHeroPivot.position,1.35f,true);
            }
            else
            {
                var block=HubShape(hubHeroModel.transform,"Default fighter",PrimitiveType.Cube,new Vector3(0,.62f,0),Vector3.one*1.24f,hubHeroMaterial,batch:false);
                var props=new MaterialPropertyBlock();
                Color color=hero!=null?hero.BodyColor:Color.white;
                props.SetColor("_BaseColor",color);props.SetColor("_Color",color);
                if(hero!=null&&hero.HasSkinTexture) { props.SetTexture("_BaseMap",hero.SkinTexture);props.SetTexture("_MainTex",hero.SkinTexture); }
                block.SetPropertyBlock(props);
                mr.enabled=false;
            }
            hubShownHero=hero;
        }
        bool showHero=hubView==HubView.Character || hubView==HubView.Common;
        hubHeroModel.SetActive(showHero);
        if(showHero && hubHeroWeaponPoint!=null && (hubShownHeroWeapon!=equippedWeapon || (equippedWeapon!=null&&hubHeroWeaponModel==null)))
        {
            if(hubHeroWeaponModel!=null) { hubHeroWeaponModel.SetActive(false);Destroy(hubHeroWeaponModel); }
            hubHeroWeaponModel=HubWeaponCopy(equippedWeapon,hubHeroWeaponPoint,.64f);
            if(hubHeroWeaponModel!=null) hubHeroWeaponModel.transform.localRotation=Quaternion.Euler(0,165,0);
            hubShownHeroWeapon=equippedWeapon;
        }
        if(hubHeroWeaponModel!=null) hubHeroWeaponModel.SetActive(showHero);
        GameMap selected=hubView==HubView.Maps ? HubSelected as GameMap : campSceneryMap;
        foreach(var kv in hubMapRims) if(kv.Value!=null) kv.Value.sharedMaterial=kv.Key==selected?hubMapSelected:hubMapNormal;
    }
    private void UpdateHubWorld()
    {
        if(hubBoard==null || campScenery==null || !campScenery.activeInHierarchy ||
           root==null || !root.activeInHierarchy) return;
        UpdateHubArt();
        if(hubView==HubView.Armory || hubView==HubView.Character || hubView==HubView.Common)
        {
            if(Time.unscaledTime-hubLastDrag>2.0f) hubPreviewYaw+=Mathf.Min(Time.unscaledDeltaTime,.05f)*16f;
            Transform pivot=hubView==HubView.Armory?hubWeaponPivot:hubHeroPivot;
            if(pivot!=null) pivot.localRotation=Quaternion.Euler(0,hubPreviewYaw,0);
        }
    }
    private void ClickHubMiniature(Vector2 screenPosition)
    {
        if(hubView!=HubView.Maps || hubMapTable==null || Camera.main==null) return;
        // The miniature stands are not gameplay objects: pick the closest
        // projected tile from the pointer position, no physics layers/colliders.
        Camera camera=Camera.main;
        float closest=float.PositiveInfinity;
        int selected=-1;
        for(int i=0;i<hubItems.Count;i++)
        {
            if(!(hubItems[i] is GameMap map)||!hubMapRims.TryGetValue(map,out Renderer rim)||rim==null) continue;
            Vector3 center=camera.WorldToScreenPoint(rim.bounds.center);
            if(center.z<=0) continue;
            Vector3 edge=camera.WorldToScreenPoint(rim.bounds.center+hubMapTable.right*.4f);
            float radius=Mathf.Max(24,(new Vector2(edge.x-center.x,edge.y-center.y)).magnitude)*1.3f;
            float distance=Vector2.Distance(screenPosition,new Vector2(center.x,center.y));
            if(distance<radius && distance<closest) { closest=distance; selected=i; }
        }
        if(selected>=0) SelectHubItem(selected);
    }
    private void ClearHubSceneryReferences()
    {
        ClearHubArt();
        hubWeaponPivot=hubHeroPivot=hubMapTable=hubHeroWeaponPoint=null;
        hubWeaponModel=hubHeroModel=hubHeroWeaponModel=null;
        hubShownWeapon=hubShownHeroWeapon=null; hubShownHero=null;
        hubHeroMaterial=hubMapNormal=hubMapSelected=null;
        hubMapRims.Clear(); hubPalette.Clear(); hubStaticRenderers.Clear();
    }
}
