// Technical room: shared washable finishes and the Blender kit's initial visual state.
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
    private const string TecnicaFolder = BaseFolder + "/Tecnica";
    private static readonly string[] TecnicaPlaceholders = { "Quadro eletrico", "Condensadora", "Tubulacao" };

    private static ZoneFinish TecnicaFinish()
    {
        Material Load(string folder, string name) => AssetDatabase.LoadAssetAtPath<Material>(folder + "/" + name + ".mat");
        return new ZoneFinish
        {
            Floor = Load(DescansoFolder, "Piso porcelanato areia"),
            Skirting = Load(DescansoFolder, "Rodape porcelanato areia"),
            Wainscot = Load(DepositoFolder, "Barrado tinta lavavel cinza"),
            WainscotHeight = 1.10f,
            UpperWall = Load(DepositoFolder, "Pintura deposito"),
            Ceiling = Load(DepositoFolder, "Laje pintada branca"),
            Friso = Load(DepositoFolder, "Friso do barrado cinza"),
            CeilingTrim = Load(RecepcaoFolder, "Cantoneira do forro"),
        };
    }

    [Serializable] private class TecnicaLayout
    {
        public TecnicaPiece[] pecas;
        public TecnicaDevice[] dispositivos;
        public TecnicaLabelPlacement[] etiquetas;
        public TecnicaDisplay[] visores;
        public TecnicaDocument[] documentos;
        public TecnicaReused[] reaproveitados;
    }
    [Serializable] private class TecnicaPiece { public string nome, prefab, pai; public float[] pos; public float yaw; }
    [Serializable] private class TecnicaDevice { public string nome, tipo, cor; public float giro; public bool aceso; }
    [Serializable] private class TecnicaLabelPlacement { public string id, pai; public float[] pos; public float yaw, pitch; }
    [Serializable] private class TecnicaAtlas { public int[] atlas; public TecnicaLabel[] etiquetas; }
    [Serializable] private class TecnicaLabel { public string id, tipo; public int[] rect; public float[] tamanho; }
    [Serializable] private class TecnicaDisplay { public string nome, pai, textura; public float[] pos, tamanho; }
    [Serializable] private class TecnicaDocument { public string id, pai; public float[] pos; public float tilt, yaw; }
    [Serializable] private class TecnicaReused { public string nome, prefab; public float[] pos; public float yaw; }

    private static Vector3 TecnicaPosition(float[] pos) => new Vector3(pos[0], pos[1], pos[2]);

    private static void BuildTecnica(Transform room)
    {
        string path = TecnicaFolder + "/Layout.json";
        if (!File.Exists(path)) { Debug.LogWarning("Unclaimed: missing " + path); return; }
        var layout = JsonUtility.FromJson<TecnicaLayout>(File.ReadAllText(path));
        if (layout == null || layout.pecas == null) { Debug.LogWarning("Unclaimed: invalid " + path); return; }
        foreach (string name in new[] { "Equipamentos", "Etiquetas", "Documentos", "Acessorios", "Marcas de uso" })
        {
            Transform old = room.Find(name);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        }
        Transform equipment = Sub(room, "Equipamentos"), documents = Sub(room, "Documentos"), accessories = Sub(room, "Acessorios");
        var objects = new Dictionary<string, Transform>();
        foreach (var piece in layout.pecas)
        {
            bool world = string.IsNullOrEmpty(piece.pai);
            Transform parent = equipment;
            if (!world && !objects.TryGetValue(piece.pai, out parent))
            { Debug.LogWarning("Unclaimed: missing technical-room parent " + piece.pai); continue; }
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(UnclaimedTecnicaAssets.PrefabPath(piece.prefab));
            if (asset == null)
            { Debug.LogWarning("Unclaimed: missing technical-room prefab " + piece.prefab + "; run Tools > Unclaimed > Tecnica > Build Prefabs."); continue; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            Undo.RegisterCreatedObjectUndo(go, "Create " + piece.nome);
            go.name = piece.nome;
            Vector3 pos = TecnicaPosition(piece.pos);
            go.transform.localPosition = world ? room.InverseTransformPoint(pos) : pos;
            go.transform.localRotation = Quaternion.Euler(0, piece.yaw, 0);
            objects[piece.nome] = go.transform;
        }
        if (layout.dispositivos != null)
            foreach (var device in layout.dispositivos)
            {
                if (!objects.TryGetValue(device.nome, out var target))
                { Debug.LogWarning("Unclaimed: missing technical-room device " + device.nome); continue; }
                if (device.tipo == "seletor" || device.tipo == "seletor_chave")
                    target.localRotation = Quaternion.Euler(0, 0, -device.giro);
                else if (device.tipo == "sinaleiro")
                {
                    Transform lens = target.Find(device.nome + " - lente");
                    var renderer = lens != null ? lens.GetComponent<MeshRenderer>() : null;
                    if (renderer == null) { Debug.LogWarning("Unclaimed: missing technical-room lens " + device.nome); continue; }
                    renderer.sharedMaterial = UnclaimedTecnicaAssets.LensMaterial(device.cor, device.aceso);
                }
            }
        TecnicaLabels(room, objects, layout.etiquetas);
        if (layout.visores != null)
            foreach (var display in layout.visores)
            {
                if (!objects.TryGetValue(display.pai, out var parent))
                { Debug.LogWarning("Unclaimed: missing technical-room display parent " + display.pai); continue; }
                var go = new GameObject(display.nome);
                Undo.RegisterCreatedObjectUndo(go, "Create " + display.nome);
                go.transform.SetParent(parent, false);
                go.transform.localPosition = TecnicaPosition(display.pos);
                go.transform.localScale = new Vector3(display.tamanho[0], display.tamanho[1], 1);
                go.AddComponent<MeshFilter>().sharedMesh = TecnicaLabelMesh();
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = UnclaimedTecnicaAssets.DisplayMaterial(display.textura);
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            }
        if (layout.documentos != null)
            foreach (var document in layout.documentos)
            {
                bool world = string.IsNullOrEmpty(document.pai);
                Transform parent = documents;
                if (!world && !objects.TryGetValue(document.pai, out parent))
                { Debug.LogWarning("Unclaimed: missing technical-room document parent " + document.pai); continue; }
                string prefabPath = UnclaimedDocumentosTecnica.Dir + "/Documento - " + document.id + ".prefab";
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (asset == null) { Debug.LogWarning("Unclaimed: missing " + prefabPath); continue; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
                Undo.RegisterCreatedObjectUndo(go, "Place " + document.id);
                go.name = "Documento tecnica - " + document.id;
                Vector3 pos = TecnicaPosition(document.pos);
                go.transform.localPosition = world ? room.InverseTransformPoint(pos) : pos;
                // The reception paper mesh's printed face is -Z, matching the north wall at yaw zero.
                go.transform.localRotation = Quaternion.Euler(document.tilt, document.yaw, 0);
            }
        if (layout.reaproveitados != null)
            foreach (var fitting in layout.reaproveitados)
            {
                Vector3 point = TecnicaPosition(fitting.pos);
                Vector3 normal = Quaternion.Euler(0, fitting.yaw, 0) * Vector3.back;
                if (fitting.prefab == "Placa SEG_Extintor")
                {
                    // JSON gives the finished surface; Sign adds the finish offset itself.
                    var sign = Sign(fitting.nome, accessories, "SEG_Extintor", point - normal * FinishOffset, normal);
                    if (sign != null) sign.name = fitting.nome;
                }
                else PlaceFitting(UnclaimedCirculacaoAssets.PrefabPath(fitting.prefab), accessories, fitting.nome, point, normal);
            }
        Transform wear = Sub(room, "Marcas de uso");
        void floor(string name, float x, float z, float sx, float sz) =>
            Decal(name, wear, new Vector3(x, .0045f, z), Vector3.up, Vector3.right, Vector3.forward, sx, sz, decalFloorWear);
        floor("Desgaste - quadros", -2f, 8.35f, 2.6f, .65f);
        floor("Desgaste - entrada", -3.5f, 6.3f, .8f, 1f);
        floor("Desgaste - bancada", 2.35f, 8f, 1.1f, .65f);
    }

    private static void TecnicaLabels(Transform room, Dictionary<string, Transform> objects, TecnicaLabelPlacement[] placements)
    {
        if (placements == null || placements.Length == 0) return;
        string path = TecnicaFolder + "/Etiquetas.json";
        if (!File.Exists(path)) { Debug.LogWarning("Unclaimed: missing " + path); return; }
        var data = JsonUtility.FromJson<TecnicaAtlas>(File.ReadAllText(path));
        if (data == null || data.etiquetas == null || data.atlas == null || data.atlas.Length != 2 || data.atlas[0] <= 0 || data.atlas[1] <= 0)
        { Debug.LogWarning("Unclaimed: invalid " + path); return; }
        var texture = Tex(UnclaimedTecnicaAssets.Folder + "/Texturas/Etiquetas_Tecnica.png", false);
        if (texture == null) { Debug.LogWarning("Unclaimed: missing technical-room label atlas"); return; }
        var labels = new Dictionary<string, TecnicaLabel>();
        foreach (var label in data.etiquetas) labels[label.id] = label;
        var materials = new Dictionary<string, Material>();
        Mesh quad = TecnicaLabelMesh();
        foreach (var placement in placements)
        {
            Transform parent = room;
            if (!string.IsNullOrEmpty(placement.pai) && !objects.TryGetValue(placement.pai, out parent))
            { Debug.LogWarning("Unclaimed: missing technical-room label parent " + placement.pai); continue; }
            if (!labels.TryGetValue(placement.id, out var label))
            { Debug.LogWarning("Unclaimed: missing technical-room label " + placement.id); continue; }
            if (!materials.TryGetValue(label.id, out var material))
            {
                material = Mat(UnclaimedTecnicaAssets.Folder + "/Materiais", "Etiqueta " + label.id, Color.white, .35f);
                material.SetTexture("_BaseMap", texture);
                material.SetTextureScale("_BaseMap", new Vector2((label.rect[2] - 8f) / data.atlas[0], (label.rect[3] - 8f) / data.atlas[1]));
                material.SetTextureOffset("_BaseMap", new Vector2((label.rect[0] + 4f) / data.atlas[0], 1f - (label.rect[1] + label.rect[3] - 4f) / data.atlas[1]));
                material.SetFloat("_Smoothness", .35f); material.SetFloat("_Metallic", 0); material.SetFloat("_Cull", 2f);
                EditorUtility.SetDirty(material);
                materials[label.id] = material;
            }
            var go = new GameObject("Etiqueta - " + placement.id);
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            go.transform.SetParent(Sub(parent, "Etiquetas"), false);
            // JSON includes the 1.2 mm surface clearance. Positive X pitch tilts the top towards +Z.
            go.transform.localPosition = TecnicaPosition(placement.pos);
            go.transform.localRotation = Quaternion.Euler(placement.pitch, placement.yaw, 0);
            go.transform.localScale = new Vector3(label.tamanho[0], label.tamanho[1], 1f);
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            go.isStatic = parent.gameObject.isStatic;
        }
    }

    private static Mesh TecnicaLabelMesh()
    {
        const string path = UnclaimedTecnicaAssets.Folder + "/Etiqueta impressa.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null) return mesh;
        EnsureFolder(UnclaimedTecnicaAssets.Folder);
        // Clockwise from -Z: the printed face points towards the front of the panels.
        mesh = new Mesh { name = "Etiqueta impressa tecnica",
            vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(.5f, .5f, 0), new Vector3(-.5f, .5f, 0) },
            uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up }, triangles = new[] { 0, 2, 1, 0, 3, 2 } };
        mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    /// <summary>Rebuilds the technical room's finishes and dressing on the open scene's map without regenerating it.</summary>
    [MenuItem("Tools/Unclaimed/Tecnica/Update Technical Room")]
    public static void UpdateTecnica()
    {
        GameObject morgue = FindExistingMorgue(SceneManager.GetActiveScene());
        if (morgue == null || morgue.name != GroupName || Application.isPlaying)
        {
            Debug.LogWarning("Unclaimed: open PrototipoNecroterio in Edit Mode first.");
            return;
        }
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Update technical room");

        Transform real = morgue.transform;
        var scratch = new GameObject("_tecnica_temp");
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

        string room = "Sala tecnica";
        Transform oldClad = real.Find("Revestimentos/" + room);
        if (oldClad != null) Undo.DestroyObjectImmediate(oldClad.gameObject);
        Transform newClad = claddingGroup.Find(room);
        if (newClad != null) Undo.SetTransformParent(newClad, real.Find("Revestimentos"), "Technical room cladding");
        foreach (Transform strip in Children(real.Find("Rodapes")))
            if (strip.name.StartsWith("Rodape - " + room)) Undo.DestroyObjectImmediate(strip.gameObject);
        foreach (Transform strip in Children(skirtingGroup))
            if (strip.name.StartsWith("Rodape - " + room)) Undo.SetTransformParent(strip, real.Find("Rodapes"), "Technical room skirting");
        foreach (Transform old in Children(real.Find("Tetos")))
            if (old.name.StartsWith("Forro - " + room)) Undo.DestroyObjectImmediate(old.gameObject);
        foreach (Transform panel in Children(ceilingsGroup))
            if (panel.name.StartsWith("Forro - " + room)) Undo.SetTransformParent(panel, real.Find("Tetos"), "Technical room ceiling");
        Undo.DestroyObjectImmediate(scratch);

        root = real;
        ceilingsGroup = real.Find("Tetos");
        claddingGroup = real.Find("Revestimentos");
        skirtingGroup = real.Find("Rodapes");
        doorsGroup = real.Find("Portas");
        Transform furniture = real.Find("Moveis");
        foreach (var t in Children(furniture))
            if (Array.IndexOf(TecnicaPlaceholders, t.name) >= 0) Undo.DestroyObjectImmediate(t.gameObject);
        Transform oldRoom = roomsGroup.Find(room);
        if (oldRoom != null) Undo.DestroyObjectImmediate(oldRoom.gameObject);
        BuildTecnica(Sub(roomsGroup, room));
        Transform oldLights = ceilingsGroup.Find("Luminarias - " + room);
        if (oldLights != null) Undo.DestroyObjectImmediate(oldLights.gameObject);
        foreach (Transform light in Children(lightsGroup))
            if (light.name.StartsWith("Luz - " + room + " - luminaria ")) Undo.DestroyObjectImmediate(light.gameObject);
        CeilingFixture(Sub(ceilingsGroup, "Luminarias - " + room), "Luz - " + room + " - luminaria 1",
            "Luminaria 1", -2.4f, 7.4f, 0, 3.6f, 4300f, false, alongZ: false, model: true);
        CeilingFixture(Sub(ceilingsGroup, "Luminarias - " + room), "Luz - " + room + " - luminaria 2",
            "Luminaria 2", 1.4f, 7.4f, 0, 3.6f, 4300f, false, alongZ: false, model: true);
        PrepareStaticLighting(zones, room);

        AssetDatabase.SaveAssets();
        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(real.gameObject.scene);
        Debug.Log("Unclaimed: technical room updated. Re-bake the lighting (Tools > Unclaimed > Bake Prototype Lighting), then save.");
    }
}
