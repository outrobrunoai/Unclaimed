"""Exterior fittings, authored in metres through Blender. Unity-local +Z faces away from wall.
Run main() in Blender; keeps other scenes intact. All textures are original RGB, all foliage is solid geometry.
"""
import bpy, math, random, json
from pathlib import Path
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[4]
SOURCE=ROOT/'Blender/Entorno/Acabamentos'
EXPORT=ROOT/'Assets/Modelos/Entorno/Acabamentos'
COLORS={
 'Chapa_Clara':((.70,.72,.68),.0,.27), 'PVC_Pluvial':((.67,.68,.61),0,.24),
 'PVC_Agua':((.24,.115,.057),0,.22), 'Aco_Galvanizado':((.32,.35,.34),.65,.32),
 'Metal_Escuro':((.045,.055,.052),.45,.22), 'Borracha':((.018,.024,.022),0,.10),
 'Latao':((.40,.28,.10),.75,.32), 'Cobre':((.42,.20,.095),.7,.34),
 'Mangueira':((.055,.16,.09),0,.22), 'Botao':((.25,.27,.25),.45,.29),
 'Folha_Espada':((1,1,1),0,.13), 'Folha_Filodendro':((1,1,1),0,.12),
 'Caule':((.12,.22,.085),0,.13), 'Papel_Tecnico':((.65,.67,.61),0,.18),
 'Face_Placa':((1,1,1),0,.2), 'Face_Interfone':((1,1,1),0,.22),
 'Chapa_Letreiro':((.68,.66,.61),0,.16),
 'PVC_Fundo':((.86,.85,.81),0,.30), 'Acrilico_Borda':((.62,.70,.66),0,.88), 'Inox':((.62,.62,.60),.9,.62),
}
PARTS=[];META={};MATS={};SCENE=None

def B(p):return Vector((p[0],p[2],p[1]))
def material(name):
    if name in MATS:return MATS[name]
    c,met,rough=COLORS.get(name,((1,1,1),0,.2));m=bpy.data.materials.get(name) or bpy.data.materials.new(name);m.use_nodes=True
    bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    bs.inputs['Base Color'].default_value=(*c,1);bs.inputs['Metallic'].default_value=met;bs.inputs['Roughness'].default_value=1-rough
    m.diffuse_color=(*c,1)
    tex={'Folha_Espada':'Espada_Folha','Folha_Filodendro':'Filodendro_Folha','Face_Interfone':'Interfone_Identificacao'}.get(name)
    if name.startswith('Print_'):tex=name[6:]
    if tex:
        node=m.node_tree.nodes.new('ShaderNodeTexImage');node.image=bpy.data.images.load(str(EXPORT/'Texturas'/(tex+'.png')),check_existing=True)
        node.extension='EXTEND';m.node_tree.links.new(node.outputs['Color'],bs.inputs['Base Color'])
    MATS[name]=m;return m

def mesh(name,verts,faces,mat,uvs=None,smooth=False):
    me=bpy.data.meshes.new(name);me.from_pydata([B(v) for v in verts],[],[tuple(reversed(f)) for f in faces]);me.update()
    # B changes handedness; reverse face loops so exported normals stay outside.
    obj=bpy.data.objects.new(name,me);SCENE.collection.objects.link(obj);me.materials.append(material(mat))
    layer=me.uv_layers.new(name='UVMap')
    for p in me.polygons:
        p.use_smooth=smooth
        for j,li in enumerate(p.loop_indices):
            vi=me.loops[li].vertex_index
            layer.data[li].uv=uvs[vi] if uvs else ((j==1 or j==2)*1.,(j>=2)*1.)
    PARTS.append(obj);return obj

def box(name,c,s,mat,bevel=0):
    x,y,z=c;a,b,d=[v/2 for v in s]
    v=[(x-a,y-b,z-d),(x+a,y-b,z-d),(x+a,y+b,z-d),(x-a,y+b,z-d),
       (x-a,y-b,z+d),(x+a,y-b,z+d),(x+a,y+b,z+d),(x-a,y+b,z+d)]
    f=[(3,2,1,0),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]
    o=mesh(name,v,f,mat)
    if bevel:
        bpy.context.view_layer.objects.active=o;o.select_set(True)
        mod=o.modifiers.new('Real rolled edge','BEVEL');mod.width=bevel;mod.segments=3
        bpy.ops.object.modifier_apply(modifier=mod.name)
        mod=o.modifiers.new('Sheet corner normals','WEIGHTED_NORMAL');mod.keep_sharp=True
        bpy.ops.object.modifier_apply(modifier=mod.name);o.select_set(False)
    return o

def tube(name,points,r,mat,sides=24,cap=True,inner=0):
    path=[Vector(p) for p in points];vs=[];uv=[];fs=[]
    length=0;norm=None
    for i,p in enumerate(path):
        t=(path[min(i+1,len(path)-1)]-path[max(0,i-1)]).normalized()
        if norm is None:norm=t.cross(Vector((1,0,0)) if abs(t.x)<.9 else Vector((0,1,0))).normalized()
        norm=(norm-t*norm.dot(t)).normalized();side=t.cross(norm)
        if i:length+=(p-path[i-1]).length
        for k in range(sides+1):
            a=2*math.pi*k/sides;vs.append(tuple(p+r*(norm*math.cos(a)+side*math.sin(a))));uv.append((k/sides,length))
    stride=sides+1
    for i in range(len(path)-1):
        for k in range(sides):
            a=i*stride+k;fs.append((a,a+1,a+stride+1,a+stride))
    if cap:fs.extend([tuple(reversed(range(sides))),tuple((len(path)-1)*stride+k for k in range(sides))])
    obj=mesh(name,vs,fs,mat,uv,True)
    if cap:
        for p in list(obj.data.polygons)[-2:]:p.use_smooth=False
    if inner:
        # A real wall/lip at each visible mouth instead of a capped cylinder.
        for end in [0,len(path)-1]:
            outer=vs[end*stride:end*stride+sides];center=path[end]
            inside=[tuple(center+(Vector(q)-center)*(r-inner)/r) for q in outer]
            mesh(name+'_lip',outer+inside,[(k,(k+1)%sides,(k+1)%sides+sides,k+sides) for k in range(sides)],mat,smooth=True)
        inside=tube(name+'_bore',points,r-inner,mat,sides,False)
        import bmesh
        bm=bmesh.new();bm.from_mesh(inside.data);bmesh.ops.reverse_faces(bm,faces=list(bm.faces));bm.to_mesh(inside.data);bm.free()
    return obj

def ring(name,center,r,wire,mat,plane='xy',segments=48):
    c=Vector(center);pts=[]
    for i in range(segments+1):
        a=2*math.pi*i/segments
        v=(math.cos(a)*r,math.sin(a)*r,0) if plane=='xy' else ((math.cos(a)*r,0,math.sin(a)*r) if plane=='xz' else (0,math.cos(a)*r,math.sin(a)*r))
        pts.append(tuple(c+Vector(v)))
    return tube(name,pts,wire,mat,8,False)

def screw(c,axis=(0,0,1),r=.004):
    p=Vector(c);a=Vector(axis)
    tube('Parafuso', [tuple(p-a*.001),tuple(p+a*.002)],r,'Aco_Galvanizado',12)
    # One dark slot; enough at player distance, no microscopic threads.
    if abs(a.z)>.5:box('Fenda',tuple(p+a*.0024),(r*1.4,.0007,.0005),'Metal_Escuro')

def beam(name,a,b,width,depth,mat):
    av,bv=Vector(a),Vector(b);o=box(name,(0,0,0),(width,(bv-av).length,depth),mat)
    o.location=B((av+bv)*.5);o.rotation_euler=(B(bv)-B(av)).to_track_quat('Z','Y').to_euler();return o

def rounded_path(points,radius=.06,steps=8):
    p=[Vector(q) for q in points];out=[p[0]]
    for i in range(1,len(p)-1):
        a,b,c=p[i-1],p[i],p[i+1];d=min(radius,(b-a).length*.45,(c-b).length*.45)
        start=b+(a-b).normalized()*d;end=b+(c-b).normalized()*d
        out.append(start)
        for k in range(1,steps+1):
            t=k/steps;out.append((1-t)**2*start+2*(1-t)*t*b+t*t*end)
    out.append(p[-1]);return [tuple(q) for q in out]

def collar(name,p,axis,r,h,mat='PVC_Pluvial'):
    p=Vector(p);v=Vector(axis)*h/2;tube(name,[tuple(p-v),tuple(p+v)],r,mat,32)

def downpipe():
    # Origin on wall at ground. 100 mm pipe, standoff, continuous elbows and an open shoe over the existing drain.
    # Roof slab top is 3.40 m: the bore invert meets it, rather than asking rain to rise to 3.62 m.
    path=rounded_path([(0,3.45,-.26),(0,3.45,.04),(0,3.25,.115),(0,.24,.115),(0,.095,.32),(0,.035,.32)],.09,10)
    tube('Condutor_DN100',path,.05,'PVC_Pluvial',32,False,.003)
    for y in [.55,1.75,2.95]:
        ring('Abracadeira',(0,y,.115),.053,.007,'Aco_Galvanizado','xz',32)
        box('Chapa_de_fixacao',(0,y,.009),(.09,.065,.016),'Aco_Galvanizado',.006)
        box('Espacador',(0,y,.037),(.022,.025,.045),'Aco_Galvanizado')
        for dx in [-.031,.031]:screw((dx,y,.019))
    for y in [.4,1.85,3.12]:collar('Bolsa', (0,y,.115),(0,1,0),.056,.085)
    collar('Bocal_na_platibanda',(0,3.45,-.005),(0,0,1),.061,.075)
    box('Arremate_de_passagem',(0,3.45,-.012),(.18,.18,.022),'Chapa_Clara',.018)

def water():
    # Origin wall, y=0. Source enters wall, DN25 riser -> union -> quarter-turn valve -> bib tap -> hose.
    tube('Alimentacao_DN25',rounded_path([(0,.18,-.02),(0,.18,.055),(0,.6,.055),(0,.6,.11)],.035),.0125,'PVC_Agua',24)
    for y in [.27,.48]:
        ring('Presilha',(0,y,.055),.016,.003,'Aco_Galvanizado','xz',24)
        box('Apoio_presilha',(0,y,.016),(.05,.03,.032),'Aco_Galvanizado',.003)
    collar('Uniao',(0,.55,.055),(0,1,0),.022,.045,'PVC_Agua')
    collar('Registro_esfera',(0,.6,.12),(0,0,1),.021,.07,'Latao')
    box('Alavanca_registro',(0,.632,.122),(.07,.009,.016),'Metal_Escuro',.004)
    tube('Bica',rounded_path([(0,.6,.145),(0,.6,.2),(0,.555,.22)],.027),.012,'Latao',24)
    collar('Engate_rapido',(0,.544,.22),(0,1,0),.018,.035,'Mangueira')
    # Wall reel, horizontal axis; open centre and actual coiled hose.
    box('Suporte_enrolador',(0,.79,.02),(.23,.25,.04),'Metal_Escuro',.014)
    for y in [.72,.87]:
        for x in [-.075,.075]:screw((x,y,.043))
    tube('Eixo_enrolador',[(0,.93,.03),(0,.93,.18)],.026,'Metal_Escuro',24)
    for z in [.09,.23]:
        ring('Flange_enrolador',(0,.93,z),.17,.012,'Metal_Escuro')
        for a in [0,2.094,4.189]:
            beam('Raio_enrolador',(0,.93,z),(.16*math.cos(a),.93+.16*math.sin(a),z),.018,.012,'Metal_Escuro')
    pts=[]
    for i in range(12*40+1):
        t=i/40;ang=t*2*math.pi;pts.append((.125*math.cos(ang),.93+.125*math.sin(ang),.105+.105*t/12))
    tube('Mangueira_enrolada',pts,.009,'Mangueira',10)
    tube('Mangueira_ligada',rounded_path([(0,.525,.22),(.08,.48,.23),(.2,.58,.22),(.16,.86,.20),(.125,.93,.21)],.05),.009,'Mangueira',16)
    tube('Ponta_mangueira',rounded_path([(-.11,.93,.11),(-.20,.7,.14),(-.22,.14,.18),(-.12,.07,.25)],.1),.009,'Mangueira',16)
    collar('Esguicho',(-.115,.07,.25),(1,0,0),.014,.055,'Latao')

def intercom():
    box('Base_de_fixacao',(0,0,.012),(.118,.196,.024),'Metal_Escuro',.008)
    box('Caixa_ABS',(0,0,.038),(.115,.19,.056),'Chapa_Clara',.012)
    box('Painel_grelha',(0,.035,.068),(.085,.064,.006),'Metal_Escuro',.008)
    for j in range(7):box('Lamina_grelha',(0,.011+j*.008,.073),(.082,.003,.006),'Chapa_Clara',.001)
    box('Botao_chamada',(0,-.04,.077),(.039,.029,.012),'Botao',.007)
    # Identification strip between the call button and the lower screw (clear of both).
    box('Identificacao',(0,-.068,.067),(.086,.02,.001),'Face_Interfone')
    for y in [-.085,.085]:screw((0,y,.069))
    # Cable enters through a recessed back grommet rather than dangling.
    collar('Prensa_cabo',(0,-.07,.009),(0,0,1),.007,.02,'Borracha')
    box('Pingadeira',(0,.10,.037),(.13,.009,.083),'Chapa_Clara',.004)

def condenser():
    # 800 x 550 x 300 mm casing; rear ventilation 140 mm from wall. No solid box behind the fan.
    for y in [.065,.595]:box('Tampa_chapa',(0,y,.29),(.80,.014,.32),'Chapa_Clara',.006)
    for x in [-.393,.393]:box('Lateral_chapa',(x,.33,.29),(.014,.52,.32),'Chapa_Clara',.006)
    box('Painel_servico',(.285,.33,.453),(.23,.528,.018),'Chapa_Clara',.006)
    # Annulus joining the circular opening to a rectangular left panel.
    cx,cy=-.105,.33;v=[];uv=[]
    angles=sorted(set([2*math.pi*i/64 for i in range(64)]+[math.atan2(y-cy,x-cx)%(2*math.pi) for x in [-.4,.17] for y in [.07,.59]]))
    N=len(angles)
    for a in angles:
        co,si=math.cos(a),math.sin(a)
        reach=min((.17-cx)/co if co>1e-8 else ((-.4-cx)/co if co< -1e-8 else 1e6),(.59-cy)/si if si>1e-8 else ((.07-cy)/si if si< -1e-8 else 1e6))
        v.extend([(cx+co*.215,cy+si*.215,.453),(cx+co*reach,cy+si*reach,.453)])
        uv.extend([(.5+co*.22,.5+si*.22),(.5+co*reach,.5+si*reach)])
    mesh('Facade_avec_ouverture',v,[(2*i,2*i+1,2*((i+1)%N)+1,2*((i+1)%N)) for i in range(N)],'Chapa_Clara',uv)
    ring('Bord_ouverture',(cx,cy,.453),.216,.007,'Chapa_Clara')
    # Black heat exchanger behind fan, with physical rear and side fins.
    box('Batterie_sombre',(0,.33,.151),(.76,.49,.019),'Metal_Escuro')
    for i in range(38):box('Ailette_arriere',(-.37+i*.02,.33,.138),(.003,.48,.022),'Aco_Galvanizado')
    for i in range(17):
        yy=.1+i*.028
        box('Ouie_laterale',(-.403,yy,.29),(.006,.011,.22),'Metal_Escuro',.002)
        box('Ouie_service',(.404,yy,.25),(.006,.009,.12),'Metal_Escuro',.002)
    tube('Moteur',[(cx,cy,.19),(cx,cy,.34)],.055,'Metal_Escuro',32)
    tube('Moyeu',[(cx,cy,.345),(cx,cy,.40)],.047,'Metal_Escuro',32)
    for k in range(5):
        a=k*2*math.pi/5;pts=[]
        for radius,delta,z in [(.04,-.4,.355),(.14,-.20,.36),(.198,.10,.37),(.187,.46,.39),(.10,.48,.395),(.04,.32,.38)]:
            pts.append((cx+radius*math.cos(a+delta),cy+radius*math.sin(a+delta),z))
        obj=mesh('Pale_ventilateur',pts,[tuple(range(6))],'Metal_Escuro');mod=obj.modifiers.new('Blade thickness','SOLIDIFY');mod.thickness=.003
        bpy.context.view_layer.objects.active=obj;bpy.ops.object.modifier_apply(modifier=mod.name)
    # Concentric steel safety grille and eight supports in front of fan.
    for radius in [.055,.085,.115,.145,.175,.205]:ring('Grelha_frontal',(cx,cy,.476),radius,.0028,'Chapa_Clara',segments=64)
    for k in range(8):
        a=k*math.pi/4;tube('Raio_grelha',[(cx,cy,.479),(cx+.208*math.cos(a),cy+.208*math.sin(a),.479)],.0027,'Chapa_Clara',8)
    tube('Disco_grelha',[(cx,cy,.478),(cx,cy,.487)],.027,'Chapa_Clara',32)
    for x in [-.365,.365]:
        for y in [.10,.56]:screw((x,y,.466))
    box('Tampa_conexoes',(.413,.19,.23),(.025,.20,.18),'Chapa_Clara',.009)
    for yy,r in [(.155,.018),(.22,.014)]:
        tube('Valvula_servico',[(.42,yy,.26),(.465,yy,.26)],r,'Latao',16)
        collar('Porca_flare',(.462,yy,.26),(1,0,0),r*1.15,.018,'Latao')
        tube('Isolamento_frigorigeno',rounded_path([(.468,yy,.26),(.51,yy,.26),(.51,yy,.075),(.37,yy,.035),(.37,yy,-.028)],.05),r*.95,'Borracha',20)
    tube('Conduite_eletrico',rounded_path([(.42,.30,.22),(.48,.30,.15),(.46,.37,.04),(.36,.37,-.03)],.04),.008,'PVC_Pluvial',16)
    box('Arremate_parede',(.37,.25,.005),(.16,.30,.012),'Chapa_Clara',.018)
    for x in [-.28,.28]:
        box('Cantoneira_vertical',(x,-.105,.018),(.055,.37,.036),'Aco_Galvanizado',.004)
        box('Cantoneira_horizontal',(x,.005,.26),(.055,.035,.51),'Aco_Galvanizado',.004)
        beam('Escora',(x,-.27,.036),(x,-.01,.46),.025,.025,'Aco_Galvanizado')
        for y in [-.23,.03]:screw((x,y,.04),r=.007)
        for z in [.18,.39]:
            box('Pe_chapa',(x,.043,z),(.085,.012,.06),'Chapa_Clara',.004)
            tube('Apoio_borracha',[(x,.025,z),(x,.043,z)],.019,'Borracha',20)
    # Compact label and restrained seam wear are geometry/material, no invented brand/model text.
    box('Etiqueta_tecnica',(.28,.48,.464),(.12,.05,.001),'Papel_Tecnico')
    for k in range(4):box('Linha_etiqueta',(.28,.465+k*.008,.465),(.10,.002,.0005),'Metal_Escuro')

def chain_link(name,center,plane='xy',length=.034,width=.018):
    # Oval forged link. Alternating xy/yz links genuinely interlace.
    pts=[];cx,cy,cz=center
    for i in range(33):
        a=2*math.pi*i/32;u=math.cos(a)*length/2;v=math.sin(a)*width/2
        pts.append((cx+u,cy+v,cz) if plane=='xy' else (cx+u,cy,cz+v))
    tube(name,pts,.0028,'Aco_Galvanizado',8,False)

def padlock(single=False):
    # Origin matches old lock point. Two eye straps span the seam without touching gate bars.
    for x in [-.035,.035]:
        box('Chapa_do_fecho',(x,0,.008),(.055,.032,.015),'Metal_Escuro',.003)
        for dx in [-.017,.017]:screw((x+dx,0,.017),r=.003)
    # Oval eyelets in YZ planes. Chain loops behind the stiles and meets in front.
    for x in [-.047,.047]:
        box('Orelha_soldada',(x,0,.019),(.008,.024,.012),'Metal_Escuro',.002)
        ring('Olhal',(x,0,.036),.011,.004,'Aco_Galvanizado','yz',24)
    for k in range(9):
        t=k/8;x=-.06+.12*t;z=.037+.026*math.sin(t*math.pi);y=-.018*math.sin(t*math.pi)
        chain_link('Elo_corrente',(x,y,z),'xy' if k%2==0 else 'xz',.026,.018)
    # Two end links sit on the rounded shackle crown, with the ring wire below the crown.
    for x in [-.009,.009]:chain_link('Elo_no_arco',(x,-.013,.066),'xz',.027,.016)
    body=(0,-.064,.066)
    box('Corpo_latao',body,(.05,.038,.023),'Latao',.005)
    arc=[(-.015,-.044,.066),(-.015,-.02,.066)]
    for i in range(17):
        a=math.pi-math.pi*i/16;arc.append((.015*math.cos(a),-.02+.015*math.sin(a),.066))
    arc.append((.015,-.044,.066));tube('Arco_aco',arc,.0038,'Aco_Galvanizado',20)
    # Keyway on underside and a visible front cylinder, never number wheels.
    tube('Cilindro_chave',[(0,-.064,.078),(0,-.064,.079)],.007,'Latao',24)
    box('Entrada_chave',(0,-.064,.0796),(.002,.009,.0008),'Metal_Escuro',.0007)
    box('Entrada_inferior',(0,-.0835,.066),(.009,.0008,.002),'Metal_Escuro')

def sign(name,width,height,thick):
    if name=='Placa_Fachada':
        main_sign();return
    if name=='Placa_Atendimento':
        paper_holder();return
    # Return-folded aluminium/ACM cassette, face shares the edge plane (no stacked printed quad).
    box('Corpo_placa',(0,0,.023+thick/2),(width,height,thick),'Aco_Galvanizado',min(.009,thick*.25))
    # Single front surface on the physical front; remove the backing's +Z face first to avoid stacked planes.
    obj=PARTS[-1]
    # The printed face sits .35 mm out and never overlaps a second front polygon: backing is an open cassette.
    import bmesh
    bm=bmesh.new();bm.from_mesh(obj.data)
    f=[f for f in bm.faces if f.normal.dot(B((0,0,1)))>.95]
    bmesh.ops.delete(bm,geom=f,context='FACES');bm.to_mesh(obj.data);bm.free()
    x=width/2;y=height/2;z=.023+thick+.0001
    mesh('Impressao',[(-x,-y,z),(x,-y,z),(x,y,z),(-x,y,z)],[(0,1,2,3)],'Print_'+name,[(1,0),(0,0),(0,1),(1,1)])
    for xx in [-width*.43,width*.43]:
        for yy in [-height*.36,height*.36]:
            tube('Espacador_parede',[(xx,yy,.001),(xx,yy,.024)],.008,'Aco_Galvanizado',16)
            screw((xx,yy,z+.001),r=.0045)

def paper_holder():
    # Staff notice: A3 sheet in a wall pocket. 2 mm white PVC back on the wall, 3 mm clear acrylic front held 4 mm
    # off it by the screw spacers, open at the top. The front face carries Print_Placa_Atendimento (back, sheet and
    # the acrylic's own gloss); its edges are a separate glossy material, so the plate reads as transparent stock.
    width,height=.44,.32
    box('Fundo_PVC',(0,0,.001),(width,height,.002),'PVC_Fundo',.0006)
    obj=box('Frente_acrilico',(0,0,.006+.0015),(width,height,.003),'Acrilico_Borda',.0008)
    import bmesh
    bm=bmesh.new();bm.from_mesh(obj.data)
    bmesh.ops.delete(bm,geom=[f for f in bm.faces if f.normal.dot(B((0,0,1)))>.95],context='FACES');bm.to_mesh(obj.data);bm.free()
    x,y,z=width/2,height/2,.009+.0001
    mesh('Folha_no_acrilico',[(-x,-y,z),(x,-y,z),(x,y,z),(-x,y,z)],[(0,1,2,3)],'Print_Placa_Atendimento',[(1,0),(0,0),(0,1),(1,1)])
    for xx in [-.208,.208]:
        for yy in [-.148,.148]:
            tube('Espacador_acrilico',[(xx,yy,.002),(xx,yy,.006)],.0045,'Inox',16)
            tube('Tampa_parafuso',[(xx,yy,z),(xx,yy,z+.004)],.0055,'Inox',20)

def main_sign():
    # Folded painted sheet: 8 mm return, 8 mm short standoffs. Not a light box.
    width,height,depth,gap=2.4,.52,.008,.008
    obj=box('Chapa_dobrada_fosca',(0,0,gap+depth/2),(width,height,depth),'Chapa_Letreiro',.0012)
    import bmesh
    bm=bmesh.new();bm.from_mesh(obj.data)
    front=[f for f in bm.faces if f.normal.dot(B((0,0,1)))>.95]
    bmesh.ops.delete(bm,geom=front,context='FACES');bm.to_mesh(obj.data);bm.free()
    # Only one opaque front surface; body front was removed to avoid z-fighting.
    x,y=width/2,height/2;z=gap+depth+.0001
    mesh('Pintura_e_tipografia',[(-x,-y,z),(x,-y,z),(x,y,z),(-x,y,z)],[(0,1,2,3)],'Print_Placa_Fachada',[(1,0),(0,0),(0,1),(1,1)])
    for xx in [-1.162,1.162]:
        for yy in [-.222,.222]:
            tube('Afastador_curto',[(xx,yy,.001),(xx,yy,gap+.001)],.005,'Aco_Galvanizado',16)
            screw((xx,yy,z+.0005),r=.0035)

def leaf(name,base,angle,h,width,lean,mat,seed=0,broad=False):
    rng=random.Random(seed);base=Vector(base);direction=Vector((math.cos(angle),0,math.sin(angle)));cross=Vector((-math.sin(angle),0,math.cos(angle)))
    vs=[];uv=[];rows=15 if broad else 18;cols=6
    twist=rng.uniform(-.15,.15)
    for side in [0,1]:
        for i in range(rows+1):
            t=i/rows
            center=base+Vector((0,h*(t-.24*t*t if broad else t),0))+direction*(lean*(t*t if not broad else t))
            if broad:profile=max(.012,math.sin(math.pi*t)**.68)*(1-.12*math.cos(t*math.pi*18))
            else:profile=max(.009,(.55+.45*math.sin(math.pi*t*.8))*(1-t**5))
            for j in range(cols+1):
                u=j/cols;q=(u-.5)*2
                ridge=(1-abs(q))*(.012 if broad else .008)*math.sin(math.pi*t)
                curl=(abs(q)**2)*(.018 if broad else .006)*math.sin(math.pi*t)
                p=center+cross*(q*width*.5*profile)+Vector((0,-curl,0))+direction*(ridge+twist*q*t*.05+(.0015 if side==0 else -.0015))
                vs.append(tuple(p));uv.append((u,1-t))
    stride=cols+1;layer=(rows+1)*stride;fs=[]
    for i in range(rows):
        for j in range(cols):
            a=i*stride+j;fs.append((a,a+1,a+stride+1,a+stride));a+=layer;fs.append((a+stride,a+stride+1,a+1,a))
    for i in range(rows):
        a=i*stride;fs.append((a,a+stride,a+stride+layer,a+layer));a+=cols;fs.append((a+layer,a+stride+layer,a+stride,a))
    for j in range(cols):
        fs.append((j+layer,j+layer+1,j+1,j));a=rows*stride+j;fs.append((a,a+1,a+layer+1,a+layer))
    mesh(name,vs,[tuple(reversed(f)) for f in fs],mat,uv,True)

def sword(seed):
    rng=random.Random(seed)
    for i in range(15):
        a=i*2.399+rng.uniform(-.25,.25);radius=rng.uniform(.015,.065)
        h=rng.uniform(.48,.92);lean=rng.uniform(.055,.17)
        leaf('Folha_carnuda', (math.cos(a)*radius,-.012,math.sin(a)*radius),a,h,rng.uniform(.06,.095),lean,'Folha_Espada',seed+i)

def philodendron(seed):
    rng=random.Random(seed)
    for i in range(13):
        a=i*2.399+rng.uniform(-.3,.3);h=rng.uniform(.26,.48);radius=rng.uniform(.10,.22)
        end=(math.cos(a)*radius,h,math.sin(a)*radius)
        tube('Peciolo',rounded_path([(0,-.02,0),(end[0]*.5,h*.65,end[2]*.5),end],.05),.005,'Caule',10)
        leaf('Folha_larga',end,a,rng.uniform(.18,.29),rng.uniform(.17,.23),rng.uniform(.15,.23),'Folha_Filodendro',seed+i,True)

def finish(name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in PARTS:o.select_set(True)
    bpy.context.view_layer.objects.active=PARTS[0];bpy.ops.object.join();o=bpy.context.object;o.name=name
    # Tiny bevel clamps on screw slots may collapse faces. Remove those before export.
    import bmesh
    bm=bmesh.new();bm.from_mesh(o.data)
    degenerate=[f for f in bm.faces if f.calc_area()<1e-12]
    if degenerate:bmesh.ops.delete(bm,geom=degenerate,context='FACES')
    loose=[v for v in bm.verts if not v.link_faces]
    if loose:bmesh.ops.delete(bm,geom=loose,context='VERTS')
    bm.to_mesh(o.data);bm.free()
    # Weld coincident tube seams; maintain intentional split normals on boxes and UV seams.
    o.data.update();o.location=(0,0,0)
    bpy.ops.export_scene.fbx(filepath=str(EXPORT/(name+'.fbx')),use_selection=True,object_types={'MESH'},
        axis_forward='-Z',axis_up='Y',bake_space_transform=True,apply_scale_options='FBX_SCALE_UNITS',
        mesh_smooth_type='OFF',use_custom_props=False,bake_anim=False,add_leaf_bones=False,path_mode='STRIP')
    META[name]={'triangles':sum(len(p.vertices)-2 for p in o.data.polygons),'materials':[m.name for m in o.data.materials],
                'size_blender':list(o.dimensions)}
    # Display only: originals stay at origin in their own collections for clean repeatable exports.
    col=bpy.data.collections.new(name);SCENE.collection.children.link(col)
    for c in list(o.users_collection):c.objects.unlink(o)
    col.objects.link(o);PARTS.clear();return o

def main():
    global SCENE
    SOURCE.mkdir(parents=True,exist_ok=True);EXPORT.mkdir(parents=True,exist_ok=True)
    old=bpy.context.scene
    existing=bpy.data.scenes.get('Acabamentos Exterior')
    if existing:
        for o in list(existing.objects):bpy.data.objects.remove(o,do_unlink=True)
        SCENE=existing
    else:SCENE=bpy.data.scenes.new('Acabamentos Exterior')
    bpy.context.window.scene=SCENE;SCENE.unit_settings.system='METRIC';SCENE.unit_settings.scale_length=1
    for name,fn in [('Condutor_Pluvial_DN100',downpipe),('Agua_Jardim_DN25',water),('Interfone_Externo',intercom),
                    ('Condensadora_Split',condenser),('Cadeado_Chave_Corrente',padlock)]:
        fn();finish(name)
    for name,w,h,d in [('Placa_Fachada',2.4,.52,.025),('Placa_Atendimento',.44,.32,.009),('Placa_Numero',.24,.12,.005),
                        ('Placa_NaoEstacione',.5,.25,.004),('Placa_Guarita',.5,.19,.006)]:
        sign(name,w,h,d);finish(name)
    for name,fn,seed in [('Espada_Ornamental_A',sword,801),('Espada_Ornamental_B',sword,802),('Filodendro_Ornamental',philodendron,803)]:
        fn(seed);finish(name)
    # Contact-sheet layout in the .blend only: FBX files were exported in their local frames above.
    for i,o in enumerate(SCENE.objects):o.location=(i%5*1.3,i//5*1.6,0)
    SCENE.world=bpy.data.worlds.new('Acabamentos studio');SCENE.world.color=(.25,.25,.25)
    for name in ['modelos.py','texturas.py']:
        block=bpy.data.texts.get('Acabamentos_'+name) or bpy.data.texts.new('Acabamentos_'+name)
        block.clear();block.write((SOURCE/'scripts'/name).read_text())
    (EXPORT/'assets.json').write_text(json.dumps({'assets':META,'materials':COLORS},indent=2))
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Acabamentos_Exterior.blend'))
    print(json.dumps(META,indent=2))

if __name__=='__main__':main()
