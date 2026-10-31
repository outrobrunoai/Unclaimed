"""Recebimento: original metre-scale models, baked with the project's existing PBR pipeline.
Run: blender -b --factory-startup --python Blender/Recebimento/scripts/gerar.py
Optional: -- MacaRecebimento ... ; SO_MALHAS=1 skips baking.
"""
import os, sys, math, json, types
import bpy, bmesh
from mathutils import Vector, Matrix

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "../../.."))
OUT = ROOT + "/Assets/Modelos/Recebimento"
SRC = ROOT + "/Blender/Recebimento"
ONLY = sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else None
if ONLY and os.path.exists(SRC+"/Recebimento.blend"):
    bpy.ops.wm.open_mainfile(filepath=SRC+"/Recebimento.blend")
def load(rel):
    p = ROOT + "/" + rel
    ns = {"__name__": "recebimento_module", "__file__": p}
    exec(compile(open(p).read(), p, "exec"), ns)
    return types.SimpleNamespace(**ns)
R = load("Blender/Recepcao/scripts/recepcao_modelo.py")
G = load("Blender/MesaAutopsia/scripts/mesa_materiais.py").G
B = load("Blender/MesaAutopsia/scripts/mesa_bake.py")
box, cyl, tube = R.box, R.cyl, R.tube

def material(key, color, rough, metallic=0, brushed=False):
    m = bpy.data.materials.new("Recebimento - " + key)
    g = G(m)
    n = g.noise(g.obj(), 7, 3)
    fine = g.noise(g.coords(scale=(3, 330, 330) if brushed else (150,150,150)), 1, 2)
    base = g.color(n, tuple(c*.94 for c in color), tuple(c*1.035 for c in color))
    rr = g.mrange(n, .15, .85, rough-.055, rough+.07)
    g.finish(base, rr, metallic, fine, bump_strength=.24, bump_dist=.00008 if brushed else .00016)
    return m

MATS = {
    "inox": material("inox acetinado", (.55,.57,.56), .34, .94, True),
    "cromado": material("cromado", (.67,.69,.69), .20, .96, True),
    "escuro": material("aco oxidado nas juntas", (.13,.145,.14), .48, .8),
    "borracha": material("borracha grafite", (.025,.03,.029), .72),
    "nylon": material("nylon dos roletes", (.45,.46,.43), .44),
    "pintura": material("esmalte cinza esverdeado", (.28,.32,.30), .44),
    "marfim": material("polimero marfim", (.69,.68,.61), .42),
    "tecido": material("algodao cru", (.70,.69,.64), .88),
    "azul": material("embalagem azul desaturada", (.13,.23,.29), .59),
    "papel": material("papel", (.75,.73,.66), .81),
}

def beam(name, mat, a, b, width=.035, depth=None):
    a,b=Vector(a),Vector(b)
    ob=box(name,mat,-width/2,-(depth or width)/2,0,width/2,(depth or width)/2,(b-a).length,r=.002)
    ob.data.transform(Matrix.Translation(a) @ Vector((0,0,1)).rotation_difference(b-a).to_matrix().to_4x4())
    return ob

def bolt(x,y,z,axis=(0,-1,0),r=.009):
    cyl("Arruela", "escuro",(x,y,z),r*1.3,.002,axis,seg=16,r=.0004)
    cyl("Parafuso sextavado", "cromado",Vector((x,y,z))+Vector(axis)*.003,r,.006,axis,seg=6,r=.0005)

def caster(x,y,r=.075):
    # Rubber tyre, inset metal hubs, fork cheeks, swivel bearing and brake pedal.
    cyl("Banda de rodagem","borracha",(x,y,r),r,.043,(0,1,0),seg=32,r=.008)
    for s in (-1,1):
        cyl("Cubo da roda","nylon",(x,y+s*.023,r),r*.68,.005,(0,1,0),seg=24,r=.002)
        cyl("Eixo da roda","cromado",(x,y+s*.028,r),.012,.009,(0,1,0),seg=12,r=.001)
        beam("Garfo do rodizio","inox",(x,y+s*.035,r),(x-.024,y+s*.035,2*r+.035),.018,.008)
    box("Ponte do garfo","inox",x-.047,y-.044,2*r+.022,x+.015,y+.044,2*r+.034,r=.004)
    cyl("Rolamento de giro","escuro",(x-.02,y,2*r+.043),.029,.019,seg=24)
    cyl("Pino do rodizio","cromado",(x-.02,y,2*r+.068),.012,.031,seg=16)
    box("Pedal do freio","borracha",x+.015,y-.028,r*1.6,x+.10,y+.028,r*1.6+.016,r=.004)

def tray(w,d,z,depth=.055):
    # Formed pan: recessed bed, sloped walls and a rolled perimeter.
    rings=[(w/2-.024,d/2-.024,z),(w/2,d/2,z+depth),(w/2-.006,d/2-.006,z+depth+.005),
           (w/2-.030,d/2-.030,z+.007)]
    verts=[]
    for a,b,h in rings:
        for x,y in [(-a,-b),(a,-b),(a,b),(-a,b)]: verts.append((x,y,h))
    faces=[(0,3,2,1),(12,13,14,15)]
    for k in range(3):
        for j in range(4): faces.append((k*4+j,k*4+(j+1)%4,(k+1)*4+(j+1)%4,(k+1)*4+j))
    me=bpy.data.meshes.new("Bandeja estampada"); me.from_pydata(verts,[],faces); me.update()
    ob=bpy.data.objects.new("Bandeja estampada",me);R._coll(R._current["asset"]).objects.link(ob);ob["mat"]="inox"
    mod=ob.modifiers.new("Bordas arredondadas","BEVEL");mod.width=.007;mod.segments=3
    bpy.context.view_layer.objects.active=ob;ob.select_set(True);bpy.ops.object.modifier_apply(modifier=mod.name);ob.select_set(False)

def maca():
    for x in (-.78,.78):
        for y in (-.285,.285): caster(x,y)
    for y in (-.285,.285):
        box("Longarina inferior","inox",-.85,y-.025,.23,.85,y+.025,.28,r=.006)
        box("Trilho superior","inox",-.94,y-.025,.79,.94,y+.025,.835,r=.004)
        beam("Tesoura ascendente","pintura",(-.65,y,.28),(.65,y,.79),.055,.027)
        beam("Tesoura descendente","pintura",(.65,y+.034,.28),(-.65,y+.034,.79),.055,.027)
        for x,z in [(-.65,.28),(.65,.79),(.65,.28),(-.65,.79),(0,.535)]:
            bolt(x,y-.020,z)
    for x in (-.78,.78):
        beam("Travessa inferior","inox",(x,-.285,.255),(x,.285,.255),.045)
    for i in range(9):
        x=-.8+i*.2
        cyl("Rolete de transferencia","nylon",(x,0,.837),.026,.54,(0,1,0),seg=16)
        cyl("Eixo do rolete","cromado",(x,0,.837),.009,.62,(0,1,0),seg=12,r=.001)
    a,b=Vector((-.55,0,.27)),Vector((.24,0,.66));dv=b-a
    cyl("Cilindro hidraulico","escuro",a+dv*.25,.039,dv.length*.5,dv,seg=24,r=.003)
    cyl("Haste do pistao","cromado",a+dv*.72,.020,dv.length*.48,dv,seg=24,r=.001)
    tube("Pedal de elevacao","inox",[(-.57,0,.28),(-.70,-.34,.24),(-.85,-.34,.24)],.014,fillet=.04)
    box("Borracha do pedal","borracha",-.92,-.38,.238,-.79,-.30,.258,r=.008)
    tray(2.04,.71,.873,.048)
    for x in (-1.025,1.025):
        tube("Alca da bandeja","inox",[(x,-.22,.9),(x*1.055,-.22,.955),(x*1.055,.22,.955),(x,.22,.9)],.012,fillet=.04)
    box("Trava da bandeja","escuro",.88,-.34,.83,.94,-.31,.927,r=.002)
    bolt(.91,-.348,.87,r=.007)

def sink(cx,cy,z,w=.50,d=.40,depth=.21):
    # Continuous rounded rectangular bowl rings, with visible depth.
    verts=[];segments=12
    def ring(a,b,rad,h):
        pts=[]
        for sx,sy,start in [(1,1,0),(-1,1,90),(-1,-1,180),(1,-1,270)]:
            for j in range(segments):
                ang=math.radians(start+j*90/(segments-1))
                pts.append((cx+sx*(a-rad)+math.cos(ang)*rad,cy+sy*(b-rad)+math.sin(ang)*rad,h))
        return pts
    for a,b,r,h in [(w/2+.018,d/2+.018,.06,z),(w/2,d/2,.05,z+.002),
                     (w/2-.025,d/2-.025,.07,z-depth+.035),(w/2-.07,d/2-.07,.05,z-depth)]:
        verts+=ring(a,b,r,h)
    n=segments*4; faces=[]
    for k in range(3):
        for j in range(n): faces.append((k*n+j,k*n+(j+1)%n,(k+1)*n+(j+1)%n,(k+1)*n+j))
    faces.append(tuple(range(3*n,4*n)))
    me=bpy.data.meshes.new("Cuba conformada");me.from_pydata(verts,[],faces);me.update()
    ob=bpy.data.objects.new("Cuba conformada",me);R._coll(R._current["asset"]).objects.link(ob);ob["mat"]="inox"
    for f in me.polygons:f.use_smooth=True
    cyl("Valvula de escoamento","cromado",(cx,cy,z-depth+.003),.027,.006,seg=32,r=.001)
    for i in range(8):
        ang=i*math.tau/8
        cyl("Furo da valvula","escuro",(cx+.016*math.cos(ang),cy+.016*math.sin(ang),z-depth+.0065),.003,.001,seg=8,r=0)
    tube("Sifao aparente","inox",[(cx,cy,z-depth),(cx,cy,z-depth-.15),(cx+.10,cy,z-depth-.20),
                                    (cx+.18,cy,z-depth-.15),(cx+.18,cy+.20,z-depth-.15)],.023,fillet=.06)

def tap(x,y,z):
    cyl("Canopla","cromado",(x,y,z),.034,.014,seg=24)
    tube("Torneira bica alta","cromado",[(x,y,z),(x,y,z+.26),(x,y-.17,z+.26),(x,y-.19,z+.20)],.015,fillet=.07)
    cyl("Arejador","escuro",(x,y-.19,z+.198),.016,.014,seg=20)
    cyl("Cartucho do misturador","cromado",(x+.04,y,z+.09),.021,.065,(1,0,0),seg=24)
    tube("Alavanca clinica","cromado",[(x+.07,y,z+.09),(x+.07,y-.15,z+.13)],.007)

def wash():
    # 2.20 x .86 m, long axis X; installed against west wall with yaw 90.
    for x in (-.97,.97):
        for y in (-.32,.32):
            box("Pe tubular","inox",x-.022,y-.022,.04,x+.022,y+.022,.85,r=.004)
            cyl("Sapata regulavel","borracha",(x,y,.035),.034,.05,seg=20)
    for y in (-.32,.32):beam("Travessa","inox",(-.97,y,.28),(.97,y,.28),.03)
    # Full-length wash pan supports an adult; the separate counter provides the deep hand basin.
    tray(2.20,.84,.84,.055)
    cyl("Valvula de drenagem","cromado",(1.015,0,.85),.031,.008,seg=24)
    for i in range(8):
        a=i*math.tau/8
        cyl("Crivo de drenagem","escuro",(1.015+.02*math.cos(a),.02*math.sin(a),.855),.003,.001,seg=8,r=0)
    tube("Sifao da mesa","inox",[(1.015,0,.84),(1.015,0,.57),(.87,0,.50),(.75,0,.57),(.75,.42,.57)],.025,fillet=.06)
    box("Espaldar","inox",-1.10,.414,.88,1.10,.434,1.12,r=.007)
    tap(.75,.34,.90)
    # removable transverse support slats above drainage tray
    for i in range(13):
        x=-.99+i*.153
        box("Regua removivel","inox",x,-.33,.87,x+.085,.33,.895,r=.007,seg=3)
    # Front-mounted service connection: the complete hanging loop stays outside the pan.
    tube("Alimentacao da ducha","inox",[(.26,.43,.68),(.26,-.47,.68),(.26,-.47,.77)],.012,fillet=.04)
    cyl("Uniao da mangueira","cromado",(.26,-.47,.77),.020,.045,seg=24)
    pts=[(.26,-.47,.79),(.25,-.48,.66)]
    # One relaxed U, rather than a self-crossing circular coil inside the wash bed.
    for i in range(33):
        a=math.pi+math.pi*i/32
        pts.append((.43+.18*math.cos(a),-.48,.64+.29*math.sin(a)))
    pts +=[(.61,-.48,.82),(.61,-.48,1.04)]
    tube("Mangueira flexivel","borracha",pts,.011,fillet=.035,res=3)
    tube("Suporte da ducha","inox",[(.61,-.405,.86),(.61,-.48,.86),(.61,-.48,1.08)],.010,fillet=.02)
    cyl("Presilha da ducha","inox",(.61,-.48,1.07),.027,.025,seg=24)
    tube("Ducha manual","marfim",[(.61,-.48,1.04),(.61,-.48,1.19),(.61,-.40,1.22)],.020,fillet=.035)
    cyl("Crivo da ducha","escuro",(.61,-.389,1.22),.025,.008,(0,1,0),seg=24)

def bancada():
    for x in (-.89,.89):
        for y in (-.23,.23):
            cyl("Pe nivelador","escuro",(x,y,.09),.024,.17,seg=16)
    box("Fundo inferior","pintura",-.98,-.28,.17,.98,.28,.19,r=.004)
    box("Lateral esquerda","pintura",-.98,-.28,.19,-.96,.28,.865,r=.004)
    box("Lateral direita","pintura",.96,-.28,.19,.98,.28,.865,r=.004)
    box("Costas","pintura",-.96,.26,.19,.96,.28,.865,r=.004)
    # Face frame flush with the doors: rails, end stiles and the posts that sit in the door gaps.
    box("Rodape frontal","pintura",-.98,-.302,.17,.98,-.28,.20,r=.003)
    box("Travessa frontal","pintura",-.98,-.302,.822,.98,-.28,.87,r=.003)
    for x0,x1 in [(-.98,-.965),(-.335,-.325),(.315,.325),(.965,.98)]:
        box("Montante frontal","pintura",x0,-.302,.20,x1,-.28,.822,r=.002)
    for x0,x1 in [(-.965,-.335),(-.325,.315),(.325,.965)]:
        box("Porta com dobra","inox",x0,-.3,.203,x1,-.282,.819,r=.003)
        box("Vinco da porta","inox",x0+.035,-.3035,.24,x1-.035,-.300,.78,r=.001)
        x=x1-.075
        tube("Puxador","cromado",[(x,-.3,.62),(x,-.335,.62),(x,-.335,.76),(x,-.3,.76)],.006,fillet=.012)
        for h in (.30,.72):box("Dobradica","escuro",x0-.002,-.3035,h,x0+.016,-.296,h+.05,r=.001)
    # Sink aperture bounded by actual countertop strips (front edge flush with the face frame).
    for x0,x1,y0,y1 in [(-1,-.80,-.31,.30),(-.24,1,-.31,.30),(-.80,-.24,-.31,-.23),(-.80,-.24,.23,.30)]:
        box("Tampo dobrado","inox",x0,y0,.87,x1,y1,.90,r=.005)
    sink(-.52,0,.90,.56,.46,.19)
    box("Espaldar","inox",-1,.28,.90,1,.30,1.05,r=.004)
    tap(-.52,.26,.90)
    for i in range(7):
        tube("Nervura do escorredor","inox",[(.02+i*.11,-.20,.901),(.02+i*.11,.19,.901)],.002,res=1)

def auxiliar():
    for x in (-.32,.32):
        for y in (-.22,.22):
            caster(x,y,.05)
            cyl("Montante","inox",(x-.02,y,.48),.013,.64,seg=16)
    tray(.79,.57,.77,.028)
    tray(.72,.49,.24,.022)
    for x in (-.37,.37):
        tube("Alca do carro","inox",[(x,-.22,.78),(x,-.22,.92),(x,.22,.92),(x,.22,.78)],.012,fillet=.035)
    # Closed double-wall kidney basin, with a recessed floor and rolled lip.
    verts=[];N=64
    for scale,z in [(.78,.778),(1,.823),(.97,.826),(.75,.782)]:
        for i in range(N):
            a=i*math.tau/N
            verts.append((.12+scale*.135*math.cos(a),.02+scale*.075*math.sin(a)*(1+.30*math.cos(a)),z))
    faces=[tuple(reversed(range(N))),tuple(range(3*N,4*N))]
    for k in range(3):
        for j in range(N):faces.append((k*N+j,k*N+(j+1)%N,(k+1)*N+(j+1)%N,(k+1)*N+j))
    me=bpy.data.meshes.new("Cuba rim dupla parede");me.from_pydata(verts,[],faces);me.update()
    ob=bpy.data.objects.new("Cuba rim conformada",me);R._coll(R._current["asset"]).objects.link(ob);ob["mat"]="inox"
    for p in me.polygons:p.use_smooth=True
    for j in range(4):
        z=.778+j*.009
        box("Compressa dobrada","tecido",-.28,-.12,z,-.105,.025,z+.008,r=.003,seg=3)
        tube("Costura da dobra","tecido",[(-.275,-.121,z+.004),(-.11,-.121,z+.004)],.0009,res=1)
    for j in range(2):
        box("Pacote selado inferior","azul",-.26+j*.27,-.12,.248,-.04+j*.27,.12,.286,r=.006)
        for y in (-.12,.115):box("Aba de selagem","papel",-.25+j*.27,y,.249,-.05+j*.27,y+.005,.279,r=.001)

def cover():
    # Sheet lofted over the covered body: smooth anatomical bulk, flat run over the tray rim, then a straight
    # hanging drop with soft pleats; tapered ends stay above the tray and inside its handles.
    def g(x,c,sx):return math.exp(-((x-c)/sx)**2)
    def body_h(x):return .015+.11*g(x,-.76,.11)+.145*g(x,-.46,.22)+.10*g(x,-.14,.20)+.125*g(x,.12,.17)+.09*g(x,.52,.32)+.075*g(x,.90,.10)
    def body_w(x):return .06+.05*g(x,-.76,.10)+.15*g(x,-.46,.20)+.04*g(x,-.14,.2)+.11*g(x,.12,.2)+.07*g(x,.52,.34)+.02*g(x,.90,.1)
    deck,rim=.940,.377
    nx,nd,nf,na=130,12,5,28
    rows=[]
    for i in range(nx+1):
        # Ends finish inside the handles; side hems clear the rolled tray by 20 mm.
        x=-.975+1.95*i/nx
        hem=.665+.012*math.sin(9.0*x+.7)+.008*math.sin(23.0*x)
        bw,bh=body_w(x),body_h(x)
        end=max(0.,min(1.,(.975-abs(x))/.105))
        bh*=end*end*(3-2*end)
        pts=[]
        for side in (-1,1):
            seq=[]
            for k in range(nd+1):          # vertical drop: k=0 hem ... k=nd rim
                f=k/nd
                z=hem+(deck-hem)*f
                out=rim+.012*(1-f)+.010*(1-f)*math.sin(19.0*x+side*1.3+f*2.0)+.006*(1-f)*math.sin(41.0*x)
                seq.append((side*out,z))
            for k in range(1,nf+1):        # flat run from the rim in to the body
                f=k/nf
                seq.append((side*(rim+(bw-rim)*f),deck+.004*(1-f)))
            if side<0:rows_left=seq
            else:rows_right=seq
        arc=[]
        for k in range(1,na):             # over the body, left to right
            th=math.pi-math.pi*k/na
            arc.append((bw*math.cos(th),deck+bh*(math.sin(th)**.65)))
        line=rows_left+arc+list(reversed(rows_right))
        rows.append([(x,y,z) for y,z in line])
    nc=len(rows[0]);verts=[v for r in rows for v in r];faces=[]
    for i in range(nx):
        for j in range(nc-1):
            a=i*nc+j;faces.append((a,a+nc,a+nc+1,a+1))
    me=bpy.data.meshes.new("Lencol com caimento");me.from_pydata(verts,[],faces);me.update()
    ob=bpy.data.objects.new("Lencol com caimento",me);R._coll(R._current["asset"]).objects.link(ob);ob["mat"]="tecido"
    for f in me.polygons:f.use_smooth=True
    mod=ob.modifiers.new("Bainha e espessura","SOLIDIFY");mod.thickness=.002
    bpy.context.view_layer.objects.active=ob;ob.select_set(True);bpy.ops.object.modifier_apply(modifier=mod.name);ob.select_set(False)

def epi():
    # Wall unit, origin at bottom centre; installed against north wall, front -Y.
    box("Fundo do suporte","inox",-.43,-.018,0,.43,.008,.25,r=.004)
    for i in range(3):
        x=-.29+i*.29
        # Carton front is a mesh around a real oval aperture, with the interior set back.
        box("Caixa de luvas","azul",x-.125,-.106,.045,x+.125,-.025,.195,r=.004)
        verts=[];N=64
        for i in range(N):
            a=i*math.tau/N;c,s=math.cos(a),math.sin(a)
            reach=min(.125/max(abs(c),1e-8),.075/max(abs(s),1e-8))
            verts.extend([(x+c*reach,-.114,.12+s*reach),(x+.060*c,-.114,.12+.025*s)])
        faces=[(2*i,2*((i+1)%N),2*((i+1)%N)+1,2*i+1) for i in range(N)]
        me=bpy.data.meshes.new("Face com abertura oval");me.from_pydata(verts,[],faces);me.update()
        ob=bpy.data.objects.new("Face da caixa",me);R._coll(R._current["asset"]).objects.link(ob);ob["mat"]="azul"
        box("Interior da abertura","escuro",x-.065,-.108,.09,x+.065,-.107,.15,r=.003)
        for sx in (-1,1):
            box("Retentor da caixa","inox",x+sx*.13-.008,-.125,.025,x+sx*.13+.008,-.018,.20,r=.002)
        box("Berco inferior","inox",x-.133,-.125,.025,x+.133,-.018,.038,r=.002)
        tube("Retencao frontal","inox",[(x-.13,-.126,.065),(x+.13,-.126,.065)],.004,res=2)
    for x in (-.39,.39):
        for z in (.015,.23):bolt(x,-.024,z,r=.004)

def bin_model():
    # Pedal bin with bag cuff and hinge.
    R.lathe("Corpo da lixeira","marfim",[(.13,.035),(.145,.05),(.16,.48),(.16,.51),(.15,.51),(.145,.08)],seg=40,r=.002)
    cyl("Fundo","marfim",(0,0,.05),.14,.018,seg=40)
    cyl("Saco dobrado na borda","tecido",(0,0,.506),.166,.025,seg=40,r=.006)
    cyl("Tampa","marfim",(0,0,.53),.175,.022,seg=40,r=.007)
    cyl("Dobradiça","escuro",(0,.16,.52),.017,.11,(1,0,0),seg=16)
    box("Pedal","escuro",-.065,-.22,.035,.065,-.12,.055,r=.008)
    tube("Haste do pedal","inox",[(0,-.15,.05),(0,.13,.05),(0,.13,.50)],.006,fillet=.02,res=1)

def kit():
    # Origin at counter height, composed at true tabletop scale.
    for x,y,h,mat in [(-.13,.05,.22,"marfim"),(.035,.04,.17,"azul")]:
        R.lathe("Frasco de bancada",mat,[(.038,0),(.043,.015),(.043,h-.04),(.024,h-.015),(.017,h-.01),(.017,h)],seg=24,r=.002,
                matrix=Matrix.Translation((x,y,0)))
        cyl("Tampa dosadora","marfim",(x,y,h+.009),.020,.018,seg=20)
        tube("Bico dosador","marfim",[(x,y,h+.017),(x,y,h+.044),(x,y-.047,h+.044)],.006,fillet=.008,res=2)
    box("Bandeja organizadora","inox",-.21,-.055,0,.13,.12,.012,r=.008)
    box("Caixa de compressas","papel",.20,-.09,0,.40,.06,.065,r=.005)
    for i in range(5):
        box("Compressas empilhadas","tecido",.215,-.075,.065+i*.003,.38,.045,.068+i*.003,r=.001)

def instrumento():
    tray(.245,.155,.003,.017)
    # Hemostatic forceps: paired finger loops, crossed shanks, pivot and opposed narrow jaws.
    for s in (-1,1):
        pts=[]
        for i in range(33):
            a=i*math.tau/32
            pts.append((-.075+.024*math.cos(a),s*.032+.019*math.sin(a),.030))
        tube("Aro do instrumental","cromado",pts,.0028,res=2)
        tube("Haste articulada","cromado",[(-.055,s*.025,.03),(-.015,s*.007,.03),(.035,-s*.006,.032),(.099,-s*.004,.034)],.0027,fillet=.01,res=2)
        for j in range(7):
            box("Serrilha da ponta","escuro",.067+j*.004,-s*.004-.001,.035,.069+j*.004,-s*.004+.001,.036,r=.0002)
    cyl("Pivo do instrumental","inox",(.035,0,.034),.006,.007,seg=16,r=.0005)

BUILDERS={"MacaRecebimento":maca,"EstacaoLavagem":wash,"BancadaRecebimento":bancada,
          "CarroAuxiliar":auxiliar,"CorpoCoberto":cover,"SuporteEPI":epi,"LixeiraClinica":bin_model,"KitBancada":kit,"InstrumentoRecebimento":instrumento}
SIZES={k:2048 if k in ("MacaRecebimento","EstacaoLavagem","BancadaRecebimento","CorpoCoberto") else 1024 for k in BUILDERS}

def main():
    os.makedirs(OUT+"/Texturas",exist_ok=True)
    only=ONLY or list(BUILDERS)
    # This script is run in a separate factory-startup Blender process.
    for o in list(bpy.data.objects):
        if not ONLY or any(o in list(R._coll(n).objects) for n in only):
            bpy.data.objects.remove(o,do_unlink=True)
    stats={}
    for i,(name,fn) in enumerate(BUILDERS.items()):
        if name not in only:continue
        R._current["asset"]=name;fn()
        ob=R.assemble(name,MATS)
        bm=bmesh.new();bm.from_mesh(ob.data)
        bmesh.ops.dissolve_degenerate(bm,dist=.000001,edges=list(bm.edges))
        bmesh.ops.triangulate(bm,faces=list(bm.faces))
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
        bm.to_mesh(ob.data);bm.free()
        density=R.layout_uvs(ob,SIZES[name],margin_px=8)
        stats[name]={"triangles":len(ob.data.polygons),"texelsPerMetre":density,"size":list(ob.dimensions),
                     "degenerate":sum(f.area<1e-12 for f in ob.data.polygons)}
        R.export_fbx(ob,OUT+"/"+name+".fbx")
        ob.location.x=i*3
    bpy.ops.wm.save_as_mainfile(filepath=SRC+"/Recebimento.blend")
    previous=json.load(open(SRC+"/revisao/malhas.json")) if ONLY and os.path.exists(SRC+"/revisao/malhas.json") else {}
    previous.update(stats)
    with open(SRC+"/revisao/malhas.json","w") as f:json.dump(previous,f,indent=2)
    if os.environ.get("SO_MALHAS")=="1":return
    for name in stats:
        ob=bpy.data.objects[name];pos=ob.location.copy();ob.location=(0,0,0)
        others=[o for o in bpy.context.scene.objects if o!=ob and o.type=="MESH"]
        for o in others:o.hide_render=True
        B.bake.__globals__["SIZE"]=SIZES[name]
        imgs,times=B.bake(ob,[s.material for s in ob.material_slots],ao_samples=32)
        B.write_unity_maps(imgs,OUT+"/Texturas",prefix=name)
        ob.location=pos
        for o in others:o.hide_render=False
        print("BAKED",name,stats[name],times,flush=True)
    bpy.ops.wm.save_as_mainfile(filepath=SRC+"/Recebimento.blend")

if __name__=="__main__":main()
