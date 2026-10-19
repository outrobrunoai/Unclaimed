// Baked Blender furniture for the archive / office. Frames map Blender (x,y,z) to Unity (x,z,y).
// Run the Pillow renderers first, then this menu; missing inputs are warned and skipped independently.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class UnclaimedAdministracaoAssets
{
    public const string Folder = "Assets/Modelos/Administracao";
    private const string Materials = Folder + "/Materiais";
    public static string PrefabPath(string name) => Folder + "/" + name + ".prefab";
    private static readonly (string file, string prefab)[] Assets =
    {
        ("MesaServidor", "Mesa do servidor"), ("ArquivoAco", "Arquivo de aco"),
        ("ArmarioPertences", "Armario de pertences"), ("EstanteAco", "Estante de aco"),
        ("EstanteCoordenacao", "Estante da coordenacao"), ("Bancada", "Bancada"),
        ("MesaApoio", "Mesa de apoio"), ("Impressora", "Impressora"),
        ("PastasManuais", "Pastas de manuais"), ("BolsaPertences", "Bolsa de pertences"),
        ("Gravador", "Gravador"), ("EnvelopeGuarda", "Envelope de guarda"),
        ("CaixaArquivo", "Caixa arquivo"), ("PastaContrato", "Pasta do contrato"),
    };

    [MenuItem("Tools/Unclaimed/Build Administracao Prefabs")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) { Debug.LogWarning("Unclaimed: missing " + Folder); return; }
        if (!AssetDatabase.IsValidFolder(Materials)) AssetDatabase.CreateFolder(Folder, "Materiais");
        foreach (var a in Assets)
        {
            Mesh mesh = Import(a.file, out Material material);
            if (mesh == null) continue;
            var root = new GameObject(a.prefab);
            try
            {
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                root.AddComponent<MeshRenderer>().sharedMaterial = material;
                // Inspected props must not be static: static batching keeps drawing the inspection copy where the
                // original lies.
                bool inspected = a.file == "BolsaPertences" || a.file == "Gravador" || a.file == "EnvelopeGuarda";
                root.isStatic = !inspected;
                BodyColliders(root, a.file, mesh.bounds);
                Complement(root.transform, a.file);
                if (a.file == "BolsaPertences" || a.file == "Gravador" || a.file == "EnvelopeGuarda")
                    Inspect(root, a.prefab, a.file == "BolsaPertences" ? .40f : .30f, a.file == "BolsaPertences" ? EixoLocal.Tras : EixoLocal.Cima);
                ApplyLabels(root.transform, a.prefab, "1");
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(a.prefab));
            }
            finally { Object.DestroyImmediate(root); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Unclaimed: administration prefabs rebuilt in " + Folder);
    }

    private static Mesh Import(string file, out Material material)
    {
        material = null;
        string path = Folder + "/" + file + ".fbx", prefix = Folder + "/Texturas/" + file;
        foreach (string suffix in new[] { "_BaseMap", "_MetallicSmoothness", "_Normal", "_Occlusion" })
            if (!File.Exists(prefix + suffix + ".png"))
            { Debug.LogWarning("Unclaimed: missing " + prefix + suffix + ".png; skipping " + file); return null; }
        if (!(AssetImporter.GetAtPath(path) is ModelImporter model))
        { Debug.LogWarning("Unclaimed: missing " + path); return null; }
        model.globalScale = 1f;
        model.materialImportMode = ModelImporterMaterialImportMode.None;
        model.importAnimation = model.importCameras = model.importLights = model.importBlendShapes = false;
        model.meshCompression = ModelImporterMeshCompression.Off;
        model.importNormals = ModelImporterNormals.Import;
        model.importTangents = ModelImporterTangents.CalculateMikk;
        model.SaveAndReimport();
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null) { Debug.LogWarning("Unclaimed: missing mesh in " + path); return null; }
        string mp = Materials + "/" + file + ".mat";
        material = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = file };
            AssetDatabase.CreateAsset(material, mp);
        }
        material.SetColor("_BaseColor", Color.white);
        material.SetTexture("_BaseMap", Tex(prefix + "_BaseMap.png", true));
        material.SetTexture("_MetallicGlossMap", Tex(prefix + "_MetallicSmoothness.png", false));
        material.SetTexture("_BumpMap", Tex(prefix + "_Normal.png", false, true));
        material.SetTexture("_OcclusionMap", Tex(prefix + "_Occlusion.png", false));
        material.SetFloat("_Metallic", 1f);
        material.SetFloat("_Smoothness", 1f);
        material.SetFloat("_SmoothnessTextureChannel", 0f);
        material.SetFloat("_BumpScale", 1f);
        material.SetFloat("_OcclusionStrength", 1f);
        // Column B uses a negative-X mesh transform: keep both faces visible under the reversed winding.
        material.SetFloat("_Cull", file == "PortaPertences" ? 0f : 2f);
        BaseShaderGUI.SetMaterialKeywords(material, LitGUI.SetMaterialKeywords);
        EditorUtility.SetDirty(material);
        return mesh;
    }

    private static Texture2D Tex(string path, bool srgb, bool normal = false)
    {
        if (AssetImporter.GetAtPath(path) is TextureImporter ti)
        {
            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = srgb;
            ti.maxTextureSize = path.EndsWith("Etiquetas_Admin.png") ? 4096 : 2048;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.anisoLevel = 8;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static void Box(GameObject go, float x, float y, float z, float w, float h, float d)
    {
        var box = go.AddComponent<BoxCollider>();
        box.center = new Vector3(x, y, z); box.size = new Vector3(w, h, d);
    }

    private static void BodyColliders(GameObject go, string file, Bounds bounds)
    {
        switch (file)
        {
            case "PastasManuais": return;
            case "MesaServidor":
                Box(go, 0, .7475f, 0, 1.60f, .025f, .70f);
                foreach (float x in new[] { -.7875f, .7875f }) Box(go, x, .3675f, 0, .025f, .735f, .68f);
                Box(go, 0, .5175f, -.321f, 1.55f, .435f, .018f);
                Box(go, .359f, .4125f, .009f, .018f, .645f, .642f);
                Box(go, .5715f, .4125f, -.303f, .407f, .645f, .018f);
                Box(go, .5625f, .054f, .018f, .425f, .108f, .624f);
                return;
            case "ArquivoAco":
                Box(go, 0, 1.32f, .0025f, .47f, .02f, .615f);
                foreach (float x in new[] { -.2314f, .2314f }) Box(go, x, .68f, .0025f, .0072f, 1.26f, .615f);
                Box(go, 0, .68f, .3064f, .4556f, 1.26f, .0072f);
                Box(go, 0, .025f, 0, .45f, .05f, .58f);
                for (int k = 0; k <= 4; k++) Box(go, 0, .05f + k * .315f, -.2875f, .4556f, .016f, .035f);
                return;
            case "ArmarioPertences":
                Box(go, 0, 1.835f, 0, .92f, .03f, .50f);
                Box(go, 0, .115f, 0, .92f, .03f, .50f);
                foreach (float x in new[] { -.456f, .456f }) Box(go, x, .975f, 0, .008f, 1.75f, .5f);
                Box(go, 0, .96f, .246f, .904f, 1.72f, .008f);
                Box(go, 0, .975f, -.004f, .03f, 1.69f, .492f);
                foreach (int level in new[] { 1, 2 }) Box(go, 0, CellBase(level) - .015f, -.004f, .904f, .03f, .492f);
                foreach (float x in new[] { -.43f, .43f }) foreach (float z in new[] { -.22f, .22f }) Box(go, x, .05f, z, .04f, .10f, .04f);
                return;
            case "EstanteAco": case "EstanteCoordenacao":
                foreach (float x in new[] { -.4425f, .4425f }) foreach (float z in new[] { -.1825f, .1825f }) Box(go, x, 1, z, .035f, 2, .035f);
                foreach (float h in new[] { .10f, .44f, .78f, 1.12f, 1.46f, 1.80f })
                {
                    Box(go, 0, h - .0005f, 0, .914f, .001f, .394f);
                    foreach (float z in new[] { -.1955f, .1955f }) Box(go, 0, h - .015f, z, .914f, .03f, .003f);
                }
                return;
            case "Bancada":
                Box(go, 0, .885f, 0, 2.4f, .03f, .6f);
                foreach (float x in new[] { -1.15f, 0, 1.15f }) foreach (float z in new[] { -.25f, .25f }) Box(go, x, .435f, z, .04f, .87f, .04f);
                Box(go, 0, .159f, .02f, 2.34f, .018f, .5f); return;
            case "MesaApoio":
                Box(go, 0, .7275f, 0, .8f, .025f, .5f);
                foreach (float x in new[] { -.37f, .37f }) foreach (float z in new[] { -.22f, .22f }) Box(go, x, .3635f, z, .03f, .703f, .03f);
                Box(go, 0, .169f, 0, .77f, .018f, .47f); return;
            default:
                Box(go, bounds.center.x, bounds.center.y, bounds.center.z, bounds.size.x, Mathf.Max(.006f, bounds.size.y), bounds.size.z); return;
        }
    }

    // pertences_cells(): same column widths and top-to-bottom numbering as admin_modelo.py.
    public const float CellWidth = .415f, CellHeight = 1.63f / 3f;
    public static float CellBase(int level) => 1.82f - (level - 1) * (CellHeight + .03f) - CellHeight;
    public static float CellX(bool columnB) => columnB ? .2225f : -.2225f;

    private static PecaDeMovel Moving(Transform root, string name, string file, Vector3 pos, bool ownFrame, float mirror = 1f)
    {
        Mesh mesh = Import(file, out Material mat);
        if (mesh == null) return null;
        var part = new GameObject(name).transform;
        part.SetParent(root, false); part.localPosition = pos;
        var visual = new GameObject("Malha"); visual.transform.SetParent(part, false);
        visual.transform.localPosition = ownFrame ? Vector3.zero : -pos;
        visual.transform.localScale = new Vector3(mirror, 1, 1);
        visual.AddComponent<MeshFilter>().sharedMesh = mesh;
        visual.AddComponent<MeshRenderer>().sharedMaterial = mat;
        var rb = part.gameObject.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
        var p = part.gameObject.AddComponent<PecaDeMovel>(); p.movel = root;
        p.fonte = part.gameObject.AddComponent<AudioSource>(); p.fonte.playOnAwake = false;
        p.fonte.spatialBlend = 1f; p.fonte.minDistance = .5f; p.fonte.maxDistance = 10f;
        p.tipo = PecaDeMovel.TipoDePeca.Gaveta; p.eixo = Vector3.back; p.curso = .32f; p.duracao = .6f; p.passoDeTeste = .01f;
        p.textoAbrir = "Abrir gaveta"; p.textoFechar = "Fechar gaveta";
        if (file.StartsWith("GavetaMesa"))
        {
            // Desk drawers are modelled closed in the furniture frame.
            p.eixo = Vector3.forward; p.curso = .40f;
            var b = mesh.bounds;
            Box(part.gameObject, b.center.x, b.center.y, b.center.z, b.size.x, b.size.y, b.size.z);
        }
        return p;
    }

    private static void Complement(Transform root, string file)
    {
        if (file == "MesaServidor")
        {
            Mesh equipment = Import("EquipamentosMesa", out Material mat);
            if (equipment != null)
            {
                var go = new GameObject("Equipamentos da mesa"); go.transform.SetParent(root, false); go.isStatic = true;
                go.AddComponent<MeshFilter>().sharedMesh = equipment; go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            }
            // GavetaMesa1 (bottom) through GavetaMesa3 (top), imported with their baked materials.
            for (int i = 1; i <= 3; i++) Moving(root, "Gaveta " + i, "GavetaMesa" + i, Vector3.zero, false);
        }
        if (file == "ArquivoAco")
            for (int i = 1; i <= 4; i++)
            {
                var p = Moving(root, "Gaveta " + i, "GavetaArquivo", new Vector3(0, .05f + (4 - i) * .315f, -.31f), true);
                if (p == null) continue;
                p.curso = .50f;
                // Open shell: front, two sides, rear and bottom, never a lid across the folders.
                Box(p.gameObject, 0, .1575f, -.01f, .462f, .303f, .02f);
                foreach (float x in new[] { -.2075f, .2075f }) Box(p.gameObject, x, .1515f, .27f, .005f, .263f, .54f);
                Box(p.gameObject, 0, .1515f, .5375f, .42f, .263f, .005f);
                Box(p.gameObject, 0, .022f, .27f, .42f, .004f, .54f);
            }
        if (file == "ArmarioPertences")
            foreach (bool b in new[] { false, true }) for (int level = 1; level <= 3; level++)
            {
                float s = b ? -1 : 1;
                var pos = new Vector3(b ? .428f : -.428f, CellBase(level) + .002f, -.25f);
                var p = Moving(root, "Porta " + (b ? "B" : "A") + level, "PortaPertences", pos, true, s);
                if (p == null) continue;
                p.tipo = PecaDeMovel.TipoDePeca.Porta; p.eixo = Vector3.up; p.curso = s * 105f; p.passoDeTeste = 1f;
                p.textoAbrir = "Abrir compartimento"; p.textoFechar = "Fechar compartimento";
                Box(p.gameObject, s * (CellWidth - .004f) / 2, (CellHeight - .004f) / 2, -.009f, CellWidth - .004f, CellHeight - .004f, .018f);
            }
    }

    private static void Inspect(GameObject go, string name, float size, EixoLocal face)
    {
        var exam = go.AddComponent<ObjetoInspecionavel>(); exam.nome = name;
        exam.distanciaMaxima = 2.2f; exam.orientacao = OrientacaoNaInspecao.FaceParaOJogador;
        exam.faceVoltadaParaJogador = face; exam.topoNaTela = face == EixoLocal.Cima ? EixoLocal.Frente : EixoLocal.Cima;
        exam.distanciaDaCamera = .45f; exam.tamanhoNaTela = size; exam.limiteDistancia = new Vector2(.16f, .75f);
        exam.zoomPorScroll = .05f; exam.ocultarOriginal = true; exam.travarMovimentoDoJogador = true; exam.duracaoAnimacao = .35f;
    }

    [Serializable] private class LabelData { public int[] atlas; public Label[] etiquetas; }
    [Serializable] private class Label
    {
        public string id, prefab, parte, nome, texto, variante;
        public float[] centro, tamanho, normal; public int[] rect;
    }

    // Variants 1/2/3 are chosen per cabinet and per bag instance by the map generator.
    // Every label is parented in the moving part's unmirrored pivot frame; B card positions are already mirrored.
    public static void ApplyLabels(Transform root, string prefab, string variant = "1")
    {
        var source = AssetDatabase.LoadAssetAtPath<TextAsset>(Folder + "/Etiquetas.json");
        if (source == null) { Debug.LogWarning("Unclaimed: missing administration label JSON"); return; }
        var data = JsonUtility.FromJson<LabelData>(source.text);
        var texture = Tex(Folder + "/Texturas/Etiquetas_Admin.png", true);
        if (texture == null) { Debug.LogWarning("Unclaimed: missing administration label atlas"); return; }
        if (!AssetDatabase.IsValidFolder(Materials)) AssetDatabase.CreateFolder(Folder, "Materiais");
        Mesh quad = LabelMesh();
        // Disable the previous variant: property overrides also work on connected prefab instances.
        foreach (var label in data.etiquetas)
        {
            if (label.prefab != prefab) continue;
            Transform parent = string.IsNullOrEmpty(label.parte) ? root : root.Find(label.parte);
            if (parent == null) continue;
            var old = parent.Find("Etiqueta impressa - " + label.id);
            if (old != null) old.gameObject.SetActive(false);
        }
        foreach (var label in data.etiquetas)
        {
            if (label.prefab != prefab || (!string.IsNullOrEmpty(label.variante) && label.variante != variant)) continue;
            Transform parent = string.IsNullOrEmpty(label.parte) ? root : root.Find(label.parte);
            if (parent == null) continue;
            string path = Materials + "/Etiqueta " + label.id + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            mat.SetColor("_BaseColor", Color.white); mat.SetTexture("_BaseMap", texture);
            mat.SetTextureScale("_BaseMap", new Vector2((label.rect[2] - 8f) / data.atlas[0], (label.rect[3] - 8f) / data.atlas[1]));
            mat.SetTextureOffset("_BaseMap", new Vector2((label.rect[0] + 4f) / data.atlas[0], 1f - (label.rect[1] + label.rect[3] - 4f) / data.atlas[1]));
            mat.SetFloat("_Smoothness", .12f); mat.SetFloat("_Metallic", 0); mat.SetFloat("_Cull", 0);
            EditorUtility.SetDirty(mat);
            var existing = parent.Find("Etiqueta impressa - " + label.id);
            var go = existing != null ? existing.gameObject : new GameObject("Etiqueta impressa - " + label.id);
            if (existing == null) go.transform.SetParent(parent, false);
            go.SetActive(true);
            Vector3 n = new Vector3(label.normal[0], label.normal[1], label.normal[2]).normalized;
            go.transform.localPosition = new Vector3(label.centro[0], label.centro[1], label.centro[2]) + n * .0003f;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.back, n);
            go.transform.localScale = new Vector3(label.tamanho[0], label.tamanho[1], 1);
            var filter = go.GetComponent<MeshFilter>();
            if (filter == null) filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = quad;
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer == null) renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            go.isStatic = parent.GetComponentInParent<PecaDeMovel>() == null && root.gameObject.isStatic;
        }
    }

    private static Mesh LabelMesh()
    {
        const string path = Folder + "/Etiqueta impressa.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null) return mesh;
        mesh = new Mesh { name = "Etiqueta impressa", vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(.5f,-.5f,0), new Vector3(.5f,.5f,0), new Vector3(-.5f,.5f,0) },
            uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up }, triangles = new[] { 0,2,1,0,3,2 } };
        mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path); return mesh;
    }
}
