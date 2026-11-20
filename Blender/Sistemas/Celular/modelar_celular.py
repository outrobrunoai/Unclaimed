# Modela o celular do jornalista e exporta para a Unity.
#   blender -b -P modelar_celular.py
# Saídas: Blender/Sistemas/Celular/celular.blend e Assets/Prefabs/Sistemas/Celular/Celular.fbx
#
# Medidas de um celular comum (m): 0,072 x 0,152 x 0,0085. Em pé: largura em X, altura em Z, espessura em Y.
# A tela fica na face -Y (a frente no Blender). Objetos exportados:
#   Celular      corpo (materiais Corpo, Vidro, Lente, Flash, Botao)
#   Tela         plano da tela, UV 0..1 (a Unity desenha a interface e o visor da câmera nele)
#   PontoFlash   vazio no LED do flash (de onde sai a luz)
#   PontoLente   vazio na lente principal
import math
import os

import bmesh
import bpy
from mathutils import Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.abspath(os.path.join(AQUI, "..", "..", ".."))
FBX = os.path.join(RAIZ, "Assets", "Prefabs", "Sistemas", "Celular", "Celular.fbx")
BLEND = os.path.join(AQUI, "celular.blend")

L, A, E = 0.072, 0.152, 0.0085      # largura, altura, espessura
RAIO = 0.0095                        # canto arredondado
TELA_L, TELA_A, TELA_RAIO = 0.0665, 0.1455, 0.0075


def limpar():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    cena = bpy.context.scene
    cena.unit_settings.system = 'METRIC'
    cena.unit_settings.scale_length = 1.0


def material(nome, cor, metal=0.0, aspereza=0.5, emissao=None):
    m = bpy.data.materials.new(nome)
    m.use_nodes = True
    p = m.node_tree.nodes["Principled BSDF"]
    p.inputs["Base Color"].default_value = (*cor, 1.0)
    p.inputs["Metallic"].default_value = metal
    p.inputs["Roughness"].default_value = aspereza
    m.diffuse_color = (*cor, 1.0)          # cor no viewport e na importação da Unity
    m.metallic, m.roughness = metal, aspereza
    if emissao:
        p.inputs["Emission Color"].default_value = (*emissao, 1.0)
        p.inputs["Emission Strength"].default_value = 1.0
    return m


def contorno_arredondado(l, a, r, passos=10):
    """Pontos (x, z) de um retângulo de cantos arredondados, no sentido anti-horário."""
    pts = []
    cantos = [(l / 2 - r, a / 2 - r, 0), (-l / 2 + r, a / 2 - r, 90), (-l / 2 + r, -a / 2 + r, 180), (l / 2 - r, -a / 2 + r, 270)]
    for cx, cz, ang in cantos:
        for i in range(passos + 1):
            t = math.radians(ang + 90 * i / passos)
            pts.append((cx + r * math.cos(t), cz + r * math.sin(t)))
    return pts


def placa(nome, l, a, r, espessura, y_centro, passos=10):
    """Prisma de cantos arredondados, de pé (perfil no plano XZ, espessura em Y)."""
    bm = bmesh.new()
    pts = contorno_arredondado(l, a, r, passos)
    frente = [bm.verts.new((x, y_centro - espessura / 2, z)) for x, z in pts]
    tras = [bm.verts.new((x, y_centro + espessura / 2, z)) for x, z in pts]
    bm.faces.new(list(reversed(frente)))
    bm.faces.new(tras)
    n = len(pts)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((frente[i], frente[j], tras[j], tras[i]))
    bm.normal_update()
    malha = bpy.data.meshes.new(nome)
    bm.to_mesh(malha)
    bm.free()
    obj = bpy.data.objects.new(nome, malha)
    bpy.context.collection.objects.link(obj)
    return obj


def chanfrar(obj, largura, segmentos):
    mod = obj.modifiers.new("Chanfro", 'BEVEL')
    mod.width = largura
    mod.segments = segmentos
    mod.limit_method = 'ANGLE'
    mod.angle_limit = math.radians(50)
    mod.harden_normals = False
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=mod.name)


def cilindro(nome, raio, profundidade, x, y, z, mat, verts=32):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=raio, depth=profundidade, location=(x, y, z),
                                        rotation=(math.radians(90), 0, 0))
    o = bpy.context.active_object
    o.name = nome
    o.data.materials.append(mat)
    return o


def caixa(nome, tamanho, local, mat):
    bpy.ops.mesh.primitive_cube_add(size=1, location=local)
    o = bpy.context.active_object
    o.name = nome
    o.scale = tamanho
    bpy.ops.object.transform_apply(scale=True)
    o.data.materials.append(mat)
    return o


def suavizar(obj, angulo=40):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    try:
        bpy.ops.object.shade_auto_smooth(angle=math.radians(angulo))
    except Exception:
        bpy.ops.object.shade_smooth()
    obj.select_set(False)


def main():
    limpar()
    m_corpo = material("Corpo", (0.045, 0.05, 0.058), metal=0.6, aspereza=0.35)
    m_vidro = material("Vidro", (0.005, 0.005, 0.007), metal=0.0, aspereza=0.08)
    m_lente = material("Lente", (0.01, 0.012, 0.02), metal=0.2, aspereza=0.05)
    m_flash = material("Flash", (0.9, 0.85, 0.7), aspereza=0.3)
    m_botao = material("Botao", (0.08, 0.085, 0.095), metal=0.7, aspereza=0.3)
    m_tela = material("Tela", (0.02, 0.02, 0.025), aspereza=0.1)

    # Corpo: placa arredondada, bordas laterais chanfradas; frente de vidro.
    corpo = placa("Celular", L, A, RAIO, E, 0.0, passos=12)
    corpo.data.materials.append(m_corpo)
    corpo.data.materials.append(m_vidro)
    chanfrar(corpo, 0.0016, 4)
    for f in corpo.data.polygons:
        f.material_index = 1 if f.normal.y < -0.95 else 0

    partes = []
    # Módulo da câmera atrás (canto superior esquerdo visto de trás = +X visto de frente).
    bx, bz = L / 2 - 0.016, A / 2 - 0.018
    modulo = placa("Modulo", 0.026, 0.026, 0.0065, 0.0016, E / 2 + 0.0008, passos=8)
    modulo.location = (bx, 0, bz)
    modulo.data.materials.append(m_corpo)
    chanfrar(modulo, 0.0005, 2)
    partes.append(modulo)
    y_lente = E / 2 + 0.0016 + 0.0004
    partes.append(cilindro("Lente1", 0.0048, 0.001, bx - 0.0005, y_lente, bz + 0.0055, m_lente))
    partes.append(cilindro("Anel1", 0.0058, 0.0007, bx - 0.0005, y_lente - 0.0002, bz + 0.0055, m_botao))
    partes.append(cilindro("Lente2", 0.0038, 0.001, bx - 0.0005, y_lente, bz - 0.006, m_lente))
    partes.append(cilindro("Anel2", 0.0047, 0.0007, bx - 0.0005, y_lente - 0.0002, bz - 0.006, m_botao))
    partes.append(cilindro("LedFlash", 0.0018, 0.0008, bx - 0.0095, y_lente - 0.0002, bz + 0.0055, m_flash, verts=16))

    # Botões na lateral direita (vista de frente: -X, porque a frente olha para -Y).
    lado = -L / 2 - 0.0004
    partes.append(caixa("Volume", (0.0012, 0.0035, 0.024), (lado, 0, A / 2 - 0.042), m_botao))
    partes.append(caixa("Liga", (0.0012, 0.0035, 0.014), (lado, 0, A / 2 - 0.072), m_botao))
    # Câmera frontal: um ponto escuro no topo da tela.
    partes.append(cilindro("Frontal", 0.0018, 0.0003, 0, -E / 2 - 0.00025, A / 2 - 0.009, m_lente, verts=16))

    for p in partes:
        bpy.ops.object.select_all(action='DESELECT')
        p.select_set(True)
        bpy.context.view_layer.objects.active = p
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.ops.object.select_all(action='DESELECT')
    for p in partes:
        p.select_set(True)
    corpo.select_set(True)
    bpy.context.view_layer.objects.active = corpo
    bpy.ops.object.join()
    suavizar(corpo)

    # Tela: plano de cantos arredondados um pouco à frente do vidro, UV 0..1 pela posição.
    bm = bmesh.new()
    y = -E / 2 - 0.00015
    vs = [bm.verts.new((x, y, z)) for x, z in contorno_arredondado(TELA_L, TELA_A, TELA_RAIO, 8)]
    face = bm.faces.new(list(reversed(vs)))
    bmesh.ops.triangulate(bm, faces=[face])
    uv = bm.loops.layers.uv.new("UVMap")
    for f in bm.faces:
        for loop in f.loops:
            co = loop.vert.co
            loop[uv].uv = (co.x / TELA_L + 0.5, co.z / TELA_A + 0.5)
    bm.normal_update()
    malha = bpy.data.meshes.new("Tela")
    bm.to_mesh(malha)
    bm.free()
    tela = bpy.data.objects.new("Tela", malha)
    bpy.context.collection.objects.link(tela)
    tela.data.materials.append(m_tela)
    for poly in tela.data.polygons:
        if poly.normal.y > 0:
            poly.flip()
    tela.data.update()

    # Pontos de referência para a Unity.
    for nome, pos in (("PontoFlash", (bx - 0.0095, E / 2 + 0.003, bz + 0.0055)), ("PontoLente", (bx - 0.0005, E / 2 + 0.003, bz + 0.0055))):
        vazio = bpy.data.objects.new(nome, None)
        vazio.location = pos
        bpy.context.collection.objects.link(vazio)

    os.makedirs(os.path.dirname(FBX), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    bpy.ops.export_scene.fbx(
        filepath=FBX, use_selection=False, object_types={'MESH', 'EMPTY'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True,
        axis_forward='-Z', axis_up='Y', use_mesh_modifiers=True, mesh_smooth_type='FACE', add_leaf_bones=False)
    print("Celular exportado:", FBX)


main()
