# Procedural materials of the reception furniture, used only to bake each asset's Unity textures.
# Built with the node helper G of the autopsy table (Blender/MesaAutopsia/scripts/mesa_materiais.py), so
# the baker of that table (mesa_bake.py) works unchanged: channels on CH_BASE / CH_ROUGH / CH_METAL.
# Patterns use object coordinates (the asset's local frame, metres). Wear is kept discreet: scuffs and
# grime low on the faces, handled edges and the counter's leaning strip, polished seats.
import bpy

# Shared colours: one family for the whole reception (laminate and granite match counter and cabinet).
CORES = dict(
    laminado=(0.50, 0.49, 0.455),     # warm light-grey melamine with matching PVC edging
    rodape=(0.10, 0.10, 0.10),        # dark plinths
    granito=((0.33, 0.33, 0.32), (0.46, 0.46, 0.45)),
    polipropileno=(0.055, 0.085, 0.14),
    epoxi=(0.04, 0.042, 0.045),
    tecido=(0.035, 0.036, 0.04),
    nylon=(0.025, 0.025, 0.027),
    metal_pintado=(0.70, 0.70, 0.67),   # off-white powder coat (water cooler)
)

G = None   # set by make_all() from mesa_materiais


def _new(name):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    return m, G(m)


def vec(g, rgb):
    return g.n("ShaderNodeCombineXYZ", in_0=rgb[0], in_1=rgb[1], in_2=rgb[2]).outputs[0]


def lerp(g, a, b, f):
    """Vector lerp a -> b by a float socket (b may be an RGB tuple)."""
    if isinstance(b, tuple):
        b = vec(g, b)
    fv = g.n("ShaderNodeCombineXYZ")
    for i in range(3):
        g.link(f, fv.inputs[i])
    d = g.n("ShaderNodeVectorMath", operation="SUBTRACT")
    g.link(b, d.inputs[0]); g.link(a, d.inputs[1])
    m = g.n("ShaderNodeVectorMath", operation="MULTIPLY")
    g.link(d.outputs[0], m.inputs[0]); g.link(fv.outputs[0], m.inputs[1])
    s = g.n("ShaderNodeVectorMath", operation="ADD")
    g.link(m.outputs[0], s.inputs[0]); g.link(a, s.inputs[1])
    return s.outputs[0]


def mul(g, a, k):
    return g.math("MULTIPLY", a, k)


def edges(g, distance=0.006):
    """1 on convex edges: ambient occlusion sampled from inside the mesh only hits nearby faces at edges."""
    ao = g.n("ShaderNodeAmbientOcclusion", samples=8, inside=True, only_local=True)
    ao.inputs["Distance"].default_value = distance
    return g.mrange(ao.outputs["AO"], 0.85, 0.45)


def spot(g, center, radius):
    d = g.n("ShaderNodeVectorMath", operation="DISTANCE")
    g.link(g.obj(), d.inputs[0])
    d.inputs[1].default_value = center
    return g.mrange(d.outputs["Value"], radius, radius * 0.25, smooth=True)


def low(g, top=0.25):
    return g.mrange(g.xyz()[2], top, 0.0, smooth=True)


def scuffs(g, density=0.62):
    """Short horizontal streaks (shoes, mop, chair feet)."""
    n = g.noise(g.coords(scale=(5.0, 5.0, 110.0)), 1.0, 2.0, 0.5)
    return g.mrange(n, density, density + 0.08)


def laminado(name, rgb, scuff_rgb=None, grime_spots=(), kick=0.25, rough=0.5):
    m, g = _new(name)
    p = g.obj()
    cloud = g.noise(g.coords(), 3.5, 3.0, 0.55)
    speck = g.noise(p, 650.0, 1.0, 0.5)
    t = g.math("ADD", mul(g, cloud, 0.6), mul(g, speck, 0.4))
    base = g.color(t, tuple(c * 0.95 for c in rgb), tuple(c * 1.035 for c in rgb))
    e = edges(g)
    lw = low(g, kick)
    grime = g.math("MULTIPLY", lw, g.mrange(g.noise(p, 8.0, 4.0, 0.6), 0.4, 0.75))
    sc = g.math("MULTIPLY", scuffs(g), lw)
    dirt = g.math("ADD", mul(g, grime, 0.3), mul(g, sc, 0.25), clamp=True)
    for c, r in grime_spots:   # hand grime around handles and the clerk's resting spots
        n = g.mrange(g.noise(p, 30.0, 3.0, 0.6), 0.3, 0.7)
        dirt = g.math("ADD", dirt, mul(g, g.math("MULTIPLY", spot(g, c, r), n), 0.22), clamp=True)
    base = lerp(g, base, tuple(c * 0.55 for c in rgb), dirt)
    if scuff_rgb:
        base = lerp(g, base, scuff_rgb, mul(g, sc, 0.6))
    base = lerp(g, base, tuple(min(1.0, c * 1.12 + 0.03) for c in rgb), mul(g, e, 0.55))
    r_ = g.math("ADD", rough, mul(g, dirt, 0.3))
    r_ = g.math("SUBTRACT", r_, mul(g, e, 0.12))
    h = g.math("SUBTRACT", mul(g, speck, 0.2), mul(g, sc, 0.3))
    g.finish(base, r_, 0.0, h, bump_strength=0.12, bump_dist=0.0004)
    return m


def granito(name, lean_y, lean_z):
    """Polished grey granite; the strip people lean on (y < lean_y on the top face) is duller."""
    m, g = _new(name)
    p = g.obj()
    lo, hi = CORES["granito"]
    cloud = g.noise(p, 14.0, 4.0, 0.6)
    dark = g.mrange(g.noise(p, 150.0, 2.0, 0.5), 0.60, 0.665)
    light = g.mrange(g.noise(g.coords(offset=(3.1, 7.2, 1.3)), 180.0, 2.0, 0.5), 0.40, 0.345)
    base = g.color(cloud, lo, hi)
    base = lerp(g, base, (0.05, 0.05, 0.05), mul(g, dark, 0.9))
    base = lerp(g, base, (0.78, 0.78, 0.76), mul(g, light, 0.75))
    x, y, z = g.xyz()
    lean = g.math("MULTIPLY", g.mrange(y, lean_y, lean_y - 0.1), g.mrange(z, lean_z - 0.004, lean_z))
    lean = g.math("MULTIPLY", lean, g.mrange(g.noise(p, 6.0, 3.0, 0.6), 0.3, 0.6))
    e = edges(g)
    r_ = g.math("ADD", 0.16, g.math("ADD", mul(g, lean, 0.22), mul(g, e, 0.15)))
    base = lerp(g, base, (0.5, 0.5, 0.49), mul(g, g.math("ADD", lean, e), 0.06))
    g.finish(base, r_, 0.0, mul(g, dark, -0.4), bump_strength=0.08, bump_dist=0.0003)
    return m


def polipropileno(name, seat_centers):
    m, g = _new(name)
    p = g.obj()
    rgb = CORES["polipropileno"]
    base = g.color(g.noise(p, 5.0, 3.0, 0.5), tuple(c * 0.9 for c in rgb), tuple(c * 1.08 for c in rgb))
    e = edges(g)
    polish = spot(g, seat_centers[0], 0.2)
    for c in seat_centers[1:]:
        polish = g.math("MAXIMUM", polish, spot(g, c, 0.2))
    sc = g.mrange(g.noise(g.coords(scale=(40.0, 3.0, 40.0)), 1.0, 2.0, 0.5), 0.66, 0.72)
    base = lerp(g, base, tuple(min(1.0, c * 1.9 + 0.04) for c in rgb), g.math("ADD", mul(g, e, 0.4), mul(g, sc, 0.25)))
    r_ = g.math("SUBTRACT", 0.6, mul(g, polish, 0.2))
    r_ = g.math("ADD", r_, mul(g, sc, 0.1))
    g.finish(base, r_, 0.0, g.noise(p, 420.0, 1.0, 0.5), bump_strength=0.1, bump_dist=0.0004)
    return m


def epoxi(name):
    """Graphite powder coat on steel tube; chipped near the floor."""
    m, g = _new(name)
    p = g.obj()
    rgb = CORES["epoxi"]
    chips = g.math("MULTIPLY", g.mrange(g.noise(p, 45.0, 3.0, 0.6), 0.68, 0.71), low(g, 0.12))
    base = lerp(g, vec(g, rgb), (0.5, 0.5, 0.5), chips)
    r_ = g.math("SUBTRACT", 0.42, mul(g, chips, 0.1))
    g.finish(base, r_, chips, g.noise(p, 260.0, 1.0, 0.5), bump_strength=0.15, bump_dist=0.0004)
    return m


def tecido(name, front_y, top_z):
    """Dark heathered upholstery; slightly faded and pilled along the seat's front edge."""
    m, g = _new(name)
    p = g.obj()
    rgb = CORES["tecido"]
    heather = g.noise(p, 380.0, 1.0, 0.5)
    base = g.color(heather, tuple(c * 0.75 for c in rgb), tuple(c * 1.6 for c in rgb))
    x, y, z = g.xyz()
    wear = g.math("MULTIPLY", g.mrange(y, front_y + 0.08, front_y), g.mrange(z, top_z - 0.05, top_z))
    wear = g.math("ADD", wear, mul(g, edges(g, 0.012), 0.5), clamp=True)
    base = lerp(g, base, (0.085, 0.085, 0.09), mul(g, wear, 0.6))
    g.finish(base, 0.88, 0.0, heather, bump_strength=0.2, bump_dist=0.0006)
    return m


def nylon(name):
    m, g = _new(name)
    p = g.obj()
    rgb = CORES["nylon"]
    e = edges(g, 0.004)
    sc = g.math("MULTIPLY", scuffs(g, 0.6), low(g, 0.14))
    base = lerp(g, vec(g, rgb), (0.11, 0.11, 0.11), g.math("ADD", mul(g, e, 0.5), mul(g, sc, 0.6), clamp=True))
    g.finish(base, g.math("ADD", 0.5, mul(g, sc, 0.2)), 0.0, g.noise(p, 300.0, 1.0, 0.5),
             bump_strength=0.08, bump_dist=0.0003)
    return m


def metal(name, rgb, rough):
    m, g = _new(name)
    p = g.obj()
    n = g.noise(p, 25.0, 3.0, 0.6)
    g.finish(vec(g, rgb), g.math("ADD", rough, mul(g, n, 0.12)), 1.0, None)
    return m


def plastico(name, rgb, rough=0.45, scuffed=True):
    """Moulded plastic (equipment housings, trays, folders, binders): faint texture, lighter worn edges."""
    m, g = _new(name)
    p = g.obj()
    base = g.color(g.noise(p, 8.0, 2.0, 0.5), tuple(c * 0.92 for c in rgb), tuple(c * 1.08 for c in rgb))
    if scuffed:
        base = lerp(g, base, tuple(min(1.0, c * 1.5 + 0.05) for c in rgb), mul(g, edges(g, 0.003), 0.5))
    g.finish(base, g.math("ADD", rough, mul(g, g.noise(p, 30.0, 2.0, 0.5), 0.1)), 0.0,
             g.noise(p, 500.0, 1.0, 0.5), bump_strength=0.05, bump_dist=0.0002)
    return m


def teclas(name):
    """Keycap plastic: matt dark grey, shinier where fingers rest (the keys are modelled one by one)."""
    m, g = _new(name)
    p = g.obj()
    shine = g.mrange(g.noise(p, 40.0, 2.0, 0.5), 0.45, 0.65)
    g.finish(vec(g, (0.045, 0.045, 0.048)), g.math("SUBTRACT", 0.55, mul(g, shine, 0.2)), 0.0, None)
    return m


def visor(name):
    """Monochrome LCD of the desk phone, switched off: grey-green behind a glossy window."""
    m, g = _new(name)
    g.finish(vec(g, (0.16, 0.19, 0.16)), 0.12, 0.0, None)
    return m


def tela(name):
    m, g = _new(name)
    g.finish(vec(g, (0.012, 0.013, 0.016)), 0.08, 0.0, None)
    return m


def papel(name, rgb=(0.82, 0.82, 0.78)):
    m, g = _new(name)
    p = g.obj()
    base = g.color(g.noise(p, 40.0, 2.0, 0.5), tuple(c * 0.96 for c in rgb), rgb)
    g.finish(base, 0.85, 0.0, g.noise(p, 300.0, 1.0, 0.5), bump_strength=0.05, bump_dist=0.0002)
    return m


def grade(name, pitch=0.012):
    """Ventilation louvres: horizontal dark slots in the cabinet colour."""
    m, g = _new(name)
    x, y, z = g.xyz()
    slot = g.mrange(g.math("FRACT", g.math("DIVIDE", z, pitch)), 0.35, 0.5, smooth=True)
    base = lerp(g, vec(g, (0.04, 0.04, 0.04)), CORES["metal_pintado"], slot)
    g.finish(base, 0.5, 0.0, slot, bump_strength=0.4, bump_dist=0.001)
    return m


def inox_puxador(name, grips):
    """Polished stainless pull: duller and slightly darker where hands grip it."""
    m, g = _new(name)
    p = g.obj()
    use = None
    for c in grips:
        f = g.math("MULTIPLY", spot(g, c, 0.09), g.mrange(g.noise(p, 60.0, 2.0, 0.6), 0.35, 0.7))
        use = f if use is None else g.math("ADD", use, f, clamp=True)
    base = lerp(g, vec(g, (0.80, 0.80, 0.79)), (0.66, 0.66, 0.64), mul(g, use, 0.5))
    g.finish(base, g.math("ADD", 0.14, mul(g, use, 0.22)), 1.0, None)
    return m


def borracha(name):
    m, g = _new(name)
    g.finish(vec(g, (0.03, 0.03, 0.03)), 0.8, 0.0, None)
    return m


def make_all(G_class, model):
    """Material dict per asset; keys match the part tags in recepcao_modelo.py."""
    global G
    G = G_class
    B, E, A, M, L = model.BALCAO, model.ESPERA, model.ATENDENTE, model.ARMARIO, model.LONGARINA
    lam, rod = CORES["laminado"], CORES["rodape"]
    scuff_dark = (0.22, 0.22, 0.21)
    alum = metal("Aluminio escovado", (0.78, 0.78, 0.77), 0.32)
    worktop_rest = ((0.0, B["back"] - 0.12, B["work_z"]), 0.35)
    # Shared by the counter and its drawers, the cabinet and its doors (same object-space patterns).
    lam_balcao = laminado("Laminado - balcao", lam, grime_spots=(worktop_rest,))
    lam_armario = laminado("Laminado - armario", lam, grime_spots=(((-0.035, -M["depth"] / 2, 0.62), 0.12),
                                                                  ((0.035, -M["depth"] / 2, 0.62), 0.12)))
    melamina = laminado("Melamina branca - interno", (0.80, 0.79, 0.76), kick=0.0, rough=0.42)
    aco = metal("Aco zincado - corredicas", (0.66, 0.67, 0.68), 0.32)
    gaveta = {"laminado": lam_balcao, "aluminio": alum, "melamina": melamina, "aco": aco,
              "papel": papel("Papel - gaveta"), "pasta_kraft": plastico("Envelope kraft", (0.45, 0.33, 0.19), 0.8, scuffed=False),
              "pasta_azul": plastico("Pasta azul", (0.05, 0.13, 0.33), 0.35),
              "plastico": plastico("Plastico preto - gaveta", (0.03, 0.03, 0.032), 0.45),
              "madeira": plastico("Madeira do carimbo", (0.45, 0.30, 0.17), 0.6, scuffed=False),
              "caneta_corpo": plastico("Corpo da caneta", (0.80, 0.82, 0.84), 0.25, scuffed=False),
              "ponta_plastico": plastico("Cone da caneta", (0.85, 0.85, 0.83), 0.4, scuffed=False),
              "ponta": metal("Ponta da caneta", (0.75, 0.74, 0.70), 0.3),
              "caneta_azul": plastico("Caneta azul", (0.03, 0.08, 0.30), 0.3, scuffed=False),
              "caneta_preta": plastico("Tampa preta", (0.025, 0.025, 0.028), 0.35, scuffed=False)}
    porta_armario = {"laminado": lam_armario, "aluminio": alum, "aco": aco, "borracha": borracha("Feltro do batente")}
    return {
        "GavetaBalcao1": gaveta, "GavetaBalcao2": gaveta, "GavetaBalcao3": gaveta,
        "PortaArmarioEsquerda": porta_armario, "PortaArmarioDireita": porta_armario,
        "BalcaoRecepcao": {
            "laminado": lam_balcao,
            "rodape": laminado("Rodape - balcao", rod, scuff_rgb=scuff_dark, kick=0.12, rough=0.55),
            "aco": aco,
            "granito": granito("Granito - balcao", B["front"] + 0.12, B["height"]),
            "aluminio": alum, "nylon": nylon("Nylon - balcao"),
        },
        "CadeiraEspera": {
            "epoxi": epoxi("Epoxi - cadeira de espera"),
            "polipropileno": polipropileno("Polipropileno - cadeira de espera", [(0.0, -0.02, E["seat_z"])]),
            "borracha": borracha("Borracha - ponteiras"),
        },
        "CadeiraAtendente": {
            "tecido": tecido("Tecido - cadeira do atendente", -A["seat_d"] / 2, A["seat_z"]),
            "nylon": nylon("Nylon - cadeira do atendente"),
            "cromo": metal("Cromado", (0.85, 0.85, 0.84), 0.12),
        },
        "ArmarioApoio": {
            "laminado": lam_armario,
            "rodape": laminado("Rodape - armario", rod, scuff_rgb=scuff_dark, kick=0.1, rough=0.55),
            "melamina": melamina, "aco": aco, "papel": papel("Papel - resmas"),
            "polionda_azul": plastico("Polionda azul", (0.10, 0.25, 0.50), 0.55),
            "polionda_cinza": plastico("Polionda cinza", (0.30, 0.31, 0.32), 0.55),
            "copos": plastico("Copos descartaveis", (0.78, 0.78, 0.76), 0.3, scuffed=False),
        },
        "LongarinaEspera": {
            "epoxi": epoxi("Epoxi - longarina"),
            "polipropileno": polipropileno("Polipropileno - longarina",
                                           [((i - 1) * L["pitch"], -0.02, E["seat_z"]) for i in range(L["seats"])]),
            "borracha": borracha("Borracha - ponteiras"),
        },
        "EquipamentosBalcao": {
            "plastico": plastico("Plastico preto - equipamentos", (0.03, 0.03, 0.032), 0.4),
            "teclado": teclas("Teclas"),
            "mouse": plastico("Plastico fosco - mouse", (0.045, 0.045, 0.05), 0.62),
            "porta_canetas": plastico("Plastico fosco - porta-canetas", (0.06, 0.06, 0.065), 0.72),
            "caneta_corpo": plastico("Corpo da caneta", (0.80, 0.82, 0.84), 0.25, scuffed=False),
            "ponta_plastico": plastico("Cone da caneta", (0.85, 0.85, 0.83), 0.4, scuffed=False),
            "ponta": metal("Ponta da caneta", (0.75, 0.74, 0.70), 0.3),
            "caneta_preta": plastico("Tampa preta", (0.025, 0.025, 0.028), 0.35, scuffed=False),
            "caneta_vermelha": plastico("Tampa vermelha", (0.45, 0.03, 0.03), 0.35, scuffed=False),
            "lapis": plastico("Lapis amarelo", (0.75, 0.55, 0.06), 0.45, scuffed=False),
            "madeira": plastico("Madeira do lapis", (0.70, 0.55, 0.38), 0.75, scuffed=False),
            "grafite": metal("Grafite", (0.12, 0.12, 0.12), 0.45),
            "cabo": plastico("Cabo", (0.03, 0.03, 0.032), 0.5, scuffed=False),
            "gabinete": plastico("Gabinete do mini PC", (0.09, 0.09, 0.095), 0.5),
            "regua": plastico("Regua de tomadas", (0.82, 0.82, 0.80), 0.45),
            "visor": visor("Visor do telefone"),
            "borracha": borracha("Borracha - roda do mouse"),
            "tela": tela("Tela desligada"),
            "tecido": tecido("Tecido - mousepad", -1.0, 0.0),
            "bandeja": plastico("Plastico fume - bandeja", (0.09, 0.09, 0.095), 0.3),
            "papel": papel("Papel - equipamentos"),
            "pasta_kraft": plastico("Pasta kraft", (0.42, 0.30, 0.16), 0.8, scuffed=False),
            "pasta_azul": plastico("Pasta azul", (0.05, 0.13, 0.33), 0.35),
            "caneta_azul": plastico("Caneta azul", (0.03, 0.08, 0.30), 0.3, scuffed=False),
            "fichario_preto": plastico("Fichario preto", (0.03, 0.03, 0.03), 0.5),
            "fichario_azul": plastico("Fichario azul", (0.04, 0.10, 0.28), 0.5),
            "fichario_vermelho": plastico("Fichario vermelho", (0.30, 0.04, 0.04), 0.5),
            "polionda_azul": plastico("Polionda azul", (0.10, 0.25, 0.50), 0.55),
            "polionda_cinza": plastico("Polionda cinza", (0.30, 0.31, 0.32), 0.55),
        },
        "LivroRegistro": {
            "papel": papel("Papel - livro", (0.78, 0.76, 0.68)),
            "capa": plastico("Capa preta - livro", (0.025, 0.025, 0.025), 0.7),
            "capa_vermelha": plastico("Cantoneira vermelha - livro", (0.28, 0.025, 0.03), 0.6),
            "caneta_corpo": plastico("Corpo da caneta", (0.80, 0.82, 0.84), 0.25, scuffed=False),
            "ponta_plastico": plastico("Cone da caneta", (0.85, 0.85, 0.83), 0.4, scuffed=False),
            "ponta": metal("Ponta da caneta", (0.75, 0.74, 0.70), 0.3),

            "caneta_azul": plastico("Caneta azul", (0.03, 0.08, 0.30), 0.3, scuffed=False),
        },
        "Bebedouro": {
            "metal_pintado": laminado("Metal pintado - bebedouro", CORES["metal_pintado"], kick=0.2, rough=0.4),
            "inox": metal("Inox - bebedouro", (0.72, 0.72, 0.71), 0.3),
            "inox_escuro": metal("Inox - cuba", (0.55, 0.55, 0.54), 0.38),
            "cromo": metal("Cromado", (0.85, 0.85, 0.84), 0.12),
            "plastico": plastico("Plastico preto - bebedouro", (0.03, 0.03, 0.03), 0.5),
            "grade": grade("Grade - bebedouro"),
            "papel": papel("Etiqueta - bebedouro", (0.75, 0.75, 0.72)),
        },
        "LuminariaSobrepor": {
            "chapa": plastico("Chapa pintada - luminaria", (0.86, 0.86, 0.84), 0.35),
            "tampa": plastico("Tampa - luminaria", (0.78, 0.78, 0.76), 0.45),
            "parafuso": metal("Aco zincado", (0.70, 0.70, 0.68), 0.35),
        },
        # Light parts and glass are not baked: these only name the FBX material slots.
        "LuminariaSobrepor_Luz": {"difusor": tela("difusor"), "tubo": tela("tubo")},
        "LuminariaExterna": {
            "aluminio_pintado": plastico("Aluminio pintado grafite", (0.12, 0.12, 0.12), 0.45),
            "parafuso": metal("Aco zincado", (0.70, 0.70, 0.68), 0.35),
        },
        "LuminariaExterna_Luz": {"difusor": tela("difusor"), "tubo": tela("tubo")},
        "BatenteEntrada": {
            "aluminio": metal("Aluminio anodizado - batente", (0.70, 0.71, 0.72), 0.38),
            "inox": metal("Inox escovado - porta", (0.72, 0.72, 0.71), 0.3),
            "inox_escuro": metal("Inox escuro - porta", (0.32, 0.32, 0.32), 0.4),
            "selante": plastico("Silicone cinza", (0.55, 0.55, 0.53), 0.5, scuffed=False),
            "granito": granito("Granito - soleira", -10.0, -10.0),   # no leaning strip on a sill
        },
        "PortaVidroFixos": {
            "inox": metal("Inox escovado - porta", (0.72, 0.72, 0.71), 0.3),
            "inox_escuro": metal("Inox escuro - porta", (0.32, 0.32, 0.32), 0.4),
            "borracha": borracha("Borracha - prendedor"),
        },
        "PortaVidroFolha": {
            "inox": metal("Inox escovado - porta", (0.72, 0.72, 0.71), 0.3),
            "inox_escuro": metal("Inox escuro - porta", (0.32, 0.32, 0.32), 0.4),
            "borracha": borracha("Borracha - rasgo da chave"),
            "puxador": inox_puxador("Inox polido - puxador",
                                    [(model.PORTA["leaf_w"] - model.PORTA["pivot_in"] - model.PORTA["handle_off"],
                                      sy * 0.07, model.PORTA["handle_z"]) for sy in (-1, 1)]),
        },
        "PortaVidroVidro": {"vidro": tela("vidro")},
        # First puzzle hardware: not baked, these only name the single FBX slot of each part (Unity sets them).
        "CadeadoCorpo": {"latao": tela("latao")}, "CadeadoArco": {"aco": tela("aco")},
        "CadeadoDisco": {"disco": tela("disco")}, "FechoBase": {"aco": tela("aco")}, "FechoAba": {"aco": tela("aco")}, "FechoSuporte": {"aco": tela("aco")},
        "ChaveMetal": {"niquel": tela("niquel")}, "ChaveEtiqueta": {"etiqueta": tela("etiqueta")},
        # Restricted circulation door: satin-painted steel (graphite frame, light grey leaves), brushed stainless
        # hardware; wear by the lever and along the bottom of the leaves.
        "PortaCirculacaoFixos": {
            "marco": laminado("Pintura acetinada - marco circulacao", (0.29, 0.30, 0.31), kick=0.18, rough=0.42),
            "folha": laminado("Pintura acetinada - folha secundaria", (0.60, 0.62, 0.62), kick=0.22, rough=0.40),
            "granito": granito("Granito - soleira circulacao", -10.0, -10.0),
            "inox": metal("Inox escovado - ferragens", (0.74, 0.74, 0.73), 0.30),
            "inox_escuro": metal("Inox escuro - ferragens", (0.36, 0.36, 0.36), 0.40),
            "borracha": borracha("Borracha - batedor"),
        },
        "PortaCirculacaoFolha": {
            "folha": laminado("Pintura acetinada - folha ativa", (0.60, 0.62, 0.62), kick=0.22, rough=0.40,
                              grime_spots=((((model.PORTA_CIRC["active"] + (-model.PORTA_CIRC["jx"] + model.PORTA_CIRC["gap"]) - model.PORTA_CIRC["pivot"][0] - 0.065), (model.PORTA_CIRC["leaf_back"] - model.PORTA_CIRC["pivot"][1]) - 0.01, 1.0), 0.13), (((model.PORTA_CIRC["active"] + (-model.PORTA_CIRC["jx"] + model.PORTA_CIRC["gap"]) - model.PORTA_CIRC["pivot"][0] - 0.065), (model.PORTA_CIRC["face"] - model.PORTA_CIRC["pivot"][1]) + 0.01, 1.0), 0.13),
                                           (((model.PORTA_CIRC["active"] + (-model.PORTA_CIRC["jx"] + model.PORTA_CIRC["gap"]) - model.PORTA_CIRC["pivot"][0] - 0.065) - 0.06, 0.0, 1.12), 0.22))),
            "inox": metal("Inox escovado - ferragens", (0.74, 0.74, 0.73), 0.30),
            "inox_escuro": metal("Inox escuro - ferragens", (0.36, 0.36, 0.36), 0.40),
            "fenda": tela("Fenda escura - cilindro"),
        },
        # Reception-archive door: the circulation door's materials (its own copies, so its bake stays separate).
        "PortaArquivoFixos": {
            "marco": laminado("Pintura acetinada - marco arquivo", (0.29, 0.30, 0.31), kick=0.18, rough=0.42),
            "folha": laminado("Pintura acetinada - folha secundaria arquivo", (0.60, 0.62, 0.62), kick=0.22, rough=0.40),
            "granito": granito("Granito - soleira arquivo", -10.0, -10.0),
            "inox": metal("Inox escovado - ferragens arquivo", (0.74, 0.74, 0.73), 0.30),
            "inox_escuro": metal("Inox escuro - ferragens arquivo", (0.36, 0.36, 0.36), 0.40),
            "borracha": borracha("Borracha - batedor arquivo"),
        },
        "PortaArquivoFolha": {
            "folha": laminado("Pintura acetinada - folha ativa arquivo", (0.60, 0.62, 0.62), kick=0.22, rough=0.40),
            "inox": metal("Inox escovado - ferragens arquivo", (0.74, 0.74, 0.73), 0.30),
            "inox_escuro": metal("Inox escuro - ferragens arquivo", (0.36, 0.36, 0.36), 0.40),
            "fenda": tela("Fenda escura - cilindro arquivo"),
        },
        "PortaArquivoTrinco": {
            "inox": metal("Inox escovado - trinco", (0.74, 0.74, 0.73), 0.28),
            "inox_escuro": metal("Inox escuro - trinco", (0.36, 0.36, 0.36), 0.40),
        },
        "Lixeira": {
            "inox": metal("Inox - lixeira", (0.70, 0.70, 0.69), 0.34),
            "plastico": plastico("Plastico preto - lixeira", (0.03, 0.03, 0.03), 0.5),
        },
    }
