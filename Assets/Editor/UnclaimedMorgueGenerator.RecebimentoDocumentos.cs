// Stage 2 papers of the receiving room. Positions are in world metres (the plan's frame) and converted to the room.
using UnityEditor;
using UnityEngine;

public static partial class UnclaimedMorgueGenerator
{
    private const string RecebimentoPaperPrefix = "Documento recebimento - ";

    // Idempotent: removes the previous copies and places every paper again.
    public static void PlaceRecebimentoDocuments(Transform room)
    {
        if (room == null) { Debug.LogWarning("Unclaimed: receiving room not found."); return; }
        foreach (var t in room.GetComponentsInChildren<Transform>(true))
            if (t != null && t.name.StartsWith(RecebimentoPaperPrefix)) Undo.DestroyObjectImmediate(t.gameObject);
        // Mauro's whiteboard on the north wall, above the stretcher guard rail; printed face (-Z) looks south.
        RecebimentoPaper(room, "d02_quadro", new Vector3(9.50f, 1.55f, 11.385f), 0, 0);
        // Night-removal sign on the east wall, beside the patio door (door spans z 7.375-9.375); faces west.
        RecebimentoPaper(room, "d08_aviso_retiradas", new Vector3(12.640f, 1.55f, 9.95f), 0, 90);
        // Leo's intake photo held on the board's free top right corner (right of the title), the stapled note over its
        // bottom right corner. Both sit just in front of the board's face (z 11.3846).
        RecebimentoPaper(room, "d05_foto_entrada", new Vector3(9.825f, 1.745f, 11.381f), 0, 0);
        RecebimentoPaper(room, "d06_bilhete_tala", new Vector3(9.875f, 1.672f, 11.378f), 0, 0);
        PlaceAccessControl(room);
    }

    // Patio door access control: badge reader on both faces of the east wall, south of the door (z 7.375), and the
    // presence sensor over the door inside. Wall faces are found by raycast so finishes of any thickness are respected.
    private static void PlaceAccessControl(Transform room)
    {
        AccessProp(room, "LeitorCracha", "Leitor de cracha - interno", new Vector3(11.5f, 1.25f, 7.02f), Vector3.right);
        AccessProp(room, "LeitorCracha", "Leitor de cracha - patio", new Vector3(14.5f, 1.25f, 7.02f), Vector3.left);
        AccessProp(room, "SensorPresenca", "Sensor de presenca - porta do patio", new Vector3(11.5f, 2.42f, 8.375f), Vector3.right);
    }

    private static void AccessProp(Transform room, string model, string name, Vector3 from, Vector3 dir)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(UnclaimedRecebimentoAssets.PrefabPath(model));
        if (asset == null) { Debug.LogWarning("Unclaimed: build " + model + " first (Recebimento > Build Sensor de Acesso)."); return; }
        Physics.SyncTransforms();
        if (!Physics.Raycast(from, dir, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore))
        { Debug.LogWarning("Unclaimed: no wall found for " + name); return; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, room);
        Undo.RegisterCreatedObjectUndo(go, "Place " + name);
        go.name = RecebimentoPaperPrefix + name;   // same prefix: removed and placed again with the papers
        go.transform.position = hit.point + hit.normal * 0.001f;
        go.transform.rotation = Quaternion.LookRotation(hit.normal, Vector3.up);   // mesh front (+Z) away from the wall
    }

    private static void RecebimentoPaper(Transform room, string id, Vector3 world, float tilt, float yaw)
    {
        string path = UnclaimedDocumentosRecebimento.PrefabPath(id);
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) { Debug.LogWarning("Unclaimed: missing " + path); return; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, room);
        Undo.RegisterCreatedObjectUndo(go, "Place " + id);
        go.name = RecebimentoPaperPrefix + id;
        go.transform.position = world;
        go.transform.rotation = Quaternion.Euler(tilt, yaw, 0);
    }
}
