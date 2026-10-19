// Narrative documents of the reception (Assets/UnclaimedPrototype/Recepcao/Documentos/DocumentosRecepcao.json, images
// rendered by Blender/Recepcao/scripts/documentos_recepcao.py; run that first when the content changes):
//   Tools > Unclaimed > Build Documentos Recepcao   prefabs, paper meshes, materials, texture settings
//   Tools > Unclaimed > Place Documentos Recepcao   (re)places them in the open scene; the map generator places them too
// Prefab frame: origin at the sheet's centre, printed face towards -Z (like the notice board's papers), top = +Y.
// Each sheet carries an ObjetoInspecionavel (E to read it up close: drag turns, wheel zooms).
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class UnclaimedDocumentosRecepcao
{
    public const string Dir = "Assets/UnclaimedPrototype/Recepcao/Documentos";
    private const string MatDir = Dir + "/Materiais";
    public const string GroupName = "Documentos";

    public static string PrefabPath(string id) => UnclaimedRecepcaoAssets.PrefabPath("Documento - " + id);

    [MenuItem("Tools/Unclaimed/Build Documentos Recepcao")]
    public static void Build()
    {
        BuildDocuments(Dir, "DocumentosRecepcao.json", UnclaimedRecepcaoAssets.Folder);
    }

    [System.Serializable] private class Transcript { public string id, transcricao; }
    [System.Serializable] private class Transcripts { public Transcript[] documentos; }

    // Shared build pipeline; directories are parameters so reception output remains unchanged.
    internal static void BuildDocuments(string dir, string jsonName, string prefabFolder)
    {
        string matDir = dir + "/Materiais", meshPathLegacy = dir + "/Malhas.asset";
        var source = AssetDatabase.LoadAssetAtPath<TextAsset>(dir + "/" + jsonName);
        if (source == null) { Debug.LogWarning("Unclaimed: missing " + dir + "/" + jsonName); return; }
        var data = QuadroDados.Carregar(source.text);
        var transcripts = JsonUtility.FromJson<Transcripts>(source.text);
        var built = new List<QuadroDados.Documento>();
        if (!AssetDatabase.IsValidFolder(matDir)) AssetDatabase.CreateFolder(dir, "Materiais");
        AssetDatabase.DeleteAsset(meshPathLegacy);   // old combined asset
        var meshes = new List<Mesh>();
        foreach (var d in data.documentos)
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/" + d.id + ".png") == null)
            { Debug.LogWarning("Unclaimed: missing document texture " + d.id); continue; }
            var root = new GameObject("Documento - " + d.id);
            try
            {
                float w = d.tamanho[0], h = d.tamanho[1];
                Texture2D tex = DocTexture(dir + "/" + d.id + ".png");
                Material front = Lit("Papel - " + d.id, Color.white, tex, matDir: matDir);
                Texture2D relief = UnclaimedQuadroDeAvisos.Tex(dir + "/" + d.id + "_relevo.png", true);
                UnclaimedQuadroDeAvisos.PaperSurface(front, d, relief);
                ColorUtility.TryParseHtmlString(d.papel, out Color paper);
                Material back = Lit("Papel - " + d.id + " (verso)", paper * 0.97f, null, matDir: matDir);
                UnclaimedQuadroDeAvisos.PaperSurface(back, d, relief);
                var mesh = Sheet(d, d.acabamento == "plastificado" ? 0f : 0.0035f);
                mesh.name = "Folha " + d.id;
                // Keep the asset (and its GUID) when it exists: deleting it would orphan the prefab's reference.
                var meshPath = dir + "/Folha " + d.id + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (existing != null) { existing.Clear(); EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); mesh = existing; EditorUtility.SetDirty(mesh); }
                else AssetDatabase.CreateAsset(mesh, meshPath);
                meshes.Add(mesh);
                AssetDatabase.SaveAssets();   // the prefab must reference a mesh that is already on disk
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = root.AddComponent<MeshRenderer>();
                r.sharedMaterials = new[] { front, back };
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var box = root.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0f, -0.002f);
                box.size = new Vector3(w, h, 0.012f);
                if (d.id.StartsWith("aviso")) AddTape(root.transform, w, h);
                if (d.id == "agenda_coleta") AddSheetLight(root.transform);

                var exam = root.AddComponent<ObjetoInspecionavel>();
                exam.nome = d.titulo;
                if (transcripts?.documentos != null)
                    foreach (var entry in transcripts.documentos)
                        if (entry.id == d.id && !string.IsNullOrEmpty(entry.transcricao)) exam.descricao = entry.transcricao;
                exam.distanciaMaxima = 2.2f;
                exam.orientacao = OrientacaoNaInspecao.FaceParaOJogador;
                exam.faceVoltadaParaJogador = EixoLocal.Tras;
                exam.topoNaTela = EixoLocal.Cima;
                exam.distanciaDaCamera = 0.5f;
                exam.tamanhoNaTela = Mathf.Max(w, h) > 0.25f ? 0.47f : 0.45f;
                exam.limiteDistancia = new Vector2(0.16f, 0.75f);
                exam.zoomPorScroll = 0.05f;
                exam.ocultarOriginal = true;
                exam.travarMovimentoDoJogador = true;
                exam.duracaoAnimacao = 0.35f;
                PrefabUtility.SaveAsPrefabAsset(root, prefabFolder + "/Documento - " + d.id + ".prefab");
                built.Add(d);
            }
            finally { Object.DestroyImmediate(root); }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        // Deleting and recreating a mesh asset in the same pass can leave the first prefab pointing at a stale
        // object: bind every prefab to the mesh as it is now on disk.
        foreach (var d in built)
        {
            AssetDatabase.ImportAsset(dir + "/Folha " + d.id + ".asset", ImportAssetOptions.ForceUpdate);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(dir + "/Folha " + d.id + ".asset");
            var contents = PrefabUtility.LoadPrefabContents(prefabFolder + "/Documento - " + d.id + ".prefab");
            try
            {
                contents.GetComponent<MeshFilter>().sharedMesh = mesh;
                PrefabUtility.SaveAsPrefabAsset(contents, prefabFolder + "/Documento - " + d.id + ".prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            AssetDatabase.ImportAsset(prefabFolder + "/Documento - " + d.id + ".prefab", ImportAssetOptions.ForceUpdate);
        }
        Debug.Log("Unclaimed: built " + meshes.Count + " documents in " + dir + ".");
    }

    // Outdoor sheet (patio wall, no light of its own): a small spot in front of it. The inspected copy is a clone of
    // the prefab, so the light travels with it and the text stays readable up close.
    private static void AddSheetLight(Transform sheet)
    {
        var obj = new GameObject("Luz da folha");
        obj.transform.SetParent(sheet, false);
        obj.transform.localPosition = new Vector3(0f, 0.75f, -0.4f);
        obj.transform.LookAt(sheet.position);
        var l = obj.AddComponent<Light>();
        l.type = LightType.Spot;
        l.spotAngle = 75f;
        l.innerSpotAngle = 40f;
        l.color = new Color(1f, 0.86f, 0.66f);
        l.intensity = 1.8f;
        l.range = 3f;
        l.shadows = LightShadows.None;
        l.lightmapBakeType = LightmapBakeType.Realtime;
    }

    [MenuItem("Tools/Unclaimed/Place Documentos Recepcao")]
    public static void PlaceMenu()
    {
        Transform room = null;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if (t.name == "Recepcao" && t.parent != null && t.parent.name == "Ambientes") room = t;
        if (room == null) { Debug.LogWarning("Unclaimed: Ambientes/Recepcao not found."); return; }
        UnclaimedMorgueGenerator.PlaceRecepcaoDocuments(room);
        UnclaimedMorgueGenerator.PlacePatioDocuments();
        EditorSceneManager.MarkSceneDirty(room.gameObject.scene);
    }

    // Sheet facing -Z, gridded so its corners can curl a few millimetres towards the viewer (loose paper).
    private static Mesh Sheet(QuadroDados.Documento d, float curl)
    {
        const int nx = 12, ny = 12;
        float w = d.tamanho[0], h = d.tamanho[1];
        var v = new Vector3[(nx + 1) * (ny + 1)];
        var uv = new Vector2[v.Length];
        for (int j = 0; j <= ny; j++)
            for (int i = 0; i <= nx; i++)
            {
                float u = i / (float)nx, t = j / (float)ny;
                float e = Mathf.Pow(Mathf.Max(Mathf.Abs(u - 0.5f), Mathf.Abs(t - 0.5f)) * 2f, 3f);
                v[j * (nx + 1) + i] = new Vector3((u - 0.5f) * w, (t - 0.5f) * h, -(0.0004f + curl * e));
                uv[j * (nx + 1) + i] = new Vector2(u, t);
            }
        var front = new List<int>();
        for (int j = 0; j < ny; j++)
            for (int i = 0; i < nx; i++)
            {
                int a = j * (nx + 1) + i, b = a + 1, c = a + nx + 1, e = c + 1;
                front.AddRange(new[] { a, c, e, a, e, b });   // seen from -Z (u right = +X) this winds clockwise
            }
        int n = v.Length;
        var all = new Vector3[n * 2]; var auv = new Vector2[n * 2];
        for (int i = 0; i < n; i++) { all[i] = v[i]; all[n + i] = v[i] + Vector3.forward * 0.0003f; auv[i] = uv[i]; auv[n + i] = new Vector2(1f - uv[i].x, uv[i].y); }
        var backTris = new List<int>();
        for (int i = 0; i < front.Count; i += 3) backTris.AddRange(new[] { n + front[i], n + front[i + 2], n + front[i + 1] });
        var mesh = new Mesh { vertices = all, uv = auv, subMeshCount = 2 };
        mesh.SetTriangles(front, 0);
        mesh.SetTriangles(backTris, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    // Two strips of clear tape over the top corners (the notice is stuck to the wall).
    private static void AddTape(Transform parent, float w, float h)
    {
        Material tape = Lit("Fita adesiva", new Color(0.97f, 0.95f, 0.86f, 0.45f), null, transparent: true);
        tape.SetFloat("_Smoothness", 0.75f);
        foreach (int s in new[] { -1, 1 })
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = "Fita";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(s * (w / 2 - 0.012f), h / 2 - 0.006f, -0.0016f);
            go.transform.localRotation = Quaternion.Euler(0f, 180f, -s * 35f);   // quad faces +Z by default: turn it to -Z
            go.transform.localScale = new Vector3(0.034f, 0.014f, 1f);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = tape;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    private static Material Lit(string name, Color color, Texture2D baseMap, bool transparent = false, string matDir = MatDir)
    {
        string path = matDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", color);
        m.SetTexture("_BaseMap", baseMap);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Smoothness", 0.07f);
        m.SetFloat("_Surface", transparent ? 1f : 0f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_Cull", 2f);
        m.SetFloat("_ReceiveShadows", 1f);
        BaseShaderGUI.SetMaterialKeywords(m, LitGUI.SetMaterialKeywords);
        EditorUtility.SetDirty(m);
        return m;
    }

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
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
