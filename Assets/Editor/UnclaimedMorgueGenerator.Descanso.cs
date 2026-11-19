// Staff rest / changing room: finish, steel lockers, benches, coffee corner, notices and wear. Prefabs are modelled in
// Blender/Descanso (built by UnclaimedDescansoAssets); the room's approved envelope is unchanged. References and layout
// decisions: Blender/Descanso/referencias.md.
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static partial class UnclaimedMorgueGenerator
{
    private const string DescansoRoom = "Descanso e vestiario";
    private const string DescansoFolder = BaseFolder + "/Descanso";
    // Free interior on the finished surfaces (wall centrelines -13 / -5.25 and -3.875 / 0.875, less half a wall, finish and skirting).
    private const float DxMin = -12.875f, DxMax = -5.375f, DzMin = -3.75f, DzMax = 0.75f;
    private static readonly string[] DescansoPlaceholders = { "Armarios do vestiario", "Banco do vestiario", "Bancada de cafe" };

    // Porcelain floor shared with the WC (same staff block), washable sage paint up to 1.5 m, matt paint above, the
    // reception's mineral ceiling.
    private static ZoneFinish DescansoFinish()
    {
        string f = DescansoFolder, w = WCTextures, t = RecepcaoTextures, a = AutopsiaTextures;
        Color barrado = new Color(0.64f, 0.70f, 0.66f);
        return new ZoneFinish
        {
            Floor = Mat(f, "Piso porcelanato areia", new Color(0.95f, 0.93f, 0.88f), 1f, 0f, w + "WC_Piso_Base.png", w + "WC_Piso_Normal.png", 1.35f,
                glossMap: w + "WC_Piso_MS.png"),
            Skirting = Mat(f, "Rodape porcelanato areia", new Color(0.52f, 0.49f, 0.44f), 0.4f),
            Wainscot = Mat(f, "Barrado tinta lavavel verde", barrado, 0.42f, 0f, t + "Recepcao_Pintura_Base.png", t + "Recepcao_Pintura_Normal.png", 2f,
                glossMap: t + "Recepcao_Pintura_MS.png"),
            WainscotHeight = 1.5f,
            UpperWall = Mat(f, "Pintura descanso", new Color(0.96f, 0.97f, 0.95f), 0.14f, 0f, t + "Recepcao_Pintura_Base.png",
                t + "Recepcao_Pintura_Normal.png", 2f, glossMap: t + "Recepcao_Pintura_MS.png"),
            Ceiling = Mat(AutopsiaFolder, "Forro mineral", Color.white, 0.05f, 0f, a + "Autopsia_Forro_Base.png", a + "Autopsia_Forro_Normal.png", 1.25f),
            Friso = Mat(f, "Friso do barrado verde", barrado * 0.84f, 0.5f),
            CeilingTrim = Mat(RecepcaoFolder, "Cantoneira do forro", new Color(0.93f, 0.93f, 0.91f), 0.35f),
        };
    }

    private static Transform DescansoProp(Transform parent, string prefab, string name, Vector3 pos, float yaw, float scale = 1f)
    {
        if (parent == null) return null;
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(UnclaimedDescansoAssets.PrefabPath(prefab));
        if (asset == null) { Debug.LogWarning("Unclaimed: missing rest-room prefab " + prefab + "; run Tools > Unclaimed > Descanso > Build Prefabs."); return null; }
        return DescansoPlace(asset, parent, name, pos, yaw, scale);
    }

    private static Transform DescansoPlace(GameObject asset, Transform parent, string name, Vector3 pos, float yaw, float scale = 1f)
    {
        if (asset == null || parent == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.name = name; go.transform.localPosition = pos; go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        go.transform.localScale = Vector3.one * scale;
        return go.transform;
    }

    private static Transform DescansoReception(Transform parent, string prefab, string name, Vector3 pos, float yaw)
        => DescansoPlace(AssetDatabase.LoadAssetAtPath<GameObject>(UnclaimedRecepcaoAssets.PrefabPath(prefab)), parent, name, pos, yaw);

    // Mauro's locker (module "ilha 1", door 2 = M. NUNES, cell x 0..+.326, floor top .112): Irene's letter flat on the
    // floor, top towards the back; Samuel's work card on it with Mauro's clipped note half over the card. Idempotent.
    public static void PlaceMauroLockerDocuments(Transform furniture)
    {
        Transform mauro = furniture != null ? furniture.Find("Armarios - ilha 1") : null;
        if (mauro == null) { Debug.LogWarning("Unclaimed: Mauro's locker (Armarios - ilha 1) not found."); return; }
        foreach (string id in new[] { "h05_carta_irene", "h01_cartao_samuel", "h01_bilhete_mauro" })
        {
            Transform old = mauro.Find("Documento descanso - " + id);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        }
        DescansoPaper(mauro, "h05_carta_irene", new Vector3(.165f, .1125f, -.01f), 90, 2);
        DescansoPaper(mauro, "h01_cartao_samuel", new Vector3(.140f, .1135f, -.07f), 90, 9);
        DescansoPaper(mauro, "h01_bilhete_mauro", new Vector3(.195f, .1142f, -.045f), 90, -7);
    }

    private static void DescansoPaper(Transform parent, string id, Vector3 pos, float tilt, float yaw)
    {
        string path = UnclaimedDescansoAssets.Folder + "/Documento - " + id + ".prefab";
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null || parent == null) { Debug.LogWarning("Unclaimed: missing " + path); return; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        Undo.RegisterCreatedObjectUndo(go, "Place " + id);
        go.name = "Documento descanso - " + id;
        go.transform.localPosition = pos; go.transform.localRotation = Quaternion.Euler(tilt, yaw, 0);
    }

    // Each locker: (module name, centre x, centre z, yaw, name cards, padlocked flags). Prefabs face -Z, yaw 90 faces west.
    private static void BuildDescanso(Transform room)
    {
        foreach (string old in new[] { "Mobiliario", "Marcas de uso" })
        {
            var previous = room.Find(old);
            if (previous != null) Undo.DestroyObjectImmediate(previous.gameObject);
        }
        Transform g = Sub(room, "Mobiliario"), wear = Sub(room, "Marcas de uso");
        const float west = 90f, east = -90f, north = 180f, south = 0f;

        // ---- Changing area (west). The island of lockers stands from the north wall and hides the changing area from the door;
        // its plain steel back carries the notice board. Doors face west.
        var cards = new Dictionary<string, string[]>
        {
            ["Armarios - ilha 1"] = new[] { "H. DUARTE", "M. NUNES" }, ["Armarios - ilha 2"] = new[] { "NINA", "CELSO" },
            ["Armarios - parede 1"] = new[] { "R. MATOS", "J. PRADO" }, ["Armarios - parede 2"] = new[] { "LIVRE", "P. SOUZA" },
            ["Armarios - parede 3"] = new[] { "ESTAGIÁRIO", "LIVRE" },
        };
        var locked = new HashSet<string> { "Armarios - parede 1/2", "Armarios - parede 3/1" };
        var modules = new (string name, Vector3 pos, float yaw)[]
        {
            ("Armarios - ilha 1", new Vector3(-9.45f, 0, DzMax - .33f), west), ("Armarios - ilha 2", new Vector3(-9.45f, 0, DzMax - .99f), west),
            ("Armarios - parede 1", new Vector3(DxMin + .23f, 0, DzMin + .33f), east), ("Armarios - parede 2", new Vector3(DxMin + .23f, 0, DzMin + .99f), east),
            ("Armarios - parede 3", new Vector3(DxMin + .23f, 0, DzMin + 1.65f), east),
        };
        var lockerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UnclaimedDescansoAssets.PrefabPath("Roupeiro"));
        foreach (var (name, pos, yaw) in modules)
        {
            Transform m = DescansoPlace(lockerPrefab, g, name, pos, yaw);
            if (m == null) { Block(name, g, pos.x - .23f, pos.z - .33f, pos.x + .23f, pos.z + .33f, 0, 1.92f, furnitureMat); continue; }
            for (int k = 0; k < 2; k++)
            {
                Transform door = m.Find("Porta " + (k + 1));
                if (door == null) continue;
                UnclaimedDescansoAssets.ApplyNameCard(door, cards[name][k]);
                if (!locked.Contains(name + "/" + (k + 1))) continue;
                var peca = door.GetComponent<PecaDeMovel>();
                peca.trancada = true;
                var pad = AssetDatabase.LoadAssetAtPath<GameObject>(UnclaimedDescansoAssets.PrefabPath("Cadeado"));
                DescansoPlace(pad, door, "Cadeado", new Vector3(UnclaimedDescansoAssets.DoorWidth - .004f, .98f, -.0105f), 0);
            }
        }
        // Small differences between lockers: clogs on the floor of two of them, a daypack in one.
        DescansoProp(g.Find("Armarios - ilha 1"), "Tamancos", "Tamancos no armario", new Vector3(-.165f, .112f, -.02f), 0);
        DescansoProp(g.Find("Armarios - ilha 2"), "Mochila", "Mochila no armario", new Vector3(.165f, .112f, -.02f), 0, .85f);
        DescansoProp(g.Find("Armarios - parede 2"), "Tamancos", "Tamancos no armario", new Vector3(.165f, .112f, -.02f), 0);
        PlaceMauroLockerDocuments(g);

        // Benches: one in front of the island's doors (1 m from them), one facing the wall bank (0.7 m).
        DescansoProp(g, "Banco do vestiario", "Banco do vestiario 1", new Vector3(-10.85f, 0, DzMax - .80f), 90);
        DescansoProp(g, "Banco do vestiario", "Banco do vestiario 2", new Vector3(-11.55f, 0, DzMin + 1.00f), 90);
        DescansoProp(g, "Mochila", "Mochila no banco", new Vector3(-10.85f, .45f, DzMax - .35f), 168);
        DescansoProp(g, "Tamancos", "Tamancos sob o banco", new Vector3(-11.55f, 0, DzMin + 1.15f), 90);
        DescansoProp(g, "Botas", "Botas", new Vector3(-11.25f, 0, DzMax - .22f), 200);

        // Hook rail on the south wall (west part) with the lab coat and the raincoat; mirror-free on purpose.
        Transform rack = DescansoProp(g, "Cabideiro de parede", "Cabideiro", new Vector3(-11.70f, 1.65f, DzMin - .021f), north);
        DescansoProp(rack, "Jaleco", "Jaleco no cabideiro", new Vector3(-.50f, .045f, -.074f), 0);
        DescansoProp(rack, "Capa de chuva", "Capa de chuva no cabideiro", new Vector3(.30f, .045f, -.074f), 0);     // the draped lab coat is 0.76 m wide with its sleeves

        // ---- Rest area (east): table with three chairs (none on the south side, which keeps the way to the counter free),
        // coffee counter on the south wall, small fridge at its end, bin beside it.
        DescansoProp(g, "Mesa de descanso", "Mesa de descanso", new Vector3(-7.0f, 0, -1.90f), 0);
        DescansoReception(g, "Cadeira de espera", "Cadeira - oeste", new Vector3(-7.88f, 0, -1.90f), east);
        DescansoReception(g, "Cadeira de espera", "Cadeira - norte", new Vector3(-7.0f, 0, -1.02f), south);
        DescansoReception(g, "Cadeira de espera", "Cadeira - leste", new Vector3(-6.14f, 0, -1.96f), west + 6f);
        Transform counter = DescansoProp(g, "Balcao da copa", "Balcao da copa", new Vector3(-8.0f, 0, DzMin + .29f), north);
        DescansoProp(counter, "Itens da copa", "Itens da copa", new Vector3(.0f, .90f, 0), 0);
        DescansoProp(g, "Frigobar", "Frigobar", new Vector3(-6.80f, 0, DzMin + .26f), north);
        DescansoProp(g, "Marmita", "Marmita vazia", new Vector3(-6.82f, .86f, DzMin + .27f), 8);
        DescansoReception(g, "Lixeira", "Lixeira", new Vector3(-9.15f, 0, DzMin + .22f), 0);

        // ---- Wall pieces. The clock sits on the east wall south of the door; the notice board on the island's back.
        DescansoProp(g, "Relogio de parede", "Relogio", new Vector3(-5.35f + FinishOffset + .001f, 2.10f, -2.50f), west);
        Transform board = DescansoProp(g, "Quadro de avisos do descanso", "Quadro de avisos", new Vector3(-9.22f + .003f, 1.45f, DzMax - .66f), east);
        DescansoPaper(board, "aviso_encerramento", new Vector3(-.255f, .196f - .0f, -.0145f), 0, 0);
        DescansoPaper(board, "escala_limpeza", new Vector3(.17f, .18f, -.0145f), 0, 0);
        DescansoPaper(g.Find("Marmita vazia"), "bilhete_marmita", new Vector3(0, .074f, 0), 90, 0);
        DescansoPaper(counter, "bilhete_cafe", new Vector3(.40f, 1.25f, .29f - .004f), 0, 0);

        // ---- Wear: scuffs where feet land (bench feet, in front of the lockers' bases, the door's threshold, the counter).
        Material scuff = decalFloorWear;
        void floor(string n, float x, float z, float sx, float sz) =>
            Decal(n, wear, new Vector3(x, .0045f, z), Vector3.up, Vector3.right, Vector3.forward, sx, sz, scuff);
        floor("Desgaste - porta", -5.85f, -.25f, .9f, 1.0f);
        floor("Desgaste - banco 1", -10.85f, DzMax - .80f, .7f, 1.5f);
        floor("Desgaste - banco 2", -11.55f, DzMin + 1.0f, .7f, 1.5f);
        floor("Desgaste - armarios", -12.25f, DzMin + 1.0f, .5f, 1.9f);
        floor("Desgaste - balcao", -8.0f, DzMin + .85f, 1.3f, .5f);
        floor("Desgaste - mesa", -7.0f, -1.9f, 1.2f, 1.2f);
    }

    // The pieces are not in the generator's furniture list any more; the saved map still carries the old blocks.
    private static void RemoveDescansoPlaceholders(Transform morgue)
    {
        foreach (var t in morgue.GetComponentsInChildren<Transform>(true))
            if (t != null && System.Array.IndexOf(DescansoPlaceholders, t.name) >= 0) Undo.DestroyObjectImmediate(t.gameObject);
    }

    /// <summary>Rebuilds the rest room's finishes and dressing on the open scene's map without regenerating it.</summary>
    [MenuItem("Tools/Unclaimed/Descanso/Update Rest Room")]
    public static void UpdateDescanso()
    {
        GameObject morgue = FindExistingMorgue(SceneManager.GetActiveScene());
        if (morgue == null || morgue.name != GroupName || Application.isPlaying)
        {
            Debug.LogWarning("Unclaimed: open PrototipoNecroterio in Edit Mode first.");
            return;
        }
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Update rest room");

        Transform real = morgue.transform;
        var scratch = new GameObject("_descanso_temp");
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

        string room = DescansoRoom;
        Transform oldClad = real.Find("Revestimentos/" + room);
        if (oldClad != null) Undo.DestroyObjectImmediate(oldClad.gameObject);
        Transform newClad = claddingGroup.Find(room);
        if (newClad != null) Undo.SetTransformParent(newClad, real.Find("Revestimentos"), "Rest room cladding");
        foreach (Transform strip in Children(real.Find("Rodapes")))
            if (strip.name.StartsWith("Rodape - " + room)) Undo.DestroyObjectImmediate(strip.gameObject);
        foreach (Transform strip in Children(skirtingGroup))
            if (strip.name.StartsWith("Rodape - " + room)) Undo.SetTransformParent(strip, real.Find("Rodapes"), "Rest room skirting");
        foreach (Transform old in Children(real.Find("Tetos")))
            if (old.name.StartsWith("Forro - " + room)) Undo.DestroyObjectImmediate(old.gameObject);
        foreach (Transform panel in Children(ceilingsGroup))
            if (panel.name.StartsWith("Forro - " + room)) Undo.SetTransformParent(panel, real.Find("Tetos"), "Rest room ceiling");
        Undo.DestroyObjectImmediate(scratch);

        root = real;
        ceilingsGroup = real.Find("Tetos");
        claddingGroup = real.Find("Revestimentos");
        skirtingGroup = real.Find("Rodapes");
        doorsGroup = real.Find("Portas");
        RemoveDescansoPlaceholders(real);
        Transform oldRoom = roomsGroup.Find(room);
        if (oldRoom != null) Undo.DestroyObjectImmediate(oldRoom.gameObject);
        BuildDescanso(Sub(roomsGroup, room));
        PrepareStaticLighting(zones);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(real.gameObject.scene);
        Debug.Log("Unclaimed: rest room updated. Re-bake the lighting (Tools > Unclaimed > Bake Prototype Lighting), then save.");
    }
}
