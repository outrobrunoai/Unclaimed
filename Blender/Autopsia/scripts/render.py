"""Neutral studio checks of the actual source meshes (Cycles, grey floor, three softboxes).
Run: blender -b --factory-startup --python Blender/Autopsia/scripts/render.py [-- view ...]"""
import bpy, os, sys
from mathutils import Vector
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),".."))
bpy.ops.wm.open_mainfile(filepath=ROOT+"/Autopsia.blend")
s=bpy.context.scene
assets={o.name:o for o in s.objects if o.type=="MESH" and not o.name.endswith("_fonte")}
for o in s.objects:
    if o.type=="MESH":o.hide_render=True
for n,o in assets.items():o.location=(0,0,2.6 if n.startswith("Luminaria") else 0)
try:s.render.engine="CYCLES"
except TypeError as e:raise RuntimeError(e)
s.cycles.samples=48;s.cycles.use_denoising=True
s.render.resolution_x=1280;s.render.resolution_y=960;s.render.resolution_percentage=100
s.world.use_nodes=True
bg=next(n for n in s.world.node_tree.nodes if n.type=="BACKGROUND")
bg.inputs["Color"].default_value=(.16,.18,.20,1);bg.inputs["Strength"].default_value=.45
bpy.ops.mesh.primitive_plane_add(size=200);floor=bpy.context.object
mat=bpy.data.materials.new("Studio neutral");mat.use_nodes=True
b=next(n for n in mat.node_tree.nodes if n.type=="BSDF_PRINCIPLED")
b.inputs["Base Color"].default_value=(.16,.17,.16,1);b.inputs["Roughness"].default_value=.85
floor.data.materials.append(mat)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,.002,0),rotation=(1.5708,0,0));wall=bpy.context.object
wall.data.materials.append(mat)
glass=bpy.data.materials.new("Studio glass");glass.use_nodes=True
gb=next(n for n in glass.node_tree.nodes if n.type=="BSDF_PRINCIPLED")
gb.inputs["Base Color"].default_value=(.9,.95,.94,1);gb.inputs["Roughness"].default_value=.02
for k in ("Transmission Weight","Transmission"):
    if k in gb.inputs:gb.inputs[k].default_value=1.0;break
for n,o in assets.items():
    if n.endswith("Vidro") or n.endswith("Lente"):
        o.data.materials.clear();o.data.materials.append(glass)
for p,power,size in [((1.5,-3.5,4.5),420,4),((-3,-2,3),260,3),((2,-1.5,3.5),300,3)]:
    ld=bpy.data.lights.new("Studio softbox","AREA");ld.energy=power;ld.shape="DISK";ld.size=size
    lo=bpy.data.objects.new("Studio softbox",ld);s.collection.objects.link(lo);lo.location=p
    lo.rotation_euler=(Vector((0,-.3,.9))-lo.location).to_track_quat("-Z","Y").to_euler()
cd=bpy.data.cameras.new("Review camera");cam=bpy.data.objects.new("Review camera",cd);s.collection.objects.link(cam);s.camera=cam;cd.lens=40
views=[
("bancada",["BancadaAutopsia"],(1.6,-4.2,2.0),(0,-.3,.75)),
("bancada-cuba",["BancadaAutopsia"],(-.25,-1.25,1.65),(-.6,-.3,.85)),
("armario-suspenso",["ArmarioSuspenso","ArmarioSuspensoVidro"],(.9,-2.2,1.25),(0,-.15,.35)),
("armario-instrumental",["ArmarioInstrumental","ArmarioInstrumentalVidro"],(1.4,-3.0,1.6),(0,-.2,1.0)),
("lavatorio",["LavatorioJoelho"],(.75,-1.5,1.35),(0,-.2,.82)),
("lavatorio-lado",["LavatorioJoelho"],(1.35,-.55,1.05),(0,-.25,.85)),
("balanca",["BalancaVisceras"],(.55,-1.3,1.8),(0,-.3,1.68)),
("luminaria",["LuminariaExame","LuminariaExameLente"],(1.9,-2.3,1.7),(.35,0,2.05)),
("luminaria-baixo",["LuminariaExame","LuminariaExameLente"],(1.25,-.95,.75),(.62,0,1.55)),
("comando",["ComandoLuminaria"],(1.9,-3.4,1.8),(0,-1.0,2.1)),
("comando-caixa",["ComandoLuminaria"],(.45,-.8,1.45),(0,-.03,1.4)),
("quadro",["QuadroBranco"],(.5,-1.4,1.45),(0,0,1.38)),
]
os.makedirs(ROOT+"/revisao/estudio",exist_ok=True)
sel=sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else None
for name,visible,pos,target in views:
    if sel and name not in sel:continue
    for key,o in assets.items():o.hide_render=key not in visible
    wall.hide_render=name.startswith("luminaria")
    cam.location=pos;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat("-Z","Y").to_euler()
    s.render.filepath=ROOT+"/revisao/estudio/"+name+".png"
    bpy.ops.render.render(write_still=True)
    print("RENDERED",name,flush=True)
