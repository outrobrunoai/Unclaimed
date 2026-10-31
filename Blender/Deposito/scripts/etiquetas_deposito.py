"""Printed labels of the storeroom (shelf plaques, shelf-fold cards, bin cards, storage-box cards), painted into one atlas
with the packaging painter, plus Etiquetas.json for the Unity builder:
python3 Blender/Deposito/scripts/etiquetas_deposito.py
Ids are the ones Layout.json places. Text says what the place holds, the minimum stock and, on cards of dated material,
that the validity is read on the box (as the SMS-SP almoxarifado manual asks: product, quantity, validity)."""
import json, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import impressos_deposito as P

ROOT = os.path.normpath(os.path.join(HERE, '../../..'))
PNG = ROOT + '/Assets/Modelos/Deposito/Texturas/Etiquetas_Deposito.png'
JSON = ROOT + '/Assets/UnclaimedPrototype/Deposito/Etiquetas.json'
PX_PER_M = 3600
ATLAS = 2048

# kind -> (width, height) in metres
KIND = {'placa': (0.40, 0.026), 'lip': (0.105, 0.025), 'bin': (0.096, 0.028), 'org': (0.13, 0.062)}

# id -> (kind, title, second line)
TEXTO = {
    'placa_epi_luvas': ('placa', 'EPI · LUVAS E MÁSCARAS', ''),
    'placa_epi': ('placa', 'EPI · AVENTAIS · PFF2 · ÓCULOS', ''),
    'placa_necropsia': ('placa', 'MATERIAL DE NECROPSIA', ''),
    'placa_identificacao': ('placa', 'IDENTIFICAÇÃO E LACRES', ''),
    'placa_sacos': ('placa', 'SACOS PARA CADÁVER', ''),
    'placa_lencois': ('placa', 'LENÇÓIS DESCARTÁVEIS', ''),
    'placa_quimicos': ('placa', 'FORMOL · MANTER NA BANDEJA', ''),
    'placa_manutencao': ('placa', 'MANUTENÇÃO', ''),
    'lip_luva_m': ('lip', 'LUVA PROCED. M', 'MÍN. 6 CX · VAL. NA CAIXA'),
    'lip_luva_nitrilica': ('lip', 'LUVA NITRÍLICA M', 'MÍN. 4 CX · VAL. NA CAIXA'),
    'lip_luva_g': ('lip', 'LUVA PROCED. G', 'MÍN. 4 CX · VAL. NA CAIXA'),
    'lip_mascara': ('lip', 'MÁSCARA CIRÚRGICA', 'MÍN. 4 CX · VAL. NA CAIXA'),
    'lip_pff2': ('lip', 'RESPIRADOR PFF2', 'MÍN. 3 CX · VAL. NA CAIXA'),
    'lip_fio': ('lip', 'FIO NYLON 0', 'MÍN. 2 CX · VAL. NA CAIXA'),
    'lip_lamina': ('lip', 'LÂMINA BISTURI Nº 22', 'MÍN. 4 CX · VAL. NA CAIXA'),
    'lip_lencol': ('lip', 'LENÇOL DESCART. 70 cm', 'MÍN. 6 ROLOS'),
    'lip_lampada': ('lip', 'LÂMPADA LED T8 60 cm', 'MÍN. 2 UN'),
    'bin_lamina': ('bin', 'LÂMINA Nº 22', 'USO DIÁRIO'),
    'bin_fio': ('bin', 'FIO NYLON 0', 'USO DIÁRIO'),
    'bin_vazio': ('bin', 'LÂMINA Nº 24', 'USO DIÁRIO'),
    'bin_eletrica': ('bin', 'ELÉTRICA', 'CONECTOR · FITA ISOL.'),
    'bin_hidraulica': ('bin', 'HIDRÁULICA', 'VEDA-ROSCA · REPARO'),
    'bin_fixacao': ('bin', 'FIXAÇÃO', 'PARAFUSO · BUCHA'),
    'bin_reposicao': ('bin', 'REPOSIÇÃO', 'FERRAMENTAS'),
    'org_oculos': ('org', 'ÓCULOS DE PROTEÇÃO', 'LAVAR APÓS O USO'),
    'org_toucas': ('org', 'TOUCAS DESCARTÁVEIS', 'PCT C/ 100'),
    'org_propes': ('org', 'PROPÉS DESCARTÁVEIS', 'PCT C/ 100'),
    'org_etiquetas': ('org', 'ETIQUETAS DE IDENTIFICAÇÃO', 'ROLOS ADESIVOS'),
    'org_lacres': ('org', 'LACRES PLÁSTICOS', 'P/ SACO E GAVETA'),
    'org_sacos_visceras': ('org', 'SACOS P/ VÍSCERAS', 'PLÁSTICO TRANSPARENTE'),
    'org_pulseiras': ('org', 'PULSEIRAS DE IDENTIFICAÇÃO', 'ADULTO'),
    'org_etiquetas_saco': ('org', 'ETIQUETAS P/ SACO', 'FIXAR NO ZÍPER'),
    'org_reserva': ('org', 'LUVA NITRÍLICA G', 'RESERVA'),
}

BLUE = (28, 62, 128)
BLACK = (30, 32, 38)


def pack():
    """Shelf packing in metres on a square canvas, rows sorted by height."""
    side = ATLAS / PX_PER_M
    pad = 0.004
    rects, x, y, row = {}, pad, pad, 0.0
    for lid in sorted(TEXTO, key=lambda i: -KIND[TEXTO[i][0]][1]):
        w, h = KIND[TEXTO[lid][0]]
        if x + w + pad > side:
            x, y, row = pad, y + row + pad, 0.0
        rects[lid] = (x, side - (y + h) - pad, w, h)   # Canvas rects: origin bottom-left
        x += w + pad
        row = max(row, h)
    assert y + row + pad < side, 'atlas too small'
    return rects, side


def paint(c, lid):
    kind, title, sub = TEXTO[lid]
    W, H = KIND[kind]
    c.board(lid, (238, 236, 228) if kind != 'lip' else (240, 236, 222), kraft=False)
    if kind == 'placa':
        # laminated plaque: blue band on the left, the category in bold
        c.fill_rect(lid, 0, 0, 0.022, H, BLUE)
        c.fill_rect(lid, W - 0.006, 0, W, H, BLUE)
        c.text(lid, [(title, 0.022 + (W - 0.028) / 2, H * 0.50, H * 0.62, P.SANS_C, W - 0.05)], BLACK, erode=0.05, fade=0.96)
        c.wear(lid, edge=0.0015, amount=0.35)
    elif kind == 'lip':
        c.text(lid, [(title, W / 2, H * 0.64, H * 0.40, P.SANS_C, W * 0.92),
                     (sub, W / 2, H * 0.22, H * 0.22, P.SANS_B, W * 0.92)], BLACK, erode=0.12, fade=0.93)
        c.wear(lid, edge=0.002, amount=0.6)
    elif kind == 'bin':
        c.fill_rect(lid, 0, H - 0.006, W, H, BLUE)
        c.text(lid, [(title, W / 2, H * 0.50, H * 0.34, P.SANS_C, W * 0.90),
                     (sub, W / 2, H * 0.16, H * 0.18, P.SANS_B, W * 0.90)], BLACK, erode=0.12, fade=0.93)
        c.wear(lid, edge=0.002, amount=0.5)
    else:
        c.fill_rect(lid, 0, H - 0.012, W, H, BLUE)
        c.text(lid, [('ALMOXARIFADO', W / 2, H - 0.006, 0.0075, P.SANS_B, W * 0.8)], (236, 236, 232), erode=0.05, fade=0.97)
        c.text(lid, [(title, W / 2, H * 0.50, H * 0.24, P.SANS_C, W * 0.90),
                     (sub, W / 2, H * 0.19, H * 0.15, P.SANS_B, W * 0.90)], BLACK, erode=0.1, fade=0.93)
        c.wear(lid, edge=0.002, amount=0.45)


def main():
    rects, side = pack()
    c = P.Canvas(rects, side, ATLAS, seed=41, bg=(236, 234, 226))
    for lid in rects:
        paint(c, lid)
    os.makedirs(os.path.dirname(PNG), exist_ok=True)
    c.img.save(PNG)
    out = []
    for lid, (x, y, w, h) in rects.items():
        x0, y0, x1, y1 = c.box(lid)
        out.append({'id': lid, 'tipo': TEXTO[lid][0], 'rect': [x0, y0, x1 - x0, y1 - y0], 'tamanho': [w, h]})
    with open(JSON, 'w', encoding='utf-8') as f:
        json.dump({'atlas': [ATLAS, ATLAS], 'etiquetas': out}, f, ensure_ascii=False, indent=1)
    print(len(out), 'etiquetas ->', PNG)


if __name__ == '__main__':
    main()
