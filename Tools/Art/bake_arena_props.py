"""Bake BulletRush's nonfunctional low-poly arena pickups and decorative barrel.

Every prop uses the existing weapon palette and one mesh/material. Run from
the project root with Python 3, Pillow and NumPy. Optional --preview renders
the actual exported meshes, not separate concept art.
"""
from pathlib import Path
import argparse
import math
import re

import numpy as np
from PIL import Image, ImageDraw, ImageFont

from bake_ground_details import save_mesh, read, write, asset_guid, bounds
from bake_weapon_models import Prop, make_meta, portrait


ART = Path("Assets/Art/ArenaProps")
PREFABS = Path("Assets/Resources/ArenaProps")
PALETTE = Path("Assets/Art/Weapons/WeaponPalette.png")
MATERIAL = Path("Assets/Art/Weapons/WeaponPalette.mat")
TEMPLATE = Path("Assets/Data/Maps/ArenaArt/MeadowTufts.prefab")


def ring(y, rx, rz=None, n=8, x=0, z=0, angle=None):
    rz = rx if rz is None else rz
    angle = math.pi/n if angle is None else angle
    return [(x+rx*math.cos(angle+math.tau*i/n), y, z+rz*math.sin(angle+math.tau*i/n))
            for i in range(n)]


def round_loft(p, levels, color, cap=None, n=8, x=0, z=0):
    p.loft([ring(y,r,n=n,x=x,z=z) for y,r in levels], color, cap)


def cut_box(p, x,y,z, w,h,d, color, cut=.035, cap=None):
    # Chamfered horizontal outline, two vertical bevel rings.
    rings=[]
    for level,ww,dd in ((y-h/2,w*.92,d*.92),(y-h/2+min(.035,h*.24),w,d),
                        (y+h/2-min(.035,h*.24),w,d),(y+h/2,w*.92,d*.92)):
        a,b=ww/2,dd/2; c=min(cut,a*.5,b*.5)
        rings.append([(x+u,level,z+v) for u,v in [(-a+c,-b),(a-c,-b),(a,-b+c),
            (a,b-c),(a-c,b),(-a+c,b),(-a,b-c),(-a,-b+c)]])
    p.loft(rings,color,cap)


def top_emblem(p, x,y,z, size, color, points):
    # Flat enamel paint on a large visible top face.
    face=[(x+u*size,y,z+v*size) for u,v in points]
    p.face(face, (x,y-.03,z), color)


def barrel():
    p=Prop()
    # Small squat octagonal prop, matching the original gameplay footprint.
    round_loft(p,[(0,.29),(.055,.35),(.18,.36),(.87,.36),(1.015,.345),(1.038,.29)],"rust", "red",10)
    round_loft(p,[(.045,.347),(.075,.384),(.16,.384),(.19,.36)],"dark","steel",10)
    round_loft(p,[(.88,.355),(.91,.385),(1.01,.385),(1.044,.35)],"dark","silver",10)
    # Recessed lid with a bright painted warning mark. Purely visual.
    round_loft(p,[(1.044,.285),(1.058,.31),(1.073,.285)],"steel","rust",10)
    top_emblem(p,0,1.077,0,.29,"amber",[(0,-.91),(.91,.63),(-.91,.63)])
    top_emblem(p,0,1.079,0,.29,"ink",[(-.10,-.38),(.10,-.38),(.10,.17),(-.10,.17)])
    top_emblem(p,0,1.079,0,.29,"ink",[(-.10,.31),(.10,.31),(.10,.47),(-.10,.47)])
    # Large painted identification plaques on opposing facets.
    for sign in (-1,1):
        z=sign*.364
        p.block(0,.54,z,.29,.33,.018,"ink",.027)
        p.block(0,.54,z+sign*.011,.22,.24,.012,"amber",.018)
        p.block(0,.58,z+sign*.020,.055,.103,.012,"dark")
        p.block(0,.48,z+sign*.020,.055,.048,.012,"dark")
    return p


def damage():
    p=Prop()
    # Amber faceted energy stone in a sturdy graphite/gold harness.
    round_loft(p,[(-.35,.19),(-.31,.26),(-.18,.29),(-.13,.25)],"dark","gold",8)
    round_loft(p,[(-.16,.155),(-.06,.29),(.16,.28),(.36,.135),(.39,.045)],"orange","amber",6)
    # Light faces on the crystal, separated using two shallow solid facets.
    rings=[ring(-.055,.291,n=6),ring(.16,.281,n=6),ring(.36,.136,n=6)]
    for idx,c in ((0,"amber"),(1,"gold"),(4,"red")):
        z=idx; k=(idx+1)%6
        for a,b in zip(rings,rings[1:]):
            p.face([a[z],a[k],b[k],b[z]],(0,.1,0),c)
    for x,z in ((.22,.13),(-.22,.13),(.22,-.13),(-.22,-.13)):
        cut_box(p,x,-.15,z,.12,.22,.13,"brass",.018,"gold")
    return p


def rate():
    p=Prop()
    # Three toy energy capsules, violet/silver; distinct from a single gem.
    cut_box(p,0,-.30,0,.82,.12,.57,"plum",.055,"dark")
    cut_box(p,0,-.225,0,.77,.07,.53,"purple",.04,"violet")
    for i in range(3):
        x=(i-1)*.255; height=.32 if i==1 else .20
        round_loft(p,[(-.19,.092),(-.15,.123),(-.07,.123)],"steel","silver",6,x=x)
        round_loft(p,[(-.075,.107),(height-.075,.107),(height-.025,.075)],"violet","pink",6,x=x)
        round_loft(p,[(height-.025,.075),(height+.025,.107),(height+.06,.075)],"silver","cream",6,x=x)
    cut_box(p,0,-.01,.235,.77,.09,.105,"dark",.025,"steel")
    for x in (-.255,0,.255): p.block(x,-.01,.296,.065,.041,.025,"pink",.01)
    return p


def speed():
    p=Prop()
    # A chunky turquoise runner's boot, with a sculpted sole and wing accents.
    sole=[(-.24,-.37),(.24,-.37),(.28,-.23),(.28,.25),(.16,.41),(-.16,.41),(-.28,.25),(-.28,-.23)]
    p.loft([[(x,-.27,z) for x,z in sole],[(x*1.03,-.16,z*1.02) for x,z in sole]],"ink","steel")
    p.loft([[(x*.91,-.16,z*.95) for x,z in sole],[(x*.89,-.06,z*.90) for x,z in sole]],"ivory","mint")
    cut_box(p,0,.025,.13,.49,.23,.48,"teal",.075,"cyan")
    cut_box(p,0,.14,-.18,.48,.43,.34,"teal",.06,"mint")
    cut_box(p,0,.36,-.18,.52,.10,.38,"navy",.045,"ice")
    cut_box(p,0,.417,-.18,.34,.025,.24,"dark",.025,"navy")
    for z in (-.025,.105): p.block(0,.155,z,.37,.038,.058,"ivory",.012)
    # Stylized wings point backward; exaggerated for top-down silhouette.
    for side in (-1,1):
        for j in range(2):
            x=side*.25
            p.side([(-.36-j*.075,.18+j*.11),(-.17,.16+j*.10),(.025,.04+j*.075),(-.26,.03+j*.07)],.115,"mint",x)
    return p


def pedestal():
    p=Prop()
    round_loft(p,[(0,.40),(.035,.48),(.11,.48),(.145,.42)],"dark","steel",8)
    round_loft(p,[(.145,.345),(.175,.38),(.195,.345)],"silver","ivory",8)
    # Subtle neutral stripes on the edge; the hovering model carries its color.
    for i in range(4):
        a=i*math.pi/2
        p.block(.385*math.cos(a),.13,.385*math.sin(a),.09,.055,.09,"gold",.015)
    return p


def loot_coin():
    p=Prop()
    round_loft(p,[(-.075,.235),(-.035,.285),(.035,.285),(.075,.235)],"brass","gold",10)
    round_loft(p,[(.075,.21),(.095,.23),(.112,.21)],"gold","amber",10)
    round_loft(p,[(.112,.145),(.119,.145)],"gold","amber",8)
    top_emblem(p,0,.121,0,.16,"brass",[(0,-.76),(.63,0),(0,.76),(-.63,0)])
    top_emblem(p,0,.122,0,.1,"cream",[(0,-.72),(.42,0),(0,.72),(-.42,0)])
    return p


def loot_health():
    p=Prop()
    cut_box(p,0,-.005,0,.59,.40,.53,"ivory",.065,"cream")
    cut_box(p,0,-.165,0,.57,.08,.51,"rust",.045,"red")
    p.block(0,.199,0,.32,.026,.102,"red",.01)
    p.block(0,.20,0,.103,.026,.32,"red",.01)
    for side in (-1,1):
        p.block(0,0,side*.252,.24,.074,.022,"rust",.012)
        p.block(0,0,side*.264,.074,.24,.022,"red",.012)
    return p


def bake_prefab(name, mesh):
    src=read(TEMPLATE).replace("m_Name: MeadowTufts","m_Name: "+name)
    src=re.sub(r"m_Mesh: \{[^\n]+\}",f"m_Mesh: {{fileID: 4300000, guid: {asset_guid(mesh)}, type: 2}}",src)
    src=re.sub(r"(?m)^  - \{fileID: 2100000, guid: \w+, type: 2\}$",f"  - {{fileID: 2100000, guid: {asset_guid(MATERIAL)}, type: 2}}",src)
    src=src.replace("m_CastShadows: 0","m_CastShadows: 1" if name=="Barrel" else "m_CastShadows: 0")
    path=PREFABS/(name+".prefab")
    write(path,src); make_meta(path,TEMPLATE)


def bake(preview=None):
    ART.mkdir(exist_ok=True,parents=True); PREFABS.mkdir(exist_ok=True,parents=True)
    make_meta(ART,Path("Assets/Art/Weapons")); make_meta(PREFABS,Path("Assets/Art/Weapons"))
    models={"Barrel":barrel(),"BoostDamage":damage(),"BoostFireRate":rate(),"BoostMoveSpeed":speed(),"BoostBase":pedestal(),
            "LootCoin":loot_coin(),"LootHealth":loot_health()}
    palette=np.asarray(Image.open(PALETTE).convert("RGB"))
    for name,prop in models.items():
        triangles=len(prop.mesh.indices)//3
        assert triangles<600 and all(math.isfinite(x) for v in prop.mesh.vertices for x in v)
        mesh=ART/(name+".asset"); save_mesh(prop.mesh,mesh); bake_prefab(name,mesh)
        print(name, triangles, "triangles",bounds(prop.mesh.vertices))
    if preview:
        labels={"Barrel":"ARENA BARREL","BoostDamage":"DAMAGE","BoostFireRate":"FIRE RATE","BoostMoveSpeed":"SPEED"}
        sheet=Image.new("RGB",(690,530),"#f1ebdb"); draw=ImageDraw.Draw(sheet)
        title=ImageFont.truetype("C:/Windows/Fonts/segoeuib.ttf",29)
        font=ImageFont.truetype("C:/Windows/Fonts/segoeuib.ttf",20)
        draw.text((16,10),"BULLETRUSH  /  ARENA PROPS",font=title,fill="#2d3436")
        for i,(name,label) in enumerate(labels.items()):
            prop=models[name]
            if name!="Barrel":
                # Preview the actual prop, floating above its in-game stand.
                combined=Prop()
                for model,offset in ((models["BoostBase"],0.),(prop,.69)):
                    start=len(combined.mesh.vertices)
                    combined.mesh.vertices.extend((x,y+offset,z,nx,ny,nz,u,v) for x,y,z,nx,ny,nz,u,v in model.mesh.vertices)
                    combined.mesh.indices.extend(start+j for j in model.mesh.indices)
                prop=combined
            x=10+(i%2)*337; y=53+(i//2)*235
            draw.rounded_rectangle((x,y,x+325,y+221),radius=8,fill="#e1dbca")
            img=portrait(prop,palette); sheet.paste(img,(x+3,y+4),img)
            draw.text((x+12,y+190),label,font=font,fill="#2d3436")
        sheet.save(preview)


if __name__=="__main__":
    parser=argparse.ArgumentParser(); parser.add_argument("--preview")
    bake(parser.parse_args().preview)
