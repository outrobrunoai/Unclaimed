// Reception: finish, furniture and fittings. Furniture and equipment are prefabs modelled in Blender
// (Blender/Recepcao, prefabs built by UnclaimedRecepcaoAssets); signs (solid PVC plates) and the clock are
// textured by Blender/Recepcao/scripts/texturas_recepcao.py. A missing prefab falls back to the old
// placeholder box, if it had one. References (public health units, a UPA waiting room) are listed in
// Blender/Recepcao/referencias.md.
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static partial class UnclaimedMorgueGenerator
{
    private const string RecepcaoFolder = BaseFolder + "/Recepcao";
    private const string RecepcaoTextures = RecepcaoFolder + "/Texturas/";

    // Free interior (wall faces) of the main rectangle, and the alcove's north face (corridor door wall).
    private const float RxMin = -14.65f, RxMax = -6.6f, RzMin = -12.275f, RzMax = -7.225f, AlcoveZ = -6.1f, AlcoveX = -10.275f;

    // Counter on the west wall facing east, 1.65 m in front of it so the clerk can walk in from the south,
    // sit and push the chair back; the cabinet closes the gap at the north wall.
    private const float CounterX = -12.88f, CounterZ = -9.35f;

    // Surfaces (second pass, Blender/Recepcao/scripts/texturas_superficies.py): porcelain with per-tile
    // shade, gloss and lippage over 2.4 m (4 x 4 tiles), recessed grout; washable paint with a fine roller
    // texture whose gloss map scales each paint's own sheen (satin wainscot, matt upper wall).
    private static ZoneFinish RecepcaoFinish()
    {
        string f = RecepcaoFolder, t = RecepcaoTextures, a = AutopsiaTextures;
        Color barrado = new Color(0.74f, 0.71f, 0.64f);
        return new ZoneFinish
        {
            Floor = Mat(f, "Piso porcelanato bege", Color.white, 1f, 0f, t + "Recepcao_Porcelanato_Base.png",
                t + "Recepcao_Porcelanato_Normal.png", 2.4f, glossMap: t + "Recepcao_Porcelanato_MS.png"),
            Skirting = Mat(f, "Rodape porcelanato bege", new Color(0.55f, 0.51f, 0.45f), 0.45f),
            // Washable satin paint up to 1.1 m (chairs, trolleys, hands), matt paint above.
            Wainscot = Mat(f, "Barrado tinta lavavel", barrado, 0.42f, 0f, t + "Recepcao_Pintura_Base.png",
                t + "Recepcao_Pintura_Normal.png", 2f, glossMap: t + "Recepcao_Pintura_MS.png"),
            WainscotHeight = 1.1f,
            UpperWall = Mat(f, "Pintura", new Color(1f, 0.99f, 0.96f), 0.14f, 0f, t + "Recepcao_Pintura_Base.png",
                t + "Recepcao_Pintura_Normal.png", 2f, glossMap: t + "Recepcao_Pintura_MS.png"),
            Ceiling = Mat(AutopsiaFolder, "Forro mineral", Color.white, 0.05f, 0f, a + "Autopsia_Forro_Base.png", a + "Autopsia_Forro_Normal.png", 1.25f),
            // A slightly darker satin line where the two paints meet, and the ceiling's perimeter angle.
            Friso = Mat(f, "Friso do barrado", barrado * 0.86f, 0.5f),
            CeilingTrim = Mat(f, "Cantoneira do forro", new Color(0.93f, 0.93f, 0.91f), 0.35f),
        };
    }

    // Prefabs face -Z: yaw -90 turns them east, 90 west, 180 north, 0 south.
    public static void ReceptionFurniture(Transform p)
    {
        ReceptionProp(p, "Balcao de recepcao", "Balcao de recepcao", CounterX, CounterZ, -90f,
            () => Block("Balcao de recepcao", p, -13.0f, -10.6f, -12.28f, -8.1f, 0, 1.1f, furnitureMat));
        ReceptionProp(p, "Equipamentos do balcao", "Equipamentos do balcao", CounterX, CounterZ, -90f, null);
        ReceptionProp(p, "Cadeira do atendente", "Cadeira do atendente", -13.62f, -9.4f, -76f, null);
        ReceptionProp(p, "Armario de apoio", "Armario de apoio", -14.18f, -7.455f, 0f, null);
        // Waiting: beam seating on the east wall facing the counter, two chairs on the south wall,
        // the wheelchair space beside the beam seating; the corner between them stays free.
        ReceptionProp(p, "Longarina de espera", "Longarina de espera", -6.875f, -10.75f, 90f, null);
        ReceptionProp(p, "Cadeira de espera", "Cadeira de espera 1", -8.45f, -11.99f, 180f,
            () => Block("Cadeira de espera 1", p, -8.75f, -12.25f, -8.15f, -11.65f, 0, 0.9f, furnitureMat));
        ReceptionProp(p, "Cadeira de espera", "Cadeira de espera 2", -7.85f, -11.99f, 180f,
            () => Block("Cadeira de espera 2", p, -8.15f, -12.25f, -7.55f, -11.65f, 0, 0.9f, furnitureMat));
        ReceptionProp(p, "Bebedouro", "Bebedouro", -10.0f, AlcoveZ - 0.165f, 0f, null);
        ReceptionProp(p, "Lixeira", "Lixeira", -9.45f, AlcoveZ - 0.19f, 0f, null);
    }

    private static void ReceptionProp(Transform p, string prefabName, string name, float x, float z, float yaw,
        Action fallback)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UnclaimedRecepcaoAssets.PrefabPath(prefabName));
        if (prefab == null)
        {
            Debug.LogWarning("Unclaimed: missing prefab " + prefabName + "; run Tools > Unclaimed > Build Recepcao Prefabs.");
            fallback?.Invoke();
            return;
        }
        var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, p);
        Undo.RegisterCreatedObjectUndo(obj, "Create " + name);
        obj.name = name;
        obj.transform.SetPositionAndRotation(new Vector3(x, 0f, z), Quaternion.Euler(0f, yaw, 0f));
    }

    // The clue placeholder on the counter's granite ledge looks like the visitors' register book; the
    // object, its name and collider stay, so whatever refers to the clue keeps working.
    private static void DressRegisterBook(GameObject clue)
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(UnclaimedRecepcaoAssets.Folder + "/LivroRegistro.fbx");
        var material = AssetDatabase.LoadAssetAtPath<Material>(UnclaimedRecepcaoAssets.Folder + "/Materiais/Livro de registro.mat");
        if (mesh == null || material == null) return;
        clue.GetComponent<MeshFilter>().sharedMesh = mesh;
        clue.GetComponent<MeshRenderer>().sharedMaterial = material;
        Vector3 p = clue.transform.position;
        clue.transform.SetPositionAndRotation(new Vector3(p.x, p.y - 0.075f, p.z), Quaternion.Euler(0f, 4f, 0f));
        clue.transform.localScale = Vector3.one;
        var box = clue.GetComponent<BoxCollider>();
        box.center = mesh.bounds.center;
        box.size = mesh.bounds.size;
    }

    private static void BuildRecepcao(Transform room)
    {
        Transform walls = Sub(room, "Parede - sinalizacao e quadros"), wear = Sub(room, "Marcas de uso"),
            ceiling = Sub(ceilingsGroup, "Luminarias - Recepcao");
        string t = RecepcaoTextures;
        Material sign(string name, string file) => Mat(RecepcaoFolder, name, Color.white, 0.35f, 0f, t + file);
        Material frame = Mat(RecepcaoFolder, "Aluminio anodizado", new Color(0.66f, 0.67f, 0.68f), 0.45f, 0.8f);
        Material black = Mat(RecepcaoFolder, "Plastico preto", new Color(0.03f, 0.03f, 0.03f), 0.4f);

        var east = Vector3.right; var west = Vector3.left; var north = Vector3.forward; var south = Vector3.back;
        RecepcaoSigns(walls);
        // Notice board (Tools > Unclaimed > Build Quadro de Avisos): readable papers from QuadroAvisos.json.
        var board = AssetDatabase.LoadAssetAtPath<GameObject>(UnclaimedQuadroDeAvisos.PrefabPath);
        if (board != null)
        {
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(board, walls);
            Undo.RegisterCreatedObjectUndo(obj, "Create Quadro de avisos");
            obj.name = UnclaimedQuadroDeAvisos.PrefabName;
            obj.transform.SetPositionAndRotation(new Vector3(-8.15f, 1.55f, RzMin + FinishOffset), Quaternion.Euler(0f, 180f, 0f));
        }
        else
            Plaque("Quadro de avisos", walls, new Vector3(-8.15f, 1.55f, RzMin), north, 0.9f, 0.6f, sign("Quadro de avisos", "Quadro_Avisos.png"), frame, 0.02f);
        Clock(walls, new Vector3(-13.35f, 2.236f, RzMax), south, black);   // over the clerk's post (placed by hand, 2026-10-10)

        // PVC guard strips where chair backs touch the walls.
        FaceStrip("Bate-cadeira - longarina", walls, true, RxMax, -1f, -11.65f, -9.85f, 0.72f, 0.86f, 0.012f, wallGuard);
        FaceStrip("Bate-cadeira - cadeiras", walls, false, RzMin, 1f, -8.8f, -7.5f, 0.72f, 0.86f, 0.012f, wallGuard);

        Material isa = Mat(RecepcaoFolder, "Decal - espaco reservado", new Color(1f, 1f, 1f, 0.9f), 0.3f, 0f, t + "Decal_EspacoReservado.png", transparent: true);
        Decal("Demarcacao - cadeira de rodas", wear, new Vector3(-7.2f, 0.005f, -9.45f), Vector3.up, Vector3.back, Vector3.right, 0.8f, 1.2f, isa);
        Decal("Desgaste - entrada", wear, new Vector3(-11.375f, 0.005f, -11.5f), Vector3.up, Vector3.right, Vector3.forward, 2.4f, 1.8f, decalFloorWear);
        Decal("Desgaste - balcao", wear, new Vector3(-11.85f, 0.005f, CounterZ), Vector3.up, Vector3.forward, Vector3.right, 2.6f, 1.2f, decalFloorWear);
        Decal("Desgaste - posto", wear, new Vector3(-13.75f, 0.005f, -9.6f), Vector3.up, Vector3.forward, Vector3.right, 1.6f, 1.0f, decalFloorWear);
        Decal("Riscos - parede do posto", wear, new Vector3(RxMin + 0.006f, 0.8f, -9.5f), east, Vector3.back, Vector3.up, 1.0f, 0.5f, decalScuffs);
        // Maintenance: two touched-up patches of paint, scuffs beside the bathroom door.
        Material retoque = Mat(RecepcaoFolder, "Decal - retoque de pintura", new Color(1f, 0.995f, 0.98f, 1f), 0.12f, 0f,
            t + "Decal_RetoquePintura.png", transparent: true);
        Decal("Retoque - parede norte", wear, new Vector3(-12.7f, 1.62f, RzMax - 0.006f), south, Vector3.left, Vector3.up, 0.48f, 0.36f, retoque);
        Decal("Retoque - parede leste", wear, new Vector3(RxMax - 0.006f, 1.45f, -10.7f), west, Vector3.forward, Vector3.up, 0.36f, 0.27f, retoque);
        // Starts just past the WC door's casing (outer edge x = -10.897): laid over the casing, it z-fought with its faces.
        Decal("Riscos - porta do WC", wear, new Vector3(-10.64f, 0.55f, RzMax - 0.006f), south, Vector3.left, Vector3.up, 0.5f, 0.45f, decalScuffs);
        RecepcaoLighting(ceiling);
        EntranceDoor();
    }

    // Two-leaf glass door (Blender/Recepcao, prefab "Porta de vidro - entrada") in the main entrance's
    // 1.94 m clear opening, between the existing jamb linings. Each leaf opens and closes on its own
    // (FolhaDePorta); they start open outwards on their floor stops so the entrance stays passable.
    private static void EntranceDoor()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UnclaimedRecepcaoAssets.PrefabPath(UnclaimedRecepcaoAssets.DoorPrefab));
        if (prefab == null) return;
        var door = (GameObject)PrefabUtility.InstantiatePrefab(prefab, Sub(doorsGroup, "Entrada principal"));
        Undo.RegisterCreatedObjectUndo(door, "Create entrance door");
        door.transform.SetPositionAndRotation(new Vector3(-11.375f, 0f, -12.375f), Quaternion.identity);
        // Each leaf's FolhaDePorta holds its initial state (open by default; set it in the Inspector).
        foreach (FolhaDePorta leaf in door.GetComponentsInChildren<FolhaDePorta>()) leaf.AplicarEstadoInicial();
    }

    // Six tube fittings (CeilingFixture, shared with every room); one has an aged tube. Two cast shadows
    // (counter, waiting area); the bounce is baked (UnclaimedMorgueGenerator.Iluminacao.cs).
    private static void RecepcaoLighting(Transform ceiling)
    {
        var fixtures = new[] { (-13.0f, -8.45f, 0, true), (-10.35f, -8.45f, 0, false), (-7.7f, -8.45f, 0, false),
                               (-13.0f, -10.95f, 0, false), (-10.35f, -10.95f, 1, false), (-7.7f, -10.95f, 0, true) };
        for (int i = 0; i < fixtures.Length; i++)
        {
            var (x, z, state, shadows) = fixtures[i];
            CeilingFixture(ceiling, "Luz - recepcao - luminaria " + (i + 1), "Luminaria " + (i + 1), x, z, state, 4.2f, 4300f,
                shadows, model: true);
        }
    }

    // Door and wall signs (PVC plates); also rebuilt on the saved map by Tools > Unclaimed > Update Wall Finishes.
    private static void RecepcaoSigns(Transform walls)
    {
        string t = RecepcaoTextures;
        Material sign(string name, string file) => Mat(RecepcaoFolder, name, Color.white, 0.35f, 0f, t + file);
        Material pvc = Mat(RecepcaoFolder, "PVC branco", new Color(0.92f, 0.92f, 0.9f), 0.4f);
        var east = Vector3.right; var west = Vector3.left; var south = Vector3.back;
        Plaque("Placa - recepcao", walls, new Vector3(RxMin, 2.25f, CounterZ), east, 1.0f, 0.25f, sign("Placa recepcao", "Placa_Recepcao.png"), pvc);
        Plaque("Placa - sanitarios", walls, new Vector3(-11.5f, 2.45f, RzMax), south, 0.6f, 0.2f, sign("Placa sanitarios", "Placa_Sanitarios.png"), pvc);
        Plaque("Placa - acesso restrito", walls, new Vector3(-8f, 2.45f, AlcoveZ), south, 0.8f, 0.2f, sign("Placa acesso restrito", "Placa_AcessoRestrito.png"), pvc);
        Plaque("Placa - administracao", walls, new Vector3(RxMax, 2.45f, -8f), west, 0.8f, 0.2f, sign("Placa administracao", "Placa_Administracao.png"), pvc);
        Plaque("Placa - espaco reservado", walls, new Vector3(RxMax, 1.55f, -9.45f), west, 0.25f, 0.325f, sign("Placa espaco reservado", "Placa_EspacoReservado.png"), pvc);
    }

    // A sign or board: one solid plate whose front carries the print (submesh 0) and whose edges and back are
    // the backing material (submesh 1). It used to be a backing box with the print as a separate quad 1.5 mm in
    // front of it, which drew a second outline round the sign and broke its edges into dashes. Visual only and
    // no shadow: a few millimetres off the wall, the shadow map could only add a stepped rectangle. Running it
    // again on an existing sign replaces the old pieces.
    private static void Plaque(string name, Transform parent, Vector3 wallPoint, Vector3 normal, float w, float h,
        Material face, Material backing, float depth = 0.004f)
    {
        Transform g = Sub(parent, name);
        for (int i = g.childCount - 1; i >= 0; i--) Undo.DestroyObjectImmediate(g.GetChild(i).gameObject);
        var obj = new GameObject("Placa");
        Undo.RegisterCreatedObjectUndo(obj, "Create " + name);
        obj.transform.SetParent(g, false);
        obj.transform.SetPositionAndRotation(wallPoint + normal * (FinishOffset + depth / 2), Quaternion.LookRotation(normal));
        obj.AddComponent<MeshFilter>().sharedMesh = PlateMesh(name, w, h, depth);
        var r = obj.AddComponent<MeshRenderer>();
        r.sharedMaterials = new[] { face, backing };
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ClampPrint(face);
    }

    // The print runs to the plate's edge: with Repeat wrapping, bilinear filtering and mipmaps blended the
    // opposite edge in (the blue band showed as a line along the white end).
    private static void ClampPrint(Material face)
    {
        string path = AssetDatabase.GetAssetPath(face.GetTexture("_BaseMap"));
        if (AssetImporter.GetAtPath(path) is TextureImporter importer && importer.wrapMode != TextureWrapMode.Clamp)
        {
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
    }

    // Box w x h x depth centred on the origin, front face on +Z. The print spans the front (texture right =
    // world right as seen from the front, i.e. local -X); the sides and the back share submesh 1.
    private static Mesh PlateMesh(string name, float w, float h, float depth)
    {
        float x = w / 2, y = h / 2, z = depth / 2;
        var v = new List<Vector3>();
        var n = new List<Vector3>();
        var uv = new List<Vector2>();
        var front = new List<int>();
        var rest = new List<int>();
        void Face(List<int> tris, Vector3 normal, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i = v.Count;
            foreach (Vector3 p in new[] { a, b, c, d })
            {
                v.Add(p);
                n.Add(normal);
                uv.Add(new Vector2(0.5f - p.x / w, 0.5f + p.y / h));
            }
            bool ccw = Vector3.Dot(Vector3.Cross(b - a, c - a), normal) > 0;
            tris.AddRange(ccw ? new[] { i, i + 1, i + 2, i, i + 2, i + 3 } : new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
        }
        Face(front, Vector3.forward, new Vector3(-x, -y, z), new Vector3(x, -y, z), new Vector3(x, y, z), new Vector3(-x, y, z));
        Face(rest, Vector3.back, new Vector3(-x, -y, -z), new Vector3(x, -y, -z), new Vector3(x, y, -z), new Vector3(-x, y, -z));
        Face(rest, Vector3.right, new Vector3(x, -y, -z), new Vector3(x, y, -z), new Vector3(x, y, z), new Vector3(x, -y, z));
        Face(rest, Vector3.left, new Vector3(-x, -y, -z), new Vector3(-x, y, -z), new Vector3(-x, y, z), new Vector3(-x, -y, z));
        Face(rest, Vector3.up, new Vector3(-x, y, -z), new Vector3(x, y, -z), new Vector3(x, y, z), new Vector3(-x, y, z));
        Face(rest, Vector3.down, new Vector3(-x, -y, -z), new Vector3(x, -y, -z), new Vector3(x, -y, z), new Vector3(-x, -y, z));
        var mesh = new Mesh { name = name, vertices = v.ToArray(), normals = n.ToArray(), uv = uv.ToArray(), subMeshCount = 2 };
        mesh.SetTriangles(front, 0);
        mesh.SetTriangles(rest, 1);
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    // Round wall clock showing ten past ten: black case, printed dial on a disc (UVs on the inscribed
    // circle, so no square quad shows around it), two hands, and a glass disc of the dial's size.
    private static void Clock(Transform parent, Vector3 wallPoint, Vector3 normal, Material black)
    {
        Transform g = Sub(parent, "Relogio de parede");
        const float d = 0.32f, depth = 0.04f, dial = 0.29f;
        Vector3 c = wallPoint + normal * (FinishOffset + depth / 2);
        Part("Aro", g, PrimitiveType.Cylinder, c, new Vector3(d, depth / 2, d), Quaternion.FromToRotation(Vector3.up, normal), black);
        Material face = Mat(RecepcaoFolder, "Mostrador do relogio", Color.white, 0.25f, 0f, RecepcaoTextures + "Relogio_Mostrador.png");
        Material glass = Mat(RecepcaoFolder, "Vidro do relogio", new Color(1f, 1f, 1f, 0.06f), 0.92f, transparent: true);
        Vector3 front = wallPoint + normal * (FinishOffset + depth);
        Disc("Mostrador", g, front + normal * 0.001f, normal, dial / 2, face);
        foreach (var (hand, angle, length, width) in new[] { ("Ponteiro das horas", 55f, 0.075f, 0.009f), ("Ponteiro dos minutos", -60f, 0.115f, 0.006f) })
        {
            Quaternion rot = Quaternion.LookRotation(normal) * Quaternion.Euler(0f, 0f, angle);
            Vector3 dir = rot * Vector3.up;
            Part(hand, g, PrimitiveType.Cube, front + normal * 0.004f + dir * (length / 2 - 0.012f), new Vector3(width, length, 0.002f), rot, black);
        }
        Disc("Vidro", g, front + normal * 0.008f, normal, dial / 2 + 0.004f, glass);
        // The ceiling spots are points: their highlight on the glass burnt to a white dot. The glass
        // keeps only the reflection probe's image of the room.
        glass.SetFloat("_SpecularHighlights", 0f);
        glass.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
    }

    // Visual-only flat disc facing `normal`; its UVs map the texture's inscribed circle (centre 0.5, 0.5),
    // with texture up = world up.
    private static void Disc(string name, Transform parent, Vector3 center, Vector3 normal, float radius,
        Material material, int segments = 64)
    {
        Vector3 right = Vector3.Cross(normal, Vector3.up), up = Vector3.up;
        var v = new Vector3[segments + 1];
        var uv = new Vector2[segments + 1];
        var tris = new int[segments * 3];
        v[0] = center;
        uv[0] = new Vector2(0.5f, 0.5f);
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments, x = Mathf.Cos(a), y = Mathf.Sin(a);
            v[i + 1] = center + (right * x + up * y) * radius;
            uv[i + 1] = new Vector2(0.5f + x * 0.5f, 0.5f + y * 0.5f);
        }
        bool ccw = Vector3.Dot(Vector3.Cross(v[1] - v[0], v[2] - v[0]), normal) > 0;
        for (int i = 0; i < segments; i++)
        {
            int j = i + 1, k = (i + 1) % segments + 1;
            tris[i * 3] = 0; tris[i * 3 + 1] = ccw ? j : k; tris[i * 3 + 2] = ccw ? k : j;
        }
        var tangent = new Vector4(right.x, right.y, right.z, Vector3.Dot(Vector3.Cross(normal, right), up) > 0 ? 1f : -1f);
        var mesh = new Mesh { name = name, vertices = v, uv = uv, triangles = tris };
        mesh.normals = Array.ConvertAll(v, _ => normal);
        mesh.tangents = Array.ConvertAll(v, _ => tangent);
        mesh.RecalculateBounds();
        var obj = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(obj, "Create " + name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = obj.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
}
