// Interior door kit on the saved map (the generator places the same prefabs through Door.Frame): swaps the generic
// jambs, casings and sill of each kit door for its prefab without regenerating the map. Skirting and dado rails that
// stopped at the old 7 cm casings are first run back to the opening edge; Update Wall Finishes then cuts them at the
// new frame's casing.
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class UnclaimedMorgueGenerator
{
    private static readonly string[] GenericFramePieces = { "Batente", "Batente superior", "Guarnicao", "Soleira" };

    private static bool IsKitDoor(Door d) => d.Frame != null && d.Frame.StartsWith(UnclaimedPortasAssets.Folder);

    [MenuItem("Tools/Unclaimed/Portas/Install Interior Doors")]
    public static void InstallInteriorDoors()
    {
        GameObject morgue = FindExistingMorgue(SceneManager.GetActiveScene());
        if (morgue == null || morgue.name != GroupName || Application.isPlaying)
        {
            Debug.LogWarning("Unclaimed: open PrototipoNecroterio in Edit Mode first.");
            return;
        }
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Install interior doors");
        root = morgue.transform;
        claddingGroup = root.Find("Revestimentos");
        skirtingGroup = root.Find("Rodapes");
        envelopeGroup = root.Find("Fechamento externo");
        doorsGroup = root.Find("Portas");
        roomsGroup = root.Find("Ambientes");

        RestoreStripsToOpenings();
        int placed = 0;
        foreach (Door d in Doors)
        {
            if (!IsKitDoor(d)) continue;
            Transform g = doorsGroup.Find(d.Name);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(d.Frame);
            if (g == null || prefab == null) { Debug.LogWarning("Unclaimed: cannot install " + d.Name); continue; }
            for (int i = g.childCount - 1; i >= 0; i--)
            {
                Transform c = g.GetChild(i);
                bool generic = System.Array.IndexOf(GenericFramePieces, c.name) >= 0 && PrefabUtility.GetCorrespondingObjectFromSource(c.gameObject) == null;
                var src = PrefabUtility.GetCorrespondingObjectFromSource(c.gameObject);
                bool oldKit = src != null && AssetDatabase.GetAssetPath(src).StartsWith(UnclaimedPortasAssets.Folder);
                if (generic || oldKit) Undo.DestroyObjectImmediate(c.gameObject);
            }
            var frame = (GameObject)PrefabUtility.InstantiatePrefab(prefab, g);
            Undo.RegisterCreatedObjectUndo(frame, "Create " + d.Name + " frame");
            frame.transform.SetPositionAndRotation(d.Vertical ? new Vector3(d.Line, 0f, d.Center) : new Vector3(d.Center, 0f, d.Line),
                Quaternion.Euler(0f, d.Yaw, 0f));
            foreach (FolhaDePorta leaf in frame.GetComponentsInChildren<FolhaDePorta>()) leaf.AplicarEstadoInicial();
            placed++;
        }
        StopStripsAtCasings();
        UnifyWallFinishes();
        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(morgue.scene);
        Debug.Log("Unclaimed: " + placed + " interior doors installed. Re-bake the lighting, then save.");
    }

    // Strips that stopped at the generic 7 cm casing run back to the opening edge (what StopStripsAtCasings expects).
    private static void RestoreStripsToOpenings()
    {
        var strips = new List<Transform>();
        foreach (Transform group in new[] { skirtingGroup, claddingGroup })
            if (group != null)
                foreach (MeshRenderer r in group.GetComponentsInChildren<MeshRenderer>(true))
                    foreach (string prefix in StripsStoppedByCasings)
                        if (r.name.StartsWith(prefix) && IsBox(r)) strips.Add(r.transform);

        const float half = WallThickness / 2, eps = 0.001f;
        foreach (Door d in Doors)
        {
            if (!IsKitDoor(d)) continue;
            float a = d.Center - d.Width / 2, b = d.Center + d.Width / 2;
            foreach (float sign in new[] { -1f, 1f })
                foreach (Transform s in strips)
                {
                    Vector3 p = s.position, size = s.localScale;
                    if (d.Vertical ? size.z < size.x : size.x < size.z) continue;
                    float across = (d.Vertical ? p.x : p.z) - d.Line, acrossSize = d.Vertical ? size.x : size.z;
                    if (across * sign < half - eps || Mathf.Abs(across) - acrossSize / 2 > half + 0.05f) continue;
                    if (p.y - size.y / 2 > DoorHeight) continue;
                    float along = d.Vertical ? p.z : p.x, length = d.Vertical ? size.z : size.x;
                    float s0 = along - length / 2, s1 = along + length / 2;
                    if (Mathf.Abs(s1 - (a - CasingWidth)) < eps) ResizeAlong(s, d.Vertical, s0, a);
                    else if (Mathf.Abs(s0 - (b + CasingWidth)) < eps) ResizeAlong(s, d.Vertical, b, s1);
                }
        }
    }
}
