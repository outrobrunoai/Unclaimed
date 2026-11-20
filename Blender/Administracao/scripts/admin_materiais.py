# Procedural materials of the archive / office, used only to bake each asset's Unity textures. Built from the
# reception's material functions (Blender/Recepcao/scripts/recepcao_materiais.py) so the colours, wear and the
# autopsy table's baker stay one family. One shared dictionary: every asset looks its part tags up in it.
import os, types

HERE = os.path.dirname(os.path.abspath(__file__))


def _load(path, module):
    ns = {"__name__": module, "__file__": path}
    exec(compile(open(path, encoding="utf-8").read(), path, "exec"), ns)
    return types.SimpleNamespace(**ns)


# Painted sheet steel and laminates of the room (sRGB-ish linear albedo, like the reception's CORES).
CORES = dict(
    tampo=(0.56, 0.54, 0.49),          # warm grey laminate ("argila"), the desk's and bench's tops
    borda=(0.20, 0.19, 0.18),          # PVC edge band
    painel=(0.46, 0.45, 0.41),         # modesty panel, pedestal, drawer fronts
    estrutura=(0.05, 0.052, 0.055),    # graphite epoxy frames
    chapa=(0.50, 0.50, 0.46),          # old beige-grey filing cabinet
    chapa_cinza=(0.40, 0.42, 0.42),    # belongings locker
    estante=(0.47, 0.48, 0.48),        # shelving
)


def _tnt(RM, name, rgb):
    """Spunbond polypropylene (TNT) of hospital belongings bags: matt white, fibrous, greyer where it was handled."""
    m, g = RM._new(name)
    p = g.obj()
    base = g.color(g.noise(p, 25.0, 3.0, 0.55), tuple(c * 0.93 for c in rgb), rgb)
    base = RM.lerp(g, base, tuple(c * 0.82 for c in rgb), RM.mul(g, RM.low(g, 0.05), 0.6))
    h = g.math("ADD", g.noise(p, 800.0, 1.0, 0.5), RM.mul(g, g.voronoi(p, 260.0), 0.8))
    g.finish(base, 0.9, 0.0, h, bump_strength=0.12, bump_dist=0.0003)
    return m


def make_all(G_class, model):
    RM = _load(os.path.join(HERE, "..", "..", "Recepcao", "scripts", "recepcao_materiais.py"), "recepcao_materiais")
    RM.laminado.__globals__["G"] = G_class   # the module functions read G from their own globals
    lam, met, pla, pap = RM.laminado, RM.metal, RM.plastico, RM.papel
    C = CORES
    shared = {
        "tampo": lam("Laminado - tampo administracao", C["tampo"], kick=0.0, rough=0.48),
        "borda": lam("Fita de borda - administracao", C["borda"], kick=0.0, rough=0.5),
        "painel": lam("Laminado - painel administracao", C["painel"], kick=0.3, rough=0.5),
        "estrutura": RM.epoxi("Epoxi - estrutura administracao"),
        # Nina's desk: the reception counter's laminate and plinth, worn where the hands rest (desk frame).
        "laminado": lam("Laminado - mesa administracao", RM.CORES["laminado"], grime_spots=(((0.12, 0.14, 0.76), 0.32),)),
        "rodape": lam("Rodape - mesa administracao", RM.CORES["rodape"], scuff_rgb=(0.22, 0.22, 0.21), kick=0.12, rough=0.55),
        "nylon": RM.nylon("Nylon - mesa administracao"),
        "chapa": lam("Aco pintado - arquivo", C["chapa"], scuff_rgb=(0.30, 0.30, 0.28), kick=0.25, rough=0.42),
        "chapa_escura": lam("Aco pintado - rodape arquivo", (0.12, 0.12, 0.12), kick=0.1, rough=0.5),
        "chapa_interna": lam("Aco pintado - interior arquivo", (0.36, 0.36, 0.34), kick=0.0, rough=0.5),
        "chapa_cinza": lam("Aco pintado - armario de pertences", C["chapa_cinza"], scuff_rgb=(0.22, 0.23, 0.23), kick=0.3, rough=0.42),
        "estante": lam("Aco pintado - estante", C["estante"], kick=0.15, rough=0.45),
        "aco": met("Aco zincado - administracao", (0.66, 0.67, 0.68), 0.32),
        "aluminio": met("Aluminio escovado - administracao", (0.76, 0.76, 0.75), 0.34),
        "melamina": lam("Melamina branca - gavetas", (0.80, 0.79, 0.76), kick=0.0, rough=0.42),
        "borracha": RM.borracha("Borracha - administracao"),
        "fenda": RM.tela("Fenda escura - administracao"),
        "tela": RM.tela("Tela desligada - administracao"),
        "visor": RM.visor("Visor - impressora"),
        "teclado": RM.teclas("Teclas - administracao"),
        "tecido": RM.tecido("Tecido - mousepad administracao", -1.0, 0.0),
        "mouse": pla("Plastico fosco - mouse administracao", (0.045, 0.045, 0.05), 0.62),
        "plastico": pla("Plastico preto - administracao", (0.03, 0.03, 0.032), 0.42),
        "gabinete": pla("Gabinete - administracao", (0.09, 0.09, 0.095), 0.5),
        "cabo": pla("Cabo - administracao", (0.03, 0.03, 0.032), 0.5, scuffed=False),
        "led": pla("Led verde", (0.05, 0.6, 0.12), 0.2, scuffed=False),
        "bandeja": pla("Acrilico fume - bandeja", (0.09, 0.09, 0.095), 0.25),
        "porta_canetas": pla("Plastico fosco - porta-canetas administracao", (0.06, 0.06, 0.065), 0.72),
        "caneta_corpo": pla("Corpo da caneta", (0.80, 0.82, 0.84), 0.25, scuffed=False),
        "ponta_plastico": pla("Cone da caneta", (0.85, 0.85, 0.83), 0.4, scuffed=False),
        "ponta": met("Ponta da caneta", (0.75, 0.74, 0.70), 0.3),
        "caneta_azul": pla("Caneta azul", (0.03, 0.08, 0.30), 0.3, scuffed=False),
        "caneta_preta": pla("Tampa preta", (0.025, 0.025, 0.028), 0.35, scuffed=False),
        "caneta_vermelha": pla("Tampa vermelha", (0.45, 0.03, 0.03), 0.35, scuffed=False),
        "lapis": pla("Lapis amarelo", (0.75, 0.55, 0.06), 0.45, scuffed=False),
        "madeira": pla("Madeira clara", (0.70, 0.55, 0.38), 0.75, scuffed=False),
        "grafite": met("Grafite", (0.12, 0.12, 0.12), 0.45),
        "caneca": pla("Ceramica - caneca", (0.62, 0.66, 0.70), 0.15, scuffed=False),
        "vaso": pla("Vaso de barro pequeno", (0.42, 0.22, 0.12), 0.8, scuffed=False),
        "terra": pla("Terra do vaso", (0.10, 0.07, 0.05), 0.95, scuffed=False),
        "cacto": pla("Cacto", (0.12, 0.25, 0.10), 0.6, scuffed=False),
        "post_it": pap("Bloco amarelo", (0.85, 0.78, 0.30)),
        "papel": pap("Papel - administracao"),
        "kraft": pla("Papelao kraft", (0.42, 0.31, 0.18), 0.85, scuffed=False),
        "kraft_velho": pla("Papelao kraft velho", (0.33, 0.25, 0.16), 0.9, scuffed=True),
        "pasta_verde": pla("Pasta suspensa verde", (0.18, 0.28, 0.16), 0.75, scuffed=False),
        "pasta_azul": pla("Pasta azul - administracao", (0.05, 0.13, 0.33), 0.35),
        "pasta_cinza": pla("Pasta cinza - Nina", (0.28, 0.29, 0.30), 0.45),
        "visor_pasta": pla("Visor transparente", (0.55, 0.56, 0.56), 0.15, scuffed=False),
        "polionda_azul": pla("Polionda azul - administracao", (0.10, 0.25, 0.50), 0.55),
        "polionda_cinza": pla("Polionda cinza - administracao", (0.30, 0.31, 0.32), 0.55),
        "az_preto": pla("Pasta AZ preta", (0.035, 0.035, 0.04), 0.55),
        "az_cinza": pla("Pasta AZ cinza", (0.20, 0.21, 0.22), 0.55),
        "az_azul": pla("Pasta AZ azul", (0.05, 0.11, 0.26), 0.55),
        "az_vermelho": pla("Pasta AZ vermelha", (0.32, 0.05, 0.04), 0.55),
        "manual_branco": pap("Manual branco", (0.80, 0.80, 0.77)),
        "manual_azul": pla("Manual azul", (0.12, 0.24, 0.45), 0.5, scuffed=False),
        "aba": pla("Aba amarela", (0.80, 0.65, 0.12), 0.5, scuffed=False),
        "regua": pla("Regua de tomadas - bancada", (0.82, 0.82, 0.80), 0.45),
        "carregador": pla("Carregador branco", (0.85, 0.85, 0.83), 0.35),
        "fita": pla("Fita de lacre vermelha", (0.45, 0.04, 0.04), 0.35, scuffed=False),
        "impressora": pla("Plastico cinza claro - impressora", (0.62, 0.63, 0.62), 0.42),
        "impressora_escura": pla("Plastico grafite - impressora", (0.09, 0.09, 0.10), 0.45),
        "saco": pla("Polietileno branco - bolsa", (0.74, 0.74, 0.71), 0.35, scuffed=False),
        "barbante": pla("Barbante", (0.62, 0.55, 0.42), 0.9, scuffed=False),
        "etiqueta": pap("Etiqueta de papel", (0.82, 0.80, 0.70)),
        "gravador": pla("Plastico grafite - gravador", (0.075, 0.077, 0.082), 0.38),
        "gravador_face": pla("Painel preto brilhante - gravador", (0.02, 0.02, 0.022), 0.18, scuffed=False),
        "prata": met("Aluminio acetinado - gravador", (0.62, 0.62, 0.63), 0.35),
        "led": pla("LED vermelho apagado", (0.25, 0.03, 0.03), 0.15, scuffed=False),
        "tnt": _tnt(RM, "TNT branco - bolsa", (0.80, 0.80, 0.77)),
        "tnt_borda": _tnt(RM, "TNT desfiado - bolsa", (0.74, 0.73, 0.69)),
        "costura_tnt": pla("Costura do TNT", (0.70, 0.70, 0.67), 0.9, scuffed=False),
        "cordao": pla("Cordao de algodao", (0.80, 0.79, 0.74), 0.95, scuffed=False),
        "linha_remendo": pla("Linha azul do remendo", (0.03, 0.045, 0.11), 0.85, scuffed=False),
        "ilhos": met("Ilhos de latao", (0.70, 0.62, 0.42), 0.35),
        "botao_vermelho": pla("Botao vermelho", (0.55, 0.04, 0.04), 0.3, scuffed=False),
        "lacre": pla("Lacre vermelho", (0.55, 0.05, 0.05), 0.3, scuffed=False),
    }
    return {asset: shared for asset in model.ASSETS}
