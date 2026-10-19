// Reception-archive door: a closed leaf that is bolted from the archive side. From the reception it stays
// locked ("trancada"); the surface bolt on the archive face (FerrolhoDePorta) frees it for good, so the
// door only becomes a shortcut after the player has reached the archive through the circulation door.
using UnityEditor;
using UnityEngine;

public static partial class UnclaimedMorgueGenerator
{
    private const string ArchiveDoorName = "Porta recepcao - arquivo";

    // Replaces the door in the saved scene without regenerating the map; Generate does the same.
    [MenuItem("Tools/Unclaimed/Place Archive Door")]
    public static void PlaceArchiveDoor()
    {
        Transform group = null;
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if (t.name == ArchiveDoorName && t.parent != null && t.gameObject.scene.IsValid()) { group = t; break; }
        if (group == null) { Debug.LogWarning("Unclaimed: door group '" + ArchiveDoorName + "' not found."); return; }
        foreach (Door d in Doors)
        {
            if (d.Name != ArchiveDoorName) continue;
            var prefab = d.Frame != null ? AssetDatabase.LoadAssetAtPath<GameObject>(d.Frame) : null;
            for (int i = group.childCount - 1; i >= 0; i--)
            {
                var child = group.GetChild(i).gameObject;
                var source = PrefabUtility.GetCorrespondingObjectFromSource(child);
                bool previousPrefab = child.name == UnclaimedRecepcaoAssets.ArchiveDoorPrefab ||
                    (source != null && source.name == UnclaimedRecepcaoAssets.ArchiveDoorPrefab);
                bool primitiveFrame = child.name == "Batente" || child.name == "Batente superior" ||
                    child.name == "Soleira" || child.name == "Guarnicao";
                if (child.name == "Folha" || previousPrefab || (prefab != null && primitiveFrame))
                    Undo.DestroyObjectImmediate(child);
            }
            if (prefab == null) ArchiveDoorLeaf(group, d);
            else
            {
                var frame = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
                Undo.RegisterCreatedObjectUndo(frame, "Create " + d.Name + " frame");
                frame.transform.SetPositionAndRotation(new Vector3(d.Line, 0f, d.Center), Quaternion.Euler(0f, d.Yaw, 0f));
                foreach (FolhaDePorta leaf in frame.GetComponentsInChildren<FolhaDePorta>()) leaf.AplicarEstadoInicial();
            }
            break;
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(group.gameObject.scene);
    }

    private static void ArchiveDoorLeaf(Transform group, Door d)
    {
        const float gap = 0.005f, thickness = 0.04f, leafBottom = 0.02f, leafTop = DoorHeight - FrameLining - 0.005f;
        float a = d.Center - d.Width / 2 + FrameLining + gap, width = d.Width - 2 * (FrameLining + gap);
        Material leafMat = Mat(BaseFolder, "Porta do arquivo", new Color(0.46f, 0.36f, 0.27f), 0.3f);
        Material metal = Mat(BaseFolder, "Ferragem da porta", new Color(0.62f, 0.63f, 0.62f), 0.55f, 0.85f);

        var leaf = new GameObject("Folha");
        Undo.RegisterCreatedObjectUndo(leaf, "Create archive door leaf");
        leaf.transform.SetParent(group, false);
        leaf.transform.SetPositionAndRotation(new Vector3(d.Line, 0f, a), Quaternion.identity);   // hinge on the south jamb
        var box = leaf.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, (leafBottom + leafTop) / 2, width / 2);
        box.size = new Vector3(thickness, leafTop - leafBottom, width);
        var body = leaf.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        var audio = leaf.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 1f;

        Local("Painel", leaf.transform, box.center, box.size, leafMat);
        foreach (float side in new[] { -1f, 1f })
        {
            Local("Maçaneta", leaf.transform, new Vector3(side * (thickness / 2 + 0.03f), 1.0f, width - 0.1f), new Vector3(0.06f, 0.025f, 0.12f), metal);
            Local("Espelho", leaf.transform, new Vector3(side * (thickness / 2 + 0.004f), 1.0f, width - 0.1f), new Vector3(0.008f, 0.16f, 0.05f), metal);
        }

        var door = leaf.AddComponent<FolhaDePorta>();
        door.sentido = 1;                       // +Z swings towards +X, into the archive
        door.anguloMaximo = 100f;
        door.duracao = 1f;
        door.alcance = 2f;
        door.iniciarAberta = false;
        door.fonte = audio;
        door.textoAbrir = "Abrir porta";
        door.textoFechar = "Fechar porta";
        door.trancada = true;
        door.chaveNecessaria = "";
        door.textoTrancada = "Porta trancada";
        door.avisoSemChave = "Está trancada. Parece haver um ferrolho do outro lado.";
        door.AplicarEstadoInicial();

        // Surface bolt on the archive face, near the latch edge.
        float x = thickness / 2 + 0.012f, z = width - 0.3f;
        var bolt = new GameObject("Ferrolho");
        bolt.transform.SetParent(leaf.transform, false);
        bolt.transform.localPosition = new Vector3(x, 1.4f, z);
        var trigger = bolt.AddComponent<BoxCollider>();
        trigger.size = new Vector3(0.08f, 0.14f, 0.32f);
        trigger.center = new Vector3(0.02f, 0f, 0.1f);
        Local("Base do ferrolho", bolt.transform, new Vector3(0f, 0f, 0.1f), new Vector3(0.014f, 0.05f, 0.24f), metal);
        var bar = Local("Trinco", bolt.transform, new Vector3(0.02f, 0f, 0.14f), new Vector3(0.016f, 0.018f, 0.25f), metal);
        Local("Pino do trinco", bar.transform, new Vector3(0.5f, 1.6f, -0.1f), new Vector3(1.8f, 2.4f, 0.12f), metal);
        var latch = bolt.AddComponent<FerrolhoDePorta>();
        latch.porta = door;
        latch.trinco = bar.transform;
        latch.deslocamento = new Vector3(0f, 0f, -0.16f);
        latch.fonte = audio;
        latch.alcance = 1.8f;
    }

    // A leaf-local visual box (no collider).
    private static GameObject Local(string name, Transform parent, Vector3 center, Vector3 size, Material material)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(obj.GetComponent<Collider>());
        Undo.RegisterCreatedObjectUndo(obj, "Create " + name);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = center;
        obj.transform.localScale = size;
        obj.GetComponent<Renderer>().sharedMaterial = material;
        return obj;
    }
}
