# Bakes the procedural materials of the autopsy table into the textures Unity's URP Lit needs.
import bpy, os, time
import numpy as np

SIZE = 2048


def _image(name, colorspace, alpha=False):
    img = bpy.data.images.get(name)
    if img and (img.size[0] != SIZE or img.size[1] != SIZE):
        bpy.data.images.remove(img)
        img = None
    if img is None:
        img = bpy.data.images.new(name, SIZE, SIZE, alpha=alpha, float_buffer=False)
    # Setting the colour space reloads the buffer, which wipes an earlier bake: only touch it when needed.
    if img.colorspace_settings.name != colorspace:
        img.colorspace_settings.name = colorspace
    return img


def _route(mats, channel, img):
    """Points every material's bake node at img; channel None = full BSDF, else that reroute via emission."""
    for m in mats:
        nt = m.node_tree
        out = next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL")
        bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
        emit = nt.nodes["BAKE_EMIT"]
        node = nt.nodes["BAKE_IMG"]
        node.image = img
        for n in nt.nodes:
            n.select = False
        node.select = True
        nt.nodes.active = node
        if channel is None:
            nt.links.new(bsdf.outputs[0], out.inputs["Surface"])
        else:
            nt.links.new(nt.nodes[channel].outputs[0], emit.inputs["Color"])
            nt.links.new(emit.outputs[0], out.inputs["Surface"])


def _pixels(img):
    a = np.empty(SIZE * SIZE * 4, dtype=np.float32)
    img.pixels.foreach_get(a)
    return a.reshape(SIZE, SIZE, 4)


def bake(ob, mats, ground=True, ao_samples=128, ao_distance=0.35, only=None):
    sc = bpy.context.scene
    prev_engine = sc.render.engine
    try:
        sc.render.engine = "CYCLES"
    except TypeError as e:
        raise RuntimeError(e)
    sc.cycles.device = "CPU"
    sc.render.bake.margin = 16
    sc.render.bake.margin_type = "EXTEND"
    sc.render.bake.use_clear = True
    if sc.world is None:
        sc.world = bpy.data.worlds.new("World")
    sc.world.light_settings.distance = ao_distance

    plane = None
    if ground:   # contact occlusion from the floor the table stands on (not exported)
        me = bpy.data.meshes.new("__bake_ground")
        me.from_pydata([(-3, -3, 0), (3, -3, 0), (3, 3, 0), (-3, 3, 0)], [], [(0, 1, 2, 3)])
        plane = bpy.data.objects.new("__bake_ground", me)
        sc.collection.objects.link(plane)

    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    imgs = {
        "base": _image("Bake_BaseColor", "sRGB"),
        "rough": _image("Bake_Roughness", "Non-Color"),
        "metal": _image("Bake_Metallic", "Non-Color"),
        "normal": _image("Bake_Normal", "Non-Color"),
        "ao": _image("Bake_AO", "Non-Color"),
    }
    times = {}
    try:
        for key, channel, kind, samples in (("base", "CH_BASE", "EMIT", 16), ("rough", "CH_ROUGH", "EMIT", 16),
                                            ("metal", "CH_METAL", "EMIT", 4), ("normal", None, "NORMAL", 16),
                                            ("ao", None, "AO", ao_samples)):
            if only and key not in only:
                continue
            sc.cycles.samples = samples
            _route(mats, channel, imgs[key])
            t = time.time()
            if kind == "NORMAL":
                bpy.ops.object.bake(type="NORMAL", normal_space="TANGENT", normal_r="POS_X", normal_g="POS_Y", normal_b="POS_Z")
            else:
                bpy.ops.object.bake(type=kind)
            times[key] = round(time.time() - t, 1)
    finally:
        _route(mats, None, imgs["base"])
        if plane:
            bpy.data.objects.remove(plane)
            bpy.data.meshes.remove(bpy.data.meshes["__bake_ground"])
        sc.render.engine = prev_engine
    return imgs, times


def write_unity_maps(imgs, folder, prefix="MesaAutopsia"):
    """BaseMap (sRGB RGB), MetallicSmoothness (R metallic, A smoothness), Normal, Occlusion."""
    os.makedirs(folder, exist_ok=True)
    def save(name, arr, colorspace, alpha):
        img = bpy.data.images.get(name) or bpy.data.images.new(name, SIZE, SIZE, alpha=alpha)
        if img.colorspace_settings.name != colorspace:
            img.colorspace_settings.name = colorspace
        img.pixels.foreach_set(arr.astype(np.float32).ravel())
        img.filepath_raw = os.path.join(folder, name + ".png")
        img.file_format = "PNG"
        img.save()
        return img.filepath_raw
    base, rough, metal = _pixels(imgs["base"]), _pixels(imgs["rough"]), _pixels(imgs["metal"])
    normal, ao = _pixels(imgs["normal"]), _pixels(imgs["ao"])
    out = []
    b = base.copy(); b[..., 3] = 1.0
    out.append(save(prefix + "_BaseMap", b, "sRGB", False))
    ms = np.zeros_like(base)
    ms[..., 0] = metal[..., 0]
    ms[..., 1] = metal[..., 0]
    ms[..., 2] = metal[..., 0]
    ms[..., 3] = 1.0 - rough[..., 0]
    out.append(save(prefix + "_MetallicSmoothness", ms, "Non-Color", True))
    n = normal.copy(); n[..., 3] = 1.0
    out.append(save(prefix + "_Normal", n, "Non-Color", False))
    o = np.repeat(ao[..., :1], 4, axis=2); o[..., 3] = 1.0
    out.append(save(prefix + "_Occlusion", o, "Non-Color", False))
    return out
