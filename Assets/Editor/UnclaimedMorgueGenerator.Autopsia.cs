// Finish and dressing of the autopsy room, the reference room for the morgue's look:
// an everyday workplace with uneven upkeep and signs of use, not a ruin.
// Furniture is placed from the Blender layout; the table and fixed details retain their own builders.
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static partial class UnclaimedMorgueGenerator
{
    private const string AutopsiaFolder = BaseFolder + "/Autopsia";
    private const string AutopsiaTextures = AutopsiaFolder + "/Texturas/";
    private const string AutopsiaReflection = AutopsiaFolder + "/Reflexo Autopsia.exr";

    // Free interior of the room (wall faces).
    private const float AxMin = -1.9f, AxMax = 7.4f, AzMin = -3.4f, AzMax = 3.525f;

    private static Material steel, steelWorn, whiteMetal, glass, sheet, whitePlastic, yellowPlastic, rubber, wallGuard,
        tube, tubeAged, tubeDead, surgicalGlass, drain, cardboard, paper,
        decalLimescale, decalFloorWear, decalCeilingStain, decalScuffs;

    private static ZoneFinish AutopsiaFinish()
    {
        string f = AutopsiaFolder, t = AutopsiaTextures;
        steel = Mat(f, "Aco inox", new Color(0.72f, 0.73f, 0.74f), 0.62f, 1f);
        steelWorn = Mat(f, "Aco inox fosco", new Color(0.62f, 0.63f, 0.64f), 0.38f, 1f);
        whiteMetal = Mat(f, "Metal pintado", new Color(0.82f, 0.83f, 0.80f), 0.5f);
        glass = Mat(f, "Vidro", new Color(0.70f, 0.80f, 0.82f, 0.22f), 0.95f, 0f, transparent: true);
        sheet = Mat(f, "Lencol", new Color(0.60f, 0.66f, 0.70f), 0.12f);
        whitePlastic = Mat(f, "Plastico branco", new Color(0.86f, 0.86f, 0.83f), 0.4f);
        yellowPlastic = Mat(f, "Plastico amarelo", new Color(0.86f, 0.68f, 0.10f), 0.4f);
        rubber = Mat(f, "Borracha", new Color(0.08f, 0.08f, 0.08f), 0.25f);
        wallGuard = Mat(f, "Protetor de parede", new Color(0.52f, 0.58f, 0.55f), 0.35f);
        // Just under clipping and slightly warm: the old 1.6 burnt to flat blue-white with no tonemapping.
        tube = Mat(f, "Difusor aceso", new Color(0.95f, 0.96f, 0.97f), 0.3f, 0f, emission: new Color(1.0f, 0.97f, 0.9f));
        tubeAged = Mat(f, "Difusor gasto", new Color(0.92f, 0.88f, 0.80f), 0.3f, 0f, emission: new Color(0.85f, 0.70f, 0.45f));
        tubeDead = Mat(f, "Difusor apagado", new Color(0.70f, 0.70f, 0.68f), 0.3f);
        surgicalGlass = Mat(f, "Lente do foco", new Color(0.95f, 0.95f, 0.92f), 0.8f, 0f, emission: new Color(2.2f, 2.15f, 2.0f));
        drain = Mat(f, "Ralo", new Color(0.16f, 0.16f, 0.16f), 0.4f, 0.6f);
        cardboard = Mat(f, "Papelao", new Color(0.55f, 0.45f, 0.32f), 0.15f);
        paper = Mat(f, "Papel", new Color(0.90f, 0.90f, 0.86f), 0.1f);
        decalLimescale = Mat(f, "Decal - calcario", new Color(1f, 1f, 1f, 0.85f), 0.2f, 0f, t + "Decal_Calcario.png", transparent: true);
        decalFloorWear = Mat(f, "Decal - desgaste do piso", new Color(1f, 1f, 1f, 0.9f), 0.2f, 0f, t + "Decal_DesgastePiso.png", transparent: true);
        decalCeilingStain = Mat(f, "Decal - mancha no forro", new Color(1f, 1f, 1f, 0.55f), 0.1f, 0f, t + "Decal_ManchaTeto.png", transparent: true);
        decalScuffs = Mat(f, "Decal - riscos", new Color(1f, 1f, 1f, 0.8f), 0.2f, 0f, t + "Decal_Riscos.png", transparent: true);

        return new ZoneFinish
        {
            Floor = Mat(f, "Piso porcelanato", Color.white, 0.35f, 0f, t + "Autopsia_Piso_Base.png", t + "Autopsia_Piso_Normal.png", 1.2f),
            Skirting = Mat(f, "Rodape porcelanato", new Color(0.40f, 0.42f, 0.41f), 0.35f),
            Wainscot = Mat(f, "Azulejo", Color.white, 0.78f, 0f, t + "Autopsia_Azulejo_Base.png", t + "Autopsia_Azulejo_Normal.png", 1.2f),
            WainscotHeight = 2.1f,
            UpperWall = Mat(f, "Pintura", Color.white, 0.12f, 0f, t + "Autopsia_Pintura_Base.png", null, 2f),
            Ceiling = Mat(f, "Forro mineral", Color.white, 0.05f, 0f, t + "Autopsia_Forro_Base.png", t + "Autopsia_Forro_Normal.png", 1.25f),
        };
    }

    private static void BuildAutopsia(Transform room)
    {
        Transform furniture = Sub(room, "Mobiliario"), details = Sub(room, "Equipamentos e detalhes"),
            wear = Sub(room, "Marcas de uso"), ceiling = Sub(ceilingsGroup, "Luminarias - Autopsia");

        AutopsyTable(furniture);
        AutopsyFixedDetails(details);
        AutopsyLayout(room, ceiling);
        AutopsyWear(wear);
        AutopsyLighting(ceiling);

        // Box-projected probe so steel reflects the room instead of the sky. Bake it with
        // Tools > Unclaimed > Bake Autopsia Reflection; the baked cubemap is reused on every regeneration.
        var probeObject = new GameObject("Sonda de reflexo");
        Undo.RegisterCreatedObjectUndo(probeObject, "Create reflection probe");
        probeObject.transform.SetParent(room, false);
        probeObject.transform.position = new Vector3((AxMin + AxMax) / 2, 1.6f, (AzMin + AzMax) / 2);
        var probe = probeObject.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Custom;
        probe.size = new Vector3(AxMax - AxMin, WallHeight, AzMax - AzMin);
        probe.boxProjection = true;
        probe.resolution = 256;
        probe.customBakedTexture = AssetDatabase.LoadAssetAtPath<Texture>(AutopsiaReflection);
    }

    // Pedestal table modelled in Blender (source in Blender/MesaAutopsia): 2.8 x 0.8 m with the drain
    // sink at the foot (east) end and the tray at 0.9 m. The prefab carries its own colliders.
    private const string AutopsyTablePrefab = "Assets/Modelos/MesaAutopsia/Mesa de autopsia.prefab";

    private static void AutopsyTable(Transform p)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AutopsyTablePrefab);
        if (prefab == null)
            Debug.LogWarning("Unclaimed: missing " + AutopsyTablePrefab + "; the autopsy table was not placed.");
        else
        {
            var table = (GameObject)PrefabUtility.InstantiatePrefab(prefab, p);
            Undo.RegisterCreatedObjectUndo(table, "Create autopsy table");
            table.transform.SetPositionAndRotation(new Vector3(2.7f, 0f, -0.45f), Quaternion.identity);
        }

        if (AutopsyBody(p)) return;

        // Fallback body under a sheet, lying head to the west.
        Transform body = Sub(p, "Corpo provisório - autopsia");
        Part("Lencol", body, PrimitiveType.Capsule, new Vector3(2.65f, 1f, -0.45f), new Vector3(0.2f, 0.86f, 0.46f),
            Quaternion.Euler(0f, 0f, 90f), sheet);
        Part("Cabeca", body, PrimitiveType.Sphere, new Vector3(1.9f, 1.04f, -0.45f), new Vector3(0.24f, 0.17f, 0.22f), Quaternion.identity, sheet);
        Part("Pes", body, PrimitiveType.Sphere, new Vector3(3.42f, 1.03f, -0.45f), new Vector3(0.12f, 0.14f, 0.34f), Quaternion.identity, sheet);
    }

    private static bool AutopsyBodyPrefabs(out GameObject occupied, out GameObject p08)
    {
        occupied = AssetDatabase.LoadAssetAtPath<GameObject>(UnclaimedAutopsiaAssets.PrefabPath("CorpoOcupado"));
        p08 = AssetDatabase.LoadAssetAtPath<GameObject>(UnclaimedAutopsiaAssets.PrefabPath("P08_LencolCaido"));
        if (occupied != null && p08 != null) return true;
        Debug.LogWarning("Unclaimed: missing autopsy body prefabs; run Tools > Unclaimed > Autopsia > Build Body Prefabs.");
        return false;
    }

    private static bool AutopsyBody(Transform furniture)
    {
        if (!AutopsyBodyPrefabs(out var occupiedPrefab, out var p08Prefab)) return false;
        var stateObject = new GameObject("Estado da mesa de autopsia");
        stateObject.transform.SetParent(furniture, false);
        // Same world placement as the table: the meshes are exported in the table's frame.
        stateObject.transform.SetPositionAndRotation(new Vector3(2.7f, 0f, -0.45f), Quaternion.identity);
        var state = stateObject.AddComponent<EstadoMesaAutopsia>();
        var occupied = (GameObject)PrefabUtility.InstantiatePrefab(occupiedPrefab, stateObject.transform);
        occupied.name = "Corpo - autopsia";
        occupied.transform.localPosition = Vector3.zero;
        occupied.transform.localRotation = Quaternion.identity;
        occupied.transform.localScale = Vector3.one;
        var p08 = (GameObject)PrefabUtility.InstantiatePrefab(p08Prefab, stateObject.transform);
        p08.name = "P08 - lencol caido";
        p08.transform.localPosition = Vector3.zero;
        p08.transform.localRotation = Quaternion.identity;
        p08.transform.localScale = Vector3.one;
        var so = new SerializedObject(state);
        so.FindProperty("ocupado").objectReferenceValue = occupied;
        so.FindProperty("p08").objectReferenceValue = p08;
        so.ApplyModifiedPropertiesWithoutUndo();
        state.MostrarP08(false);
        Undo.RegisterCreatedObjectUndo(stateObject, "Create autopsy body");
        return true;
    }

    [MenuItem("Tools/Unclaimed/Autopsia/Update Autopsy Body")]
    public static void UpdateAutopsiaCorpo()
    {
        var scene = EditorSceneManager.GetActiveScene();
        GameObject morgue = FindExistingMorgue(scene);
        if (scene.path != "Assets/Scenes/PrototipoNecroterio.unity" || morgue == null ||
            morgue.name != GroupName || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Unclaimed: open PrototipoNecroterio in Edit Mode first.");
            return;
        }
        var furniture = morgue.transform.Find("Ambientes/Autopsia/Mobiliario");
        if (furniture == null)
        { Debug.LogWarning("Unclaimed: missing autopsy-room map groups."); return; }
        // Check before removing the fallback so missing assets leave the scene intact.
        if (!AutopsyBodyPrefabs(out _, out _)) return;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Update autopsy body");
        try
        {
            foreach (Transform child in Children(furniture))
                if (child.name == "Corpo provisório - autopsia" || child.name == "Estado da mesa de autopsia")
                    Undo.DestroyObjectImmediate(child.gameObject);
            AutopsyBody(furniture);
            EditorSceneManager.MarkSceneDirty(scene);
        }
        finally { Undo.CollapseUndoOperations(undoGroup); }
    }

    // Fixed fittings retained as primitives; all replaceable furniture comes from Layout.json.
    private static void AutopsyFixedDetails(Transform details)
    {
        Piece("Ralo", details, 3.09f, FinishOffset, -1.71f, 3.31f, 0.008f, -1.49f, drain);
        Piece("Protetor de parede", details, -1.35f, 0.82f, AzMin, 4.45f, 0.96f, AzMin + 0.03f, wallGuard);
        Piece("Protetor de parede", details, 3.95f, 0.82f, AzMax - 0.03f, 7.35f, 0.96f, AzMax, wallGuard);
        Piece("Protetor de parede", details, AxMax - 0.03f, 0.82f, -3.35f, AxMax, 0.96f, -0.65f, wallGuard);
        Piece("Coletor perfurocortante", details, 3.45f, 0.9f, 3.22f, 3.69f, 1.16f, 3.44f, yellowPlastic);
        AutopsyProbeFlags(details);
    }

    [System.Serializable] private class AutopsyLayoutData { public AutopsyLayoutPiece[] pecas; }
    [System.Serializable] private class AutopsyLayoutPiece { public string nome, prefab, grupo; public float[] pos; public float yaw; }

    private static AutopsyLayoutData ReadAutopsyLayout()
    {
        string path = AutopsiaFolder + "/Layout.json";
        if (!File.Exists(path)) { Debug.LogWarning("Unclaimed: missing " + path); return null; }
        var layout = JsonUtility.FromJson<AutopsyLayoutData>(File.ReadAllText(path));
        if (layout == null || layout.pecas == null)
        { Debug.LogWarning("Unclaimed: invalid " + path); return null; }
        return layout;
    }

    private static void AutopsyLayout(Transform room, Transform ceiling)
    {
        var layout = ReadAutopsyLayout();
        if (layout == null) return;
        foreach (var piece in layout.pecas)
        {
            if (piece == null || piece.pos == null || piece.pos.Length != 3 ||
                (piece.grupo != "Mobiliario" && piece.grupo != "Equipamentos e detalhes" && piece.grupo != "Luminaria"))
            { Debug.LogWarning("Unclaimed: invalid autopsy-room layout piece."); continue; }
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(piece.prefab);
            if (asset == null)
            {
                Debug.LogWarning("Unclaimed: missing autopsy-room prefab " + piece.prefab +
                    "; run Tools > Unclaimed > Autopsia > Build Prefabs.");
                continue;
            }
            Transform parent = piece.grupo == "Luminaria" ? Sub(ceiling, "Luminaria de exame") : Sub(room, piece.grupo);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            Undo.RegisterCreatedObjectUndo(go, "Create " + piece.nome);
            go.name = piece.nome;
            go.transform.SetPositionAndRotation(new Vector3(piece.pos[0], piece.pos[1], piece.pos[2]), Quaternion.Euler(0, piece.yaw, 0));
            var lamp = go.GetComponent<LuminariaExame>();
            if (lamp != null) lamp.Ligar(false);
            AutopsyProbeFlags(go.transform);
        }
    }

    // Only newly created objects: the general static pass also changes preserved room finishes.
    private static void AutopsyProbeFlags(Transform parent)
    {
        foreach (var r in parent.GetComponentsInChildren<MeshRenderer>(true))
        {
            GameObjectUtility.SetStaticEditorFlags(r.gameObject,
                GameObjectUtility.GetStaticEditorFlags(r.gameObject) & ~StaticEditorFlags.ContributeGI);
            r.receiveGI = ReceiveGI.LightProbes;
        }
    }

    [MenuItem("Tools/Unclaimed/Autopsia/Update Autopsy Room")]
    public static void UpdateAutopsia()
    {
        var scene = EditorSceneManager.GetActiveScene();
        GameObject morgue = FindExistingMorgue(scene);
        if (scene.path != "Assets/Scenes/PrototipoNecroterio.unity" || morgue == null ||
            morgue.name != GroupName || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Unclaimed: open PrototipoNecroterio in Edit Mode first.");
            return;
        }
        Transform real = morgue.transform;
        var rooms = real.Find("Ambientes");
        var room = rooms != null ? rooms.Find("Autopsia") : null;
        var ceilings = real.Find("Tetos");
        var lights = real.Find("Luzes");
        if (room == null || ceilings == null || lights == null)
        { Debug.LogWarning("Unclaimed: missing autopsy-room map groups."); return; }
        if (ReadAutopsyLayout() == null) return;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Update autopsy furniture");
        try
        {
            root = real; roomsGroup = rooms; ceilingsGroup = ceilings; lightsGroup = lights;
            yellowPlastic = Mat(AutopsiaFolder, "Plastico amarelo", new Color(.86f, .68f, .10f), .4f);
            wallGuard = Mat(AutopsiaFolder, "Protetor de parede", new Color(.52f, .58f, .55f), .35f);
            drain = Mat(AutopsiaFolder, "Ralo", new Color(.16f, .16f, .16f), .4f, .6f);

            Transform furniture = room.Find("Mobiliario");
            foreach (Transform child in Children(furniture))
                if (child.name != "Mesa de autopsia" && child.name != "Corpo provisório - autopsia" &&
                    child.name != "Estado da mesa de autopsia")
                    Undo.DestroyObjectImmediate(child.gameObject);
            Transform details = room.Find("Equipamentos e detalhes");
            if (details != null) Undo.DestroyObjectImmediate(details.gameObject);
            Transform ceiling = Sub(ceilingsGroup, "Luminarias - Autopsia");
            foreach (Transform child in Children(ceiling))
                if (child.name == "Foco cirurgico" || child.name == "Luminaria de exame")
                    Undo.DestroyObjectImmediate(child.gameObject);
            foreach (Transform child in Children(lightsGroup))
                if (child.name == "Luz - autopsia - foco cirurgico") Undo.DestroyObjectImmediate(child.gameObject);

            AutopsyFixedDetails(Sub(room, "Equipamentos e detalhes"));
            AutopsyLayout(room, ceiling);
            // PrepareStaticFlags("Autopsia") would also touch the preserved table, wear and finishes.
            // AutopsyProbeFlags already configures only recreated objects, including glass and lamp children.
            EditorSceneManager.MarkSceneDirty(scene);
        }
        finally { Undo.CollapseUndoOperations(undoGroup); }
    }

    // Signs of everyday use and uneven upkeep, kept local: worn floor where people work and walk,
    // limescale under taps, stretcher scuffs, one old leak in the ceiling.
    private static void AutopsyWear(Transform p)
    {
        Decal("Desgaste - mesa", p, new Vector3(2.7f, 0.005f, -0.45f), Vector3.up, Vector3.right, Vector3.forward, 4.2f, 2.6f, decalFloorWear);
        Decal("Desgaste - porta da circulacao", p, new Vector3(-1.2f, 0.005f, 0f), Vector3.up, Vector3.right, Vector3.forward, 1.6f, 1.8f, decalFloorWear);
        Decal("Desgaste - porta da camara fria", p, new Vector3(6.75f, 0.005f, 0.25f), Vector3.up, Vector3.right, Vector3.forward, 1.4f, 1.8f, decalFloorWear);
        Decal("Desgaste - ralo", p, new Vector3(3.2f, 0.006f, -1.6f), Vector3.up, Vector3.right, Vector3.forward, 0.8f, 0.8f, decalFloorWear);
        Decal("Mancha de infiltracao", p, new Vector3(6.3f, WallHeight - 0.006f, 2.7f), Vector3.down, Vector3.right, Vector3.forward, 1.1f, 0.9f, decalCeilingStain);
        Decal("Calcario - pia de maos", p, new Vector3(6.35f, 0.45f, AzMin + 0.006f), Vector3.forward, Vector3.right, Vector3.up, 0.7f, 0.5f, decalLimescale);
        Decal("Calcario - bancada", p, new Vector3(1.3f, 1.2f, AzMax - 0.006f), Vector3.back, Vector3.right, Vector3.up, 0.6f, 0.4f, decalLimescale);
        Decal("Riscos - parede sul", p, new Vector3(0.4f, 0.85f, AzMin + 0.006f), Vector3.forward, Vector3.right, Vector3.up, 2.2f, 0.7f, decalScuffs);
        Decal("Riscos - porta da camara fria", p, new Vector3(AxMax - 0.006f, 0.9f, 1.45f), Vector3.left, Vector3.forward, Vector3.up, 0.7f, 0.6f, decalScuffs);
        Decal("Riscos - porta da circulacao", p, new Vector3(AxMin + 0.006f, 0.85f, 1.2f), Vector3.right, Vector3.forward, Vector3.up, 0.7f, 0.6f, decalScuffs);
    }

    // Six surface-mounted fluorescent fixtures (one aged tube, one burnt out).
    // Only the two fixtures flanking the table cast shadows, so the shadow atlas keeps its resolution.
    // Bounce light comes from the baked indirect lighting (UnclaimedMorgueGenerator.Iluminacao.cs).
    private static void AutopsyLighting(Transform ceiling)
    {
        var fixtures = new[] { (0f, -1.9f, 0), (2.75f, -1.9f, 0), (5.5f, -1.9f, 1), (0f, 1.4f, 2), (2.75f, 1.4f, 0), (5.5f, 1.4f, 0) };
        for (int i = 0; i < fixtures.Length; i++)
        {
            var (x, z, state) = fixtures[i];
            CeilingFixture(ceiling, "Luz - autopsia - luminaria " + (i + 1), "Luminaria " + (i + 1), x, z, state, 5.6f, 5600f,
                Mathf.Approximately(x, 2.75f), model: true);
        }

    }

    // Shared by ceiling fixtures and exterior lighting; the examination lamp uses its own prefab light.
    private static Light SpotLight(string name, Vector3 position, float angle, float innerAngle, float range,
        float intensity, float kelvin, bool shadows)
    {
        var obj = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(obj, "Create light");
        obj.transform.SetParent(lightsGroup, false);
        obj.transform.SetPositionAndRotation(position, Quaternion.Euler(90f, 0f, 0f));
        var light = obj.AddComponent<Light>();
        light.type = LightType.Spot;
        light.spotAngle = angle;
        light.innerSpotAngle = innerAngle;
        light.range = range;
        light.intensity = intensity;
        light.useColorTemperature = true;
        light.colorTemperature = kelvin;
        light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        light.lightmapBakeType = LightmapBakeType.Mixed;
        return light;
    }

    // Transparent quad laid just over a surface; its texture spans the whole quad.
    private static void Decal(string name, Transform parent, Vector3 center, Vector3 normal, Vector3 axisU, Vector3 axisV,
        float sizeU, float sizeV, Material material)
    {
        Vector3 origin = center - axisU * (sizeU / 2) - axisV * (sizeV / 2);
        Panel(name, parent, origin, axisU, axisV, sizeU, sizeV, normal, material, worldUv: false);
    }

    [MenuItem("Tools/Unclaimed/Bake Autopsia Reflection")]
    public static void BakeAutopsiaReflection()
    {
        foreach (ReflectionProbe probe in Object.FindObjectsByType<ReflectionProbe>())
        {
            if (probe.name != "Sonda de reflexo" || probe.transform.parent == null || probe.transform.parent.name != "Autopsia")
                continue;
            EnsureFolder(AutopsiaFolder);
            if (!Lightmapping.BakeReflectionProbe(probe, AutopsiaReflection))
            {
                Debug.LogWarning("Unclaimed: the autopsy reflection probe could not be baked.", probe);
                return;
            }
            AssetDatabase.ImportAsset(AutopsiaReflection);
            Undo.RecordObject(probe, "Bake autopsy reflection");
            probe.customBakedTexture = AssetDatabase.LoadAssetAtPath<Texture>(AutopsiaReflection);
            Debug.Log("Unclaimed: autopsy reflection baked to " + AutopsiaReflection, probe);
            BakeDoorwayReflections();
            return;
        }
        Debug.LogWarning("Unclaimed: no autopsy reflection probe in the open scenes.");
    }
}
