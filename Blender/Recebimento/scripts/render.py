"""Neutral studio checks of the actual source meshes (not a concept image)."""
import bpy, os, math, sys
from mathutils import Vector
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),".."))
bpy.ops.wm.open_mainfile(filepath=ROOT+"/Recebimento.blend")
s=bpy.context.scene
assets={o.name:o for o in s.objects if o.type=="MESH"}
for o in assets.values():o.hide_render=True;o.location=(0,0,0)
try:s.render.engine="CYCLES"
except TypeError as e:raise RuntimeError(e)
s.cycles.samples=32;s.cycles.use_denoising=True
s.render.resolution_x=1440;s.render.resolution_y=1080;s.render.resolution_percentage=100
s.world.use_nodes=True
bg=next(n for n in s.world.node_tree.nodes if n.type=="BACKGROUND")
bg.inputs["Color"].default_value=(.16,.18,.20,1);bg.inputs["Strength"].default_value=.45
bpy.ops.mesh.primitive_plane_add(size=200)
floor=bpy.context.object;floor.name="Studio floor"
mat=bpy.data.materials.new("Studio neutral");mat.use_nodes=True
bsdf=next(n for n in mat.node_tree.nodes if n.type=="BSDF_PRINCIPLED")
bsdf.inputs["Base Color"].default_value=(.16,.17,.16,1);bsdf.inputs["Roughness"].default_value=.85
floor.data.materials.append(mat)
for p,power,size in [((1,-3,5),1100,4),((-3,1,3),850,3),((1,3,4),1400,3)]:
    ld=bpy.data.lights.new("Studio softbox","AREA");ld.energy=power;ld.shape="DISK";ld.size=size
    lo=bpy.data.objects.new("Studio softbox",ld);s.collection.objects.link(lo);lo.location=p
    lo.rotation_euler=(Vector((0,0,.6))-lo.location).to_track_quat("-Z","Y").to_euler()
cd=bpy.data.cameras.new("Review camera");cam=bpy.data.objects.new("Review camera",cd);s.collection.objects.link(cam);s.camera=cam;cd.lens=48
views=[
("maca-sem-cobertura",["MacaRecebimento"],(2.5,-3.2,2.15),(0,0,.48)),
("maca-coberta",["MacaRecebimento","CorpoCoberto"],(2.5,-3.2,2.15),(0,0,.55)),
("lavagem",["EstacaoLavagem"],(2.5,-3.2,2.4),(0,0,.68)),
("bancada",["BancadaRecebimento","KitBancada"],(2.4,-3,2),(0,0,.63)),
("carro",["CarroAuxiliar"],(1.4,-1.6,1.3),(0,0,.48)),
("epi",["SuporteEPI"],(.9,-1.3,.9),(0,0,.18)),
("instrumento",["InstrumentoRecebimento"],(.30,-.40,.35),(0,0,.02)),
]
os.makedirs(ROOT+"/revisao/estudio",exist_ok=True)
for name,visible,pos,target in views:
    if "--" in sys.argv and name not in sys.argv[sys.argv.index("--")+1:]:continue
    for key,o in assets.items():o.hide_render=key not in visible
    if "KitBancada" in assets:assets["KitBancada"].location=(.45,0,.903)
    cam.location=pos;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat("-Z","Y").to_euler()
    s.render.filepath=ROOT+"/revisao/estudio/"+name+".png"
    bpy.ops.render.render(write_still=True)
    print("RENDERED",name,flush=True)
