"""Bake 24 cosmetic cube-enemy skins into a single 512px atlas.

Run from the Unity project root with Python 3, Pillow and NumPy.
All bodies retain the stock 1x1x1 cube, face normals, and six flat faces.
"""
from pathlib import Path
import argparse
import re

import numpy as np
from PIL import Image, ImageDraw, ImageFont

from bake_ground_details import Mesh, asset_guid, read, save_mesh, write
from bake_weapon_models import make_meta


OUT = Path("Assets/Art/EnemySkins")
DATA = Path("Assets/_Game/Scripts/Enemies")
SIZE, PAD, TILE, ATLAS, COLS = 32, 4, 40, 512, 12
FACES = ("front", "left", "back", "right", "top", "bottom")
COLORS = {
    # Primary, paint/secondary, ink, highlight, accent
    "Normal": ("#c44f3e", "#a23c32", "#542921", "#f0ad91", "#df765c"),
    "Fast": ("#6fcdb1", "#439e91", "#20534a", "#d7f3d7", "#b7e9c8"),
    "Tank": ("#753039", "#a88d7c", "#3a1e27", "#f1c3ad", "#54252e"),
    "Ranged": ("#d96b83", "#a9435e", "#502634", "#ffded0", "#ed9ba6"),
    "Elite": ("#a63268", "#872653", "#4c1e37", "#ffd4ba", "#efbc60"),
    "Bomber": ("#d95239", "#a73328", "#472a26", "#ffe0a0", "#f5c755"),
}
ROLES = tuple(COLORS)


class Paint:
    def __init__(self, role):
        self.base, self.mid, self.ink, self.light, self.accent = COLORS[role]
        self.image = Image.new("RGB", (SIZE, SIZE), self.base)
        self.d = ImageDraw.Draw(self.image)

    def box(self, x0, y0, x1, y1, color):
        self.d.rectangle((x0,y0,x1,y1), fill=color)

    def poly(self, pts, color):
        self.d.polygon(pts, fill=color)

    def stripe(self, x0, width, color=None):
        self.box(x0,0,x0+width-1,31,color or self.light)

    def ring(self, a,b,c,d, color, thickness=3):
        self.box(a,b,c,d,color)
        self.box(a+thickness,b+thickness,c-thickness,d-thickness,self.base)

    def eyes(self, y=15, color=None, angry=True):
        c=color or self.ink
        for x in (8,21):
            self.box(x,y,x+3,y+5,c)
        if angry:
            # Large, stepped angular eyebrows remain legible at gameplay zoom.
            self.poly([(5,y-5),(15,y-2),(15,y+1),(5,y-2)],self.ink)
            self.poly([(17,y-2),(27,y-5),(27,y-2),(17,y+1)],self.ink)

    def visor(self, y=11, angry=False, color=None):
        self.box(0,y,31,y+11,self.ink)
        if angry:
            self.poly([(7,y+4),(12,y+5),(12,y+9),(7,y+8)],self.light)
            self.poly([(20,y+5),(25,y+4),(25,y+8),(20,y+9)],self.light)
        else:
            self.box(8,y+3,12,y+8,color or self.light)
            self.box(20,y+3,24,y+8,color or self.light)

    def chevron(self, color, y=3, thick=4):
        self.poly([(4,y),(16,y+8),(27,y),(27,y+thick),(16,y+8+thick),(4,y+thick)], color)

    def diagonal(self, y0, y1, color, width=5):
        for offset in range(-40,64,2*width):
            self.poly([(offset,y0),(offset+width,y0),(offset+width+y1-y0,y1),(offset+y1-y0,y1)],color)


def normal(p, n, face):
    if face == "front":
        if n==1:
            p.box(0,2,31,8,p.ink); p.eyes(15,angry=False)
        elif n==3:
            for x in (9,20): p.box(x,0,x+4,6,p.light)
            p.box(4,12,27,15,p.ink); p.eyes(15,angry=False)
        else:
            p.eyes(15)
            if n==2:
                p.poly([(23,23),(26,23),(29,27),(29,29),(26,29),(23,25)],p.light)
                p.poly([(27,23),(29,23),(29,25),(24,29),(22,29),(22,27)],p.light)
    elif face=="top":
        if n==0: p.box(7,7,24,24,p.mid)
        elif n==1: p.box(0,24,31,28,p.mid)
        elif n==2:
            for i in range(4): p.box(6+i*5,25-i*6,11+i*5,30-i*6,p.light)
        else:
            p.stripe(9,5); p.stripe(20,5)
    elif face in ("left","right","back"):
        if n==0:
            p.box(18,7,24,12,p.mid); p.box(18,22,24,26,p.mid)
        elif n==1:
            p.box(0,2,31,8,p.ink)
            if face!="back": p.poly([(21,8),(26,8),(25,19),(21,16)],p.ink)
        elif n==3:
            p.box(3,12,29,15,p.accent); p.box(3,22,29,25,p.accent)
        else: p.box(22,18,27,22,p.mid)


def fast(p, n, face):
    if n==0:
        if face in ("front","top","back"): p.stripe(13,6,p.light)
        elif face!="bottom": p.box(0,16,31,21,p.light)
    elif n==1:
        if face in ("front","top","back"):
            p.stripe(9,4,p.light); p.stripe(19,4,p.light)
        elif face!="bottom":
            p.box(0,10,31,13,p.light); p.box(0,20,31,23,p.light)
    elif n==2 and face in ("top","left","right","back"):
        p.poly([(18,3),(6,16),(15,16),(10,29),(27,13),(18,13)],p.light)
    elif n==3:
        if face=="top":
            for x,h in ((9,10),(17,19),(25,26)): p.box(x,0,x+3,h,p.light)
        elif face in ("left","right","back"):
            p.box(4,14,24,17,p.light); p.box(4,23,17,26,p.light)
    if face=="front":
        if n==1: p.visor(12)
        elif n==3: p.eyes(16,angry=False)
        else: p.eyes(16)


def tank(p, n, face):
    if face=="top":
        if n==0:
            p.box(3,3,28,28,p.mid)
            for x in (5,23):
                for y in (5,23): p.box(x,y,x+3,y+3,p.accent)
        elif n==1:
            p.stripe(12,8,p.mid)
            for x in (1,25):
                for y in (1,25): p.box(x,y,x+5,y+5,p.mid)
        elif n==2: p.ring(5,5,26,26,p.mid,4)
        else: p.chevron(p.mid,3,6); p.chevron(p.mid,18,4)
    elif face=="front":
        p.visor(9,True)
        if n==0:
            p.box(0,22,31,31,p.mid)
            for x in (5,13,21): p.box(x,24,x+3,29,p.ink)
        elif n==1:
            p.box(12,0,19,8,p.mid); p.box(8,24,23,31,p.mid)
        elif n==2:
            p.box(0,23,31,31,p.mid)
            for x in (3,12,22): p.box(x,26,x+4,31,p.accent)
        else:
            p.chevron(p.mid,0,4); p.poly([(3,31),(16,22),(28,31),(23,31),(16,27),(9,31)],p.mid)
        for x in (0,26):
            p.box(x,0,x+5,5,p.mid); p.box(x+1,1,x+4,4,p.accent)
            p.box(x,26,x+5,31,p.mid)
    elif face in ("right","left","back"):
        p.box(3,8,28,25,p.mid)
        for x in (6,23):
            p.box(x,10,x+3,13,p.accent); p.box(x,21,x+3,24,p.accent)
        if n in (2,3):
            for x in (12,17): p.box(x,14,x+2,21,p.accent)


def ranged(p, n, face):
    if face=="front":
        if n==0: p.visor(12)
        elif n==1 or n==3:
            p.eyes(13)
            p.poly([(0,23),(31,19),(31,31),(0,31)],p.mid)
        else:
            p.box(0,15,31,18,p.ink)
            for x in (5,19):
                p.box(x,11,x+10,22,p.ink); p.box(x+3,14,x+7,19,p.accent)
    elif face=="top":
        if n==0:
            for x,y in ((5,6),(21,6),(5,21),(21,21)):
                p.box(x,y,x+6,y+2,p.light); p.box(x,y,x+2,y+6,p.light)
            p.box(13,13,18,18,p.light)
        elif n==1: p.stripe(9,5,p.mid); p.stripe(19,5,p.mid)
        elif n==2: p.ring(7,7,24,24,p.mid,5)
        else:
            p.chevron(p.mid,4,3); p.chevron(p.mid,15,3)
    elif face in ("right","left","back"):
        if n==0:
            for x,y in ((5,7),(21,7),(13,15),(5,23),(21,23)): p.box(x,y,x+3,y+3,p.light)
        elif n==1: p.box(0,19,31,28,p.mid)
        elif n==2:
            p.box(0,9,31,12,p.mid); p.box(0,19,31,22,p.mid)
        else: p.chevron(p.mid,5,3); p.chevron(p.mid,17,3)


def elite(p, n, face):
    if face=="front":
        if n==1: p.stripe(13,6,p.accent)
        if n in (1,3): p.visor(12,True)
        else:
            p.eyes(16,p.light)
            if n==2: p.box(12,26,19,31,p.mid)
    elif face=="top":
        if n==0:
            p.poly([(5,9),(12,16),(16,7),(20,16),(27,9),(24,25),(8,25)],p.accent)
        elif n==1:
            p.stripe(13,6,p.accent)
            p.poly([(16,5),(28,16),(16,27),(4,16)],p.accent)
            p.poly([(16,11),(22,16),(16,21),(10,16)],p.mid)
        elif n==2:
            p.poly([(3,8),(14,13),(14,17),(3,13)],p.accent)
            p.poly([(29,8),(18,13),(18,17),(29,13)],p.accent)
            p.poly([(8,19),(16,24),(24,19),(16,29)],p.accent)
        else: p.chevron(p.accent,5,5)
    elif face in ("left","right","back"):
        if n==0: p.box(14,9,18,23,p.accent)
        elif n==1:
            p.poly([(16,5),(25,16),(16,27),(7,16)],p.accent); p.poly([(16,11),(21,16),(16,21),(11,16)],p.mid)
        else:
            p.box(6,10,26,14,p.accent); p.box(6,20,26,23,p.accent)


def bomber(p, n, face):
    if face=="top":
        if n==0:
            p.box(2,2,29,29,p.ink); p.diagonal(4,27,p.accent,6)
        elif n==1:
            p.poly([(16,3),(29,27),(3,27)],p.accent)
            p.box(14,11,17,19,p.ink); p.box(14,22,17,24,p.ink)
        elif n==2:
            p.poly([(16,4),(22,9),(20,15),(12,15),(10,9)],p.accent)
            p.poly([(7,15),(13,18),(13,24),(5,29),(3,22)],p.accent)
            p.poly([(25,15),(19,18),(19,24),(27,29),(29,22)],p.accent)
        else:
            p.ring(4,4,27,27,p.accent,5); p.box(13,13,18,18,p.accent)
    elif face=="front":
        p.eyes(13)
        p.box(0,25,31,31,p.ink)
        if n==0: p.diagonal(25,31,p.accent,5)
        elif n in (2,3):
            for x in range(1,32,10): p.box(x,25,x+4,31,p.accent)
    elif face in ("left","right","back"):
        if n==0:
            p.box(0,11,31,21,p.ink); p.diagonal(11,21,p.accent,5)
        elif n==1:
            p.poly([(16,4),(28,27),(4,27)],p.accent); p.box(14,12,17,21,p.ink)
        elif n==2:
            for x,y in ((9,5),(20,17),(6,23)): p.box(x,y,x+5,y+4,p.accent)
        else:
            p.box(0,15,31,25,p.ink)
            for x in range(2,32,10): p.box(x,15,x+4,25,p.accent)


PAINTERS = {'Normal':normal,'Fast':fast,'Tank':tank,'Ranged':ranged,'Elite':elite,'Bomber':bomber}


def paint(role, n, face):
    p=Paint(role)
    PAINTERS[role](p,n,face)
    return p.image


def cube_mesh(atlas, skin_id, role, n):
    m = Mesh()
    a,b=-.5,.5
    points = [
        [(a,a,b),(b,a,b),(b,b,b),(a,b,b)],
        [(a,a,a),(a,a,b),(a,b,b),(a,b,a)],
        [(b,a,a),(a,a,a),(a,b,a),(b,b,a)],
        [(b,a,b),(b,a,a),(b,b,a),(b,b,b)],
        [(a,b,b),(b,b,b),(b,b,a),(a,b,a)],
        [(a,a,a),(b,a,a),(b,a,b),(a,a,b)],
    ]
    for i,face in enumerate(FACES):
        tile=skin_id*len(FACES)+i
        row,col=divmod(tile,COLS)
        tx,ty=col*TILE,row*TILE
        pixels=np.asarray(paint(role,n,face))
        atlas.paste(Image.fromarray(np.pad(pixels,((PAD,PAD),(PAD,PAD),(0,0)),mode="edge")),(tx,ty))
        x,y=tx+PAD,ty+PAD
        uv=[(x,y+SIZE),(x+SIZE,y+SIZE),(x+SIZE,y),(x,y)]
        start=len(m.vertices)
        m.face(points[i],(0,0))
        for j,(u,v) in enumerate(uv):
            m.vertices[start+j]=(*m.vertices[start+j][:6],u/ATLAS,1-v/ATLAS)
    return m


def preview(mesh, atlas):
    camera=np.array([.66,.70,1.]); camera/=np.linalg.norm(camera)
    right=np.cross((0,1,0),camera); right/=np.linalg.norm(right)
    up=np.cross(camera,right)
    verts=np.asarray(mesh.vertices); positions=verts[:,:3]
    pts=np.column_stack((positions@right,-positions@up))
    low,high=pts.min(axis=0),pts.max(axis=0)
    W,H=220,200
    pts=(pts-(low+high)/2)*(min(W*.83/(high[0]-low[0]),H*.86/(high[1]-low[1])))+np.array([W/2,H/2])
    depth=positions@camera
    out=np.zeros((H,W,4),dtype=np.uint8); zbuf=np.full((H,W),-np.inf)
    light=np.array([-.32,.83,.44]); light/=np.linalg.norm(light)
    for tri in np.asarray(mesh.indices).reshape(-1,3):
        normal=verts[tri[0],3:6]
        if np.dot(normal,camera)<=0: continue
        a,b,c=pts[tri]; lo=np.maximum(0,np.floor(np.minimum(np.minimum(a,b),c)).astype(int)); hi=np.minimum((W-1,H-1),np.ceil(np.maximum(np.maximum(a,b),c)).astype(int))
        xx,yy=np.meshgrid(np.arange(lo[0],hi[0]+1)+.5,np.arange(lo[1],hi[1]+1)+.5)
        v,w=b-a,c-a; det=v[0]*w[1]-v[1]*w[0]
        if abs(det)<1e-9: continue
        s=((xx-a[0])*w[1]-(yy-a[1])*w[0])/det; t=(v[0]*(yy-a[1])-v[1]*(xx-a[0]))/det
        weights=np.stack((1-s-t,s,t),axis=-1); z=weights@depth[tri]
        rows,cols=slice(lo[1],hi[1]+1),slice(lo[0],hi[0]+1)
        mask=(weights.min(axis=-1)>=-1e-8)&(z>zbuf[rows,cols]); uv=weights[mask]@verts[tri,6:8]
        tx=np.clip((uv[:,0]*ATLAS).astype(int),0,ATLAS-1); ty=np.clip(((1-uv[:,1])*ATLAS).astype(int),0,ATLAS-1)
        shade=.70+.12*(normal[1]+1)/2+.24*max(0,np.dot(normal,light))
        out[rows,cols,:3][mask]=np.clip(atlas[ty,tx]*shade,0,255); out[rows,cols,3][mask]=255; zbuf[rows,cols][mask]=z[mask]
    return Image.fromarray(out)


def material(texture):
    mat=OUT/"EnemySkins.mat"
    template=Path("Assets/Art/Weapons/WeaponPalette.mat")
    source=read(template).replace("m_Name: WeaponPalette","m_Name: EnemySkins")
    ref=f"{{fileID: 2800000, guid: {asset_guid(texture)}, type: 3}}"
    for key in ("_BaseMap","_MainTex"):
        source=re.sub(r"(    - "+key+r":\n        m_Texture: )\{[^\n]+\}",lambda m:m[1]+ref,source)
    write(mat,source); make_meta(mat,template)
    return mat


def assign_skins(role, paths, mat):
    path=DATA/f"Enemy_{role}.asset"
    source=read(path)
    # Idempotent: replace only cosmetic fields in existing EnemyData assets.
    source=re.sub(r"(?m)^  bodySkinMaterial: .*\n", "", source)
    source=re.sub(r"(?m)^  bodySkins:\n(?:  - \{[^\n]+\}\n)+", "", source)
    refs=f"  bodySkinMaterial: {{fileID: 2100000, guid: {asset_guid(mat)}, type: 2}}\n  bodySkins:\n"
    refs+="".join(f"  - {{fileID: 4300000, guid: {asset_guid(p)}, type: 2}}\n" for p in paths)
    source=source.rstrip("\n")+"\n"+refs
    write(path,source)


def bake(preview_path=None):
    OUT.mkdir(parents=True,exist_ok=True)
    make_meta(OUT,Path("Assets/Art/PlayerSkins"))
    atlas=Image.new("RGB",(ATLAS,ATLAS),"#49362f")
    meshes,paths=[],{}
    for row,role in enumerate(ROLES):
        paths[role]=[]
        for n in range(4):
            mesh=cube_mesh(atlas,row*4+n,role,n); meshes.append(mesh)
            assert len(mesh.vertices)==24 and len(mesh.indices)==36
            path=OUT/f"{role}_{n+1:02d}.asset"
            save_mesh(mesh,path); paths[role].append(path)
    texture=OUT/"EnemySkins.png"; atlas.save(texture,optimize=True)
    make_meta(texture,Path("Assets/Art/PlayerSkins/Bee_Albedo.png"),[("textureCompression: 1","textureCompression: 0")])
    mat=material(texture)
    for role,group in paths.items(): assign_skins(role,group,mat)
    if preview_path:
        sheet=Image.new("RGB",(1030,1240),"#f5efdf"); draw=ImageDraw.Draw(sheet)
        font=ImageFont.truetype("C:/Windows/Fonts/segoeuib.ttf",22)
        pixels=np.asarray(atlas)
        for row,role in enumerate(ROLES):
            draw.text((10,row*205+88),role.upper(),font=font,fill="#333333")
            for n in range(4):
                img=preview(meshes[row*4+n],pixels)
                sheet.paste(img,(135+n*220,row*205+2),img)
        sheet.save(preview_path)
    print("Baked 24 cube meshes, 144 painted faces, one 512px atlas and one shared material.")


if __name__=="__main__":
    parser=argparse.ArgumentParser(); parser.add_argument("--preview")
    bake(parser.parse_args().preview)
