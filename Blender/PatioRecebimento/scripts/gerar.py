"""Original service-yard models; metre scale, shared project PBR baker, no downloaded meshes."""
import bpy,bmesh,math,os,json,types,sys
from mathutils import Vector,Matrix
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'../../..'))
SRC=ROOT+'/Blender/PatioRecebimento';OUT=ROOT+'/Assets/Modelos/PatioRecebimento'
ONLY=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else None
if ONLY and os.path.exists(SRC+'/PatioRecebimento.blend'):bpy.ops.wm.open_mainfile(filepath=SRC+'/PatioRecebimento.blend')
def load(rel):
    p=ROOT+'/'+rel;ns={'__name__':'patio_module','__file__':p};exec(compile(open(p).read(),p,'exec'),ns);return types.SimpleNamespace(**ns)
R=load('Blender/Recepcao/scripts/recepcao_modelo.py');G=load('Blender/MesaAutopsia/scripts/mesa_materiais.py').G;B=load('Blender/MesaAutopsia/scripts/mesa_bake.py')
box,cyl,tube=R.box,R.cyl,R.tube

def mat(name,color,rough,metal=0,wood=False):
    m=bpy.data.materials.new('Patio - '+name);g=G(m)
    n=g.noise(g.coords(scale=(1.8,75,95) if wood else (1,1,1)),5 if wood else 9,3)
    fine=g.noise(g.coords(scale=(2,160,180) if wood else (80,80,100)),1,3)
    low=g.mrange(g.xyz()[2],.4,.02)
    scuff=g.mrange(g.noise(g.coords(scale=(5,5,90)),1,2),.60,.76)
    dirt=g.math('MULTIPLY',low,g.math('MULTIPLY',scuff,.22))
    tone=g.math('SUBTRACT',g.mrange(n,.15,.85,.87,1.05),dirt)
    base=g.color(tone,tuple(c*.32 for c in color),color)
    rr=g.mrange(n,.15,.85,rough-.09,rough+.10)
    if wood:
        cracks=g.mrange(fine,.73,.84)
        base=g.color(g.math('SUBTRACT',tone,g.math('MULTIPLY',cracks,.3)),tuple(c*.25 for c in color),color)
    g.finish(base,rr,metal,fine,bump_strength=.35,bump_dist=.0005 if wood else .00016)
    return m
MATS={
 'verde':mat('esmalte verde gasto',(.13,.19,.15),.54,.12),
 'cinza':mat('chapa cinza esverdeada',(.29,.32,.30),.50,.12),
 'galv':mat('zincado',(.43,.46,.45),.47,.85),
 'pvc':mat('PVC pluvial marfim',(.67,.68,.61),.76),
 'aco':mat('ferragem',(.10,.12,.115),.43,.8),
 'cromo':mat('pistao polido',(.64,.66,.65),.23,.94),
 'borracha':mat('borracha',(.025,.03,.027),.78),
 'nylon':mat('nylon envelhecido',(.48,.44,.34),.56),
 'madeira':mat('pinho usado',(.35,.25,.14),.85,wood=True),
 'madeira_escura':mat('pinho escurecido',(.23,.17,.10),.88,wood=True),
 'ponta':mat('topo da madeira',(.30,.22,.13),.87,wood=True),
 'bege':mat('pintura bege da porta',(.44,.385,.32),.52,.06),
 'azul':mat('pintura azul gasta da paleteira',(.115,.17,.22),.56,.1),
 'latão':mat('latao do cadeado',(.36,.25,.09),.42,.75)
}
def beam(n,m,a,b,w=.035,d=None):
    a,b=Vector(a),Vector(b);ob=box(n,m,-w/2,-(d or w)/2,0,w/2,(d or w)/2,(b-a).length,r=.002)
    ob.data.transform(Matrix.Translation(a)@Vector((0,0,1)).rotation_difference(b-a).to_matrix().to_4x4());return ob
def bolt(x,y,z,axis=(0,-1,0),r=.007):
    cyl('Arruela','aco',(x,y,z),r*1.4,.002,axis,seg=12,r=.0003)
    cyl('Cabeca sextavada','galv',Vector((x,y,z))+Vector(axis)*.003,r,.006,axis,seg=6,r=.0003)
def wheel(x,y,r=.075):
    cyl('Pneu','borracha',(x,y,r),r,.05,(0,1,0),seg=28,r=.008)
    for s in (-1,1):
        cyl('Cubo','galv',(x,y+s*.026,r),r*.63,.006,(0,1,0),seg=20,r=.001)
        beam('Garfo','galv',(x,y+s*.036,r),(x-.018,y+s*.036,2*r+.035),.022,.008)
        bolt(x,y+s*.04,r,(0,s,0),.008)
    box('Ponte','galv',x-.05,y-.045,2*r+.023,x+.015,y+.045,2*r+.036,r=.004)
    cyl('Giro do rodizio','aco',(x-.02,y,2*r+.047),.03,.025,seg=24)
    cyl('Pino','galv',(x-.02,y,2*r+.077),.013,.035,seg=16)
    box('Freio','borracha',x+.005,y-.026,r*1.6,x+.11,y+.026,r*1.6+.018,r=.006)

def contentor():
    # Compact steel refuse container within the existing 1.75 x 1.9 m reserved corner.
    for x in (-.65,.65):
        for y in (-.52,.52):wheel(x,y,.09)
    box('Fundo','verde',-.69,-.60,.25,.69,.60,.29,r=.012)
    for s in (-1,1):
        R.hull('Lateral conformada','verde',[(s*.69,-.60,.26),(s*.69,.60,.26),(s*.82,-.72,1.20),(s*.82,.72,1.20),
            (s*(.69-.005),-.60,.26),(s*(.69-.005),.60,.26),(s*(.82-.005),-.72,1.20),(s*(.82-.005),.72,1.20)],r=.004)
        # Broad sheet ribs and lifting trunnions are visible from the yard.
        cyl('Munhao de coleta','galv',(s*.84,0,.91),.045,.12,(1,0,0),seg=24,r=.004)
        box('Reforco do munhao','verde',s*.815-.007,-.15,.69,s*.815+.007,.15,1.08,r=.004)
    for y in (-.715,.715):
        sy=1 if y>0 else -1
        R.hull('Chapa frontal e traseira','verde',[(-.69,sy*.60,.26),(.69,sy*.60,.26),(-.82,sy*.72,1.20),(.82,sy*.72,1.20),
            (-.69,sy*.595,.26),(.69,sy*.595,.26),(-.82,sy*.715,1.20),(.82,sy*.715,1.20)],r=.003)
        for x in (-.56,0,.56):beam('Reforco estampado','verde',(x,sy*.62,.38),(x,sy*.719,1.11),.04,.024)
    for y in (-.735,.735):beam('Borda dobrada','galv',(-.84,y,1.21),(.84,y,1.21),.022)
    for x in (-.84,.84):beam('Borda lateral','galv',(x,-.735,1.21),(x,.735,1.21),.022)
    for s in (-1,1):
        x0,x1=(-.84,-.008) if s<0 else (.008,.84)
        box('Tampa bipartida','verde',x0,-.75,1.218,x1,.75,1.246,r=.012,seg=3)
        for y in (-.42,.42):
            cyl('Dobradiça da tampa','galv',(s*.815,y,1.237),.017,.13,(0,1,0),seg=16)
        tube('Puxador da tampa','aco',[(s*.20,-.25,1.25),(s*.20,-.25,1.31),(s*.20,.03,1.31),(s*.20,.03,1.25)],.010,fillet=.025,res=2)
    cyl('Bujão de drenagem','aco',(0,-.727,.33),.023,.012,(0,1,0),seg=16)

def pallet_one(z,angle=0):
    before=set(R._coll(R._current['asset']).objects)
    for y in (-.43,0,.43):box('Longarina inferior','madeira_escura',-.6,y-.065,z,.6,y+.065,z+.022,r=.003)
    for x in (-.48,0,.48):
        for y in (-.43,0,.43):box('Toco','madeira',x-.07,y-.065,z+.022,x+.07,y+.065,z+.102,r=.004)
    for x in (-.48,0,.48):box('Travessa de apoio','madeira_escura',x-.065,-.50,z+.102,x+.065,.50,z+.124,r=.003)
    for j in range(7):
        y=-.5+j*(1-.11)/6
        box('Regua superior','madeira_escura' if j==2 else 'madeira',-.6,y,z+.124,.6,y+.11,z+.145,r=.003)
        for x in (-.603,.603):box('Topo cortado','ponta',x-.0004,y+.002,z+.126,x+.0004,y+.108,z+.143,r=0)
        for x in (-.49,-.46,.0,.03,.46,.49):
            cyl('Prego rebaixado','aco',(x,y+.025 if x<0 else y+.082,z+.145),.0028,.0007,seg=8,r=0)
    m=Matrix.Translation((0,0,0))@Matrix.Rotation(math.radians(angle),4,'Z')
    for o in R._coll(R._current['asset']).objects:
        if o not in before:o.data.transform(m)
def paletes():pallet_one(0);pallet_one(.145,2)

def armario():
    for x in (-.51,.51):
        for y in (-.23,.23):box('Pe','aco',x-.03,y-.03,0,x+.03,y+.03,.13,r=.004)
    box('Corpo do armario','cinza',-.59,-.285,.10,.59,.285,1.18,r=.008)
    for s in (-1,1):
        x=s*.294
        box('Porta dobrada','cinza',x-.286,-.307,.14,x+.286,-.289,1.15,r=.003)
        for row in range(5):
            z=.23+row*.033
            box('Fenda veneziana','aco',x-.18,-.311,z,x+.18,-.309,z+.012,r=.002)
            beam('Aba da veneziana','cinza',(x-.18,-.317,z+.014),(x+.18,-.317,z+.014),.012,.004)
        for z in (.28,.99):
            cyl('Dobradiça','galv',(s*.566,-.313,z),.01,.09,seg=16)
        tube('Puxador','galv',[(s*.07,-.313,.67),(s*.07,-.35,.67),(s*.07,-.35,.83),(s*.07,-.313,.83)],.007,fillet=.014,res=2)
    box('Fecho','galv',-.05,-.322,.62,.06,-.312,.65,r=.002)
    box('Cadeado','latão',-.022,-.35,.571,.022,-.327,.61,r=.004)
    tube('Arco do cadeado','galv',[(-.013,-.338,.61),(-.013,-.338,.635),(.013,-.338,.635),(.013,-.338,.61)],.0035,fillet=.012,res=2)
    R.hull('Chapeu com pingadeira','galv',[(-.63,-.33,1.18),(.63,-.33,1.18),(.63,.32,1.21),(-.63,.32,1.21),
        (-.63,-.33,1.195),(.63,-.33,1.195),(.63,.32,1.225),(-.63,.32,1.225)],r=.003)

def paleteira():
    # 525 mm fork spread, 1150 mm usable fork length; painted steel forks, black polyurethane wheels.
    for s in (-1,1):
        x=s*.185
        poly=[(-.17,.13),(.68,.13),(.80,.08),(.80,.045),(-.17,.045)]
        R.extrude('Garfo estampado','azul',poly,'YZ',x-.077,x+.077,r=.009,seg=3)
        cyl('Roda de carga','borracha',(x,.64,.05),.05,.07,(1,0,0),seg=28,r=.006)
        cyl('Cubo da roda de carga','galv',(x+s*.036,.64,.05),.028,.006,(1,0,0),seg=16,r=.001)
        bolt(x+s*.040,.64,.05,(s,0,0),.007)
    box('Travessa dos garfos','azul',-.262,-.285,.075,.262,-.13,.255,r=.02,seg=3)
    box('Tampa da bomba','aco',-.075,-.34,.20,.075,-.20,.34,r=.012,seg=3)
    cyl('Pistao de elevacao','cromo',(0,-.30,.385),.02,.12,seg=24)
    for s in (-1,1):
        cyl('Roda direcional','borracha',(s*.10,-.33,.09),.09,.05,(1,0,0),seg=32,r=.008)
        cyl('Cubo da roda direcional','galv',(s*.128,-.33,.09),.045,.008,(1,0,0),seg=20)
        box('Garfo da roda','aco',s*.10-.032,-.37,.09,s*.10+.032,-.29,.27,r=.004)
        bolt(s*.134,-.33,.09,(s,0,0),.008)
    # Drawbar ends in the middle of the handle loop; no stub through it.
    tube('Timao','aco',[(0,-.31,.34),(0,-.44,.80),(0,-.50,1.02)],.017,fillet=.07,res=2)
    tube('Punho oval','borracha',[(-.15,-.50,1.02),(-.19,-.50,1.17),(.19,-.50,1.17),(.15,-.50,1.02),(-.15,-.50,1.02)],.013,fillet=.05,res=2)
    box('Cabeca do timao','aco',-.04,-.53,.99,.04,-.47,1.05,r=.008)
    tube('Alavanca de comando','galv',[(.085,-.50,1.03),(.12,-.57,1.06),(.12,-.63,1.04)],.005,fillet=.015,res=2)
    box('Manopla da alavanca','borracha',.105,-.665,1.015,.135,-.62,1.06,r=.006)
    for s in (-1,1):beam('Articulacao','aco',(s*.11,-.30,.15),(s*.11,-.17,.23),.016)

def cobertura():
    # Local X along wall, -Y projects into yard, mounting at 3.25 m keeps the existing lamp below the roof.
    for x in (-1.28,1.28):
        box('Chapa de ancoragem','galv',x-.055,-.03,2.53,x+.055,.015,3.21,r=.004)
        for z in (2.59,2.77,3.12):bolt(x,-.034,z,r=.010)
        beam('Viga em balanço','galv',(x,-.02,3.18),(x,-1.55,3.04),.045,.075)
        beam('Mao francesa','galv',(x,-.03,2.57),(x,-1.16,3.075),.035)
    for y in (-.08,-.80,-1.50):beam('Terça','galv',(-1.57,y,3.24+y*.09),(1.57,y,3.24+y*.09),.04,.045)
    # Corrugated folded metal with actual thickness, not an alpha plane.
    verts=[];faces=[];n=160
    for j in range(n+1):
        x=-1.65+3.3*j/n;wave=.011*math.cos(j*math.tau/8)
        for y in (.025,-1.65):verts.append((x,y,3.27+y*.09+wave))
    for j in range(n):faces.append((j*2,j*2+1,j*2+3,j*2+2))
    me=bpy.data.meshes.new('Telha ondulada');me.from_pydata(verts,[],faces);me.update()
    ob=bpy.data.objects.new('Telha ondulada',me);R._coll(R._current['asset']).objects.link(ob);ob['mat']='galv'
    bpy.context.view_layer.objects.active=ob;ob.select_set(True);mod=ob.modifiers.new('Espessura da chapa','SOLIDIFY');mod.thickness=.0012;bpy.ops.object.modifier_apply(modifier=mod.name);ob.select_set(False)
    box('Rufo na parede','galv',-1.66,.015,3.23,1.66,.035,3.36,r=.002)
    # Open U-shaped gutter and real downpipe, with an outlet above the new grille box.
    for y in (-1.64,-1.74):box('Face da calha','galv',-1.67,y-.002,3.05,1.67,y+.002,3.15,r=.001)
    box('Fundo da calha','galv',-1.67,-1.74,3.05,1.67,-1.64,3.054,r=.001)
    for x in (-1.67,1.67):box('Cabeceira da calha','galv',x-.002,-1.74,3.05,x+.002,-1.64,3.15,r=.001)
    # Same hollow DN100 PVC, socket joints and metal wall straps as the existing exterior.
    E=load('Blender/Entorno/Acabamentos/scripts/modelos.py')
    E.mesh.__globals__['SCENE']=bpy.context.scene
    def pvc(n,points,r=.05,inner=.003,mat='pvc'):
        path=E.rounded_path(points,.09,10)
        # Exterior helper uses Unity coordinates; adapt only its mesh construction to this atlas.
        before=len(E.PARTS)
        ob=E.tube(n,[(x,z,y) for x,y,z in path],r,'PVC_Pluvial',32,False,inner)
        for part in E.PARTS[before:]:
            for c in list(part.users_collection):c.objects.unlink(part)
            R._coll(R._current['asset']).objects.link(part);part['mat']=mat
        return ob
    x=-1.54
    pvc('Condutor DN100',[(x,-1.69,3.05),(x,-1.69,2.92),(x,-.115,2.92),(x,-.115,.24),(x,-.25,.095),(x,-.25,.055)])
    for z in (.40,1.85,2.70):pvc('Bolsa de uniao',[(x,-.115,z-.0425),(x,-.115,z+.0425)],.056)
    for z in (.55,1.75,2.65):
        pts=[(x+.053*math.cos(i*math.tau/48),-.115+.053*math.sin(i*math.tau/48),z) for i in range(49)]
        tube('Abracadeira metalica','galv',pts,.0035,res=2)
        box('Chapa de fixacao','galv',x-.045,-.014,z-.0325,x+.045,.002,z+.0325,r=.003)
        box('Espacador da abracadeira','galv',x-.011,-.065,z-.0125,x+.011,-.014,z+.0125,r=.002)
        for dx in (-.031,.031):bolt(x+dx,-.017,z,r=.004)

def porta_folha():
    # One closed service-door leaf: hinge axis on the origin, body towards +X, yard face at -Y.
    # Warm painted steel like the interior doors (archive door), black kick plates, levers on both faces.
    W,T,Z0,Z1=.958,.022,.02,2.165
    box('Folha de servico','bege',0,-T,Z0,W,T,Z1,r=.004)
    for y0,y1,s in ((-T-.0035,-T,-1),(T,T+.0035,1)):
        x0,x1,z0,z1=.10,W-.10,.60,2.04
        for bx in ((x0,y0,z0,x1,y1,z0+.03),(x0,y0,z1-.03,x1,y1,z1),(x0,y0,z0,x0+.03,y1,z1),(x1-.03,y0,z0,x1,y1,z1)):
            box('Moldura do painel','bege',bx[0],bx[1],bx[2],bx[3],bx[4],bx[5],r=.0012)
        box('Chapa de chute','borracha',.025,min(y0,y1),.055,W-.025,max(y0,y1)+(0 if s>0 else 0),.33,r=.0015)
        lx=W-.07
        box('Espelho da macaneta','aco',lx-.022,min(y0,y1)-(.002 if s<0 else 0),.90,lx+.022,max(y0,y1)+(.002 if s>0 else 0),1.12,r=.003)
        ys=s*(T+.012)
        tube('Macaneta','cromo',[(lx,s*(T+.004),1.04),(lx,s*(T+.052),1.04),(lx-.13,s*(T+.052),1.04)],.0085,fillet=.014,res=2)
        cyl('Roseta','galv',(lx,s*(T+.006),1.04),.019,.006,(0,s,0),seg=20,r=.0006)
    for z in (.26,1.09,1.93):
        cyl('Gonzo','aco',(0,0,z),.013,.115,seg=16)
        cyl('Pino do gonzo','galv',(0,0,z+.060),.009,.012,seg=12)
    box('Fecho de embutir','galv',W-.012,-.012,1.00,W+.0,.012,1.08,r=.002)

def ralo():
    box('Aro do ralo','galv',-.16,-.16,0,.16,.16,.012,r=.005)
    box('Sombra sob grelha','aco',-.143,-.143,.010,.143,.143,.011,r=.002)
    for i in range(12):box('Barra da grelha','galv',-.14+i*.024,-.14,.012,-.132+i*.024,.14,.019,r=.001)
    for x in (-.15,.15):
        for y in (-.15,.15):bolt(x,y,.014,(0,0,1),.004)

BUILDERS={'ContentorMetalico':contentor,'PaletesMadeira':paletes,'ArmarioServico':armario,'PaleteiraManual':paleteira,'CoberturaRecebimento':cobertura,'PortaServicoFolha':porta_folha,'RaloPatio':ralo}
SIZES={n:2048 if n in ('ContentorMetalico','CoberturaRecebimento','PaletesMadeira') else 1024 for n in BUILDERS}
def main():
    os.makedirs(OUT+'/Texturas',exist_ok=True)
    for o in list(bpy.data.objects):
        if not ONLY or any(o in list(R._coll(n).objects) for n in ONLY):bpy.data.objects.remove(o,do_unlink=True)
    stats={}
    for i,(name,fn) in enumerate(BUILDERS.items()):
        if ONLY and name not in ONLY:continue
        R._current['asset']=name;fn();ob=R.assemble(name,MATS)
        bm=bmesh.new();bm.from_mesh(ob.data);bmesh.ops.dissolve_degenerate(bm,dist=.000001,edges=list(bm.edges));bmesh.ops.triangulate(bm,faces=list(bm.faces));bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(ob.data);bm.free()
        density=R.layout_uvs(ob,SIZES[name],margin_px=8)
        stats[name]={'triangles':len(ob.data.polygons),'degenerate':sum(p.area<1e-12 for p in ob.data.polygons),'texelsPerMetre':density,'size':list(ob.dimensions)}
        R.export_fbx(ob,OUT+'/'+name+'.fbx');ob.location.x=i*4
    bpy.ops.wm.save_as_mainfile(filepath=SRC+'/PatioRecebimento.blend')
    previous=json.load(open(SRC+'/revisao/malhas.json')) if ONLY else {}
    previous.update(stats)
    with open(SRC+'/revisao/malhas.json','w') as f:json.dump(previous,f,indent=2)
    for name in stats:
        ob=bpy.data.objects[name];pos=ob.location.copy();ob.location=(0,0,0)
        others=[o for o in bpy.context.scene.objects if o!=ob and o.type=='MESH']
        for o in others:o.hide_render=True
        B.bake.__globals__['SIZE']=SIZES[name]
        imgs,times=B.bake(ob,[s.material for s in ob.material_slots],ao_samples=32,ground=name not in ('CoberturaRecebimento','PortaServicoFolha'))
        B.write_unity_maps(imgs,OUT+'/Texturas',prefix=name)
        ob.location=pos
        for o in others:o.hide_render=False
        print('BAKED',name,stats[name],times,flush=True)
    bpy.ops.wm.save_as_mainfile(filepath=SRC+'/PatioRecebimento.blend')
if __name__=='__main__':main()
