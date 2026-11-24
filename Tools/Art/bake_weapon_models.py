"""Bake the fifteen stylized, nonfunctional BulletRush weapon props and UI portraits.

Run from the project root with Python 3, Pillow and NumPy. All dimensions are
arbitrary game-space art proportions. Each prefab has one baked mesh, one
shared palette material and the original family's FirePoint marker.
"""
from pathlib import Path
import hashlib
import math
import re

import numpy as np
from PIL import Image, ImageDraw

from bake_ground_details import Mesh, asset_guid, bounds, read, save_mesh, write


ART = Path("Assets/Art/Weapons")
PREFABS = Path("Assets/Prefabs/Weapons/Stylized")
DATA = Path("Assets/Data/Weapons")
TEMPLATES = Path("Assets/Prefabs/Weapons")
SKIN = Path("Assets/Art/PlayerSkins")
PALETTE = {
    "ink": "#242b32", "dark": "#343e46", "steel": "#55636b", "silver": "#89969a",
    "ivory": "#e5dfcc", "sand": "#c9b17a", "gold": "#d4a04c", "brass": "#a47637",
    "wood": "#91603e", "woodlight": "#b98958", "olive": "#77805a", "moss": "#4e604a",
    "orange": "#d68742", "rust": "#a84735", "red": "#de6745", "amber": "#f9bb59",
    "blue": "#426f91", "navy": "#314a66", "cyan": "#63c6d5", "ice": "#b1e9e7",
    "teal": "#45867f", "mint": "#9ccdb4", "purple": "#765e9b", "violet": "#a285cb",
    "plum": "#493e60", "pink": "#ca91d1", "cream": "#fff1ce", "white": "#f0f1ea",
}
SWATCH = {name: i for i, name in enumerate(PALETTE)}
ATLAS_SIZE = 128


def uv(color):
    i = SWATCH[color]
    return ((i % 8 + .5) / 8, 1 - (i // 8 + .5) / 8)


def make_meta(path, template, replacements=None):
    target = Path(str(path) + ".meta")
    source = read(Path(str(template) + ".meta"))
    guid = asset_guid(path) if target.exists() else hashlib.sha256(
        ("BulletRush/weapon-art/" + path.as_posix()).encode()).hexdigest()[:32]
    source = re.sub(r"(?m)^guid: \w+", "guid: " + guid, source)
    if replacements:
        for before, after in replacements:
            source = source.replace(before, after)
    write(target, source)


class Prop:
    def __init__(self):
        self.mesh = Mesh()

    def face(self, points, center, color):
        p = np.array(points)
        normal = np.cross(p[1] - p[0], p[2] - p[0])
        if np.dot(normal, p.mean(axis=0) - center) < 0:
            points = points[::-1]
        self.mesh.face(points, uv(color))

    def loft(self, rings, color, cap=None):
        center = np.asarray([p for ring in rings for p in ring]).mean(axis=0)
        for a, b in zip(rings, rings[1:]):
            for i in range(len(a)):
                j = (i + 1) % len(a)
                self.face([a[i], a[j], b[j], b[i]], center, color)
        self.face(rings[0], center, color)
        self.face(rings[-1], center, cap or color)

    def block(self, x, y, z, w, h, d, color, cut=0, tip=None):
        """A solid rectangular or corner-cut toy shape, along the game +Z axis."""
        a, b, l, t = x-w/2, x+w/2, y-h/2, y+h/2
        if cut:
            c = min(cut, w*.35, h*.35)
            xy = [(a+c,l),(b-c,l),(b,l+c),(b,t-c),(b-c,t),(a+c,t),(a,t-c),(a,l+c)]
        else:
            xy = [(a,l),(b,l),(b,t),(a,t)]
        self.loft([[(px,py,zz) for px,py in xy] for zz in (z-d/2,z+d/2)],color,tip)

    def facet(self, x, y, z, w, h, d, color, tip=None, n=8):
        """Solid faceted decorative capsule; no internal or moving components."""
        self.loft([[(x+w*.5*math.cos(math.tau*i/n+math.pi/n),
                     y+h*.5*math.sin(math.tau*i/n+math.pi/n),zz)
                    for i in range(n)] for zz in (z-d/2,z+d/2)],color,tip)

    def side(self, profile, width, color, x=0):
        # The profile is a convex silhouette in (z, y), thickened along X.
        self.loft([[(xx,y,z) for z,y in profile] for xx in (x-width/2,x+width/2)],color)

    def panel(self, x, y, z, w, h, d, color):
        self.block(x,y,z,w,h,d,color,.018)

    def bands(self, z_values, y, w, h, color, depth=.023):
        for z in z_values:
            self.block(0,y,z,w,h,depth,color)


def rifle(name):
    p = Prop(); b, f, s, panel = p.block, p.facet, p.side, p.panel
    if name == "BasicRifle":
        s([(-.40,-.07),(-.40,.045),(-.18,.085),(-.14,-.065)],.13,"dark")
        b(0,0,-.055,.19,.23,.31,"steel",.025)
        b(0,.022,.16,.165,.17,.25,"dark",.02); panel(0,.065,.095,.184,.09,.13,"gold")
        f(0,0,.357,.105,.105,.185,"steel", "ink"); b(0,0,.439,.142,.12,.06,"dark",.014,"ink")
        s([(-.115,-.1),(.025,-.1),(.06,-.24),(-.045,-.24)],.12,"ink")
        b(0,.127,-.065,.095,.045,.19,"dark")
    elif name == "BurstRifle":
        s([(-.41,-.075),(-.40,.08),(-.23,.07),(-.17,-.025),(-.22,-.11)],.14,"navy")
        b(0,.005,-.015,.218,.22,.33,"teal",.037)
        b(0,.025,.21,.19,.17,.28,"steel",.03); p.bands([.10,.15],.045,.205,.18,"mint")
        f(0,0,.395,.13,.13,.148,"dark","ink")
        b(0,.145,.015,.145,.07,.22,"navy",.012)
        s([(-.16,-.09),(.025,-.09),(-.015,-.21),(-.12,-.235)],.125,"dark")
    elif name == "HunterRifle":
        s([(-.43,-.105),(-.43,.065),(-.13,.075),(-.09,-.06),(-.30,-.115)],.145,"wood")
        b(0,0,-.065,.175,.195,.24,"olive",.026)
        b(0,-.01,.135,.155,.135,.25,"woodlight",.019)
        f(0,0,.345,.09,.09,.25,"dark","ink")
        b(0,.137,-.058,.09,.06,.115,"ink")
        f(0,.187,-.035,.125,.125,.265,"moss","ice")
        b(0,.187,-.147,.145,.13,.045,"dark",.022)
        b(0,-.135,-.105,.105,.13,.115,"dark",.014)
    elif name == "GoldenRifle":
        s([(-.395,-.1),(-.405,.065),(-.25,.11),(-.14,.055),(-.19,-.09)],.155,"brass")
        b(0,0,-.055,.216,.25,.324,"gold",.038)
        b(0,.01,.176,.173,.17,.255,"dark",.024); p.bands([.09,.17,.25],.06,.19,.135,"gold",.028)
        f(0,0,.378,.13,.13,.177,"brass","ink")
        b(0,.143,-.07,.14,.07,.21,"brass",.018); b(0,.188,-.04,.1,.037,.1,"cream",.012)
        s([(-.105,-.11),(.055,-.11),(.03,-.25),(-.076,-.22)],.12,"ink")
    elif name == "PlasmaRifle":
        b(0,0,-.288,.18,.18,.22,"navy",.045)
        b(0,.015,-.055,.26,.26,.39,"ivory",.05)
        f(0,0,.15,.15,.15,.31,"cyan","ice")
        for x in (-.12,.12):
            s([(-.045,.09),(.27,.075),(.42,.025),(.29,-.055),(-.055,-.065)],.07,"silver",x)
        b(0,0,.408,.235,.18,.12,"navy",.042,"cyan")
        b(0,.151,-.045,.145,.06,.225,"teal",.016); b(0,.185,.006,.071,.025,.11,"ice")
        b(0,-.165,-.08,.13,.14,.16,"navy",.025)
    return p


def shotgun(name):
    p = Prop(); b, f, s, panel = p.block, p.facet, p.side, p.panel
    if name == "BasicShotgun":
        s([(-.33,-.065),(-.33,.055),(-.13,.077),(-.1,-.085)],.145,"wood")
        b(0,0,-.035,.185,.22,.22,"dark",.025)
        b(0,0,.13,.20,.18,.18,"woodlight",.025); p.bands([.09,.15],0,.21,.188,"wood",.022)
        f(0,0,.235,.19,.164,.074,"steel","ink")
        b(0,-.14,-.07,.11,.11,.11,"ink",.015)
    elif name == "HeavyShotgun":
        b(0,-.015,-.227,.22,.19,.205,"dark",.03)
        b(0,.005,-.015,.26,.27,.28,"moss",.045)
        b(0,0,.165,.285,.23,.165,"steel",.04)
        b(0,0,.238,.31,.24,.045,"orange",.034,"dark")
        b(0,.163,-.015,.19,.068,.16,"olive",.02)
        b(0,-.17,-.01,.15,.12,.16,"ink",.016)
    elif name == "DoubleShotgun":
        s([(-.36,-.09),(-.36,.06),(-.14,.08),(-.075,-.05),(-.18,-.12)],.155,"wood")
        b(0,0,-.043,.23,.22,.21,"brass",.023)
        for x in (-.067,.067):
            f(x,.016,.133,.124,.132,.272,"steel","ink")
            f(x,.016,.237,.143,.151,.067,"dark","ink")
        b(0,-.056,.122,.252,.111,.135,"woodlight",.024)
        b(0,-.14,-.116,.115,.128,.12,"ink",.018)
    elif name == "InfernoShotgun":
        b(0,-.015,-.208,.198,.19,.22,"rust",.036)
        b(0,0,-.017,.25,.26,.27,"rust",.047)
        b(0,.018,.132,.29,.22,.22,"orange",.042)
        for x in (-.14,.14):
            s([(-.02,.1),(.155,.13),(.215,.055),(.12,-.06),(-.07,-.05)],.045,"red",x)
        f(0,0,.242,.24,.205,.06,"dark","amber")
        b(0,.145,.01,.13,.045,.17,"amber",.012)
        b(0,-.17,-.055,.145,.12,.14,"dark",.018)
    elif name == "VoidShotgun":
        b(0,0,-.205,.224,.185,.185,"plum",.036)
        b(0,.015,-.027,.27,.27,.27,"ink",.045)
        f(0,0,.122,.275,.255,.22,"purple","violet",n=6)
        for x in (-.155,.155):
            s([(-.065,.09),(.11,.13),(.258,.078),(.225,-.028),(.006,-.08)],.06,"plum",x)
        f(0,0,.243,.19,.19,.057,"dark","pink",n=6)
        b(0,.16,-.045,.16,.057,.13,"violet",.017)
        b(0,-.16,-.06,.135,.12,.14,"plum",.024)
    return p


def smg(name):
    p = Prop(); b, f, s, panel = p.block, p.facet, p.side, p.panel
    if name == "BasicSMG":
        b(0,.015,-.225,.16,.16,.17,"ink",.015)
        b(0,.02,-.051,.20,.22,.30,"navy",.025)
        b(0,.055,.045,.202,.12,.135,"sand",.018)
        b(0,0,.18,.145,.143,.135,"steel",.02)
        f(0,0,.276,.14,.135,.058,"dark","ink")
        s([(-.065,-.09),(.045,-.09),(.07,-.26),(-.04,-.26)],.125,"dark")
    elif name == "TwinSMG":
        b(0,0,-.221,.2,.16,.18,"navy",.025)
        b(0,0,-.035,.22,.22,.29,"blue",.035)
        for x in (-.115,.115):
            b(x,.016,.12,.14,.16,.285,"navy",.023)
            b(x,.016,.273,.153,.166,.06,"silver",.018,"ink")
            b(x,.101,.09,.077,.032,.13,"cyan",.01)
        b(0,.14,-.04,.105,.067,.18,"ice",.015)
        b(0,-.167,-.04,.14,.13,.16,"dark",.02)
    elif name == "AssassinSMG":
        s([(-.35,-.06),(-.346,.035),(-.18,.063),(-.14,-.055)],.14,"plum")
        b(0,.005,-.045,.18,.215,.285,"ink",.03)
        b(0,.065,-.005,.179,.11,.205,"plum",.024)
        f(0,0,.205,.157,.157,.194,"dark","purple")
        b(0,.126,-.017,.11,.038,.17,"purple",.01)
        s([(-.08,-.085),(.02,-.085),(.015,-.225),(-.072,-.24)],.111,"ink")
    elif name == "LightningSMG":
        b(0,0,-.213,.19,.165,.16,"steel",.029)
        b(0,.01,-.052,.24,.22,.285,"ivory",.042)
        f(0,0,.136,.147,.147,.245,"cyan","ice")
        for x in (-.115,.115):
            s([(-.04,.075),(.055,.105),(.18,.06),(.19,-.026),(-.04,-.045)],.054,"blue",x)
        b(0,0,.268,.225,.20,.072,"blue",.03,"ice")
        b(0,.145,-.065,.142,.06,.17,"cyan",.016)
        b(0,-.163,-.03,.13,.14,.13,"navy",.024)
    elif name == "Minigun":
        b(0,-.012,-.235,.24,.22,.213,"dark",.033)
        b(0,0,-.075,.32,.31,.26,"olive",.05)
        f(0,0,.064,.32,.30,.126,"steel","dark")
        f(0,0,.13,.30,.28,.046,"gold","brass")
        for i in range(5):
            a = math.tau*i/5+math.pi/2
            x,y=.101*math.cos(a),.098*math.sin(a)
            f(x,y,.218,.09,.09,.18,"dark","steel",n=6)
            f(x,y,.281,.108,.108,.044,"steel","ink",n=6)
        f(0,0,.188,.108,.108,.114,"dark","orange",n=6)
        b(0,.185,-.075,.17,.063,.19,"sand",.018)
        b(0,-.185,-.08,.175,.13,.175,"dark",.025)
    return p


RECIPES = {
    "Rifle": (rifle, "Rifle_01", 9071840178064284164,
              ("BasicRifle", "BurstRifle", "HunterRifle", "GoldenRifle", "PlasmaRifle")),
    "Shotgun": (shotgun, "Shotgun_01", 8559010253350651214,
                ("BasicShotgun", "HeavyShotgun", "DoubleShotgun", "InfernoShotgun", "VoidShotgun")),
    "SMG": (smg, "SMG_01", 105748006008224434,
            ("BasicSMG", "TwinSMG", "AssassinSMG", "LightningSMG", "Minigun")),
}


def bake_palette():
    img = Image.new("RGB", (ATLAS_SIZE, ATLAS_SIZE), PALETTE["ink"])
    draw = ImageDraw.Draw(img)
    for i, color in enumerate(PALETTE.values()):
        x,y = i%8*16,i//8*16
        draw.rectangle((x,y,x+15,y+15),fill=color)
    tex_path = ART / "WeaponPalette.png"
    img.save(tex_path, optimize=True)
    make_meta(tex_path, SKIN/"Bee_Albedo.png", [("enableMipMap: 1", "enableMipMap: 0"),
                                               ("textureCompression: 1", "textureCompression: 0")])
    mat = ART / "WeaponPalette.mat"
    source = read(Path("Assets/Materials/Player.mat")).replace("m_Name: Player", "m_Name: WeaponPalette")
    tex_ref = "{fileID: 2800000, guid: " + asset_guid(tex_path) + ", type: 3}"
    for key in ("_BaseMap", "_MainTex"):
        source = re.sub(r"(    - " + key + r":\n        m_Texture: )\{fileID: 0\}",
                        lambda m: m[1]+tex_ref, source)
    source = source.replace("_Smoothness: 0.5", "_Smoothness: 0.16")
    source = source.replace("m_EnableInstancingVariants: 0", "m_EnableInstancingVariants: 1")
    write(mat,source); make_meta(mat,Path("Assets/Materials/Player.mat"))
    return np.asarray(img), mat


def portrait(prop, palette):
    """Orthographic rendering of the identical baked mesh for the equipment UI."""
    camera = np.array([-.92,.56,.38]); camera /= np.linalg.norm(camera)
    right = np.cross((0,1,0),camera); right /= np.linalg.norm(right)
    up = np.cross(camera,right)
    verts = np.asarray(prop.mesh.vertices); pts = verts[:,:3]
    projected = np.column_stack((pts@right,-pts@up))
    lo,hi = projected.min(axis=0),projected.max(axis=0)
    w,h = 640,384
    scale = min(w*.88/(hi[0]-lo[0]),h*.84/(hi[1]-lo[1]))
    projected = (projected-(lo+hi)/2)*scale+np.array([w/2,h/2])
    depth = pts@camera
    result = np.zeros((h,w,4),dtype=np.uint8); zbuf=np.full((h,w),-np.inf)
    light=np.array([-.38,.82,.43]); light/=np.linalg.norm(light)
    for tri in np.asarray(prop.mesh.indices).reshape(-1,3):
        normal=verts[tri[0],3:6]
        if np.dot(normal,camera)<=0: continue
        a,b,c=projected[tri]
        low=np.maximum(0,np.floor(np.minimum(np.minimum(a,b),c)).astype(int))
        high=np.minimum((w-1,h-1),np.ceil(np.maximum(np.maximum(a,b),c)).astype(int))
        xx,yy=np.meshgrid(np.arange(low[0],high[0]+1)+.5,np.arange(low[1],high[1]+1)+.5)
        u,v=b-a,c-a; det=u[0]*v[1]-u[1]*v[0]
        if abs(det)<1e-9: continue
        s=((xx-a[0])*v[1]-(yy-a[1])*v[0])/det
        t=(u[0]*(yy-a[1])-u[1]*(xx-a[0]))/det
        weights=np.stack((1-s-t,s,t),axis=-1); z=weights@depth[tri]
        rows,cols=slice(low[1],high[1]+1),slice(low[0],high[0]+1)
        front=(weights.min(axis=-1)>=-1e-8)&(z>zbuf[rows,cols])
        tx,ty=verts[tri[0],6:8]
        color=palette[int((1-ty)*ATLAS_SIZE),int(tx*ATLAS_SIZE)]
        shade=.65+.12*(normal[1]+1)/2+.29*max(0,np.dot(normal,light))
        result[rows,cols,:3][front]=np.clip(color*shade,0,255)
        result[rows,cols,3][front]=255; zbuf[rows,cols][front]=z[front]
    return Image.fromarray(result).resize((320,192),Image.Resampling.LANCZOS)


def visual_muzzle_positions(name, family):
    """Cosmetic particle anchors only, separate from the existing gameplay marker."""
    if name == "DoubleShotgun":
        return [(-.067, .016, .278), (.067, .016, .278)]
    if name == "TwinSMG":
        return [(-.115, .016, .311), (.115, .016, .311)]
    if name == "Minigun":
        return [(.101*math.cos(math.tau*i/5+math.pi/2),
                 .098*math.sin(math.tau*i/5+math.pi/2), .311) for i in range(5)]
    _, hi = bounds(RECIPES[family][0](name).mesh.vertices)
    return [(0, 0, hi[2] + .008)]


def add_visual_muzzles(source, name, family):
    if "m_Name: VisualMuzzle_" in source:
        return source
    root_object = RECIPES[family][2]
    root = next(m for m in re.finditer(r"(?ms)^--- !u!4 &(\d+)\nTransform:\n.*?(?=^--- !u!|\Z)", source)
                if f"m_GameObject: {{fileID: {root_object}}}" in m[0])
    root_transform = int(root[1])
    children = []
    blocks = []
    for i,(x,y,z) in enumerate(visual_muzzle_positions(name,family)):
        go_id = 8200000000000000010 + i*2
        tr_id = go_id+1
        children.append(f"  - {{fileID: {tr_id}}}\n")
        blocks.append(f"""--- !u!1 &{go_id}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {tr_id}}}
  m_Layer: 0
  m_Name: VisualMuzzle_{i:02d}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{tr_id}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: {x:.6g}, y: {y:.6g}, z: {z:.6g}}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {root_transform}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
""")
    new_root = root[0].replace("  m_Father:", "".join(children)+"  m_Father:", 1)
    return source[:root.start()]+new_root+source[root.end():]+"".join(blocks)


def bake_prefab(name, family, old, mesh, mat):
    template = TEMPLATES / family / (old + ".prefab")
    source = read(template).replace("m_Name: " + old, "m_Name: " + name)
    # Only the Model child's oversized primitive scale is replaced. Root and
    # FirePoint poses retain the original local position, rotation and scale.
    start = source.index("  m_Name: Model\n")
    source = source[:start]+re.sub(r"m_LocalScale: \{[^\n]+\}",
                                  "m_LocalScale: {x: 1, y: 1, z: 1}",source[start:],count=1)
    source = re.sub(r"m_Mesh: \{[^\n]+\}",
                    "m_Mesh: {fileID: 4300000, guid: " + asset_guid(mesh) + ", type: 2}",source)
    source = re.sub(r"(?m)^  - \{fileID: 2100000, guid: \w+, type: 2\}$",
                    "  - {fileID: 2100000, guid: " + asset_guid(mat) + ", type: 2}",source)
    # The old primitive's collider was disabled and is unnecessary for visuals.
    collider = re.search(r"(?m)^--- !u!65 &(\d+)\n",source)
    if collider:
        source = source.replace("  - component: {fileID: " + collider[1] + "}\n","")
        source = re.sub(r"(?ms)^--- !u!65 &\d+\n.*?(?=^--- !u!|\Z)","",source)
    prefab = PREFABS/(name+".prefab")
    source = add_visual_muzzles(source,name,family)
    write(prefab,source); make_meta(prefab,template)
    return prefab


def update_data(path, prefab, root, preview):
    text = read(path)
    ref = f"  weaponPrefab: {{fileID: {root}, guid: {asset_guid(prefab)}, type: 3}}"
    icon = f"  preview: {{fileID: 21300000, guid: {asset_guid(preview)}, type: 3}}"
    text = re.sub(r"(?m)^  weaponPrefab: .*",ref,text)
    if re.search(r"(?m)^  preview:",text):
        text = re.sub(r"(?m)^  preview: .*",icon,text)
    else:
        text=text.replace(ref+"\n",ref+"\n"+icon+"\n")
    write(path,text)


def bake():
    ART.mkdir(parents=True,exist_ok=True); PREFABS.mkdir(parents=True,exist_ok=True)
    make_meta(ART,SKIN); make_meta(PREFABS,TEMPLATES/"Rifle")
    palette,mat=bake_palette()
    for family,(builder,original,root,names) in RECIPES.items():
        for name in names:
            prop=builder(name)
            lo,hi=bounds(prop.mesh.vertices)
            assert len(prop.mesh.indices)//3 <= 600, name
            assert all(math.isfinite(v) for vertex in prop.mesh.vertices for v in vertex)
            mesh=ART/(name+".asset"); save_mesh(prop.mesh,mesh)
            image=ART/(name+"_Preview.png"); portrait(prop,palette).save(image,optimize=True)
            make_meta(image,SKIN/"Bee_Preview.png")
            prefab=bake_prefab(name,family,original,mesh,mat)
            update_data(DATA/family/("Weapon_"+name+".asset"),prefab,root,image)
            if name.startswith("Basic"):
                legacy=Path("Assets/_Game/Scripts/Combat")/("Weapon_"+original+".asset")
                if legacy.exists(): update_data(legacy,prefab,root,image)
            print(f"{name:19s} {len(prop.mesh.indices)//3:3d} tris   {lo[2]:.3f}..{hi[2]:.3f} z")


if __name__ == "__main__":
    bake()
