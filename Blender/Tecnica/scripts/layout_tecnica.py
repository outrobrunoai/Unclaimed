"""Layout of the technical room for the Unity builder: python3 Blender/Tecnica/scripts/layout_tecnica.py
Reads Blender/Tecnica/revisao/montagem/<Asset>.json (written by gerar.py: door devices, labels and displays in asset
coordinates, Blender axes) and writes Assets/UnclaimedPrototype/Tecnica/Layout.json in Unity coordinates:
  pecas: world placements (pai "") and door devices parented to their board (local position, yaw);
  dispositivos: initial state of the P02 / cold-board devices (selector turn in degrees clockwise seen from the front,
                lens colour and whether it is lit) - the puzzle logic comes later and drives the same objects;
  etiquetas: atlas labels (Etiquetas.json) on their parent, face towards local -Z, pitch 90 = lying face up;
  visores: emissive display quads (texture in Assets/Modelos/Tecnica/Texturas);
  documentos / reaproveitados: D03 and the reused corridor prefabs.
Room "Sala tecnica" R(-4.5, 5.75, 3.25, 9): finished north wall face z 8.875, west wall x -4.375, south wall z 5.875."""
import json, os

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '../../..'))
MONT = ROOT + '/Blender/Tecnica/revisao/montagem'
OUT = ROOT + '/Assets/UnclaimedPrototype/Tecnica/Layout.json'
NORTE, OESTE, SUL = 8.875, -4.375, 5.875
LABEL_OUT = 0.0006       # labels sit this far in front of the face they are laid on

PECAS = [('Quadro geral', 'QuadroGeral', -3.00), ('Painel de contingencia', 'PainelContingencia', -1.95),
         ('Comando da camara fria', 'QuadroCamaraFria', -1.05), ('Infraestrutura eletrica', 'Infraestrutura', 0.0),
         ('Bancada de manutencao', 'Bancada', 2.35)]
GIRO = {'Seletor - refrigeracao': 45.0, 'Seletor - luz_geral': 45.0, 'Seletor - exame': -45.0}
LENTE = {'Sinaleiro - refrigeracao': ('verde', True), 'Sinaleiro - luz_geral': ('verde', True), 'Sinaleiro - exame': ('verde', False),
         'Sinaleiro - ligado': ('verde', True), 'Sinaleiro - degelo': ('ambar', False), 'Sinaleiro - alarme': ('vermelho', False)}
VISOR_TEX = {'Visor - contingencia': 'Visor_Contingencia.png', 'Visor - camara fria': 'Visor_CamaraFria.png'}


def r(v):
    return [round(c, 5) for c in v]


def main():
    pecas, disp, etq, vis = [], [], [], []
    for nome, prefab, x in PECAS:
        pecas.append({'nome': nome, 'prefab': prefab, 'pai': '', 'pos': r((x, 0.0, NORTE)), 'yaw': 0.0})
        p = f'{MONT}/{prefab}.json'
        if not os.path.exists(p):
            continue
        info = json.load(open(p))
        for kind, dev, dx, dz, dy in info.get('devices', []):
            pecas.append({'nome': dev, 'prefab': kind, 'pai': nome, 'pos': r((dx, dz, dy)), 'yaw': 0.0})
            if kind == 'Sinaleiro':
                cor, aceso = LENTE[dev]
                pecas.append({'nome': dev + ' - lente', 'prefab': 'SinaleiroLente', 'pai': dev, 'pos': [0.0, 0.0, 0.0], 'yaw': 0.0})
                disp.append({'nome': dev, 'tipo': 'sinaleiro', 'cor': cor, 'aceso': aceso})
            else:
                disp.append({'nome': dev, 'tipo': 'seletor_chave' if kind == 'SeletorChave' else 'seletor', 'giro': GIRO[dev],
                             'travado': kind == 'SeletorChave'})
        for lid, dx, dz, dy, *pitch in info.get('labels', []):
            pitch = pitch[0] if pitch else 0.0
            pos = (dx, dz + LABEL_OUT, dy) if pitch > 45 else (dx, dz, dy - LABEL_OUT)
            etq.append({'id': lid, 'pai': nome, 'pos': r(pos), 'yaw': 0.0, 'pitch': pitch})
        for vn, dx, dz, dy, (w, h) in info.get('displays', []):
            vis.append({'nome': vn, 'pai': nome, 'pos': r((dx, dz, dy - LABEL_OUT * 0.5)), 'tamanho': [w, h], 'textura': VISOR_TEX[vn]})
    docs = [{'id': 'manutencao_provisoria', 'pai': '', 'pos': [-0.60, 1.42, round(NORTE - 0.0012, 4)], 'tilt': 0.0, 'yaw': 0.0}]
    # reused corridor kit: extinguisher on the west wall clear of the open door leaf (which lies along that wall up to
    # z 6.73), its sign above it, and an emergency block over the door on the room side
    reuso = [{'nome': 'Extintor - sala tecnica', 'prefab': 'Extintor', 'pos': [OESTE, 0.0, 7.40], 'yaw': -90.0},
             {'nome': 'Placa extintor - sala tecnica', 'prefab': 'Placa SEG_Extintor', 'pos': [OESTE, 1.85, 7.40], 'yaw': -90.0},
             {'nome': 'Bloco de emergencia - sala tecnica', 'prefab': 'Bloco de emergencia', 'pos': [-3.50, 2.42, SUL], 'yaw': 180.0}]
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, 'w', encoding='utf-8') as f:
        json.dump({'pecas': pecas, 'dispositivos': disp, 'etiquetas': etq, 'visores': vis, 'documentos': docs, 'reaproveitados': reuso},
                  f, ensure_ascii=False, indent=1)
    print(len(pecas), 'pecas,', len(etq), 'etiquetas,', len(disp), 'dispositivos ->', OUT)


if __name__ == '__main__':
    main()
