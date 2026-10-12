# Procedural materials of the bathroom fittings, built with the reception's material helpers
# (Blender/Recepcao/scripts/recepcao_materiais.py) on the autopsy table's node helper G.
import os
import types

_HERE = os.path.dirname(os.path.abspath(__file__))
_RM = os.path.join(_HERE, "..", "..", "Recepcao", "scripts", "recepcao_materiais.py")


def load_base(G_class):
    ns = {"__name__": "recepcao_materiais", "__file__": _RM}
    exec(compile(open(_RM, encoding="utf-8").read(), _RM, "exec"), ns)
    ns["G"] = G_class
    return types.SimpleNamespace(**ns)


def make_all(G_class, model):
    """Material dict per asset; keys match the part tags in wc_modelo.py."""
    rm = load_base(G_class)
    louca = rm.plastico("Louca vitrificada", (0.92, 0.92, 0.90), 0.12, scuffed=False)
    cromo = rm.metal("Cromado - WC", (0.86, 0.86, 0.85), 0.10)
    inox = rm.metal("Inox escovado - WC", (0.72, 0.72, 0.71), 0.30)
    branco = rm.plastico("Plastico branco - dispensadores", (0.88, 0.88, 0.86), 0.45)
    janela = rm.plastico("Visor fume", (0.10, 0.11, 0.12), 0.15, scuffed=False)
    cinza = rm.plastico("Plastico cinza", (0.35, 0.36, 0.37), 0.5)
    papel = rm.papel("Papel - WC", (0.90, 0.90, 0.86))
    selante = rm.plastico("Silicone branco - WC", (0.86, 0.86, 0.83), 0.35)
    pvc = rm.plastico("PVC branco - sifao", (0.86, 0.86, 0.84), 0.4)
    fenda = rm.tela("Fenda escura")
    P = model.PORTA_WC
    hx = -P["leaf_w"] + 0.065 + (P["raw"] / 2 - P["jamb"] - P["gap"] - P["pivot"][0])   # handle, leaf frame
    return {
        "VasoSanitario": {"louca": louca, "cromo": cromo, "inox": inox,
                          "assento": rm.plastico("Assento", (0.93, 0.93, 0.92), 0.32), "selante": selante},
        "Lavatorio": {"louca": louca, "cromo": cromo, "inox": inox, "pvc": pvc, "selante": selante},
        # Moving buttons: not baked, chrome set in Unity (these only name the FBX slots).
        "VasoBotao": {"cromo": rm.tela("cromo")}, "LavatorioBotao": {"cromo": rm.tela("cromo")},
        "Ralo": {"inox": inox, "fenda": fenda, "pvc_cinza": rm.plastico("PVC cinza - ralo", (0.35, 0.36, 0.37), 0.5)},
        "EscovaSanitaria": {"plastico_branco": branco, "cinza": cinza},
        "Exaustor": {"plastico_branco": branco, "fenda": fenda, "cinza": cinza},
        "Espelho": {"espelho": rm.tela("espelho"), "borda": rm.tela("borda"), "cromo": rm.tela("cromo")},   # Unity sets these
        "Saboneteira": {"plastico_branco": branco, "janela": janela, "cinza": cinza},
        "ToalheiroPapel": {"plastico_branco": branco, "janela": janela, "cinza": cinza, "papel": papel},
        "PapeleiraRolao": {"plastico_branco": branco, "janela": janela, "cinza": cinza, "papel": papel},
        "BarraApoio": {"inox": inox},
        "PortaWCFixos": {"laca": rm.laminado("Laca - batente WC", (0.84, 0.83, 0.79), kick=0.2, rough=0.4),
                         "granito": rm.granito("Granito - soleira WC", -10.0, -10.0), "cromo": cromo,
                         "borracha": rm.borracha("Borracha - batedor")},
        "PortaWCFolha": {"folha": rm.laminado("Laminado - porta WC", (0.74, 0.73, 0.69), kick=0.3, rough=0.45,
                                              grime_spots=(((hx, -0.03, 1.0), 0.12), ((hx, 0.03, 1.0), 0.12),
                                                           ((hx, 0.0, 0.25), 0.25))),
                         "inox": inox, "inox_escuro": rm.metal("Inox escuro - WC", (0.30, 0.30, 0.30), 0.4)},
    }
