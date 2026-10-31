// Storage room: washable finishes, baked supplies and labels placed from the Blender layout JSON.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static partial class UnclaimedMorgueGenerator
{
    private const string DepositoFolder = BaseFolder + "/Deposito";
    private static readonly string[] DepositoPlaceholders = { "Estante do deposito", "Balde e esfregao" };

    private static ZoneFinish DepositoFinish()
    {
        string f = DepositoFolder, w = WCTextures, t = RecepcaoTextures;
        Color barrado = new Color(.66f, .67f, .66f);
        return new ZoneFinish
        {
            Floor = Mat(DescansoFolder, "Piso porcelanato areia", new Color(.95f, .93f, .88f), 1f, 0f, w + "WC_Piso_Base.png", w + "WC_Piso_Normal.png", 1.35f,
                glossMap: w + "WC_Piso_MS.png"),
            Skirting = Mat(DescansoFolder, "Rodape porcelanato areia", new Color(.52f, .49f, .44f), .4f),
            Wainscot = Mat(f, "Barrado tinta lavavel cinza", barrado, .42f, 0f, t + "Recepcao_Pintura_Base.png", t + "Recepcao_Pintura_Normal.png", 2f,
                glossMap: t + "Recepcao_Pintura_MS.png"),
            WainscotHeight = 1.10f,
            UpperWall = Mat(f, "Pintura deposito", new Color(.96f, .97f, .95f), .14f, 0f, t + "Recepcao_Pintura_Base.png",
                t + "Recepcao_Pintura_Normal.png", 2f, glossMap: t + "Recepcao_Pintura_MS.png"),
            Ceiling = Mat(f, "Laje pintada branca", new Color(.93f, .93f, .92f), .14f, 0f, t + "Recepcao_Pintura_Base.png",
                t + "Recepcao_Pintura_Normal.png", 2f, glossMap: t + "Recepcao_Pintura_MS.png"),
            Friso = Mat(f, "Friso do barrado cinza", barrado * .84f, .5f),
            CeilingTrim = Mat(RecepcaoFolder, "Cantoneira do forro", new Color(.93f, .93f, .91f), .35f),
        };
    }

    [Serializable] private class DepositoLayout { public DepositoPiece[] pecas; public DepositoLabelPlacement[] etiquetas; }
    [Serializable] private class DepositoPiece { public string nome, prefab, pai; public float[] pos; public float yaw; }
    [Serializable] private class DepositoLabelPlacement { public string id, pai; public float[] pos; public float yaw, pitch; }
    [Serializable] private class DepositoAtlas { public int[] atlas; public DepositoLabel[] etiquetas; }
    [Serializable] private class DepositoLabel { public string id, tipo; public int[] rect; public float[] tamanho; }

    private static Vector3 DepositoPosition(float[] pos) => new Vector3(pos[0], pos[1], pos[2]);

    private static void BuildDeposito(Transform room)
    {
        string path = DepositoFolder + "/Layout.json";
        if (!File.Exists(path)) { Debug.LogWarning("Unclaimed: missing " + path); return; }
        var layout = JsonUtility.FromJson<DepositoLayout>(File.ReadAllText(path));
        if (layout == null || layout.pecas == null) { Debug.LogWarning("Unclaimed: invalid " + path); return; }
        var objects = new Dictionary<string, Transform>();
        foreach (var piece in layout.pecas)
        {
            bool world = string.IsNullOrEmpty(piece.pai);
            Transform parent = room;
            if (!world && !objects.TryGetValue(piece.pai, out parent))
            { Debug.LogWarning("Unclaimed: missing storage-room parent " + piece.pai); continue; }
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(UnclaimedDepositoAssets.PrefabPath(piece.prefab));
            if (asset == null)
            { Debug.LogWarning("Unclaimed: missing storage-room prefab " + piece.prefab + "; run Tools > Unclaimed > Deposito > Build Prefabs."); continue; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            Undo.RegisterCreatedObjectUndo(go, "Create " + piece.nome);
            go.name = piece.nome;
            Vector3 pos = DepositoPosition(piece.pos);
            go.transform.localPosition = world ? room.InverseTransformPoint(pos) : pos;
            go.transform.localRotation = Quaternion.Euler(0, piece.yaw, 0);
            objects[piece.nome] = go.transform;
        }
        DepositoLabels(room, objects, layout.etiquetas);
        Transform wear = Sub(room, "Marcas de uso");
        void floor(string name, float x, float z, float sx, float sz) =>
            Decal(name, wear, new Vector3(x, .0045f, z), Vector3.up, Vector3.right, Vector3.forward, sx, sz, decalFloorWear);
        floor("Desgaste - entrada", -5.0f, 3.5f, .8f, 1f);
        floor("Desgaste - estantes oeste", -7.1f, 2.9f, .6f, 1.8f);
        floor("Desgaste - estrado", -5.3f, 1.7f, .9f, .6f);
    }

    private static void DepositoLabels(Transform room, Dictionary<string, Transform> objects, DepositoLabelPlacement[] placements)
    {
        if (placements == null || placements.Length == 0) return;
        string path = DepositoFolder + "/Etiquetas.json";
        if (!File.Exists(path)) { Debug.LogWarning("Unclaimed: missing " + path); return; }
        var data = JsonUtility.FromJson<DepositoAtlas>(File.ReadAllText(path));
        if (data == null || data.etiquetas == null || data.atlas == null || data.atlas.Length != 2 || data.atlas[0] <= 0 || data.atlas[1] <= 0)
        { Debug.LogWarning("Unclaimed: invalid " + path); return; }
        var texture = Tex(UnclaimedDepositoAssets.Folder + "/Texturas/Etiquetas_Deposito.png", false);
        if (texture == null) { Debug.LogWarning("Unclaimed: missing storage-room label atlas"); return; }
        var labels = new Dictionary<string, DepositoLabel>();
        foreach (var label in data.etiquetas) labels[label.id] = label;
        var materials = new Dictionary<string, Material>();
        Mesh quad = DepositoLabelMesh();
        foreach (var placement in placements)
        {
            Transform parent = room;
            if (!string.IsNullOrEmpty(placement.pai) && !objects.TryGetValue(placement.pai, out parent))
            { Debug.LogWarning("Unclaimed: missing storage-room label parent " + placement.pai); continue; }
            if (!labels.TryGetValue(placement.id, out var label))
            { Debug.LogWarning("Unclaimed: missing storage-room label " + placement.id); continue; }
            if (!materials.TryGetValue(label.id, out var material))
            {
                material = Mat(DepositoFolder, "Etiqueta " + label.id, Color.white, .35f);
                material.SetTexture("_BaseMap", texture);
                material.SetTextureScale("_BaseMap", new Vector2((label.rect[2] - 8f) / data.atlas[0], (label.rect[3] - 8f) / data.atlas[1]));
                material.SetTextureOffset("_BaseMap", new Vector2((label.rect[0] + 4f) / data.atlas[0], 1f - (label.rect[1] + label.rect[3] - 4f) / data.atlas[1]));
                material.SetFloat("_Smoothness", .35f); material.SetFloat("_Metallic", 0); material.SetFloat("_Cull", 2f);
                EditorUtility.SetDirty(material);
                materials[label.id] = material;
            }
            var go = new GameObject("Etiqueta - " + placement.id);
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            go.transform.SetParent(parent, false);
            // JSON includes the 1.2 mm surface clearance. Positive X pitch tilts the top towards +Z.
            go.transform.localPosition = DepositoPosition(placement.pos);
            go.transform.localRotation = Quaternion.Euler(placement.pitch, placement.yaw, 0);
            go.transform.localScale = new Vector3(label.tamanho[0], label.tamanho[1], 1f);
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            go.isStatic = parent.gameObject.isStatic;
        }
    }

    private static Mesh DepositoLabelMesh()
    {
        const string path = DepositoFolder + "/Etiqueta impressa.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null) return mesh;
        EnsureFolder(DepositoFolder);
        // Clockwise from -Z: the printed face points towards the front of the shelves.
        mesh = new Mesh { name = "Etiqueta impressa deposito",
            vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(.5f, .5f, 0), new Vector3(-.5f, .5f, 0) },
            uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up }, triangles = new[] { 0, 2, 1, 0, 3, 2 } };
        mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    /// <summary>Rebuilds the storage room's finishes and dressing on the open scene's map without regenerating it.</summary>
    [MenuItem("Tools/Unclaimed/Deposito/Update Storage Room")]
    public static void UpdateDeposito()
    {
        GameObject morgue = FindExistingMorgue(SceneManager.GetActiveScene());
        if (morgue == null || morgue.name != GroupName || Application.isPlaying)
        {
            Debug.LogWarning("Unclaimed: open PrototipoNecroterio in Edit Mode first.");
            return;
        }
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Update storage room");

        Transform real = morgue.transform;
        var scratch = new GameObject("_deposito_temp");
        Undo.RegisterCreatedObjectUndo(scratch, "Scratch");
        scratch.transform.SetParent(morgue.transform.parent, false);
        root = scratch.transform;
        floorsGroup = Group("Pisos"); wallsGroup = Group("Paredes"); claddingGroup = Group("Revestimentos"); skirtingGroup = Group("Rodapes");
        doorsGroup = Group("Portas"); ceilingsGroup = Group("Tetos"); envelopeGroup = Group("Fechamento externo");
        roomsGroup = real.Find("Ambientes");
        lightsGroup = real.Find("Luzes");
        PrepareMaterials();
        corridorLegs = null;
        finishes = new Dictionary<string, ZoneFinish> { ["Autopsia"] = AutopsiaFinish() };
        finishes["Recepcao"] = RecepcaoFinish();
        finishes["WC"] = WCFinish();
        finishes["Arquivo e escritorio"] = ArquivoFinish();
        finishes[CirculacaoRoom] = CorridorFinish(0);
        finishes[VestibuleRoom] = CorridorFinish(2);
        finishes[DescansoRoom] = DescansoFinish();
        finishes["Deposito"] = DepositoFinish();
        finishes["Sala tecnica"] = TecnicaFinish();

        List<Zone> zones = Zones();
        grid = Rasterize(zones);
        doorUsed = new bool[Doors.Length];
        Floors(zones);
        Walls(zones);
        Doorways(zones);
        StopStripsAtCasings();
        UnifyWallFinishes();
        Ceilings(zones);

        string room = "Deposito";
        Transform oldClad = real.Find("Revestimentos/" + room);
        if (oldClad != null) Undo.DestroyObjectImmediate(oldClad.gameObject);
        Transform newClad = claddingGroup.Find(room);
        if (newClad != null) Undo.SetTransformParent(newClad, real.Find("Revestimentos"), "Storage room cladding");
        foreach (Transform strip in Children(real.Find("Rodapes")))
            if (strip.name.StartsWith("Rodape - " + room)) Undo.DestroyObjectImmediate(strip.gameObject);
        foreach (Transform strip in Children(skirtingGroup))
            if (strip.name.StartsWith("Rodape - " + room)) Undo.SetTransformParent(strip, real.Find("Rodapes"), "Storage room skirting");
        foreach (Transform old in Children(real.Find("Tetos")))
            if (old.name.StartsWith("Forro - " + room)) Undo.DestroyObjectImmediate(old.gameObject);
        foreach (Transform panel in Children(ceilingsGroup))
            if (panel.name.StartsWith("Forro - " + room)) Undo.SetTransformParent(panel, real.Find("Tetos"), "Storage room ceiling");
        Undo.DestroyObjectImmediate(scratch);

        root = real;
        ceilingsGroup = real.Find("Tetos");
        claddingGroup = real.Find("Revestimentos");
        skirtingGroup = real.Find("Rodapes");
        doorsGroup = real.Find("Portas");
        foreach (var t in real.GetComponentsInChildren<Transform>(true))
            if (t != null && Array.IndexOf(DepositoPlaceholders, t.name) >= 0) Undo.DestroyObjectImmediate(t.gameObject);
        Transform oldRoom = roomsGroup.Find(room);
        if (oldRoom != null) Undo.DestroyObjectImmediate(oldRoom.gameObject);
        BuildDeposito(Sub(roomsGroup, room));
        Transform oldLights = ceilingsGroup.Find("Luminarias - " + room);
        if (oldLights != null) Undo.DestroyObjectImmediate(oldLights.gameObject);
        foreach (Transform light in Children(lightsGroup))
            if (light.name.StartsWith("Luz - " + room + " - luminaria ")) Undo.DestroyObjectImmediate(light.gameObject);
        CeilingFixture(Sub(ceilingsGroup, "Luminarias - " + room), "Luz - " + room + " - luminaria 1",
            "Luminaria 1", -6.25f, 3.45f, 0, 3.4f, 4300f, false, alongZ: true, model: true);
        PrepareStaticLighting(zones, room);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(real.gameObject.scene);
        Debug.Log("Unclaimed: storage room updated. Re-bake the lighting (Tools > Unclaimed > Bake Prototype Lighting), then save.");
    }
}
