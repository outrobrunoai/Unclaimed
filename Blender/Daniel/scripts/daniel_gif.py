"""Review GIFs of Daniel's chosen clips (three-quarter view, camera following the hips).
Run: blender -b --python Blender/Daniel/scripts/daniel_gif.py -- clip:inicio:fim:passo [...]
Reads Blender/Daniel/anim_<clip>.blend (from daniel_anim.py); writes Blender/Daniel/revisao/gif_<clip>.gif."""
import os, sys
import bpy
from mathutils import Vector

SRC = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(SRC, "revisao")
ARGS = sys.argv[sys.argv.index("--") + 1:]

for item in ARGS:
    clip, a, b, step = item.split(":"); a, b, step = int(a), int(b), int(step)
    bpy.ops.wm.open_mainfile(filepath=os.path.join(SRC, f"anim_{clip}.blend"))
    scene = bpy.context.scene; rig = bpy.data.objects["Daniel.rig"]; cam = scene.camera
    scene.render.resolution_x, scene.render.resolution_y = 400, 560
    frames = []
    for i, f in enumerate(range(a, b + 1, step)):
        scene.frame_set(f)
        hip = rig.matrix_world @ rig.pose.bones["Hips"].head
        cam.location = (hip.x + 2.9, hip.y - 2.9, 1.3)
        cam.rotation_euler = (Vector((hip.x, hip.y, 0.9)) - cam.location).to_track_quat("-Z", "Y").to_euler()
        path = os.path.join(OUT, f"_gif_{clip}_{i:03d}.png"); scene.render.filepath = path
        bpy.ops.render.render(write_still=True); frames.append(path)
    try:
        from PIL import Image
        ims = [Image.open(p).convert("P", palette=Image.ADAPTIVE) for p in frames]
        ims[0].save(os.path.join(OUT, f"gif_{clip}.gif"), save_all=True, append_images=ims[1:],
                    duration=int(1000 * step / 30), loop=0)
    except ImportError:
        print("PIL missing in Blender's Python; frames kept:", frames[0])
    print("GIF", clip, len(frames))
