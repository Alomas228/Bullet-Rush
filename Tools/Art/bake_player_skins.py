"""Bake BulletRush player meshes, face maps and matching shop portraits. Requires Pillow and NumPy."""
from pathlib import Path
import math
import random

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

from bake_ground_details import Mesh, save_mesh


OUT = Path("Assets/Art/PlayerSkins")
SKINS = ("Classic Hawaiian Suit Cowboy Astronaut Ninja Gamer Dinosaur Santa Police Zombie "
         "King Hipster Shark Pumpkin Winter Bee Camo Neon").split()
SIZE = 48
ATLAS = 256
FACES = ("front", "left", "back", "right", "top", "bottom")


class Surface:
    def __init__(self, color):
        self.image = Image.new("RGB", (SIZE, SIZE), color)
        self.draw = ImageDraw.Draw(self.image)

    def rect(self, bounds, color):
        self.draw.rectangle(bounds, fill=color)

    def poly(self, points, color):
        self.draw.polygon(points, fill=color)

    def band(self, y, height, color):
        self.rect((0, y, 47, y + height - 1), color)

    def eyes(self, y, color="#231f23"):
        for x in (12, 32):
            self.rect((x, y, x + 4, y + 7), color)


def speckles(face, rng, palette, count, minimum=1, maximum=4):
    for _ in range(count):
        x, y = rng.randrange(48), rng.randrange(48)
        w, h = rng.randint(minimum, maximum), rng.randint(minimum, maximum)
        face.rect((x, y, x+w, y+h), rng.choice(palette))


def buckle(face, y):
    face.rect((20, y, 28, y+5), "#e5b445")
    face.rect((22, y+2, 26, y+3), "#523c2a")


def flower(face, x, y, radius):
    for angle in (0, 72, 144, 216, 288):
        a = math.radians(angle)
        petal = [(x + radius*(r*math.cos(a)-w*math.sin(a)),
                  y + radius*(r*math.sin(a)+w*math.cos(a)))
                 for r,w in [(0,0),(.5,-.36),(.93,-.26),(1,.08),(.65,.37)]]
        face.poly(petal, "#faf5e6")
    face.rect((x-1, y-1, x+1, y+1), "#e6b653")


def snowflake(face, x, y, color):
    face.rect((x-3,y,x+3,y), color)
    face.rect((x,y-3,x,y+3), color)
    for dx in (-2,2):
        for dy in (-2,2):
            face.rect((x+dx,y+dy,x+dx,y+dy),color)
            face.rect((x+dx//2,y+dy//2,x+dx//2,y+dy//2),color)


def paint(skin, side):
    colors = dict(Classic="#f0f2f4", Hawaiian="#288ad2", Suit="#242831", Cowboy="#916341",
                  Astronaut="#e7e9e5", Ninja="#212531", Gamer="#222433", Dinosaur="#55994b",
                  Santa="#aa2c37", Police="#305782", Zombie="#8d9d5b", King="#f4bc35",
                  Hipster="#33343c", Shark="#518bb7", Pumpkin="#df7523", Winter="#b73340",
                  Bee="#efba37", Camo="#687446", Neon="#33ff99")
    p = Surface(colors[skin])
    rng = random.Random(skin + side)
    front, lateral = side == "front", side in FACES[:4]
    if skin == "Hawaiian":
        for row in range(4):
            for col in range(4):
                x = -2+col*16+(row%2)*7+rng.randint(-2,2)
                y = -2+row*16+rng.randint(-2,2)
                p.poly([(x+4,y+4),(x+8,y+3),(x+11,y-2),(x+6,y)], "#62b5dc")
                flower(p,x,y,rng.randrange(5,9))
        if front:
            p.rect((23,1,24,47), "#4ba2ce")
            for y in (23,34,44):
                p.rect((24,y,25,y+1), "#f5eaca")
    elif skin == "Suit" and lateral:
        p.band(44,4,"#202129")
        if front:
            p.poly([(8,0),(39,0),(24,36)], "#eee9dd")
            p.poly([(20,1),(27,1),(26,6),(24,8),(21,5)], "#aa343a")
            p.poly([(23,6),(26,6),(28,22),(24,30),(21,23)], "#c14344")
            for coords in [[(5,0),(11,0),(24,34),(10,20),(13,16),(6,12)],
                           [(36,0),(43,0),(40,12),(34,16),(37,20),(24,34)]]:
                p.poly(coords, "#414650")
            p.poly([(34,19),(42,17),(42,20),(34,21)], "#f6efe0")
            for y in (35,41):
                p.rect((23,y,24,y+1),"#141921")
    elif skin == "Cowboy" and lateral:
        p.band(3,6,"#ad3439")
        p.band(38,6,"#49372d")
        if front:
            p.poly([(0,4),(47,4),(43,14),(33,21),(24,30),(13,20),(3,13)],"#c34442")
            p.poly([(0,5),(47,5),(33,11),(18,13)],"#db5950")
            p.poly([(17,14),(35,10),(26,17)],"#ac3835")
            buckle(p,38)
        else:
            p.rect((4,36,7,45),"#84583a")
    elif skin == "Astronaut":
        if lateral:
            p.band(29,3,"#e37837")
            p.band(45,3,"#647584")
            if front:
                p.rect((16,33,32,43),"#465767")
                for x,color in [(18,"#f5af63"),(23,"#78bac2"),(28,"#df724a")]:
                    p.rect((x,35,x+2,37),color)
                p.rect((18,40,24,41),"#d8e3df")
            else:
                p.rect((16,7,26,16),"#9ca9ae")
                p.rect((18,9,24,14),"#4a5868")
                for y in (20,23,39,42):
                    p.rect((16,y,25,y),"#a2aeb1")
        elif side == "top":
            p.rect((18,19,25,22),"#b6c1c6")
    elif skin == "Ninja" and lateral:
        p.band(3,6,"#b62d38")
        p.band(33,6,"#ac3039")
        if front:
            p.rect((5,11,42,22),"#ebc7a1")
            p.rect((5,20,42,22),"#cfa681")
            p.eyes(12)
            p.poly([(21,40),(26,39),(32,47),(26,47)],"#3a3b45")
    elif skin == "Gamer":
        if lateral:
            p.band(3,2,"#2682af")
            for x,y,c in [(1,38,"#7054bd"),(6,42,"#ba54ce"),(32,40,"#3ea9d0"),(40,34,"#6e5fbd")]:
                p.rect((x,y,x+6,y+2),c)
                p.rect((x+4,y-4,x+6,y+1),c)
        elif side == "top":
            for x,y,c in [(2,4,"#438cc2"),(33,4,"#8959bd"),(7,36,"#7044a9"),(34,39,"#38a4c9")]:
                p.rect((x,y,x+8,y+2),c)
    elif skin == "Dinosaur":
        if front:
            p.poly([(4,47),(4,34),(9,34),(9,30),(15,30),(15,27),(32,27),(32,30),
                    (39,30),(39,34),(43,34),(43,47)],"#ced071")
            for y in (36,43):
                p.rect((11,y,36,y),"#b8bd65")
            p.eyes(14)
            for x in (19,27):
                p.rect((x,24,x+1,25),"#426c3e")
        elif lateral:
            speckles(p,rng,["#94b55e","#c1c771"],7,2,4)
    elif skin == "Santa" and lateral:
        p.band(36,6,"#333138")
        if front:
            p.band(3,16,"#e7bb98")
            p.eyes(7)
            p.poly([(0,14),(7,17),(11,19),(18,18),(22,16),(27,16),(32,19),(38,18),
                    (47,13),(47,22),(40,25),(37,28),(30,28),(26,32),(21,32),(17,29),
                    (11,28),(7,24),(0,23)],"#f3ede4")
            p.rect((21,16,26,19),"#d39879")
            buckle(p,36)
    elif skin == "Police" and lateral:
        p.band(40,5,"#26333d")
        if front:
            p.eyes(10)
            p.poly([(18,20),(29,20),(25,25),(27,36),(24,39),(21,36),(23,25)],"#263343")
            p.rect((5,29,14,32),"#456e92")
            p.rect((5,33,14,33),"#224563")
            buckle(p,40)
    elif skin == "Zombie":
        speckles(p,rng,["#75874e","#9aa35f","#69784a"],14,2,5)
        if lateral:
            p.band(28,20,"#d9d2b9")
            speckles_lower = [(rng.randrange(45),rng.randrange(30,46)) for _ in range(9)]
            for x,y in speckles_lower:
                p.rect((x,y,x+3,y+4),rng.choice(["#a45448","#b4ad92"]))
            p.rect((1,2,7,10),"#855449")
            if front:
                p.eyes(13)
                p.poly([(16,24),(23,24),(23,22),(33,22),(33,26),(27,26),(27,28),(16,28)],"#75463d")
                p.rect((31,26,33,31),"#985447")
    elif skin == "King" and front:
        p.eyes(20)
    elif skin == "Hipster" and lateral:
        if front:
            p.band(5,21,"#d6ab8a")
            p.poly([(8,25),(18,25),(23,31),(24,47),(20,47),(20,31)],"#464850")
            p.poly([(40,25),(30,25),(24,31),(24,47),(27,47),(27,31)],"#464850")
            for x,y in [(17,30),(30,29)]:
                p.rect((x,y,x+1,y+10),"#e7e2d6")
        else:
            p.band(43,3,"#282c33")
    elif skin == "Shark":
        if front:
            p.eyes(14)
            p.poly([(0,47),(0,36),(6,30),(13,28),(20,23),(28,23),(37,28),(42,30),(47,36),(47,47)],"#eee8db")
            p.poly([(5,33),(12,27),(19,24),(26,23),(34,25),(42,30),(44,34),(35,31),
                    (28,30),(22,29),(14,31)],"#ab4146")
            for x,y in ((10,29),(17,26),(24,25),(31,26),(38,29)):
                p.poly([(x-2,y),(x+2,y-1),(x,y+3)],"#fff4df")
        elif lateral:
            for x in (29,33,37):
                p.poly([(x,23),(x+1,23),(x,29),(x-1,30)],"#3f7098")
    elif skin == "Pumpkin":
        for x in (1,12,30,42):
            p.rect((x,0,x+1,47),rng.choice(["#d26b22","#ec852a"]))
        if front:
            for points in [[(7,22),(13,14),(15,22)],[(31,22),(35,15),(40,21)],[(21,26),(24,22),(26,26)],
                           [(6,28),(13,32),(17,30),(21,35),(28,34),(29,30),(33,32),(42,26),
                            (38,36),(32,39),(18,40),(11,35)]]:
                p.poly(points,"#382b29")
    elif skin == "Winter" and lateral:
        if front:
            p.band(2,15,"#e7c2a1")
            p.eyes(7)
        p.band(18,3,"#f4e8d5")
        p.band(24,1,"#efddc4")
        p.band(39,1,"#efddc4")
        p.band(43,3,"#f4e8d5")
        for x in (6,18,30,42):
            snowflake(p,x,32,"#f6ebe0")
        for x in range(0,48,4):
            p.rect((x,22,x+1,22),"#e9d9c4")
    elif skin == "Bee" and lateral:
        p.band(31,6,"#42382e")
        if front:
            p.eyes(14)
        else:
            p.band(12,7,"#443a31")
    elif skin == "Camo":
        speckles(p,rng,["#44543b","#87905b","#a3a167","#53613e"],28,3,9)
        if lateral:
            if front:
                p.rect((4,10,43,23),"#dfb99a")
                p.eyes(12)
                p.rect((8,24,38,25),"#4e603d")
    return p.image


class Outfit:
    """Author in a 48-unit cube, with the feet at y=0 and the face towards +z."""
    def __init__(self, skin):
        self.mesh = Mesh()
        self.atlas = Image.new("RGB", (ATLAS,ATLAS), "#303030")
        self.swatches = {}
        for index, face in enumerate(FACES):
            tile = np.asarray(paint(skin,face))
            padded = Image.fromarray(np.pad(tile,((8,8),(8,8),(0,0)),mode="edge"))
            self.atlas.paste(padded, (index%3*64,index//3*64))
        self.box(0,24,0,48,48,48, FACES)

    def face(self, points, material):
        start = len(self.mesh.vertices)
        if material in FACES:
            index = FACES.index(material)
            x, y = index%3*64+8, index//3*64+8
            uv = [(x,y+48),(x+48,y+48),(x+48,y),(x,y)]
        else:
            if material not in self.swatches:
                i = len(self.swatches)
                x, y = (i%16)*16, 128+(i//16)*16
                ImageDraw.Draw(self.atlas).rectangle((x,y,x+15,y+15), fill=material)
                self.swatches[material] = (x+8,y+8)
            uv = [self.swatches[material]]*len(points)
        self.mesh.face(points, (0,0))
        for i,(u,v) in enumerate(uv):
            self.mesh.vertices[start+i] = (*self.mesh.vertices[start+i][:6],u/ATLAS,1-v/ATLAS)

    def box(self, x,y,z,w,h,d, colors):
        a,b = x-w/2,x+w/2
        l,r = z-d/2,z+d/2
        low,high = y-h/2,y+h/2
        faces = [
            [(a,low,r),(b,low,r),(b,high,r),(a,high,r)],
            [(a,low,l),(a,low,r),(a,high,r),(a,high,l)],
            [(b,low,l),(a,low,l),(a,high,l),(b,high,l)],
            [(b,low,r),(b,low,l),(b,high,l),(b,high,r)],
            [(a,high,r),(b,high,r),(b,high,l),(a,high,l)],
            [(a,low,l),(b,low,l),(b,low,r),(a,low,r)]]
        for i, points in enumerate(faces):
            self.face(points, colors if isinstance(colors,str) else colors[i])

    def hat(self, y, height, bottom, top, color, cut=3, offset=(0,0)):
        def ring(w,d,c,at):
            return [(x,at,z) for x,z in [(-w/2+c,d/2),(w/2-c,d/2),(w/2,d/2-c),
                    (w/2,-d/2+c),(w/2-c,-d/2),(-w/2+c,-d/2),(-w/2,-d/2+c),(-w/2,d/2-c)]]
        lower = ring(*bottom,cut,y)
        upper = [(x+offset[0],h,z+offset[1]) for x,h,z in ring(*top,cut,y+height)]
        for i in range(8):
            j = (i+1)%8
            self.face([lower[i],lower[j],upper[j],upper[i]],color)
        self.face(upper,color)
        self.face(lower[::-1],color)

    def fin(self, profile, width, color, center=0):
        # Profile travels counter-clockwise in the (y,z) plane.
        left = [(center-width/2,y,z) for y,z in profile]
        right = [(center+width/2,y,z) for y,z in profile]
        n = np.cross(np.subtract(right[1],right[0]), np.subtract(right[2],right[0]))
        if n[0] < 0:
            left.reverse()
            right.reverse()
        self.face(left[::-1],color)
        self.face(right,color)
        for i in range(len(left)):
            j = (i+1)%len(left)
            self.face([left[i],left[j],right[j],right[i]],color)

    def badge(self, x,y,z, radius, color):
        for i in range(10):
            pts = [(x,y,z)]
            for k in (i,i+1):
                a = math.pi/2 + math.pi*k/5
                r = radius if k%2 == 0 else radius*.47
                pts.append((x+r*math.cos(a),y+r*math.sin(a),z))
            self.face(pts,color)

    def glasses(self, y, pixel=False):
        black, lens = "#222630", "#303843"
        self.box(0,y+2,25,44,2.8,3.7,black)
        for x in (-11,11):
            self.box(x,y-1,25,16,9,3,black)
            self.box(x,y-1,26.6,12,6,.8,lens)
            if pixel:
                for dx,dy in ((-4,1),(-1,-2),(2,1)):
                    self.box(x+dx,y+dy,27.2,2.6,2.6,.5,"#f6f4e8")
            else:
                self.box(x-3,y+.5,27.2,3,1.3,.4,"#5d6b75")
        for x in (-24.3,24.3):
            self.box(x,y+2,12,2,2.7,26,black)


def model(skin):
    o = Outfit(skin)
    box, hat = o.box, o.hat
    if skin in ("Hawaiian","Gamer","Hipster"):
        o.glasses(34 if skin == "Hawaiian" else 33, skin == "Gamer")
    if skin == "Cowboy":
        # An upturned brim has a different silhouette from a flat cap.
        xs = [-30,-22,-14,14,22,30]
        levels = [50,47.7,47,47,47.7,50]
        depths = [17,26,29,29,26,17]
        for i in range(len(xs)-1):
            a,b = xs[i:i+2]
            h,k = levels[i:i+2]
            d,e = depths[i:i+2]
            top = [(a,h,d),(b,k,e),(b,k,-e),(a,h,-d)]
            bottom = [(x,y-2,z) for x,y,z in top]
            o.face(top,"#95603c")
            o.face(bottom[::-1],"#6e452e")
            for j in range(4):
                if (j==1 and i!=len(xs)-2) or (j==3 and i!=0):
                    continue
                n = (j+1)%4
                o.face([bottom[j],bottom[n],top[n],top[j]],"#794b32")
        hat(47,8,(38,37),(32,31),"#86563a",5)
        hat(47.2,2.4,(39,38),(38,37),"#583c2d",5)
        hat(55,2,(32,31),(27,26),"#986342",5)
        o.badge(13,32,24.5,5.2,"#edbd4d")
    elif skin == "Astronaut":
        box(0,34,24.5,43,24,4,"#9ba7af")
        box(0,34,26,39,21,4,"#303a4c")
        box(0,34,28.1,35,17,.8,"#273145")
        box(-12,39,28.7,6,2,.5,"#65788d")
        box(-15,36.5,28.7,2,3,.5,"#65788d")
        box(0,25,-24,27,33,7,"#ced3d0")
    elif skin == "Ninja":
        box(24.8,41,14,5,6,7,"#a83038")
        o.fin([(40,14),(29,9),(30,15)],4,"#c14347",27)
        o.fin([(41,13),(35,2),(35,9)],3,"#92303a",28)
    elif skin == "Dinosaur":
        for z,h in [(14,7),(2,10),(-11,8),(-21,5)]:
            o.fin([(47,z-5),(48+h,z-3),(48,z+5)],4.5,"#e1d57d")
    elif skin in ("Santa","Winter"):
        knit = "#bf3741" if skin == "Santa" else "#aa3440"
        hat(44.5,4,(50,50),(50,50),"#f0e9df",1)
        hat(48.5,6,(47,47),(39,37),knit,3,(-1,-2))
        if skin == "Winter":
            hat(53,2.5,(38,36),(21,20),knit,3,(-3,-4))
            box(-4,58,-5,7,7,7,"#f8f2e6")
        else:
            box(23,46,-12,6,6,6,"#f6eee1")
    elif skin == "Police":
        box(0,46.5,0,49,4,49,"#222e3d")
        hat(48,5,(50,49),(49,48),"#315a83",2)
        box(0,45,19,43,2,16,"#253346")
        o.badge(0,48.5,25,4.6,"#ecc147")
        o.badge(13,21,24.5,4.2,"#edc04d")
    elif skin == "King":
        for axis in ("x","z"):
            for sign in (-1,1):
                def rim(a,y,w,h,t,c,depth=23.5):
                    if axis=="x":
                        box(a,y,sign*depth,w,h,t,c)
                    else:
                        box(sign*depth,y,a,t,h,w,c)
                rim(0,47,49,5,2.6,"#e6a735")
                for j, a in enumerate((-21,-10.5,0,10.5,21)):
                    h = 6 if j%2==0 else 4
                    rim(a,49+h/2,4.7,h,2.7,"#f6c644")
                for a,c in [(-13,"#4f9bb9"),(0,"#c64a45"),(13,"#7eab76")]:
                    rim(a,47.6,4,3.4,1,c,25)
    elif skin == "Hipster":
        hat(44,5,(50,50),(49,49),"#bb3940",3)
        hat(49,3,(49,49),(41,40),"#c84447",3)
        box(0,44,-22,38,2.8,17,"#39353b")
        box(0,44.6,24.6,12,3,1.5,"#372f33")
        box(0,52.5,-2,3,1,3,"#80373b")
    elif skin == "Shark":
        o.fin([(47,-13),(61,-9),(48,7)],4,"#5085ad")
        for sign in (-1,1):
            tri = [(sign*23,19,6),(sign*35,7,9),(sign*23,10,-10)]
            if sign>0:
                tri.reverse()
            if np.cross(np.subtract(tri[1],tri[0]),np.subtract(tri[2],tri[0]))[1] < 0:
                tri.reverse()
            underside = [(x,y-2,z) for x,y,z in tri]
            o.face(tri,"#6094ba")
            o.face(underside[::-1],"#416f97")
            for j in range(3):
                n=(j+1)%3
                o.face([underside[j],underside[n],tri[n],tri[j]],"#4d7ba3")
    elif skin == "Pumpkin":
        hat(48,2,(13,12),(9,9),"#608547",2)
        box(1,52,-1,6,7,6,"#477443")
        box(2,55,-2,5,3,5,"#56884b")
    elif skin == "Bee":
        for sign in (-1,1):
            box(sign*11,50,9,3,7,3,"#423b34")
            box(sign*12,54,9,5,5,5,"#3e3730")
            box(sign*24.5,35,-9,5,8,13,"#b2dbe2")
            box(sign*27,37,-13,6,9,13,"#d2ebeb")
    elif skin == "Camo":
        box(0,44,0,49,3,49,"#48563d")
        # The cap continues the camouflage pattern over the top face.
        box(0,47.5,0,49,5,49,("back","left","back","right","top","bottom"))
        box(0,44,20,43,2.2,14,"#657448")
    return o


def portrait(outfit):
    camera = np.array([-.64,.46,1.0])
    camera /= np.linalg.norm(camera)
    right = np.cross((0,1,0),camera)
    right /= np.linalg.norm(right)
    up = np.cross(camera,right)
    vertices = np.asarray(outfit.mesh.vertices)
    pts = vertices[:,:3]
    projected = np.column_stack((pts@right,-pts@up))
    lo,hi = projected.min(axis=0),projected.max(axis=0)
    resolution = 640
    projected = (projected-(lo+hi)/2) * (resolution*.89/max(hi-lo)) + resolution*np.array([.5,.485])
    depth = pts@camera
    zbuf = np.full((resolution,resolution), -np.inf)
    result = np.zeros((resolution,resolution,4),dtype=np.uint8)
    tex = np.asarray(outfit.atlas)
    light = np.array([.24,.83,.54])
    for tri in np.array(outfit.mesh.indices).reshape(-1,3):
        normal = vertices[tri[0],3:6]
        if np.dot(normal,camera) <= 0:
            continue
        a,b,c = projected[tri]
        xy0 = np.floor(np.minimum(np.minimum(a,b),c)).astype(int).clip(0,resolution-1)
        xy1 = np.ceil(np.maximum(np.maximum(a,b),c)).astype(int).clip(0,resolution-1)
        xx,yy = np.meshgrid(np.arange(xy0[0],xy1[0]+1)+.5,np.arange(xy0[1],xy1[1]+1)+.5)
        u,v = b-a,c-a
        det = u[0]*v[1]-u[1]*v[0]
        if abs(det)<1e-9:
            continue
        s = ((xx-a[0])*v[1]-(yy-a[1])*v[0])/det
        t = (u[0]*(yy-a[1])-u[1]*(xx-a[0]))/det
        weights = np.stack((1-s-t,s,t),axis=-1)
        z = weights@depth[tri]
        rows,cols = slice(xy0[1],xy1[1]+1),slice(xy0[0],xy1[0]+1)
        front = (weights.min(axis=-1)>=-1e-8) & (z>zbuf[rows,cols])
        w = weights[front]
        uv = w@vertices[tri,6:8]
        tx = (uv[:,0]*ATLAS).astype(int).clip(0,ATLAS-1)
        ty = ((1-uv[:,1])*ATLAS).astype(int).clip(0,ATLAS-1)
        sky = (normal[1]+1)/2
        shade = .66 + .10*sky + .28*max(0,np.dot(normal,light))
        result[rows,cols,:3][front] = np.clip(tex[ty,tx]*shade,0,255)
        result[rows,cols,3][front] = 255
        zbuf[rows,cols][front] = z[front]
    shadow = Image.new("RGBA",(resolution,resolution))
    bottom = projected[:,1].max()
    ImageDraw.Draw(shadow).ellipse((resolution*.23,bottom-resolution*.077,resolution*.77,bottom+2),
                                   fill=(52,43,39,50))
    shadow = shadow.filter(ImageFilter.GaussianBlur(9))
    return Image.alpha_composite(shadow,Image.fromarray(result)).resize((256,256),Image.Resampling.LANCZOS)


def bake():
    OUT.mkdir(parents=True,exist_ok=True)
    for skin in SKINS:
        outfit = model(skin)
        portrait(outfit).save(OUT/(skin+"_Preview.png"),optimize=True)
        if skin in ("Classic","Neon"):
            continue  # Both already use the player's original cube and a solid material tint.
        outfit.atlas.save(OUT/(skin+"_Albedo.png"),optimize=True)
        mesh = outfit.mesh
        # Unity looks down +z. Mirror authored x so asymmetrical details read the same in-game.
        mesh.vertices = [(-x/48,(y-24)/48,z/48,-nx,ny,nz,u,v)
                         for x,y,z,nx,ny,nz,u,v in mesh.vertices]
        for i in range(0,len(mesh.indices),3):
            mesh.indices[i+1],mesh.indices[i+2] = mesh.indices[i+2],mesh.indices[i+1]
        save_mesh(mesh,OUT/(skin+".asset"))


if __name__ == "__main__":
    bake()
