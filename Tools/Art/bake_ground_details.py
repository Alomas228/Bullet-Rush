"""Bake the six arena dressing prefabs. Run with Python 3 from the Unity project root."""
from functools import lru_cache
from pathlib import Path
import hashlib
import math
import random
import re
import struct

MAPS = Path("Assets/Data/Maps")
ART = MAPS / "ArenaArt"
OUT = ART / "GroundDetails"
FOREST = "Les"
ROWS = {"Plyazh": 0, "Gory": 1, "Pustynya": 2, "Gorod": 3, "Kosmos": 4}
SEEDS = {"Les": 3101, "Plyazh": 2202, "Gory": 4404, "Pustynya": 5505, "Gorod": 8808, "Kosmos": 7707}
ROOT_ID = 4186967400244600867
TR_ID = 1640600089488504829
TILE = 30

# Each row describes objects that belong together. Dimensions are in game units.
RECIPES = {
    "Les": [
        [("Fern",1.5,.61,1.05),("MossyStones",.87,.28,.61),("Fern",.70,.31,.53)],
        [("FallenTimber",1.72,.31,.68),("Mushroom",.36,.28,.32),("Mushroom",.23,.18,.22)],
        [("ClearingStump",.72,.39,.66),("Fern",1.10,.44,.77),("Mushroom",.30,.23,.26)]],
    "Plyazh": [
        [("Driftwood",1.34,.19,.53),("Shell",.49,.14,.43),("Shell",.33,.10,.31)],
        [("SeaGrass",1.35,.61,.98),("BeachRock",.97,.32,.73),("SeaGrass",.84,.40,.65)],
        [("BeachRock",1.40,.43,.94),("LeafTrash",.95,.15,.61),("Shell",.44,.12,.40)]],
    "Gory": [
        [("Boulder",1.30,.57,1.0),("Pebble",.91,.23,.58),("AlpineGrass",.74,.36,.52)],
        [("AlpineGrass",1.27,.51,.91),("Pebble",1.15,.28,.71),("AlpineGrass",.64,.27,.50)],
        [("RockFormation",1.53,.53,.84),("Boulder",.62,.26,.5),("Pebble",.60,.17,.40)]],
    "Pustynya": [
        [("Agave",1.22,.62,.92),("StoneSmall",.67,.25,.57),("Agave",.65,.32,.56)],
        [("DriedBush",1.25,.49,.90),("Stone",1.01,.30,.68),("StoneSmall",.53,.18,.34)],
        [("Bones",1.23,.30,.62),("StoneSmall",.80,.27,.62),("Agave",.82,.37,.60)],
        [("Stone",1.32,.35,.78),("Agave",.87,.43,.71),("StoneSmall",.51,.19,.42)]],
    "Gorod": [
        [("Drain",1.35,.09,.84),("Paint",1.48,.022,.16),("Weeds",.48,.21,.32)],
        [("Manhole",1.28,.08,1.28),("Paver",.85,.085,.48),("Weeds",.58,.27,.41)],
        [("Paver",1.31,.16,.72),("Paver",.79,.10,.45),("Weeds",.79,.34,.52)],
        [("Curb",1.68,.21,.30),("Paint",1.15,.018,.15),("Weeds",.51,.24,.38)],
        [("UtilityCover",1.05,.07,.76),("Curb",1.22,.16,.28),("Paver",.44,.07,.32)]],
    "Kosmos": [
        [("CrystalShard",.98,.64,.74),("SparkleShard",.40,.44,.33),("Asteroid",.67,.28,.54)],
        [("PanelFragment",1.05,.12,.68),("PipeGlow",.31,.29,.31),("Asteroid",.73,.29,.60)],
        [("Asteroid",1.39,.49,.98),("SparkleShard",.35,.30,.26),("PipeGlow",.37,.23,.30)],
        [("HeatShield",1.16,.16,.81),("PanelFragment",.59,.08,.52),("SparkleShard",.42,.32,.30)]]}

def read(p):
    return p.read_text(encoding="utf-8")

def write(p, text):
    data = text.encode("utf-8")
    if not p.exists() or p.read_bytes() != data:
        p.write_bytes(data)

def meta(p):
    return Path(str(p) + ".meta")

def asset_guid(p):
    return re.search(r"(?m)^guid: (\w+)", read(meta(p)))[1]

def make_meta(p, template):
    if not meta(p).exists():
        guid = hashlib.sha256(("BulletRush/ground/" + p.as_posix()).encode()).hexdigest()[:32]
        write(meta(p), re.sub(r"(?m)^guid: \w+", "guid: " + guid, read(meta(template))))

def field(text, key):
    return re.search(r"(?m)^\s*" + key + r": ([^\n]+)", text)[1]

def vector(v):
    return "{" + ", ".join(f"{axis}: {n:.8g}" for axis,n in zip("xyz",v)) + "}"

def bounds(vertices):
    axes = list(zip(*(v[:3] for v in vertices)))
    return [min(a) for a in axes], [max(a) for a in axes]

class Mesh:
    def __init__(self, vertices=None, indices=None):
        self.vertices = [] if vertices is None else vertices
        self.indices = [] if indices is None else indices

    def face(self, points, uv):
        a,b,c = points[:3]
        x,y,z = (b[i]-a[i] for i in range(3))
        u,v,w = (c[i]-a[i] for i in range(3))
        normal = (y*w-z*v,z*u-x*w,x*v-y*u)
        size = math.hypot(*normal)
        start = len(self.vertices)
        self.vertices.extend((*p,*(n/size for n in normal),*uv) for p in points)
        for i in range(1,len(points)-1):
            self.indices.extend((start,start+i,start+i+1))

    def add(self, source, x, z, dims, yaw):
        lo,hi = bounds(source.vertices)
        sx,sy,sz = (d/(b-a) for d,a,b in zip(dims,lo,hi))
        cx,cz = (lo[0]+hi[0])/2,(lo[2]+hi[2])/2
        co,si = math.cos(yaw), math.sin(yaw)
        base = len(self.vertices)
        for px,py,pz,nx,ny,nz,u,v in source.vertices:
            px,pz = (px-cx)*sx,(pz-cz)*sz
            nx,ny,nz = nx/sx,ny/sy,nz/sz
            length = math.hypot(nx,ny,nz)
            self.vertices.append((x+px*co+pz*si,.009+(py-lo[1])*sy,z-px*si+pz*co,
                                  (nx*co+nz*si)/length,ny/length,(-nx*si+nz*co)/length,u,v))
        self.indices.extend(base+i for i in source.indices)

def prism(mesh, lower, upper, uv, cap):
    for i,a in enumerate(lower):
        j=(i+1)%len(lower)
        mesh.face([a,upper[i],upper[j],lower[j]],uv)
    mesh.face(upper[::-1],cap)

def ring(radius, height, count):
    return [(radius*math.cos(i*math.tau/count),height,radius*math.sin(i*math.tau/count)) for i in range(count)]

def slab(mesh,x,z,y,w,h,d,uv):
    rim=[(x+a*w/2,y,z+b*d/2) for a,b in [(1,1),(-1,1),(-1,-1),(1,-1)]]
    prism(mesh,rim,[(a,b+h,c) for a,b,c in rim],uv,uv)

@lru_cache(maxsize=None)
def model(theme, name):
    path=(ART if theme==FOREST else ART/"Props"/("Env_"+theme))/(name+".asset")
    if path.is_file():
        src=read(path)
        vertices=list(struct.iter_unpack("<8f",bytes.fromhex(field(src,"_typelessdata"))))
        indices=[i[0] for i in struct.iter_unpack("<H",bytes.fromhex(field(src,"m_IndexBuffer")))]
        return Mesh(vertices,indices)
    def uv(col):
        return ((col+.5)/16, .5 if theme==FOREST else 1-(ROWS[theme]+.5)/8)
    m=Mesh()
    if name in ("SeaGrass","AlpineGrass","Agave","Fern","Weeds"):
        palettes={"SeaGrass":(3,4,5),"AlpineGrass":(3,4,5),"Agave":(3,4,5),"Fern":(4,6,11),"Weeds":(3,4,5)}
        colors=palettes[name]
        for i in range(9):
            a=i*2.39996; dx,dz=math.cos(a),math.sin(a)
            r=.43+.15*(i%3);h=.29+.10*((i+1)%3)
            width=.10 if name in ("Agave","Fern") else .052
            base=(dx*.055,0,dz*.055)
            left=(dx*r*.44+dz*width,h*.64,dz*r*.44-dx*width)
            right=(dx*r*.44-dz*width,h*.64,dz*r*.44+dx*width)
            ridge=(dx*r*.46,h*.94,dz*r*.46)
            tip=(dx*r,h*(.72 if name=="Fern" else 1.32),dz*r)
            for p in ([base,ridge,left],[left,ridge,tip],[base,right,ridge],[right,tip,ridge]):
                m.face(p,uv(colors[i%3]))
                m.face([(x,y-.008,z) for x,y,z in reversed(p)],uv(colors[(i+1)%3]))
    elif name=="Mushroom":
        prism(m,ring(.08,0,7),ring(.065,.23,7),uv(13),uv(13))
        prism(m,ring(.23,.20,7),ring(.12,.32,7),uv(15),uv(2))
    elif name=="Drain":
        slab(m,0,0,0,1,.05,.65,uv(1))
        slab(m,0,0,.05,.91,.012,.55,uv(0))
        for i in range(7):slab(m,(i-3)*.126,0,.062,.049,.012,.52,uv(2))
    elif name=="Manhole":
        prism(m,ring(.50,0,12),ring(.50,.042,12),uv(1),uv(1))
        prism(m,ring(.43,.042,12),ring(.43,.053,12),uv(0),uv(2))
        for i in [-1,0,1]:slab(m,0,i*.13,.053,.48,.008,.022,uv(1))
    elif name=="Paint":
        for x in [-.31,.31]:slab(m,x,0,0,.38,.012,.12,uv(10))
    elif name=="Curb":
        for x in [-.27,.27]:
            slab(m,x,0,0,.51,.15,.24,uv(1))
            slab(m,x,0,.15,.50,.028,.22,uv(14))
    elif name=="UtilityCover":
        slab(m,0,0,0,.98,.05,.65,uv(1))
        for x in [-.23,.23]:
            slab(m,x,0,.05,.44,.014,.54,uv(2))
            slab(m,x,.16,.064,.10,.01,.028,uv(0))
    elif name in ("PanelFragment","HeatShield"):
        rim=[(.58,0,.13),(.19,0,.34),(-.49,0,.28),(-.58,0,-.23),(.31,0,-.21)]
        prism(m,rim,[(x,.06+.035*x,z) for x,_,z in rim],uv(6),uv(4 if name=="PanelFragment" else 7))
        for x in [-.24,.035]:slab(m,x,.01,.085,.052,.018,.40,uv(5 if name=="PanelFragment" else 8))
    elif name=="Paver":
        outline=[(.48,0,.14),(.35,0,.28),(-.47,0,.24),(-.51,0,-.18),(.39,0,-.26)]
        prism(m,outline,[(x,.08,z) for x,_,z in outline],uv(1),uv(2))
    else:
        raise ValueError(name)
    return m

def landmarks(theme):
    src=read(MAPS/"Environments"/("Env_"+theme+".asset")).split("  insideDecor:")[0]
    coords=re.findall(r"(?:initialOffsets:\n    - |fixedPlacement: 1\n    fixedOffset: )\{x: ([-.\d]+), y: ([-.\d]+)\}",src)
    return [(float(x),float(z)) for x,z in coords]

def make_tile(theme, variant):
    rng=random.Random(SEEDS[theme]+173*variant)
    centers=[]
    protected=landmarks(theme) if variant==0 else []
    if variant==0:
        for x,z in [(-4.4,-3.9),(3.8,-3.1),(-2.4,5.3),(4.2,7.4),(-10.0,-.7),(10.6,1.9)]:
            centers.append((x+rng.uniform(-.65,.65),z+rng.uniform(-.65,.65)))
    for i in [-1,0,1]:
        for j in [-1,0,1]:
            x,z=i*9.8+rng.uniform(-2.4,2.4),j*9.8+rng.uniform(-2.4,2.4)
            if variant==0 and (math.hypot(x,z)<5 or any(math.hypot(x-a,z-b)<4 for a,b in centers)):continue
            centers.append((x,z))
    mesh=Mesh();placed=[]
    for index,(x,z) in enumerate(centers):
        recipe=RECIPES[theme][(index+variant+SEEDS[theme])%len(RECIPES[theme])]
        axis=rng.choice([0,math.pi/2]) if theme=="Gorod" else rng.uniform(0,math.tau)
        scale=rng.uniform(.87,1.24)
        for j,(name,w,h,d) in enumerate(recipe):
            direction=axis+(math.pi if j==2 else 0)+rng.uniform(-.55,.55)
            distance=0 if j==0 else rng.uniform(.85,1.38)*scale
            px,pz=x+math.cos(direction)*distance,z+math.sin(direction)*distance
            factor=scale*rng.uniform(.91,1.09);dims=(w*factor,h*factor,d*factor)
            radius=math.hypot(dims[0],dims[2])/2
            if variant==0 and (math.hypot(px,pz)<3.25+radius or any(math.hypot(px-a,pz-b)<1.08+radius for a,b in protected)):continue
            yaw=axis if theme=="Gorod" and name in ("Paint","Drain","Paver","Curb","UtilityCover") else rng.uniform(0,math.tau)
            mesh.add(model(theme,name),px,pz,dims,yaw)
            placed.append((name,px,pz,radius))
    return mesh,placed

def save_mesh(mesh, path):
    template=ART/"ClearingPine.asset"
    src=read(template).replace("m_Name: ClearingPine","m_Name: "+path.stem)
    lo,hi=bounds(mesh.vertices);count=len(mesh.vertices)
    assert count<65536
    values={"m_Center":vector([(a+b)/2 for a,b in zip(lo,hi)]),"m_Extent":vector([(b-a)/2 for a,b in zip(lo,hi)]),
            "indexCount":len(mesh.indices),"vertexCount":count,"m_VertexCount":count,"m_DataSize":32*count,
            "m_IndexBuffer":struct.pack("<"+"H"*len(mesh.indices),*mesh.indices).hex(),
            "_typelessdata":b"".join(struct.pack("<8f",*v) for v in mesh.vertices).hex()}
    for key,value in values.items():
        src=re.sub(r"(?m)^(\s*"+key+r": )[^\n]+",lambda m:m[1]+str(value),src)
    write(path,src);make_meta(path,template)

def tile_layout():
    return [(0,0,0,0),(-1,-1,1,1),(0,-1,2,0),(1,-1,3,1),(-1,0,3,3),(1,0,1,2),(-1,1,2,3),(0,1,1,0),(1,1,3,2)]

def save_prefab(theme, paths):
    template=ART/"MeadowTufts.prefab"; base=read(template)
    header,body=base.split("--- !u!1 &",1)
    body="--- !u!1 &"+body
    root=body if theme==FOREST else body.split("--- !u!33 &")[0]
    if theme!=FOREST:
        root=re.sub(r"^  - component: \{fileID: (?:3626307074279406595|159511843466587337)\}\n","",root,flags=re.M)
    name="Map_"+theme
    root=root.replace("m_Name: MeadowTufts","m_Name: "+name)
    children=[];blocks=[header,root]
    mat=ART/"Woodland.mat" if theme==FOREST else ART/"Props/BiomeProps.mat"
    for n,(cx,cz,variant,q) in enumerate(tile_layout(),start=1):
        b=body
        for old,offset in [(ROOT_ID,0),(TR_ID,1),(3626307074279406595,2),(159511843466587337,3)]:
            b=b.replace(str(old),str(n*1000+offset))
        children.append(f"  - {{fileID: {n*1000+1}}}")
        b=b.replace("m_Name: MeadowTufts",f"m_Name: Ground {cx} {cz}")
        b=b.replace("m_Father: {fileID: 0}","m_Father: {fileID: "+str(TR_ID)+"}")
        b=re.sub(r"m_LocalPosition: .*","m_LocalPosition: "+vector((cx*TILE,0,cz*TILE)),b)
        b=re.sub(r"m_LocalRotation: .*",f"m_LocalRotation: {{x: 0, y: {math.sin(q*math.pi/4):.9g}, z: 0, w: {math.cos(q*math.pi/4):.9g}}}",b)
        b=b.replace(asset_guid(ART/"MeadowTufts.asset"),asset_guid(paths[variant]))
        b=b.replace(asset_guid(ART/"Woodland.mat"),asset_guid(mat))
        blocks.append(b)
    blocks[1]=blocks[1].replace("  m_Children: []","  m_Children:\n"+"\n".join(children))
    prefab=OUT/(name+".prefab")
    write(prefab,"".join(blocks));make_meta(prefab,template)
    return prefab

def bake():
    OUT.mkdir(exist_ok=True);make_meta(OUT,ART)
    for theme in SEEDS:
        paths=[];counts=[]
        for variant in range(4):
            mesh,items=make_tile(theme,variant)
            path=OUT/f"{theme}_{variant}.asset"
            save_mesh(mesh,path);paths.append(path);counts.append(len(items))
        prefab=save_prefab(theme,paths)
        path=MAPS/(prefab.stem+".asset")
        original=path.read_bytes()
        changed=re.sub(rb"(?m)^  groundDecorPrefab: \{[^}]+\}",
                       f"  groundDecorPrefab: {{fileID: {ROOT_ID}, guid: {asset_guid(prefab)}, type: 3}}".encode(),original)
        if original!=changed:path.write_bytes(changed)
        print(theme,sum(counts[v] for _,_,v,_ in tile_layout()),"details")

if __name__=="__main__":
    bake()
