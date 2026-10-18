// Builds the reception notice board prefab from QuadroAvisos.json and the images rendered by
// Blender/Recepcao/scripts/quadro_avisos.py (run that first when the content changes):
//   Tools > Unclaimed > Build Quadro de Avisos      prefab, meshes, materials, texture settings
//   Tools > Unclaimed > Place Quadro de Avisos      swaps the board in the open scene (same parent, name and spot)
// Prefab frame: origin at the board's centre on the wall face, front towards -Z, x to the viewer's right.
// Board: MDF back, cork panel, swept aluminium frame with mitred corners and grey plastic corner caps.
// Papers: one gridded mesh each (printed front, plain back), flat where pinned or pressed under another paper
// and curling a few millimetres away from its pins; pushpins, flat tacks or tape as the JSON says. Each paper
// carries an ObjetoInspecionavel that the board opens when the paper is clicked in the board view; the paper
// flies from the board to the camera and back. Matte paper surface: own relief map plus a shared fibre detail.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class UnclaimedQuadroDeAvisos
{
    public const string Dir = "Assets/UnclaimedPrototype/Recepcao/QuadroDeAvisos";
    public const string PrefabName = "Quadro de avisos";
    public static string PrefabPath => UnclaimedRecepcaoAssets.PrefabPath(PrefabName);
    private const string MatDir = Dir + "/Materiais", MeshPath = Dir + "/Malhas.asset";
    private const string Fonts = "Assets/UnclaimedPrototype/Computador/Fontes/";
    // Cork face and board depth (m from the wall). Papers float 1.5 mm off the cork and 1.2 mm apart per layer:
    // closer than that, depth precision (the player camera's near plane is 1 cm) made the layers fight from
    // a few metres away (speckles and stripes on the cork and the papers).
    private const float Back = 0.004f, CorkFace = 0.014f, Gap = 0.0015f, Layer = 0.0012f;

    private static float PaperDepth(int ordem) => CorkFace + Gap + Layer * ordem;

    [MenuItem("Tools/Unclaimed/Build Quadro de Avisos")]
    public static void Build()
    {
        var json = AssetDatabase.LoadAssetAtPath<TextAsset>(Dir + "/QuadroAvisos.json");
        var data = QuadroDados.Carregar(json.text);
        var q = data.quadro;
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder(Dir, "Materiais");
        AssetDatabase.DeleteAsset(MeshPath);
        var meshes = new List<Mesh>();

        var root = new GameObject(PrefabName);
        try
        {
            // ---- board
            Material mdf = Lit("Fundo MDF", new Color(0.32f, 0.24f, 0.17f), 0.15f);
            Material alu = Lit("Moldura aluminio", new Color(0.78f, 0.79f, 0.8f), 0.62f, metallic: 0.9f);
            Material cap = Lit("Cantoneira plastica", new Color(0.46f, 0.47f, 0.49f), 0.35f);
            Material cork = Lit("Cortica", Color.white, 0.08f, Tex(Dir + "/Cortica_Base.png", false), Tex(Dir + "/Cortica_Normal.png", true));
            cork.SetFloat("_BumpScale", 0.6f);

            float w = q.largura, h = q.altura, m = q.moldura, depth = q.profundidade;
            Part("Fundo", root.transform, Box(w - 0.004f, h - 0.004f, Back, -Back / 2f, meshes, "Fundo"), mdf);
            float cw = w - 2 * m, ch = h - 2 * m;
            // cork body stops 2 mm behind the textured face (no coplanar front faces)
            float body = CorkFace - 0.002f;
            Part("Cortica", root.transform, Box(cw + 0.006f, ch + 0.006f, body - Back, -(Back + body) / 2f, meshes, "Cortica (corpo)"), mdf);
            Part("Cortica - face", root.transform, Quad(cw, ch, -CorkFace, meshes, "Cortica (face)"), cork);
            Part("Moldura", root.transform, Frame(w, h, m, depth, meshes), alu);
            foreach (var (sx, sy) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
                Part("Cantoneira", root.transform, CornerCap(sx, sy, w, h, depth, meshes), cap);

            // ---- papers
            var docs = data.documentos;
            Material pinMetal = Lit("Percevejo metal", new Color(0.8f, 0.78f, 0.72f), 0.7f, metallic: 1f);
            Material pinOld = Lit("Percevejo oxidado", new Color(0.55f, 0.47f, 0.36f), 0.35f, metallic: 0.8f);
            Material needle = Lit("Alfinete aco", new Color(0.75f, 0.75f, 0.76f), 0.8f, metallic: 1f);
            Material tape = Lit("Fita adesiva", new Color(0.97f, 0.95f, 0.86f, 0.42f), 0.75f, transparent: true);
            var pinColors = new Dictionary<string, Color>
            {
                ["azul"] = new Color(0.12f, 0.3f, 0.75f), ["vermelho"] = new Color(0.78f, 0.1f, 0.1f),
                ["verde"] = new Color(0.12f, 0.55f, 0.25f), ["amarelo"] = new Color(0.95f, 0.75f, 0.1f),
                ["branco"] = new Color(0.92f, 0.92f, 0.9f),
            };
            var group = new GameObject("Documentos").transform;
            group.SetParent(root.transform, false);
            var exams = new List<ObjetoInspecionavel>();
            for (int i = 0; i < docs.Length; i++)
            {
                var d = docs[i];
                Texture2D tex = DocTexture(Dir + "/Documentos/" + d.id + ".png");
                // Front with the print; plain back (its own image where the paper has torn parts, so they stay cut).
                string versoPath = Dir + "/Documentos/" + d.id + "_verso.png";
                Texture2D verso = AssetImporter.GetAtPath(versoPath) != null ? DocTexture(versoPath) : null;
                bool cut = verso != null;
                Material front = Lit("Papel - " + d.id, Color.white, 0f, tex, clip: cut);
                ColorUtility.TryParseHtmlString(d.papel, out Color paperColor);
                Material back = Lit("Papel - " + d.id + " (verso)", cut ? Color.white : paperColor * 0.97f, 0f, verso, clip: cut);
                Texture2D relief = Tex(Dir + "/Documentos/" + d.id + "_relevo.png", true);
                PaperSurface(front, d, relief);
                PaperSurface(back, d, relief);
                var paper = Part("Documento - " + d.id, group, Paper(d, docs, meshes), front);
                paper.GetComponent<MeshRenderer>().sharedMaterials = new[] { front, back };
                paper.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                // Examined from the board view with the clues' inspection: turn with a drag, zoom with the wheel.
                var exam = paper.AddComponent<ObjetoInspecionavel>();
                exam.nome = d.titulo;
                exam.ocultarOriginal = true;              // the paper leaves the board while it is in the hand
                exam.orientacao = OrientacaoNaInspecao.FaceParaOJogador;
                exam.faceVoltadaParaJogador = EixoLocal.Tras;
                exam.topoNaTela = EixoLocal.Cima;
                exam.giroNaTela = -d.rotacao;             // straight, not tilted as it hangs on the board
                exam.distanciaDaCamera = 0.5f;
                exam.tamanhoNaTela = 0.47f;               // every paper fills most of the screen
                exam.limiteDistancia = new Vector2(0.16f, 0.75f);
                exam.zoomPorScroll = 0.05f;
                exam.desativarDuranteInspecao = new Behaviour[0];   // the board view already holds the player
                exam.duracaoAnimacao = 0.4f;              // leaves the board towards the camera, goes back on close
                exams.Add(exam);

                var fx = d.fixacao;
                if (fx == null || fx.pontos == null) continue;
                for (int k = 0; k < fx.pontos.Length; k++)
                {
                    var uv = new Vector2(fx.pontos[k].u, fx.pontos[k].v);
                    Vector2 p = d.NoQuadro(uv);
                    float z = -(PaperDepth(d.ordem) + 0.0002f);
                    string color = fx.cores != null && k < fx.cores.Length ? fx.cores[k] : "metal";
                    if (fx.tipo == "fita")
                    {
                        var t = Part("Fita", group, Quad(0.052f, 0.019f, 0f, meshes, "Fita"), tape);
                        t.transform.localPosition = new Vector3(p.x, p.y, z - 0.0008f);
                        t.transform.localRotation = Quaternion.Euler(0, 0, d.rotacao + (k == 0 ? 7f : -5f));
                        t.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                    }
                    else if (color == "metal" || color == "metal_velho")
                    {
                        // flat drawing pin: a low dome
                        var dome = Primitive("Percevejo", group, PrimitiveType.Sphere, color == "metal" ? pinMetal : pinOld);
                        dome.transform.localPosition = new Vector3(p.x, p.y, z - 0.0007f);
                        dome.transform.localScale = new Vector3(0.0105f, 0.0105f, 0.0022f);
                    }
                    else
                    {
                        // push pin: grip (cylinder with a wider top), short steel point showing under it
                        Material plastic = Lit("Alfinete " + color, pinColors.TryGetValue(color, out var c) ? c : Color.red, 0.55f);
                        var tip = Primitive("Alfinete", group, PrimitiveType.Cylinder, needle);
                        tip.transform.localPosition = new Vector3(p.x, p.y, z - 0.0012f);
                        tip.transform.localRotation = Quaternion.Euler(90, 0, 0);
                        tip.transform.localScale = new Vector3(0.0011f, 0.0012f, 0.0011f);
                        var grip = Primitive("Alfinete - corpo", group, PrimitiveType.Cylinder, plastic);
                        grip.transform.localPosition = new Vector3(p.x, p.y, z - 0.0024f - 0.0028f);
                        grip.transform.localRotation = Quaternion.Euler(90, 0, 0);
                        grip.transform.localScale = new Vector3(0.0062f, 0.0028f, 0.0062f);
                        var head = Primitive("Alfinete - cabeca", group, PrimitiveType.Cylinder, plastic);
                        head.transform.localPosition = new Vector3(p.x, p.y, z - 0.0024f - 0.0056f - 0.0015f);
                        head.transform.localRotation = Quaternion.Euler(90, 0, 0);
                        head.transform.localScale = new Vector3(0.0105f, 0.0015f, 0.0105f);
                    }
                }
            }

            // ---- interaction
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0f, -depth / 2f);
            box.size = new Vector3(w, h, depth);
            var board = root.AddComponent<QuadroDeAvisos>();
            board.conteudo = json;
            board.papeis = exams.ToArray();
            board.fonte = AssetDatabase.LoadAssetAtPath<Font>(Fonts + "NotoSans-Regular.ttf");
            board.fonteNegrito = AssetDatabase.LoadAssetAtPath<Font>(Fonts + "NotoSans-Bold.ttf");
            board.faceDaCortica = CorkFace;

            // meshes into one asset next to the content
            AssetDatabase.CreateAsset(meshes[0], MeshPath);
            for (int i = 1; i < meshes.Count; i++) AssetDatabase.AddObjectToAsset(meshes[i], MeshPath);
            AssetDatabase.SaveAssets();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("Unclaimed: notice board prefab built at " + PrefabPath + " (" + docs.Length + " documents).");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    /// <summary>Replaces the board in the open scene: same parent, name and wall spot; the old object is removed.</summary>
    [MenuItem("Tools/Unclaimed/Place Quadro de Avisos")]
    public static string Place()
    {
        Transform old = null;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if (t.name == PrefabName && t.parent != null && t.parent.name == "Parede - sinalizacao e quadros") old = t;
        if (old == null) return "board not found";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Vector3 at = Position(old, out Transform parent);
        var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        Undo.RegisterCreatedObjectUndo(obj, "Place notice board");
        obj.name = PrefabName;
        obj.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, 180f, 0f));
        obj.transform.SetSiblingIndex(old.GetSiblingIndex());
        Undo.DestroyObjectImmediate(old.gameObject);
        EditorSceneManager.MarkSceneDirty(obj.scene);
        return "placed at " + at.ToString("F3");
    }

    // Where the board goes: the old flat plaque's centre on the wall (its backing plate's back face), or the
    // existing prefab's own origin when it is already placed.
    private static Vector3 Position(Transform old, out Transform parent)
    {
        parent = old.parent;
        if (old.GetComponent<QuadroDeAvisos>() != null) return old.position;
        var b = old.Find("Base").GetComponent<Renderer>().bounds;
        return new Vector3(b.center.x, b.center.y, b.min.z);
    }

    // ------------------------------------------------------------------ meshes

    private static Mesh Finish(Mesh mesh, List<Mesh> list, string name)
    {
        mesh.name = name;
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        list.Add(mesh);
        return mesh;
    }

    // Quad facing -Z at depth z, UVs 0..1 (v up).
    private static Mesh Quad(float w, float h, float z, List<Mesh> list, string name)
    {
        var mesh = new Mesh
        {
            vertices = new[] { new Vector3(-w / 2, -h / 2, z), new Vector3(w / 2, -h / 2, z), new Vector3(w / 2, h / 2, z), new Vector3(-w / 2, h / 2, z) },
            uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) },
            normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back },
            triangles = new[] { 0, 3, 2, 0, 2, 1 },
        };
        return Finish(mesh, list, name);
    }

    private static Mesh Box(float w, float h, float d, float zCenter, List<Mesh> list, string name)
    {
        var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var src = tmp.GetComponent<MeshFilter>().sharedMesh;
        var v = src.vertices;
        for (int i = 0; i < v.Length; i++) v[i] = new Vector3(v[i].x * w, v[i].y * h, v[i].z * d + zCenter);
        var mesh = new Mesh { vertices = v, normals = src.normals, uv = src.uv, triangles = src.triangles };
        Object.DestroyImmediate(tmp);
        return Finish(mesh, list, name);
    }

    /// <summary>
    /// Aluminium profile swept around the board with mitred corners. Profile (u inwards from the outer edge,
    /// v out of the wall): rounded outer face, flat front, a lip that steps down onto the cork.
    /// </summary>
    private static Mesh Frame(float w, float h, float m, float depth, List<Mesh> list)
    {
        var prof = new List<Vector2>
        {
            new Vector2(0f, 0f), new Vector2(0f, depth - 0.004f), new Vector2(0.0012f, depth - 0.0012f), new Vector2(0.004f, depth),
            new Vector2(m - 0.006f, depth), new Vector2(m - 0.0015f, depth - 0.003f), new Vector2(m, CorkFace + 0.004f),
            new Vector2(m, CorkFace), new Vector2(m - 0.002f, CorkFace), new Vector2(m - 0.002f, Back), new Vector2(0.002f, 0f),
        };
        // corners in order (counter-clockwise seen from the front), with their inward directions
        var corners = new[] { new Vector2(-w / 2, -h / 2), new Vector2(w / 2, -h / 2), new Vector2(w / 2, h / 2), new Vector2(-w / 2, h / 2) };
        var inward = new[] { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(-1, -1), new Vector2(1, -1) };
        var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
        for (int e = 0; e < 4; e++)
        {
            int a = e, b = (e + 1) % 4;
            Vector2 dir = (corners[b] - corners[a]).normalized;
            Vector2 n2 = new Vector2(-dir.y, dir.x);           // inward normal of this edge (CCW path)
            for (int k = 0; k < prof.Count; k++)
            {
                Vector2 p0 = prof[k], p1 = prof[(k + 1) % prof.Count];
                Vector3 A0 = P(corners[a], inward[a], p0), A1 = P(corners[a], inward[a], p1);
                Vector3 B0 = P(corners[b], inward[b], p0), B1 = P(corners[b], inward[b], p1);
                // normal of this strip: perpendicular to the edge and to the profile segment
                Vector3 seg = (Vector3)(n2 * (p1.x - p0.x)) + Vector3.back * (p1.y - p0.y);
                Vector3 nrm = Vector3.Cross((Vector3)dir, seg).normalized;
                Vector3 mid = (A0 + A1) / 2f;
                Vector3 centroid = P(corners[a], inward[a], new Vector2(m * 0.5f, depth * 0.5f));
                if (Vector3.Dot(nrm, mid - centroid) < 0) nrm = -nrm;
                int i0 = verts.Count;
                verts.AddRange(new[] { A0, B0, B1, A1 });
                norms.AddRange(new[] { nrm, nrm, nrm, nrm });
                float len = (corners[b] - corners[a]).magnitude;
                uvs.AddRange(new[] { new Vector2(0, k * 0.1f), new Vector2(len, k * 0.1f), new Vector2(len, (k + 1) * 0.1f), new Vector2(0, (k + 1) * 0.1f) });
                AddFacing(tris, verts, i0, i0 + 1, i0 + 2, nrm);
                AddFacing(tris, verts, i0, i0 + 2, i0 + 3, nrm);
            }
        }
        var mesh = new Mesh { vertices = verts.ToArray(), normals = norms.ToArray(), uv = uvs.ToArray(), triangles = tris.ToArray() };
        return Finish(mesh, list, "Moldura");
    }

    private static Vector3 P(Vector2 corner, Vector2 inward, Vector2 prof) =>
        new Vector3(corner.x + inward.x * prof.x, corner.y + inward.y * prof.x, -prof.y);

    // Adds a triangle wound so that its face looks along n (Unity: clockwise seen from the front).
    private static void AddFacing(List<int> tris, List<Vector3> v, int a, int b, int c, Vector3 n)
    {
        if (Vector3.Dot(Vector3.Cross(v[b] - v[a], v[c] - v[a]), n) >= 0) tris.AddRange(new[] { a, b, c });
        else tris.AddRange(new[] { a, c, b });
    }

    /// <summary>Grey plastic corner piece over the mitre: an L block with a rounded outer corner.</summary>
    private static Mesh CornerCap(int sx, int sy, float w, float h, float depth, List<Mesh> list)
    {
        const float arm = 0.042f, thick = 0.026f, r = 0.008f;
        float d = depth + 0.003f;
        // outline (counter-clockwise seen from the front) in the cap's own frame: corner at the origin, arms along +x, +y
        var outline = new List<Vector2>();
        for (int k = 0; k <= 6; k++)
        {
            float a = Mathf.PI + k * (Mathf.PI / 2f) / 6f;   // rounded outer corner
            outline.Add(new Vector2(r + Mathf.Cos(a) * r, r + Mathf.Sin(a) * r));
        }
        outline.Add(new Vector2(arm, 0f)); outline.Add(new Vector2(arm, thick)); outline.Add(new Vector2(thick, thick));
        outline.Add(new Vector2(thick, arm)); outline.Add(new Vector2(0f, arm));
        var verts = new List<Vector3>(); var norms = new List<Vector3>(); var tris = new List<int>();
        Vector3 Map(Vector2 p, float z) => new Vector3(sx * (w / 2 + 0.0015f) - sx * p.x, sy * (h / 2 + 0.0015f) - sy * p.y, z);
        bool mirror = sx * sy < 0;
        // front face (fan from the inner corner point (thick, thick))
        int c0 = verts.Count;
        foreach (var p in outline) { verts.Add(Map(p, -d)); norms.Add(Vector3.back); }
        int centre = verts.Count; verts.Add(Map(new Vector2(thick * 0.6f, thick * 0.6f), -d)); norms.Add(Vector3.back);
        for (int k = 0; k < outline.Count; k++) AddFacing(tris, verts, centre, c0 + k, c0 + (k + 1) % outline.Count, Vector3.back);
        // sides
        for (int k = 0; k < outline.Count; k++)
        {
            Vector3 a0 = Map(outline[k], -d), a1 = Map(outline[(k + 1) % outline.Count], -d);
            Vector3 b0 = Map(outline[k], 0f), b1 = Map(outline[(k + 1) % outline.Count], 0f);
            Vector3 n = Vector3.Cross(a1 - a0, Vector3.forward).normalized;
            Vector3 cen = Map(new Vector2(thick * 0.6f, thick * 0.6f), -d / 2);
            if (Vector3.Dot(n, (a0 + a1) / 2 - cen) < 0) n = -n;
            int i0 = verts.Count;
            verts.AddRange(new[] { a0, a1, b1, b0 }); norms.AddRange(new[] { n, n, n, n });
            AddFacing(tris, verts, i0, i0 + 1, i0 + 2, n);
            AddFacing(tris, verts, i0, i0 + 2, i0 + 3, n);
        }
        _ = mirror;
        var mesh = new Mesh { vertices = verts.ToArray(), normals = norms.ToArray(), triangles = tris.ToArray() };
        mesh.uv = new Vector2[verts.Count];
        return Finish(mesh, list, "Cantoneira");
    }

    /// <summary>
    /// A paper on the board: a grid in board coordinates, flat at its pins and where a later paper covers it
    /// (with a margin), curling outwards with the square of the distance from the nearest pin elsewhere.
    /// </summary>
    private static Mesh Paper(QuadroDados.Documento d, QuadroDados.Documento[] all, List<Mesh> list)
    {
        const int nx = 14, ny = 18;
        var fx = d.fixacao;
        var pins = new List<Vector2>();
        if (fx?.pontos != null) foreach (var p in fx.pontos) pins.Add(new Vector2(p.u * d.tamanho[0], p.v * d.tamanho[1]));
        float k = fx == null || fx.tipo == "nenhuma" ? 0f : fx.tipo == "fita" ? 0.07f : pins.Count >= 4 ? 0.035f : pins.Count >= 2 ? 0.07f : 0.11f;
        if (d.acabamento == "plastificado") k *= 0.45f;
        float baseZ = PaperDepth(d.ordem);
        var verts = new Vector3[(nx + 1) * (ny + 1)];
        var uvs = new Vector2[verts.Length];
        for (int j = 0; j <= ny; j++)
            for (int i = 0; i <= nx; i++)
            {
                var uv = new Vector2(i / (float)nx, j / (float)ny);                 // u right, v down (paper)
                var paperPt = new Vector2(uv.x * d.tamanho[0], uv.y * d.tamanho[1]);
                float dist = 10f;
                foreach (var p in pins) dist = Mathf.Min(dist, (paperPt - p).magnitude);
                if (pins.Count == 0) dist = 0f;
                float lift = Mathf.Min(0.0055f, k * Mathf.Max(0f, dist - 0.035f) * Mathf.Max(0f, dist - 0.035f));
                Vector2 boardPt = d.NoQuadro(uv);
                lift *= Freedom(d, all, boardPt);
                verts[j * (nx + 1) + i] = new Vector3(boardPt.x, boardPt.y, -(baseZ + lift));
                uvs[j * (nx + 1) + i] = new Vector2(uv.x, 1f - uv.y);
            }
        var tris = new List<int>();
        for (int j = 0; j < ny; j++)
            for (int i = 0; i < nx; i++)
            {
                int a = j * (nx + 1) + i, b = a + 1, c = a + nx + 1, e = c + 1;   // a top-left, c below it
                tris.AddRange(new[] { a, b, e, a, e, c });
            }
        // Front (towards -Z) and back: the same grid twice, the back wound the other way; same UVs, so the back's
        // image (plain paper with the torn strips cut) lines up with the front.
        var front = new Mesh { vertices = verts, uv = uvs, triangles = tris.ToArray() };
        front.RecalculateNormals();
        if (front.normals[0].z > 0f)
            for (int i = 0; i < tris.Count; i += 3) (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);
        int n = verts.Length;
        var allVerts = new Vector3[n * 2];
        var allUvs = new Vector2[n * 2];
        for (int i = 0; i < n; i++)
        {
            allVerts[i] = verts[i]; allVerts[n + i] = verts[i];
            allUvs[i] = uvs[i]; allUvs[n + i] = uvs[i];
        }
        var backTris = new int[tris.Count];
        for (int i = 0; i < tris.Count; i += 3) { backTris[i] = n + tris[i]; backTris[i + 1] = n + tris[i + 2]; backTris[i + 2] = n + tris[i + 1]; }
        var mesh = new Mesh { vertices = allVerts, uv = allUvs, subMeshCount = 2 };
        mesh.SetTriangles(tris, 0);
        mesh.SetTriangles(backTris, 1);
        mesh.RecalculateNormals();
        return Finish(mesh, list, "Papel " + d.id);
    }

    // 0 under (or within 2 cm of) a paper laid over this one, rising to 1 six centimetres away.
    private static float Freedom(QuadroDados.Documento d, QuadroDados.Documento[] all, Vector2 p)
    {
        float f = 1f;
        foreach (var o in all)
        {
            if (o == d || o.ordem <= d.ordem) continue;
            o.Contem(p, out Vector2 uv);
            float dx = Mathf.Max(0f, Mathf.Abs(uv.x - 0.5f) - 0.5f) * o.tamanho[0];
            float dy = Mathf.Max(0f, Mathf.Abs(uv.y - 0.5f) - 0.5f) * o.tamanho[1];
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            f = Mathf.Min(f, Mathf.Clamp01((dist - 0.02f) / 0.04f));
        }
        return f;
    }

    // ------------------------------------------------------------------ objects, materials, textures

    private static GameObject Part(string name, Transform parent, Mesh mesh, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    private static GameObject Primitive(string name, Transform parent, PrimitiveType type, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = name;
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    private static Material Lit(string name, Color color, float smoothness, Texture2D baseMap = null, Texture2D normal = null,
        float metallic = 0f, bool transparent = false, bool clip = false)
    {
        string path = MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", color);
        m.SetTexture("_BaseMap", baseMap);
        m.SetTexture("_BumpMap", normal);
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Surface", transparent ? 1f : 0f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_Cull", 2f);
        m.SetFloat("_AlphaClip", clip ? 1f : 0f);
        m.SetFloat("_Cutoff", 0.5f);
        m.SetFloat("_ReceiveShadows", 1f);
        BaseShaderGUI.SetMaterialKeywords(m, LitGUI.SetMaterialKeywords);
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>
    /// Matte paper (laminated sheets stay glossy, a sticker satin): the sheet's own relief (waviness, crumples,
    /// folds) as the normal map and a tileable fibre detail every 4 cm, with no environment reflection or
    /// specular highlight on plain paper (they made it read as plastic).
    /// </summary>
    private static void PaperSurface(Material m, QuadroDados.Documento d, Texture2D relief)
    {
        bool laminated = d.acabamento == "plastificado", sticker = d.acabamento == "adesivo";
        m.SetFloat("_Smoothness", laminated ? 0.72f : sticker ? 0.32f : d.acabamento == "desbotado" ? 0.04f : 0.07f);
        m.SetTexture("_BumpMap", relief);
        m.SetFloat("_BumpScale", laminated ? 0.45f : 1f);
        m.SetTexture("_DetailAlbedoMap", Tex(Dir + "/Papel_Fibras_Detalhe.png", false, linear: true));
        m.SetTexture("_DetailNormalMap", Tex(Dir + "/Papel_Fibras_Normal.png", true));
        m.SetFloat("_DetailAlbedoMapScale", laminated ? 0.5f : 1f);
        m.SetFloat("_DetailNormalMapScale", laminated ? 0.25f : 0.8f);
        m.SetTextureScale("_DetailAlbedoMap", new Vector2(d.tamanho[0] / 0.04f, d.tamanho[1] / 0.04f));
        bool shine = laminated || sticker;
        m.SetFloat("_EnvironmentReflections", shine ? 1f : 0f);
        m.SetFloat("_SpecularHighlights", shine ? 1f : 0f);
        BaseShaderGUI.SetMaterialKeywords(m, LitGUI.SetMaterialKeywords);
        // detail keywords as URP's (internal) LitDetailGUI sets them: scaled when the detail albedo scale is not 1
        bool scaled = !Mathf.Approximately(m.GetFloat("_DetailAlbedoMapScale"), 1f);
        if (scaled) { m.DisableKeyword("_DETAIL_MULX2"); m.EnableKeyword("_DETAIL_SCALED"); }
        else { m.DisableKeyword("_DETAIL_SCALED"); m.EnableKeyword("_DETAIL_MULX2"); }
        EditorUtility.SetDirty(m);
    }

    private static Texture2D Tex(string path, bool normal, bool linear = false)
    {
        if (AssetImporter.GetAtPath(path) is TextureImporter ti)
        {
            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = !normal && !linear;
            ti.mipmapEnabled = true;
            ti.anisoLevel = 8;
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // Paper images: full size (read up close in the inspection), mipmaps for the board at a distance, no
    // power-of-two rescale, alpha kept (torn strips).
    private static Texture2D DocTexture(string path)
    {
        if (AssetImporter.GetAtPath(path) is TextureImporter ti)
        {
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.mipmapEnabled = true;
            ti.anisoLevel = 8;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.alphaIsTransparency = true;
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
