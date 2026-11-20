"""Autopsia: original metre-scale equipment for the autopsy room, baked with the project's PBR pipeline.
Run: blender -b --factory-startup --threads 8 --python Blender/Autopsia/scripts/gerar.py
Optional: -- BancadaAutopsia ... ; SO_MALHAS=1 skips baking.

Conventions (all pieces): X = width, front = -Y, back face on the wall plane y = 0, Z up from the floor
(the exam lamp hangs from z = 0 = ceiling). Placement in Unity: yaw 0 puts the back against the north wall.
Glass and the lamp lens are separate, unbaked meshes (shared glass material, controllable emission)."""
import os, sys, math, json, types
import bpy, bmesh
from mathutils import Vector, Matrix

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "../../.."))
OUT = ROOT + "/Assets/Modelos/Autopsia"
SRC = ROOT + "/Blender/Autopsia"
ONLY = sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else None
if ONLY and os.path.exists(SRC+"/Autopsia.blend"):
    bpy.ops.wm.open_mainfile(filepath=SRC+"/Autopsia.blend")
def load(rel):
    p = ROOT + "/" + rel
    ns = {"__name__": "autopsia_module", "__file__": p}
    exec(compile(open(p).read(), p, "exec"), ns)
    return types.SimpleNamespace(**ns)
R = load("Blender/Recepcao/scripts/recepcao_modelo.py")
G = load("Blender/MesaAutopsia/scripts/mesa_materiais.py").G
B = load("Blender/MesaAutopsia/scripts/mesa_bake.py")
box, cyl, tube = R.box, R.cyl, R.tube

def material(key, color, rough, metallic=0, brushed=False):
    m = bpy.data.materials.new("Autopsia - " + key)
    g = G(m)
    n = g.noise(g.obj(), 7, 3)
    fine = g.noise(g.coords(scale=(3, 330, 330) if brushed else (150,150,150)), 1, 2)
    base = g.color(n, tuple(c*.94 for c in color), tuple(c*1.035 for c in color))
    rr = g.mrange(n, .15, .85, rough-.055, rough+.07)
    g.finish(base, rr, metallic, fine, bump_strength=.24, bump_dist=.00008 if brushed else .00016)
    return m

MATS = {
    # Same satin stainless as the receiving room, so the two clinical rooms read as one installation.
    "inox": material("inox acetinado", (.55,.57,.56), .34, .94, True),
    "cromado": material("cromado", (.67,.69,.69), .20, .96, True),
    "escuro": material("aco oxidado nas juntas", (.13,.145,.14), .48, .8),
    "borracha": material("borracha grafite", (.025,.03,.029), .72),
    "pintura": material("epoxi cinza claro", (.58,.60,.58), .42),
    "marfim": material("polimero marfim", (.69,.68,.61), .42),
    "carcaca": material("epoxi branco da luminaria", (.80,.81,.78), .36),
    "tecido": material("algodao cru", (.70,.69,.64), .88),
    "azul": material("embalagem azul desaturada", (.13,.23,.29), .59),
    "papel": material("papel", (.75,.73,.66), .81),
    "branco": material("laminado branco do quadro", (.80,.81,.79), .20),
    "aluminio": material("aluminio anodizado", (.60,.61,.61), .40, .90),
    "galvanizado": material("eletroduto galvanizado", (.47,.49,.49), .48, .85),
    "mostrador": material("mostrador esmaltado", (.83,.83,.80), .38),
    "preto": material("tinta preta", (.03,.03,.03), .50),
    "vermelho": material("ponteiro vermelho", (.45,.05,.04), .40),
}
GLASS = {"vidro": material("vidro (malha separada)", (.6,.65,.64), .05)}
LENS = {"lente": material("lente difusora (malha separada)", (.9,.9,.88), .25)}

def beam(name, mat, a, b, width=.035, depth=None):
    a,b=Vector(a),Vector(b)
    ob=box(name,mat,-width/2,-(depth or width)/2,0,width/2,(depth or width)/2,(b-a).length,r=.002)
    ob.data.transform(Matrix.Translation(a) @ Vector((0,0,1)).rotation_difference(b-a).to_matrix().to_4x4())
    return ob

def bolt(x,y,z,axis=(0,-1,0),r=.009):
    cyl("Arruela","escuro",(x,y,z),r*1.3,.002,axis,seg=16,r=.0004)
    cyl("Parafuso","cromado",Vector((x,y,z))+Vector(axis)*.003,r,.006,axis,seg=6,r=.0005)

def link(name, mat, verts, faces, smooth=True):
    me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update()
    ob=bpy.data.objects.new(name,me);R._coll(R._current["asset"]).objects.link(ob);ob["mat"]=mat
    if smooth:
        for f in me.polygons:f.use_smooth=True
    return ob

def sink(cx,cy,z,w,d,depth,trap=True):
    # Deep rounded bowl welded under the top: lip, vertical wall, coved floor and drain.
    verts=[];seg=12
    def ring(a,b,rad,h):
        pts=[]
        for sx,sy,start in [(1,1,0),(-1,1,90),(-1,-1,180),(1,-1,270)]:
            for j in range(seg):
                ang=math.radians(start+j*90/(seg-1))
                pts.append((cx+sx*(a-rad)+math.cos(ang)*rad,cy+sy*(b-rad)+math.sin(ang)*rad,h))
        return pts
    for a,b,r,h in [(w/2+.018,d/2+.018,.06,z),(w/2,d/2,.05,z+.001),(w/2-.012,d/2-.012,.05,z-depth+.04),
                     (w/2-.05,d/2-.05,.04,z-depth)]:
        verts+=ring(a,b,r,h)
    n=seg*4;faces=[]
    for k in range(3):
        for j in range(n):faces.append((k*n+j,k*n+(j+1)%n,(k+1)*n+(j+1)%n,(k+1)*n+j))
    faces.append(tuple(range(3*n,4*n)))
    link("Cuba funda","inox",verts,faces)
    cyl("Valvula de escoamento","cromado",(cx,cy,z-depth+.003),.03,.006,seg=32,r=.001)
    for i in range(8):
        a=i*math.tau/8
        cyl("Furo da valvula","escuro",(cx+.017*math.cos(a),cy+.017*math.sin(a),z-depth+.0065),.003,.001,seg=8,r=0)
    if trap: tube("Sifao","inox",[(cx,cy,z-depth),(cx,cy,z-depth-.12),(cx+.08,cy,z-depth-.18),(cx+.08,cy+.3,z-depth-.18)],.022,fillet=.05)

def gooseneck(x,y,z,reach=.20,height=.30,lever_side=1):
    # Clinical mixer: deck canopy, high swan neck and a long elbow lever.
    cyl("Canopla","cromado",(x,y,z),.032,.014,seg=24)
    tube("Bica alta","cromado",[(x,y,z),(x,y,z+height),(x,y-reach,z+height),(x,y-reach-.015,z+height-.07)],.0135,fillet=.075)
    cyl("Arejador","escuro",(x,y-reach-.016,z+height-.072),.0145,.014,seg=20)
    cyl("Corpo do misturador","cromado",(x+lever_side*.035,y,z+.07),.021,.06,(1,0,0),seg=24)
    tube("Alavanca de cotovelo","cromado",[(x+lever_side*.065,y,z+.07),(x+lever_side*.065,y-.17,z+.10)],.0065)

def door(x0,x1,z0,z1,yf,handle_x,mat="inox",hinge_x=None):
    box("Porta com dobra",mat,x0,yf-.018,z0,x1,yf,z1,r=.003)
    box("Vinco da porta",mat,x0+.03,yf-.0195,z0+.035,x1-.03,yf-.017,z1-.035,r=.001)
    tube("Puxador","cromado",[(handle_x,yf-.018,z1-.21),(handle_x,yf-.048,z1-.21),(handle_x,yf-.048,z1-.07),(handle_x,yf-.018,z1-.07)],.0055,fillet=.01)
    hx=x0 if hinge_x is None else hinge_x
    for h in (z0+.06,z1-.11):box("Dobradica","escuro",hx-.004,yf-.0195,h,hx+.012,yf-.013,h+.05,r=.001)

def drawer(x0,x1,z0,z1,yf):
    box("Frente de gaveta","inox",x0,yf-.018,z0,x1,yf,z1,r=.003)
    xm=(x0+x1)/2
    tube("Puxador da gaveta","cromado",[(xm-.11,yf-.018,z1-.05),(xm-.11,yf-.045,z1-.05),(xm+.11,yf-.045,z1-.05),(xm+.11,yf-.018,z1-.05)],.0055,fillet=.01)

# ------------------------------------------------------------------ rigid equipment

def bancada():
    """3.80 x 0.65 m support counter, top at 0.90: AISI 304 hairline top with down-turned front, raised
    containment lip, 150 mm splashback, deep bowl and swan-neck mixer; stainless base cabinets on a
    recessed plinth. Bowl centre at x = -0.60 (scene x 1.30)."""
    W,D,top=3.80,.65,.90; xa,xb=-W/2,W/2; yf=-.60
    for x in (xa+.12,-.63,.63,xb-.12):
        for y in (-.48,-.12): cyl("Pe nivelador","escuro",(x,y,.06),.022,.12,seg=16)
    box("Rodape recuado","inox",xa+.02,-.53,.0,xb-.02,-.51,.12,r=.002)
    box("Fundo do gabinete","inox",xa,yf,.12,xb,0,.14,r=.003)
    box("Lateral esquerda","inox",xa,yf,.12,xa+.018,0,.87,r=.003)
    box("Lateral direita","inox",xb-.018,yf,.12,xb,0,.87,r=.003)
    box("Costas","inox",xa,-.018,.14,xb,0,.87,r=.003)
    box("Travessa inferior","inox",xa,yf-.02,.12,xb,yf,.152,r=.003)
    box("Travessa superior","inox",xa,yf-.02,.822,xb,yf,.87,r=.003)
    edges=[xa,-1.25,0.0,.62,1.26,xb]
    for e in edges:
        x0,x1=(e,e+.018) if e==xa else (e-.018,e) if e==xb else (e-.008,e+.008)
        box("Montante frontal","inox",x0,yf-.02,.152,x1,yf,.822,r=.002)
    g=.004
    door(edges[0]+.018+g,edges[1]-.008-g,.156,.818,yf-.002,edges[1]-.08)
    door(edges[1]+.008+g,-.625-g,.156,.818,yf-.002,-.625-.07)
    door(-.625+g,edges[2]-.008-g,.156,.818,yf-.002,-.625+.07,hinge_x=edges[2]-.02)
    for z0,z1 in [(.156,.40),(.404,.61),(.614,.818)]: drawer(edges[2]+.008+g,edges[3]-.008-g,z0,z1,yf-.002)
    door(edges[3]+.008+g,edges[4]-.008-g,.156,.818,yf-.002,edges[4]-.08)
    door(edges[4]+.008+g,edges[5]-.018-g,.156,.818,yf-.002,edges[4]+.08,hinge_x=edges[5]-.03)
    # Top around the bowl aperture (x -.88..-.32, y -.55..-.11), front folded down 60 mm.
    t0=top-.03
    for x0,x1,y0,y1 in [(xa-.01,-.88,-.65,0),(-.32,xb+.01,-.65,0),(-.88,-.32,-.65,-.55),(-.88,-.32,-.11,0)]:
        box("Tampo inox","inox",x0,y0,t0,x1,y1,top,r=.004)
    box("Frontal dobrado","inox",xa-.01,-.655,top-.06,xb+.01,-.645,top,r=.003)
    box("Borda de contencao","inox",xa-.01,-.655,top,xb+.01,-.64,top+.012,r=.004)
    for x0,x1 in [(xa-.01,xa+.005),(xb-.005,xb+.01)]:
        box("Espelho lateral","inox",x0,-.64,top,x1,0,top+.08,r=.003)
    box("Espelho traseiro","inox",xa-.01,-.018,top,xb+.01,0,top+.15,r=.003)
    box("Dobra do espelho","inox",xa-.01,-.04,top+.142,xb+.01,0,top+.152,r=.003)
    sink(-.60,-.33,top,.56,.44,.26)
    gooseneck(-.60,-.065,top)
    # Pressed drainboard ribs beside the bowl, falling toward it.
    for i in range(7):
        x=-.24+i*.07
        tube("Nervura do escorredor","inox",[(x,-.52,top+.0015),(x,-.14,top+.0015)],.0022,res=1)

def armario_suspenso():
    """1.60 x 0.70 x 0.33 m wall cabinet: stainless carcass, glass shelf and two sliding glass panes in
    top/bottom tracks. Sparse consumables only, nothing printed."""
    W,H,D=1.60,.70,.33; xa,xb=-W/2,W/2
    box("Teto","inox",xa,-D,H-.018,xb,0,H,r=.003)
    box("Base","inox",xa,-D,0,xb,0,.018,r=.003)
    box("Lateral","inox",xa,-D,0,xa+.016,0,H,r=.003)
    box("Lateral","inox",xb-.016,-D,0,xb,0,H,r=.003)
    box("Fundo","inox",xa,-.014,0,xb,0,H,r=.002)
    box("Montante central","inox",-.008,-.30,.018,.008,-.014,H-.018,r=.002)
    for z0,z1 in [(.018,.034),(H-.034,H-.018)]:
        box("Trilho de correr","escuro",xa+.016,-D+.004,z0,xb-.016,-D+.030,z1,r=.002)
    for x in (xa+.10,-.10,.10,xb-.10):
        cyl("Pino da prateleira","cromado",(x,-.17,.346),.004,.30,(0,1,0),seg=8,r=0)
    for x in (xa+.15,xb-.15): bolt(x,-.0145,H-.08,(0,-1,0),r=.006)
    # Contents: folded towels, gauze carton, three bottles and a stack of kidney dishes.
    for j in range(4): box("Campo dobrado","tecido",xa+.07,-.27,.018+j*.022,xa+.40,-.05,.038+j*.022,r=.006,seg=3)
    box("Caixa de compressas","papel",xa+.47,-.26,.018,xa+.74,-.06,.15,r=.004)
    for k,(x,h,m) in enumerate([(.16,.20,"marfim"),(.26,.17,"azul"),(.35,.20,"marfim")]):
        R.lathe("Frasco","marfim" if m=="marfim" else m,[(.036,0),(.04,.012),(.04,h-.04),(.022,h-.015),(.016,h-.01),(.016,h)],
                seg=24,r=.002,matrix=Matrix.Translation((x,-.15,.018)))
        cyl("Tampa","marfim",(x,-.15,.018+h+.008),.019,.016,seg=20)
    for j in range(3):
        R.lathe("Cuba rim empilhada","inox",[(.10,.0),(.115,.03),(.112,.032),(.095,.004)],seg=28,r=.001,
                matrix=Matrix.Translation((.58,-.17,.36+j*.012))@Matrix.Diagonal((1,.55,1,1)))
    box("Pacote selado","azul",xa+.08,-.25,.352,xa+.44,-.07,.40,r=.006)

def armario_suspenso_vidro():
    W,H,D=1.60,.70,.33; xa,xb=-W/2,W/2
    box("Prateleira de vidro","vidro",xa+.016,-.30,.35,xb-.016,-.016,.356,r=.001,seg=1)
    for k,(x0,x1,y) in enumerate([(xa+.02,.03,-D+.010),(-.03,xb-.02,-D+.022)]):
        box("Folha de correr","vidro",x0,y-.003,.03,x1,y+.003,H-.03,r=.0015,seg=1)

def puxador_vidro(x,yf,z):
    box("Puxador da folha","cromado",x-.007,yf-.02,z-.06,x+.007,yf-.005,z+.06,r=.003)

def armario_instrumental():
    """1.40 x 0.45 x 1.95 m instrument cabinet: closed stainless base (two doors) and a glazed upper case
    with framed hinged doors and two glass shelves; instrument boxes and trays, no labels."""
    W,D,H=1.40,.45,1.95; xa,xb=-W/2,W/2; yf=-D
    for x in (xa+.06,xb-.06):
        for y in (-.39,-.06): cyl("Pe nivelador","escuro",(x,y,.05),.02,.10,seg=16)
    box("Base","inox",xa,yf,.10,xb,0,.12,r=.003)
    box("Lateral","inox",xa,yf,.10,xa+.018,0,H,r=.003)
    box("Lateral","inox",xb-.018,yf,.10,xb,0,H,r=.003)
    box("Fundo","inox",xa,-.016,.10,xb,0,H,r=.002)
    box("Tampo","inox",xa-.005,yf-.005,H-.02,xb+.005,0,H,r=.004)
    box("Divisoria da bancadinha","inox",xa,yf-.012,.84,xb,0,.875,r=.004)
    box("Travessa inferior","inox",xa,yf-.012,.10,xb,yf,.14,r=.002)
    box("Montante central inferior","inox",-.008,yf-.012,.14,.008,yf,.84,r=.002)
    g=.004
    door(xa+.018+g,-.008-g,.144,.836,yf-.001,-.08)
    door(.008+g,xb-.018-g,.144,.836,yf-.001,.08,hinge_x=xb-.03)
    # Upper glazed doors: stainless frames 32 mm (panes come from ArmarioInstrumentalVidro).
    zt=H-.024
    for x0,x1,hx,px in [(xa+.018+g,-.004,xa+.018,-.035),(.004,xb-.018-g,xb-.03,.035)]:
        f=.032
        box("Quadro da porta","inox",x0,yf-.02,.88,x1,yf-.004,.88+f,r=.003)
        box("Quadro da porta","inox",x0,yf-.02,zt-f,x1,yf-.004,zt,r=.003)
        box("Quadro da porta","inox",x0,yf-.02,.88,x0+f,yf-.004,zt,r=.003)
        box("Quadro da porta","inox",x1-f,yf-.02,.88,x1,yf-.004,zt,r=.003)
        puxador_vidro(px,yf-.004,1.30)
        for h in (.95,zt-.12):box("Dobradica","escuro",hx-.004,yf-.022,h,hx+.012,yf-.015,h+.05,r=.001)
    for z in (1.24,1.58):
        for x in (xa+.04,xb-.04):
            for y in (-.38,-.06): cyl("Suporte de prateleira","cromado",(x,y,z-.004),.005,.008,seg=8,r=0)
    # Contents, kept sparse: lidded instrument boxes, a tray, two bottles, a roll of gauze.
    for j,(x0,x1) in enumerate([(xa+.08,xa+.44),(xa+.50,xa+.86)]):
        box("Caixa de instrumental","inox",x0,-.36,.875,x1,-.12,.955,r=.006)
        box("Tampa da caixa","inox",x0-.004,-.364,.952,x1+.004,-.116,.964,r=.004)
        for x in (x0+.04,x1-.04): box("Trava da caixa","cromado",x-.012,-.368,.93,x+.012,-.362,.96,r=.002)
    box("Caixa de instrumental","inox",xa+.10,-.34,1.246,xa+.52,-.12,1.30,r=.006)
    box("Tampa da caixa","inox",xa+.096,-.344,1.297,xa+.524,-.116,1.308,r=.004)
    R.lathe("Rolo de atadura","tecido",[(.02,0),(.05,0),(.05,.10),(.02,.10)],seg=24,r=.003,matrix=Matrix.Translation((.30,-.20,1.246)))
    for x,h,m in [(.48,.18,"marfim"),(.56,.15,"azul")]:
        R.lathe("Frasco","marfim" if m=="marfim" else m,[(.034,0),(.038,.012),(.038,h-.04),(.02,h-.015),(.015,h-.01),(.015,h)],
                seg=24,r=.002,matrix=Matrix.Translation((x,-.22,1.246)))
        cyl("Tampa","marfim",(x,-.22,1.246+h+.008),.018,.016,seg=20)
    for j in range(3): box("Pacote de campo","azul",.08,-.36,1.586+j*.03,.58,-.10,1.612+j*.03,r=.006)

def armario_instrumental_vidro():
    W,D,H=1.40,.45,1.95; xa,xb=-W/2,W/2; yf=-D; zt=H-.024; g=.004
    for z in (1.24,1.58): box("Prateleira de vidro","vidro",xa+.02,-.40,z,xb-.02,-.02,z+.006,r=.001,seg=1)
    for x0,x1 in [(xa+.018+g,-.004),(.004,xb-.018-g)]:
        box("Vidro da porta","vidro",x0+.028,yf-.014,.908,x1-.028,yf-.010,zt-.028,r=.001,seg=1)

def lavatorio():
    """Wall-hung stainless scrub sink 0.60 x 0.45 m, rim at 0.90, 160 mm front, sloping underside,
    knee paddle valve, swan-neck spout from the splashback, exposed trap to the wall."""
    W,D,top=.60,.45,.90; xa,xb=-W/2,W/2
    sink(0,-.235,top,.50,.34,.20,trap=False)
    for x0,x1,y0,y1 in [(xa,-.25,-D-.008,0),(.25,xb,-D-.008,0),(-.25,.25,-D-.008,-.405),(-.25,.25,-.065,0)]:
        box("Tampo do lavatorio","inox",x0,y0,top-.02,x1,y1,top,r=.004)
    box("Frontal","inox",xa,-D-.008,top-.16,xb,-D,top-.012,r=.003)
    for x in (xa,xb-.008):
        R.extrude("Lateral do lavatorio","inox",[(-D,top-.16),(-D,top),(0,top),(0,top-.30)],"YZ",x,x+.008,r=.003)
    a=(-D,top-.16);b=(0,top-.30)
    R.hull("Fundo inclinado","inox",[(xa,a[0],a[1]),(xb,a[0],a[1]),(xa,b[0],b[1]),(xb,b[0],b[1]),
                                    (xa,a[0],a[1]+.008),(xb,a[0],a[1]+.008),(xa,b[0],b[1]+.008),(xb,b[0],b[1]+.008)],r=.002)
    box("Espelho","inox",xa,-.015,top,xb,0,top+.28,r=.003)
    box("Dobra do espelho","inox",xa,-.035,top+.272,xb,0,top+.282,r=.003)
    for x in (xa+.05,xb-.05):
        for z in (top+.05,top+.23): bolt(x,-.015,z,r=.005)
    # Spout from the splashback (supply hidden behind it): short stub, then a single swan neck.
    cyl("Canopla da bica","cromado",(0,-.015,1.10),.026,.012,(0,1,0),seg=24)
    tube("Bica pescoco de ganso","cromado",[(0,-.02,1.10),(0,-.085,1.10),(0,-.085,1.23),(0,-.235,1.23),(0,-.245,1.15)],.012,fillet=.035)
    cyl("Arejador","escuro",(0,-.246,1.145),.013,.012,seg=20)
    # Knee paddle hung just under the front edge, on a short rod from the valve tucked under the bowl.
    box("Caixa da valvula","inox",-.05,-.42,.665,.05,-.33,.705,r=.005)
    tube("Haste da alavanca","cromado",[(0,-.42,.69),(0,-.462,.69)],.005,res=1)
    box("Alavanca de joelho","inox",-.085,-.474,.60,.085,-.462,.715,r=.006)
    box("Borracha da alavanca","borracha",-.075,-.478,.62,.075,-.473,.68,r=.004)
    tube("Alimentacao","cromado",[(.035,-.33,.675),(.035,-.012,.675)],.007,res=1)
    cyl("Canopla","cromado",(.035,-.006,.675),.02,.012,(0,1,0),seg=20)
    tube("Sifao","inox",[(0,-.235,.70),(0,-.235,.58),(-.09,-.235,.53),(-.09,-.10,.50),(-.09,-.012,.50)],.020,fillet=.04)
    cyl("Canopla do esgoto","cromado",(-.09,-.006,.50),.032,.012,(0,1,0),seg=20)

def balanca():
    """Hanging dial scale on a wall bracket: arm at 2.05 m, dial 260 mm facing the room, three chains to a
    shallow stainless pan whose rim sits 0.40 m above the counter. Graduations only, no numerals."""
    zA=2.05; reach=.36
    box("Placa de fixacao","inox",-.07,-.012,zA-.26,.07,0,zA+.05,r=.004)
    for z in (zA-.22,zA+.02): bolt(0,-.012,z,r=.006)
    tube("Braco","inox",[(0,-.012,zA),(0,-reach,zA)],.016,res=2)
    tube("Mao-francesa","inox",[(0,-.012,zA-.22),(0,-reach+.08,zA-.005)],.010,res=2)
    cyl("Ponteira do braco","escuro",(0,-reach-.004,zA),.017,.012,(0,1,0),seg=16)
    tube("Gancho","cromado",[(0,-reach+.01,zA-.016),(0,-reach+.01,zA-.06),(.012,-reach+.01,zA-.075),(.012,-reach+.01,zA-.10)],.0045,fillet=.01)
    cz=zA-.30; cy=-reach+.01
    tube("Alca de suspensao","cromado",[(0,cy,zA-.10),(0,cy,cz+.15)],.006,res=1)
    cyl("Caixa do mostrador","pintura",(0,cy,cz),.13,.075,(0,1,0),seg=48,r=.008)
    tube("Aro","cromado",[(math.cos(i*math.tau/48)*.118,cy-.040,cz+math.sin(i*math.tau/48)*.118) for i in range(49)],.0055,res=1)
    cyl("Mostrador","mostrador",(0,cy-.0385,cz),.114,.003,(0,1,0),seg=48,r=0)
    for i in range(60):
        a=math.radians(90-i*6); big=i%5==0; r0=.112-(.018 if big else .010)
        p0=(math.cos(a)*r0,cy-.0405,cz+math.sin(a)*r0); p1=(math.cos(a)*.106,cy-.0405,cz+math.sin(a)*.106)
        beam("Graduacao","preto",p0,p1,.0028 if big else .0014,.0008)
    beam("Ponteiro","vermelho",(0,cy-.043,cz-.02),(0,cy-.043,cz+.095),.004,.0012)
    cyl("Eixo do ponteiro","cromado",(0,cy-.044,cz),.008,.004,(0,1,0),seg=16,r=0)
    tube("Haste inferior","cromado",[(0,cy,cz-.13),(0,cy,cz-.18)],.006,res=1)
    hz=cz-.19; R.lathe("Argola","cromado",[(.010,-.004),(.016,0),(.010,.004)],seg=20,matrix=Matrix.Translation((0,cy,hz)))
    pan_z=1.30; rp=.17
    for i in range(3):
        a=math.radians(90+i*120)
        tube("Corrente","cromado",[(0,cy,hz),(rp*.97*math.cos(a),cy+rp*.97*math.sin(a),pan_z+.004)],.0022,res=1)
    R.lathe("Prato","inox",[(.0,pan_z-.07),(.10,pan_z-.068),(.15,pan_z-.04),(.168,pan_z),(.172,pan_z+.004),(.164,pan_z+.002),
                            (.145,pan_z-.036),(.097,pan_z-.062),(.0,pan_z-.064)],seg=48,r=.001,matrix=Matrix.Translation((0,cy,0)))

def luminaria():
    """Ceiling LED exam light, single head: canopy, 450 mm drop, two articulated 400 mm arms, a single-sided
    yoke and a 340 mm domed head with eight LED cells and a central handle. Origin = ceiling point; arm reaches +X; head centre at
    (0.62, 0, -1.10); the lens (LuminariaExameLente) faces down."""
    cyl("Canopla do teto","pintura",(0,0,-.015),.085,.03,seg=40,r=.004)
    for i in range(3):
        a=i*math.tau/3+.5; cyl("Parafuso da canopla","cromado",(.06*math.cos(a),.06*math.sin(a),-.0305),.006,.002,seg=8,r=0)
    tube("Tubo de descida","pintura",[(0,0,-.03),(0,0,-.45)],.020,res=2)
    def knuckle(p): cyl("Articulacao","escuro",p,.031,.07,(0,1,0),seg=24,r=.004)
    k1=(0,0,-.47); k2=(.40,0,-.585); k3=(.62,0,-.90)
    for k in (k1,k2,k3): knuckle(k)
    for a,b in [(k1,k2),(k2,k3)]:
        for s in (-1,1):
            tube("Braco articulado","pintura",[(a[0],s*.022,a[2]),(b[0],s*.022,b[2])],.013,res=2)
    cyl("Mola compensadora","escuro",((k1[0]+k2[0])/2,0,(k1[2]+k2[2])/2+.002),.010,.33,Vector(k2)-Vector(k1),seg=12)
    hc=Vector((.62,0,-1.10))
    # Single-sided cardan yoke (LED exam lights, e.g. VOG Luxiflex): short drop from the last joint, a curved arm
    # around the head's +Y side and a pivot on its rim, instead of a wide fork.
    tube("Garfo","pintura",[(k3[0],0,k3[2]),(k3[0],0,-.975),(k3[0],.215,-.975),(k3[0],.215,hc.z+.015)],.013,fillet=.035)
    cyl("Pivo da cabeca","escuro",(hc.x,.196,hc.z+.015),.024,.034,(0,1,0),seg=20,r=.003)
    # Head: 340 mm domed white housing, 120 mm deep, dark front face with eight LED cells in chrome rings
    # around the handle. Faces down; front face at z0.
    z0=hc.z-.04
    R.lathe("Cabeca","carcaca",[(.0,z0+.118),(.05,z0+.116),(.10,z0+.104),(.138,z0+.084),(.162,z0+.058),(.172,z0+.030),
                               (.172,z0+.008),(.168,z0+.001),(.160,z0),(.0,z0+.004)],seg=64,r=.002,
            matrix=Matrix.Translation((hc.x,hc.y,0)))
    cyl("Aro da cabeca","pintura",(hc.x,hc.y,z0+.010),.1735,.016,seg=64,r=.003)
    cyl("Face frontal","escuro",(hc.x,hc.y,z0-.001),.158,.004,seg=64,r=.001)
    for k in range(8):
        a=k*math.tau/8+math.pi/8
        R.lathe("Aro do LED","cromado",[(.033,0),(.033,.004),(.026,.004),(.026,0)],seg=32,r=0,
                matrix=Matrix.Translation((hc.x+.104*math.cos(a),hc.y+.104*math.sin(a),z0-.006)))
    cyl("Tampa superior","pintura",(hc.x,hc.y,z0+.118),.045,.012,seg=32,r=.003)
    cyl("Base da manopla","escuro",(hc.x,hc.y,z0-.006),.036,.010,seg=32,r=.002)
    cyl("Manopla","marfim",(hc.x,hc.y,z0-.050),.020,.080,seg=24,r=.006,radius_top=.023)

def luminaria_lente():
    """Emissive LED cells of the exam light: eight discs of 52 mm inside the chrome rings."""
    hc=Vector((.62,0,-1.10)); z=hc.z-.04-.004
    verts=[];faces=[]
    def disc(cx,cy,rad,N=24):
        b=len(verts);verts.append((cx,cy,z))
        for i in range(N):
            a=i*math.tau/N;verts.append((cx+rad*math.cos(a),cy+rad*math.sin(a),z))
        for i in range(N):faces.append((b,b+1+(i+1)%N,b+1+i))
    for k in range(8):
        a=k*math.tau/8+math.pi/8;disc(hc.x+.104*math.cos(a),hc.y+.104*math.sin(a),.026)
    link("Lente difusora","lente",verts,faces,smooth=False)

def comando():
    """Local switch of the exam light on the south wall, with the provisional surface conduit: up the wall
    to the ceiling, then across to a junction box beside the light's canopy. Placed with yaw 180 at the
    wall below the light mount; RUN = horizontal run to the canopy (scene z -3.4 -> -1.04)."""
    RUN=2.36; zc=3.175; r=.0125
    box("Caixa de sobrepor","pintura",-.045,-.055,1.24,.045,0,1.36,r=.006)
    box("Espelho do interruptor","marfim",-.038,-.059,1.255,.038,-.054,1.345,r=.003)
    box("Tecla","marfim",-.013,-.064,1.285,.013,-.058,1.315,r=.003)
    cyl("Bucha","escuro",(0,-.028,1.365),.017,.012,seg=20)
    # The junction box stops 6 cm short of the light's 170 mm canopy; a short PP cable crosses the gap.
    tube("Eletroduto aparente","galvanizado",[(0,-.028,1.37),(0,-.028,zc),(0,-RUN+.24,zc)],r,fillet=.09,res=3)
    box("Condulete","galvanizado",-.04,-RUN+.24,zc-.055,.04,-RUN+.145,zc+.025,r=.006)
    cyl("Tampa do condulete","galvanizado",(0,-RUN+.19,zc-.057),.032,.004,seg=24,r=.001)
    tube("Cabo PP","borracha",[(0,-RUN+.145,zc-.03),(0,-RUN+.11,zc-.035),(0,-RUN+.085,3.2-.018)],.005,fillet=.012,res=2)
    def strap(p,axis):
        cyl("Abracadeira","galvanizado",p,r+.004,.018,axis,seg=20,r=.001)
    for z in (1.85,2.45,3.0): strap((0,-.028,z),(0,0,1))
    for y in (-.45,-1.05,-1.65): strap((0,y,zc),(0,1,0))

def quadro():
    """Wall whiteboard 0.80 x 0.60 m, bottom edge 1.10 m, anodised frame and marker tray; erased."""
    xa,xb,z0,z1=-.40,.40,1.10,1.70
    box("Placa","branco",xa+.01,-.014,z0+.01,xb-.01,0,z1-.01,r=.001)
    for x0,x1,za,zb in [(xa,xb,z0,z0+.018),(xa,xb,z1-.018,z1),(xa,xa+.018,z0,z1),(xb-.018,xb,z0,z1)]:
        box("Moldura","aluminio",x0,-.02,za,x1,0,zb,r=.003)
    for x in (xa,xb-.026): box("Cantoneira","escuro",x,-.022,z0,x+.026,-.016,z0+.026,r=.002)
    box("Calha porta-marcador","aluminio",xa+.10,-.06,z0-.012,xb-.10,-.014,z0+.004,r=.003)
    box("Aba da calha","aluminio",xa+.10,-.062,z0+.004,xb-.10,-.056,z0+.016,r=.002)
    R.lathe("Marcador","preto",[(.0085,0),(.0085,.10),(.006,.115)],seg=16,r=.001,
            matrix=Matrix.Translation((.08,-.035,z0+.006))@Matrix.Rotation(math.pi/2,4,"Y"))
    box("Apagador","azul",-.14,-.05,z0+.004,-.02,-.02,z0+.03,r=.005)
    box("Feltro do apagador","tecido",-.138,-.048,z0+.004,-.022,-.022,z0+.008,r=.002)

BUILDERS={"BancadaAutopsia":bancada,"ArmarioSuspenso":armario_suspenso,"ArmarioSuspensoVidro":armario_suspenso_vidro,
          "ArmarioInstrumental":armario_instrumental,"ArmarioInstrumentalVidro":armario_instrumental_vidro,
          "LavatorioJoelho":lavatorio,"BalancaVisceras":balanca,"LuminariaExame":luminaria,
          "LuminariaExameLente":luminaria_lente,"ComandoLuminaria":comando,"QuadroBranco":quadro}
UNBAKED={"ArmarioSuspensoVidro":GLASS,"ArmarioInstrumentalVidro":GLASS,"LuminariaExameLente":LENS}
SIZES={k:2048 if k in ("BancadaAutopsia","ArmarioInstrumental") else 1024 for k in BUILDERS}

def main():
    os.makedirs(OUT+"/Texturas",exist_ok=True);os.makedirs(SRC+"/revisao",exist_ok=True)
    only=ONLY or list(BUILDERS)
    for o in list(bpy.data.objects):
        if not ONLY or any(o in list(R._coll(n).objects) or o.name in (n,n+"_fonte") for n in only):
            bpy.data.objects.remove(o,do_unlink=True)
    stats={}
    for i,(name,fn) in enumerate(BUILDERS.items()):
        if name not in only:continue
        R._current["asset"]=name;fn()
        ob=R.assemble(name,UNBAKED.get(name,MATS))
        bm=bmesh.new();bm.from_mesh(ob.data)
        bmesh.ops.dissolve_degenerate(bm,dist=.000001,edges=list(bm.edges))
        bmesh.ops.triangulate(bm,faces=list(bm.faces))
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
        bm.to_mesh(ob.data);bm.free()
        density=0 if name in UNBAKED else R.layout_uvs(ob,SIZES[name],margin_px=8)
        stats[name]={"triangles":len(ob.data.polygons),"texelsPerMetre":density,"size":[round(v,4) for v in ob.dimensions],
                     "bounds":[[round(min(v.co[k] for v in ob.data.vertices),4) for k in range(3)],
                               [round(max(v.co[k] for v in ob.data.vertices),4) for k in range(3)]],
                     "degenerate":sum(f.area<1e-12 for f in ob.data.polygons)}
        R.export_fbx(ob,OUT+"/"+name+".fbx",keep_slots=name in UNBAKED)
        ob.location.x=i*3
    bpy.ops.wm.save_as_mainfile(filepath=SRC+"/Autopsia.blend")
    path=SRC+"/revisao/malhas.json"
    previous=json.load(open(path)) if ONLY and os.path.exists(path) else {}
    previous.update(stats)
    with open(path,"w") as f:json.dump(previous,f,indent=2)
    print("MALHAS",json.dumps(stats),flush=True)
    if os.environ.get("SO_MALHAS")=="1":return
    for name in stats:
        if name in UNBAKED:continue
        ob=bpy.data.objects[name];pos=ob.location.copy();ob.location=(0,0,0)
        others=[o for o in bpy.context.scene.objects if o!=ob and o.type=="MESH"]
        for o in others:o.hide_render=True
        B.bake.__globals__["SIZE"]=SIZES[name]
        imgs,times=B.bake(ob,[s.material for s in ob.material_slots],ao_samples=32)
        B.write_unity_maps(imgs,OUT+"/Texturas",prefix=name)
        ob.location=pos
        for o in others:o.hide_render=False
        print("BAKED",name,stats[name]["triangles"],times,flush=True)
    bpy.ops.wm.save_as_mainfile(filepath=SRC+"/Autopsia.blend")

if __name__=="__main__":main()
