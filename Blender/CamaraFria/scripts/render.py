"""Neutral studio checks of seven compartments, lift, tray rack, checking desk and installation trim.
New views go to revisao/estudio2; ESTUDIO_ANTIGO=1 retains the previous studio set.
Run after gerar.py: blender -b --factory-startup --python Blender/CamaraFria/scripts/render.py [-- view ...]"""
import bpy, os, sys, json, math
from mathutils import Vector, Matrix
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
bpy.ops.wm.open_mainfile(filepath=ROOT + "/CamaraFria.blend")
s = bpy.context.scene
piv = json.load(open(ROOT + "/revisao/pivos.json"))
src = {o.name: o for o in s.objects if o.type == "MESH"}
for o in src.values(): o.hide_render = True; o.hide_viewport = True
def inst(name, loc, rot_z=0.0):
    o = bpy.data.objects.new(name + " (revisao)", src[name].data); s.collection.objects.link(o)
    o.location = loc; o.rotation_euler = (0, 0, rot_z); return o
B = lambda u: Vector((u[0], u[2], u[1]))          # Unity local (x, y, z) -> Blender (x, z, y)

def studio2():
    # Keep the previous review shots available through ESTUDIO_ANTIGO=1.
    import types
    script = ROOT + "/scripts/gerar.py"
    argv = sys.argv
    sys.argv = [script]
    ns = {"__name__": "studio_generator", "__file__": script}
    try:
        exec(compile(open(script).read(), script, "exec"), ns)
    finally:
        sys.argv = argv
    g = types.SimpleNamespace(**ns)
    g.R._current["asset"] = "CarroElevadorAlto (estudio)"
    g.carro_elevador(1.812)
    high = g.R.assemble("CarroElevadorAlto (estudio)", g.MATS)
    src["CarroElevadorAlto"] = high
    high.hide_render = True
    high.hide_viewport = True
    s.render.engine = "CYCLES"; s.cycles.device = "CPU"
    s.cycles.samples = 48; s.cycles.use_denoising = True
    s.render.resolution_x, s.render.resolution_y = 1600, 1100
    s.render.resolution_percentage = 100
    s.world.use_nodes = True
    bg = next(n for n in s.world.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs["Color"].default_value = (.19, .20, .21, 1)
    bg.inputs["Strength"].default_value = .35
    m = bpy.data.materials.new("Estudio2 neutro"); m.use_nodes = True
    bs = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bs.inputs["Base Color"].default_value = (.17, .18, .17, 1)
    bs.inputs["Roughness"].default_value = .8
    bpy.ops.mesh.primitive_plane_add(size=60)
    bpy.context.object.data.materials.append(m)
    bpy.ops.mesh.primitive_plane_add(size=1, location=(-.6, 2.365, 1.7), rotation=(math.pi / 2, 0, 0))
    wall = bpy.context.object; wall.scale = (9, 3.4, 1); wall.data.materials.append(m)
    for p, power, size in (((0, -4, 4.5), 1050, 5), ((-4, -.5, 3.5), 750, 3), ((3, 1, 4), 950, 3)):
        ld = bpy.data.lights.new("Softbox2", "AREA"); ld.energy = power; ld.shape = "DISK"; ld.size = size
        ob = bpy.data.objects.new("Softbox2", ld); s.collection.objects.link(ob); ob.location = p
        ob.rotation_euler = (Vector((-.6, .4, 1)) - ob.location).to_track_quat("-Z", "Y").to_euler()
    cd = bpy.data.cameras.new("Camera2"); cam = bpy.data.objects.new("Camera2", cd)
    s.collection.objects.link(cam); s.camera = cam
    group = []
    def put(name, loc=(0, 0, 0), angle=0):
        ob = inst(name, loc, angle); group.append(ob); return ob
    def module(name, loc, compartments, opened=False):
        put(name, loc)
        for code, c in compartments.items():
            opened_here = opened and code == "C-05"
            angle = math.radians(-c["aberturaPortaGraus"]) if opened_here else 0
            put(c["porta"], Vector(loc) + B(c["pinoPorta"]), angle)
            ref = Vector(loc) + B(c["referencia"])
            put("TrilhoIntermediario", ref + Vector((0, -.5 if opened_here else 0, 0)))
            put("MacaCompartimento", ref + Vector((0, -1 if opened_here else 0, 0)))
    views = [
        ("torre_modulo_frente", "closed", (0, -8.6, 2.6), (-.60, .5, 1.22), 48),
        ("torre_modulo_tres_quartos", "closed", (-5.5, -6.8, 3.8), (-.6, .65, 1.20), 48),
        ("torre_aberta_carro", "open", (-6.0, -7.3, 3.8), (-.9, 0, 1.15), 48),
        ("carro_elevador", "lift", (-2.8, -3.7, 2.7), (0, 0, .65), 55),
        ("suporte_bandejas", "rack", (-2.7, -3.2, 1.8), (0, -.12, .50), 60),
        ("posto_conferencia", "desk", (-.95, -1.20, 1.98), (0, -.12, 1.08), 60),
        ("instalacoes_topo_linhas", "services", (-4.4, -.8, 4.6), (-.8, 1.8, 2.22), 48),
        ("instalacoes_arremates", "services", (-3.2, -3.8, 1.45), (-1.3, .15, .65), 48),
    ]
    pick = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else None
    folder = ROOT + "/revisao/estudio2"; os.makedirs(folder, exist_ok=True)
    files = []
    for name, kind, pos, target, lens in views:
        if pick and name not in pick:
            continue
        for ob in group:
            bpy.data.objects.remove(ob, do_unlink=True)
        group.clear()
        if kind in ("closed", "open", "services"):
            module("ModuloCamara", (0, 0, 0), piv["compartimentos"])
            module("ModuloTorre", (-1.7775, 0, 0), piv["modulos"]["ModuloTorre"]["compartimentos"], kind == "open")
            put("InstalacoesCamara")
            put("ControladorCamara", (-2.945, 2.33, 1.45))
            if kind == "open":
                put("CarroElevadorAlto", (-1.7775, -1.01, 0))
        elif kind == "lift": put("CarroElevador")
        elif kind == "rack": put("SuporteBandejas")
        elif kind == "desk": put("PostoConferencia", (0, 0, 1.05))
        cd.lens = lens; cam.location = pos
        cam.rotation_euler = (Vector(target) - Vector(pos)).to_track_quat("-Z", "Y").to_euler()
        s.render.filepath = folder + "/" + name + ".png"
        bpy.ops.render.render(write_still=True)
        files.append(name + ".png"); print("RENDERED", name, flush=True)
    with open(ROOT + "/revisao/estudio2/vistas.json", "w") as f:
        json.dump([v[0] + ".png" for v in views if os.path.exists(folder + "/" + v[0] + ".png")], f, indent=2)


if not os.environ.get("ESTUDIO_ANTIGO"):
    studio2()

if os.environ.get("ESTUDIO_ANTIGO"):
    OPEN = {"C-02": ("door", 1.00), "C-04": ("door", 1.95)}
    if os.environ.get("TODAS_FECHADAS"): OPEN = {}
    inst("ModuloCamara", (0, 0, 0))
    for n, c in piv["compartimentos"].items():
        st = OPEN.get(n)
        # Blender angle = -Unity angle (handedness).
        inst(c["porta"], B(c["pinoPorta"]), math.radians(-c["aberturaPortaGraus"]) if st else 0)
        ref = B(c["referencia"]); out = st[1] if st else 0
        inst("TrilhoIntermediario", ref + Vector((0, -out / 2, 0)))
        inst("MacaCompartimento", ref + Vector((0, -out, 0)))
    s.render.engine = "CYCLES"; s.cycles.samples = 48; s.cycles.use_denoising = True
    s.render.resolution_x, s.render.resolution_y = 1600, 1000
    s.world.use_nodes = True
    bg = next(n for n in s.world.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs["Color"].default_value = (.16, .18, .20, 1); bg.inputs["Strength"].default_value = .25
    bpy.ops.mesh.primitive_plane_add(size=60); fl = bpy.context.object
    m = bpy.data.materials.new("Piso neutro"); m.use_nodes = True
    bs = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bs.inputs["Base Color"].default_value = (.17, .18, .17, 1); bs.inputs["Roughness"].default_value = .8; fl.data.materials.append(m)
    # Wall behind the cabinet (it stands against one in the room).
    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, 2.36, 1.6), rotation=(math.pi / 2, 0, 0)); wl = bpy.context.object
    wl.scale = (8, 3.2, 1); wl.data.materials.append(m)
    for p, power, size in [((1.5, -4, 3.2), 420, 4), ((-3.5, -1, 2.6), 220, 3), ((2, -1, 3.1), 260, 2)]:
        ld = bpy.data.lights.new("Softbox", "AREA"); ld.energy = power; ld.shape = "DISK"; ld.size = size
        lo = bpy.data.objects.new("Softbox", ld); s.collection.objects.link(lo); lo.location = p
        lo.rotation_euler = (Vector((0, 0, .8)) - lo.location).to_track_quat("-Z", "Y").to_euler()
    cd = bpy.data.cameras.new("Cam"); cam = bpy.data.objects.new("Cam", cd); s.collection.objects.link(cam); s.camera = cam
    views = [
        ("geral", 35, (-2.6, -4.6, 1.75), (0.1, -.6, .8)),
        ("lateral_bandejas", 35, (3.6, -1.6, 1.25), (0.2, -1.1, .65)),
        ("frente", 40, (0, -4.4, 1.0), (0, 0, .85)),
        ("interior_c02", 35, (.75, -1.55, 1.42), (.58, .9, 1.0)),
        ("trinco_dobradica", 45, (.05, -.62, 1.42), (-.27, -.06, 1.25)),
        ("dobradica_aberta", 40, (1.75, -.95, 1.55), (1.0, -.25, 1.25)),
        ("bandeja_estendida_perto", 40, (-.15, -2.2, 1.0), (.55, -1.2, .35)),
        ("embaixo", 30, (-.9, -1.1, .22), (-.3, .6, .1)),
    ]
    os.makedirs(ROOT + "/revisao/estudio", exist_ok=True)
    pick = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else None
    suffix = os.environ.get("SUFIXO", "")
    for name, lens, pos, tgt in views:
        if pick and name not in pick: continue
        cd.lens = lens; cam.location = pos; cam.rotation_euler = (Vector(tgt) - Vector(pos)).to_track_quat("-Z", "Y").to_euler()
        s.render.filepath = ROOT + "/revisao/estudio/" + name + suffix + ".png"
        bpy.ops.render.render(write_still=True); print("RENDERED", name, flush=True)
