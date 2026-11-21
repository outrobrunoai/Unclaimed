"""Daniel's animation candidates: CMU motion-capture clips (BVH, Blender/Fontes/CMU) put on Daniel's cmu_mb rig the
way MPFB's AnimationService.import_bvh_file_as_pose does it (copy the BVH bone rolls, then the rotations hold),
frame by frame, at 30 fps. Review strips in Blender/Daniel/revisao/anim_<clip>.png.

Run: blender -b --python Blender/Daniel/scripts/daniel_anim.py -- clip [clip ...]   (clip = BVH name, e.g. 35_01)
"""
import os, sys, math
import bpy
from mathutils import Vector, Matrix, Euler

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.dirname(HERE)
ROOT = os.path.dirname(os.path.dirname(SRC))
BLEND = os.path.join(SRC, "Daniel.blend")
OUT = os.path.join(SRC, "revisao")
CMU = os.path.join(ROOT, "Blender", "Fontes", "CMU")
CLIPS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else ["35_01"]
OUTFIT = "C_jaqueta_escura"
STEP = 4            # CMU is 120 fps; every 4th frame = 30 fps
STRIP = 8           # frames shown per strip
SPAN = 1.6          # seconds covered by the strip


def upd(): bpy.context.view_layer.update()


TPOSE = os.path.expanduser("~/.config/blender/5.2/extensions/blender_org/mpfb/data/poses/cmu_mb_fk/t-pose.json")
YUP = Matrix.Rotation(math.radians(90), 4, "X")        # BVH stays Y-up after import; this turns it Z-up


def depth(b): return 0 if b.parent is None else 1 + depth(b.parent)


def retarget(rig, bvh_path):
    """World-space retarget: each bone's rotation away from the BVH rest (a T-pose) is applied to Daniel's own
    T-pose (MPFB t-pose.json), so differing rest poses and bone rolls do not matter."""
    import json
    scene = bpy.context.scene
    bpy.ops.import_anim.bvh(filepath=bvh_path, axis_forward="Y", axis_up="Z", rotate_mode="XYZ")
    src = bpy.context.object
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="POSE")
    for pb in rig.pose.bones: pb.rotation_mode = "QUATERNION"; pb.rotation_quaternion = (1, 0, 0, 0); pb.location = (0, 0, 0)
    # Daniel's reference pose: every bone turned to point where the same BVH bone points at rest (a T-pose with
    # straight legs). MPFB's t-pose.json turns the legs slightly inwards, which crossed them in standing clips, so the
    # legs are realigned to the BVH rest; arms and clavicles keep t-pose.json (realigning them hunched the shoulders).
    for name, e in json.load(open(TPOSE))["bone_rotations"].items():
        pb = rig.pose.bones.get(name)
        if pb: pb.rotation_quaternion = Euler(e, "XYZ").to_quaternion()
    upd()
    inv_rig = rig.matrix_world.inverted()
    PERNA = ("UpLeg", "Leg", "Foot", "ToeBase")       # only the legs are realigned; arms and clavicles keep t-pose.json
    by_depth = sorted([pb for pb in rig.pose.bones if pb.name in src.data.bones and pb.name.endswith(PERNA)
                       and pb.name != "Hips"],
                      key=lambda pb: depth(pb.bone))
    for pb in by_depth:
        sb = src.data.bones[pb.name]
        if sb.length < 1e-4: continue
        want = ((YUP @ src.matrix_world).to_3x3() @ sb.matrix_local.to_3x3()).col[1].normalized()
        m = rig.matrix_world @ pb.matrix
        q = m.to_3x3().col[1].normalized().rotation_difference(want)
        pb.matrix = inv_rig @ (Matrix.Translation(m.to_translation()) @ (q.to_matrix() @ m.to_3x3()).to_4x4())
        upd()
    dst_t = {pb.name: (rig.matrix_world @ pb.matrix).to_quaternion() for pb in rig.pose.bones}
    src_rest = {b.name: (YUP @ src.matrix_world @ b.matrix_local).to_quaternion() for b in src.data.bones}
    order = sorted([pb for pb in rig.pose.bones if pb.name in src_rest], key=lambda pb: depth(pb.bone))
    levels = {}
    for pb in order: levels.setdefault(depth(pb.bone), []).append(pb)
    act = src.animation_data.action
    f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])
    scene.frame_set(f0)
    hip0 = YUP @ src.matrix_world @ src.pose.bones["Hips"].head
    k = (rig.matrix_world @ rig.data.bones["Hips"].head_local).z / max(hip0.z, 1e-6)
    out = 0
    for f in range(f0, f1 + 1, STEP):
        scene.frame_set(f)
        src_pose = {b.name: (YUP @ src.matrix_world @ b.matrix).to_quaternion() for b in src.pose.bones}
        hip = YUP @ src.matrix_world @ src.pose.bones["Hips"].head
        for lvl in sorted(levels):
            for pb in levels[lvl]:
                q = src_pose[pb.name] @ src_rest[pb.name].inverted() @ dst_t[pb.name]
                cur = rig.matrix_world @ pb.matrix
                loc = hip * k if pb.name == "Hips" else cur.to_translation()
                pb.matrix = inv_rig @ (Matrix.Translation(loc) @ q.to_matrix().to_4x4())
            upd()
        out += 1
        for pb in order:
            pb.keyframe_insert("rotation_quaternion", frame=out)
            if pb.name == "Hips": pb.keyframe_insert("location", frame=out)
    bpy.data.objects.remove(src, do_unlink=True)
    scene.frame_start, scene.frame_end, scene.render.fps = 1, out, 30
    assentar(rig, out)
    return out


def assentar(rig, frames):
    """Lowers/raises the whole clip so the lowest toe touches the floor."""
    scene = bpy.context.scene; low = 9.0
    for f in range(1, frames + 1):
        scene.frame_set(f)
        for n in ("LeftToeBase", "RightToeBase", "LeftFoot", "RightFoot"):
            low = min(low, (rig.matrix_world @ rig.pose.bones[n].tail).z, (rig.matrix_world @ rig.pose.bones[n].head).z)
    fc = [c for c in rig.animation_data.action.fcurves if c.data_path == 'pose.bones["Hips"].location'] \
        if hasattr(rig.animation_data.action, "fcurves") else []
    shift = 0.035 - low
    hp = rig.pose.bones["Hips"]
    for f in range(1, frames + 1):
        scene.frame_set(f)
        m = rig.matrix_world @ hp.matrix
        hp.matrix = rig.matrix_world.inverted() @ (Matrix.Translation((0, 0, shift)) @ m)
        hp.keyframe_insert("location", frame=f)


def strip(rig, clip, frames, span=SPAN):
    scene = bpy.context.scene
    cam = scene.camera
    shots = []
    n = min(frames, int(span * 30))
    start = max(1, (frames - n) // 2)
    for i in range(STRIP):
        f = start + int(i * (n - 1) / (STRIP - 1))
        scene.frame_set(f)
        hip = rig.matrix_world @ rig.pose.bones["Hips"].head
        cam.location = (hip.x + 4.0, hip.y, 1.0)
        cam.rotation_euler = (Vector((hip.x, hip.y, 0.92)) - cam.location).to_track_quat("-Z", "Y").to_euler()
        path = os.path.join(OUT, f"_tmp_{clip}_{i}.png"); scene.render.filepath = path
        bpy.ops.render.render(write_still=True); shots.append(path)
    return shots


def main():
    import importlib.util
    spec = importlib.util.spec_from_file_location("dm", os.path.join(HERE, "daniel_modelo.py"))
    sys_argv = sys.argv; sys.argv = [sys.argv[0], "--"]          # keep daniel_modelo from running its steps
    dm = importlib.util.module_from_spec(spec); spec.loader.exec_module(dm); sys.argv = sys_argv
    results = {}
    for item in CLIPS:
        clip, _, span = item.partition(":")      # "56_02:28" = strip over 28 s
        span = float(span) if span else SPAN
        bpy.ops.wm.open_mainfile(filepath=BLEND)
        scene = bpy.context.scene; dm.studio(scene); dm.mostrar(OUTFIT)
        scene.render.resolution_x, scene.render.resolution_y = 360, 540
        cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam")); scene.collection.objects.link(cam)
        scene.camera = cam; cam.data.lens = 50
        rig = bpy.data.objects["Daniel.rig"]
        frames = retarget(rig, os.path.join(CMU, clip + ".bvh"))
        results[clip] = (frames, strip(rig, clip, frames, span))
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SRC, f"anim_{clip}.blend"))
    print("CLIPS", {c: r[0] for c, r in results.items()})


main()
