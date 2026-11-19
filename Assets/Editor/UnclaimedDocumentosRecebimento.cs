// Stage 2 papers of the receiving room (Assets/UnclaimedPrototype/Recebimento/Documentos/DocumentosRecebimento.json,
// images rendered by Blender/Recebimento/scripts/documentos_recebimento.py; run that first when the content changes):
//   Tools > Unclaimed > Recebimento > Build Documentos   prefabs through the reception's shared document pipeline
//   Tools > Unclaimed > Recebimento > Place Documentos   (re)places them in the open scene; the map generator places them too
// Access control of the patio door (D07's hardware; the log itself is on the reception computer):
//   Tools > Unclaimed > Recebimento > Build Sensor de Acesso   prefabs from LeitorCracha.fbx and SensorPresenca.fbx
//   (modelled in Blender/Recebimento/SensorAcesso.blend; mesh front = +Z, back on the wall plane z = 0)
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class UnclaimedDocumentosRecebimento
{
    public const string Dir = "Assets/UnclaimedPrototype/Recebimento/Documentos";
    public static string PrefabFolder => UnclaimedRecebimentoAssets.Folder + "/Prefabs";
    public static string PrefabPath(string id) => PrefabFolder + "/Documento - " + id + ".prefab";

    [MenuItem("Tools/Unclaimed/Recebimento/Build Documentos")]
    public static void Build() => UnclaimedDocumentosRecepcao.BuildDocuments(Dir, "DocumentosRecebimento.json", PrefabFolder);

    public static readonly string[] AccessModels = { "LeitorCracha", "SensorPresenca" };

    [MenuItem("Tools/Unclaimed/Recebimento/Build Sensor de Acesso")]
    public static void BuildAccess()
    {
        string folder = UnclaimedRecebimentoAssets.Folder;
        if (!AssetDatabase.IsValidFolder(folder + "/Materiais")) AssetDatabase.CreateFolder(folder, "Materiais");
        foreach (string name in AccessModels)
        {
            string path = folder + "/" + name + ".fbx";
            if (!(AssetImporter.GetAtPath(path) is ModelImporter mi)) { Debug.LogWarning("Unclaimed: missing " + path); continue; }
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importCameras = false; mi.importLights = false; mi.importAnimation = false;
            mi.SaveAndReimport();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var go = (GameObject)Object.Instantiate(source);
            go.name = name;
            try
            {
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
                {
                    var mesh = r.GetComponent<MeshFilter>().sharedMesh;
                    var mats = new Material[mesh.subMeshCount];
                    // Slot names come from the Blender materials (FBX keeps their order); one URP material each.
                    string[] slots = name == "LeitorCracha"
                        ? new[] { "plastico grafite", "painel preto", "led verde", "led vermelho" }
                        : new[] { "plastico branco", "lente leitosa", "led vermelho" };
                    for (int i = 0; i < mats.Length && i < slots.Length; i++) mats[i] = AccessMaterial(folder + "/Materiais/Acesso - " + slots[i] + ".mat", slots[i]);
                    r.sharedMaterials = mats;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    var box = r.gameObject.AddComponent<BoxCollider>();
                    box.center = mesh.bounds.center; box.size = mesh.bounds.size;
                }
                PrefabUtility.SaveAsPrefabAsset(go, folder + "/Prefabs/" + name + ".prefab");
            }
            finally { Object.DestroyImmediate(go); }
        }
        AssetDatabase.SaveAssets();
    }

    private static Material AccessMaterial(string path, string slot)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        (Color c, float smooth) = slot switch
        {
            "plastico grafite" => (new Color(.16f, .165f, .175f), .35f),
            "painel preto" => (new Color(.03f, .032f, .035f), .85f),
            "led verde" => (new Color(.1f, .75f, .25f), .6f),
            "led vermelho" => (new Color(.6f, .06f, .05f), .6f),
            "plastico branco" => (new Color(.82f, .82f, .79f), .35f),
            _ => (new Color(.88f, .88f, .86f), .55f),
        };
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Smoothness", smooth);
        if (slot == "led verde")
        {
            // The reader is on: its green LED glows faintly (enough to be seen in the dark room).
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(.15f, 1f, .35f) * 2.2f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        BaseShaderGUI.SetMaterialKeywords(m, LitGUI.SetMaterialKeywords);
        EditorUtility.SetDirty(m);
        return m;
    }

    [MenuItem("Tools/Unclaimed/Recebimento/Place Documentos")]
    public static void Place()
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if (t.name == "Recebimento de corpos" && t.parent != null && t.parent.name == "Ambientes")
            {
                UnclaimedMorgueGenerator.PlaceRecebimentoDocuments(t);
                EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
                return;
            }
        Debug.LogWarning("Unclaimed: Ambientes/Recebimento de corpos not found.");
    }
}
