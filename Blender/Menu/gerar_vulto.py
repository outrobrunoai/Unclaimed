import bpy, bmesh, mathutils, math, sys
args=sys.argv[sys.argv.index('--')+1:]
modo=args[0]; out=args[1]
sc=bpy.context.scene
rig=bpy.data.objects['CorpoBase.rig']; corpo=bpy.data.objects['CorpoBase']
def rot(b, eixo, graus):
    pb=rig.pose.bones[b]; pb.rotation_mode='XYZ'; e=list(pb.rotation_euler); e['XYZ'.index(eixo)]+=math.radians(graus); pb.rotation_euler=e
P=dict(a.split('=') for a in args[2:]) if len(args)>2 else {}
for k,v in P.items():
    b,eixo=k.split(':'); rot(b,eixo,float(v))
bpy.context.view_layer.update()
dg=bpy.context.evaluated_depsgraph_get()
ev=corpo.evaluated_get(dg)
me=bpy.data.meshes.new_from_object(ev, depsgraph=dg)
me.transform(corpo.matrix_world)
# em pé: cabeça (-x) para +z, rosto (+z) para -y (frente do Blender)
me.transform(mathutils.Matrix.Rotation(math.radians(90),4,'Y'))
me.transform(mathutils.Matrix.Rotation(math.radians(-90),4,'Z'))
bm=bmesh.new(); bm.from_mesh(me)
zs=[v.co.z for v in bm.verts]; xs=[v.co.x for v in bm.verts]; ys=[v.co.y for v in bm.verts]
off=mathutils.Vector(((min(xs)+max(xs))/2,(min(ys)+max(ys))/2,min(zs)))
for v in bm.verts: v.co-=off
bm.to_mesh(me); bm.free()
v=bpy.data.objects.new('Vulto', me)
for o in list(sc.objects): bpy.data.objects.remove(o)
sc.collection.objects.link(v)
dec=v.modifiers.new('dec','DECIMATE'); dec.ratio=0.35
print('dims', tuple(round(d,2) for d in v.dimensions), 'verts', len(me.vertices))
if modo=='ver':
    cam=bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera=cam
    sc.render.engine='BLENDER_WORKBENCH'; sc.render.resolution_x=400; sc.render.resolution_y=600
    for nome,loc,r in (('f',(0,-5,0.95),(90,0,0)),('l',(5,0,0.95),(90,0,90))):
        cam.location=loc; cam.rotation_euler=[math.radians(a) for a in r]; sc.render.filepath=out+'_'+nome+'.png'; bpy.ops.render.render(write_still=True)
else:
    bpy.context.view_layer.objects.active=v; v.select_set(True)
    bpy.ops.object.modifier_apply(modifier='dec')
    bpy.ops.object.shade_smooth()
    m=bpy.data.materials.new('Vulto'); v.data.materials.clear(); v.data.materials.append(m)
    bpy.ops.wm.save_as_mainfile(filepath=out.replace('.fbx','.blend'))
    bpy.ops.export_scene.fbx(filepath=out, use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y', bake_space_transform=True, mesh_smooth_type='FACE', add_leaf_bones=False)
    print('exportado', out)
