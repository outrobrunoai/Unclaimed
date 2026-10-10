// Finish and dressing of the autopsy room, the reference room for the morgue's look:
// an everyday workplace with uneven upkeep and signs of use, not a ruin.
// Models are composed from primitives until real assets replace them.
using UnityEditor;
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
        AutopsyCounter(furniture, details);
        AutopsyStorage(furniture);
        AutopsyWorkArea(furniture, details);
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

        // A body under a sheet, lying head to the west.
        Transform body = Sub(p, "Corpo provisório - autopsia");
        Part("Lencol", body, PrimitiveType.Capsule, new Vector3(2.65f, 1f, -0.45f), new Vector3(0.2f, 0.86f, 0.46f),
            Quaternion.Euler(0f, 0f, 90f), sheet);
        Part("Cabeca", body, PrimitiveType.Sphere, new Vector3(1.9f, 1.04f, -0.45f), new Vector3(0.24f, 0.17f, 0.22f), Quaternion.identity, sheet);
        Part("Pes", body, PrimitiveType.Sphere, new Vector3(3.42f, 1.03f, -0.45f), new Vector3(0.12f, 0.14f, 0.34f), Quaternion.identity, sheet);
    }

    // Stainless counter (top at 0.9 m) with a sink and base cabinets along the north wall; the clue
    // placeholder "Pista - laudo da autopsia" rests on it.
    private static void AutopsyCounter(Transform p, Transform details)
    {
        Transform c = Solid("Bancada com pia - autopsia", p, 0f, 2.875f, 3.8f, AzMax, 0f, 0.9f);
        Piece("Rodape recuado", c, 0f, 0f, 2.98f, 3.8f, 0.1f, AzMax, rubber);
        Piece("Gabinete", c, 0f, 0.1f, 2.925f, 1f, 0.86f, AzMax, steel);
        Piece("Gabinete", c, 1.6f, 0.1f, 2.925f, 3.8f, 0.86f, AzMax, steel);
        Piece("Gabinete", c, 1f, 0.1f, 2.925f, 1.6f, 0.6f, AzMax, steel);
        for (int i = 0; i < 4; i++)
        {
            float x0 = 0.02f + i * 0.95f, hx = i % 2 == 0 ? x0 + 0.74f : x0 + 0.05f;
            Piece("Porta do gabinete", c, x0, 0.14f, 2.91f, x0 + 0.91f, 0.82f, 2.925f, steelWorn);
            Piece("Puxador", c, hx, 0.72f, 2.885f, hx + 0.12f, 0.74f, 2.91f, rubber);
        }
        Piece("Tampo", c, -0.02f, 0.86f, 2.875f, 1f, 0.9f, AzMax, steelWorn);
        Piece("Tampo", c, 1.6f, 0.86f, 2.875f, 3.82f, 0.9f, AzMax, steelWorn);
        Piece("Tampo", c, 1f, 0.86f, 2.875f, 1.6f, 0.9f, 2.95f, steelWorn);
        Piece("Tampo", c, 1f, 0.86f, 3.45f, 1.6f, 0.9f, AzMax, steelWorn);
        Piece("Cuba - fundo", c, 1f, 0.6f, 2.95f, 1.6f, 0.62f, 3.45f, steel);
        Piece("Cuba", c, 1f, 0.6f, 2.95f, 1.6f, 0.86f, 2.965f, steel);
        Piece("Cuba", c, 1f, 0.6f, 3.435f, 1.6f, 0.86f, 3.45f, steel);
        Piece("Cuba", c, 1f, 0.6f, 2.965f, 1.015f, 0.86f, 3.435f, steel);
        Piece("Cuba", c, 1.585f, 0.6f, 2.965f, 1.6f, 0.86f, 3.435f, steel);
        Piece("Espelho", c, -0.02f, 0.9f, 3.495f, 3.82f, 1f, AzMax, steel);
        Cyl("Torneira", c, new Vector3(1.3f, 1.06f, 3.44f), 0.03f, 0.32f, steel);
        Piece("Bica", c, 1.285f, 1.2f, 3.21f, 1.315f, 1.225f, 3.455f, steel);
        Cyl("Arejador", c, new Vector3(1.3f, 1.17f, 3.225f), 0.025f, 0.06f, steel);

        Piece("Balanca de visceras - base", details, 2.19f, 0.9f, 3.07f, 2.51f, 0.96f, 3.33f, whiteMetal);
        Cyl("Balanca de visceras - prato", details, new Vector3(2.35f, 0.975f, 3.2f), 0.3f, 0.03f, steel);
        foreach (float x in new[] { 3.35f, 3.5f, 3.65f })
        {
            Cyl("Frasco de amostra", details, new Vector3(x, 0.98f, 3.3f), 0.11f, 0.16f, glass);
            Cyl("Tampa do frasco", details, new Vector3(x, 1.075f, 3.3f), 0.115f, 0.03f, whitePlastic);
        }
        Piece("Coletor perfurocortante", details, 0.18f, 0.9f, 3.22f, 0.42f, 1.16f, 3.44f, yellowPlastic);
        Piece("Prancheta", details, 0.62f, 0.9f, 2.92f, 0.86f, 0.91f, 3.24f, paper);
        for (int i = 0; i < 3; i++)
        {
            float x0 = 0.12f + i * 0.28f;
            Piece("Caixa de luvas", details, x0, 1.2f, 3.39f, x0 + 0.24f, 1.33f, 3.52f, whitePlastic);
        }

        // Wall cabinet with glass doors above the counter.
        Transform w = Solid("Armario suspenso", p, 2.2f, 3.175f, 3.8f, AzMax, 1.5f, 2.2f);
        Piece("Fundo", w, 2.2f, 1.5f, 3.505f, 3.8f, 2.2f, AzMax, whiteMetal);
        Piece("Topo", w, 2.2f, 2.18f, 3.175f, 3.8f, 2.2f, 3.505f, whiteMetal);
        Piece("Base", w, 2.2f, 1.5f, 3.175f, 3.8f, 1.52f, 3.505f, whiteMetal);
        Piece("Lateral", w, 2.2f, 1.52f, 3.175f, 2.22f, 2.18f, 3.505f, whiteMetal);
        Piece("Lateral", w, 3.78f, 1.52f, 3.175f, 3.8f, 2.18f, 3.505f, whiteMetal);
        Piece("Prateleira", w, 2.22f, 1.84f, 3.2f, 3.78f, 1.855f, 3.505f, whiteMetal);
        Piece("Porta de vidro", w, 2.22f, 1.52f, 3.175f, 2.995f, 2.18f, 3.183f, glass);
        Piece("Porta de vidro", w, 3.005f, 1.52f, 3.175f, 3.78f, 2.18f, 3.183f, glass);
        Piece("Montante", w, 2.99f, 1.52f, 3.17f, 3.01f, 2.18f, 3.185f, whiteMetal);
        Piece("Caixa", w, 2.3f, 1.52f, 3.28f, 2.6f, 1.68f, 3.48f, cardboard);
        Piece("Caixa", w, 2.65f, 1.52f, 3.3f, 2.9f, 1.64f, 3.48f, whitePlastic);
        foreach (float x in new[] { 3.15f, 3.3f, 3.45f })
            Cyl("Frasco", w, new Vector3(x, 1.6f, 3.38f), 0.08f, 0.16f, whitePlastic);
        Piece("Caixa", w, 2.35f, 1.855f, 3.27f, 2.75f, 2f, 3.48f, cardboard);
        Piece("Caixa", w, 3.2f, 1.855f, 3.3f, 3.7f, 1.95f, 3.48f, cardboard);
    }

    // Tall instrument cabinet (west wall) and open supply shelving (east wall).
    private static void AutopsyStorage(Transform p)
    {
        Transform k = Solid("Armario de instrumental", p, AxMin, -3.1f, -1.45f, -1.7f, 0f, 2f);
        Piece("Rodape", k, AxMin, 0f, -3.08f, -1.47f, 0.1f, -1.72f, rubber);
        Piece("Fundo", k, AxMin, 0.1f, -3.1f, -1.88f, 2f, -1.7f, whiteMetal);
        Piece("Base", k, -1.88f, 0.1f, -3.1f, -1.45f, 0.14f, -1.7f, whiteMetal);
        Piece("Topo", k, -1.88f, 1.97f, -3.1f, -1.45f, 2f, -1.7f, whiteMetal);
        Piece("Lateral", k, -1.88f, 0.14f, -3.1f, -1.45f, 1.97f, -3.08f, whiteMetal);
        Piece("Lateral", k, -1.88f, 0.14f, -1.72f, -1.45f, 1.97f, -1.7f, whiteMetal);
        foreach (float y in new[] { 0.6f, 1.05f, 1.5f })
            Piece("Prateleira", k, -1.88f, y, -3.08f, -1.47f, y + 0.015f, -1.72f, whiteMetal);
        Piece("Porta de vidro", k, -1.458f, 0.14f, -3.08f, -1.45f, 1.97f, -2.405f, glass);
        Piece("Porta de vidro", k, -1.458f, 0.14f, -2.395f, -1.45f, 1.97f, -1.72f, glass);
        Piece("Montante", k, -1.46f, 0.14f, -2.41f, -1.445f, 1.97f, -2.39f, whiteMetal);
        Piece("Campos dobrados", k, -1.85f, 0.14f, -3f, -1.55f, 0.32f, -2.3f, sheet);
        Piece("Bandeja de instrumental", k, -1.85f, 0.615f, -3f, -1.55f, 0.65f, -2.6f, steel);
        Piece("Bandeja de instrumental", k, -1.85f, 0.615f, -2.5f, -1.55f, 0.65f, -2.1f, steel);
        Piece("Caixa", k, -1.85f, 1.065f, -3f, -1.6f, 1.22f, -2.65f, cardboard);
        Piece("Caixa", k, -1.85f, 1.065f, -2.55f, -1.6f, 1.18f, -2.2f, whitePlastic);
        foreach (float z in new[] { -2.9f, -2.75f, -2.6f })
            Cyl("Frasco", k, new Vector3(-1.7f, 1.6f, z), 0.08f, 0.17f, glass);
        Piece("Caixa", k, -1.85f, 1.515f, -2.2f, -1.6f, 1.68f, -1.8f, cardboard);

        Transform s = Solid("Estante de insumos", p, 6.95f, 1.9f, AxMax, 3.3f, 0f, 2f);
        foreach (float x in new[] { 6.97f, 7.37f })
            foreach (float z in new[] { 1.92f, 3.28f })
                Piece("Coluna", s, x - 0.015f, 0f, z - 0.015f, x + 0.015f, 2f, z + 0.015f, whiteMetal);
        foreach (float y in new[] { 0.12f, 0.6f, 1.08f, 1.56f, 1.98f })
            Piece("Prateleira", s, 6.955f, y - 0.02f, 1.905f, 7.395f, y, 3.295f, whiteMetal);
        Piece("Galao de detergente", s, 7f, 0.12f, 1.98f, 7.3f, 0.5f, 2.3f, whitePlastic);
        Piece("Galao de detergente", s, 7f, 0.12f, 2.4f, 7.3f, 0.45f, 2.7f, whitePlastic);
        Piece("Caixa", s, 7f, 0.6f, 2f, 7.35f, 0.85f, 2.5f, cardboard);
        Piece("Caixa", s, 7f, 0.6f, 2.6f, 7.35f, 0.8f, 3.2f, cardboard);
        for (int i = 0; i < 4; i++)
            Piece("Caixa de luvas", s, 7f, 1.08f, 2f + i * 0.3f, 7.3f, 1.2f, 2.25f + i * 0.3f, whitePlastic);
        Piece("Papel toalha", s, 7f, 1.56f, 2f, 7.35f, 1.8f, 2.9f, paper);
        Piece("Caixa", s, 7f, 1.98f, 2.2f, 7.35f, 2.15f, 2.8f, cardboard);
    }

    // Hand-wash sink, bins and wall fittings on the south wall; mobile pieces around the table.
    private static void AutopsyWorkArea(Transform p, Transform details)
    {
        Transform h = Solid("Pia de lavagem de maos", p, 6f, AzMin, 6.7f, -2.95f, 0f, 0.9f);
        Piece("Fundo", h, 6f, 0.68f, AzMin, 6.7f, 0.7f, -2.95f, steel);
        Piece("Cuba", h, 6f, 0.68f, -2.97f, 6.7f, 0.9f, -2.95f, steel);
        Piece("Cuba", h, 6f, 0.68f, AzMin, 6.02f, 0.9f, -2.97f, steel);
        Piece("Cuba", h, 6.68f, 0.68f, AzMin, 6.7f, 0.9f, -2.97f, steel);
        Piece("Espelho", h, 6f, 0.9f, AzMin, 6.7f, 1.05f, AzMin + 0.02f, steel);
        Piece("Valvula de joelho", h, 6.3f, 0.45f, -3.02f, 6.4f, 0.6f, -2.96f, steel);
        Piece("Torneira", h, 6.33f, 0.95f, AzMin, 6.37f, 0.99f, -3.15f, steel);
        Piece("Dispenser de sabonete", details, 5.72f, 1.1f, AzMin, 5.84f, 1.32f, AzMin + 0.1f, whitePlastic);
        Piece("Dispenser de papel toalha", details, 6.9f, 1.15f, AzMin, 7.2f, 1.47f, AzMin + 0.12f, whitePlastic);

        Transform bin = Solid("Lixeira infectante", p, 4.55f, -3.37f, 5f, -3f, 0f, 0.62f);
        Piece("Corpo", bin, 4.56f, 0f, -3.36f, 4.99f, 0.58f, -3.01f, whitePlastic);
        Piece("Tampa", bin, 4.55f, 0.58f, -3.37f, 5f, 0.62f, -3f, whitePlastic);
        Piece("Pedal", bin, 4.7f, 0f, -3f, 4.85f, 0.04f, -2.95f, rubber);
        Transform bin2 = Solid("Lixeira comum", p, 5.1f, -3.37f, 5.45f, -3.05f, 0f, 0.55f);
        Piece("Corpo", bin2, 5.1f, 0f, -3.37f, 5.45f, 0.55f, -3.05f, steelWorn);

        Cyl("Mangueira enrolada", details, new Vector3(2.6f, 1.35f, AzMin + 0.06f), 0.45f, 0.1f, rubber, new Vector3(90f, 0f, 0f));
        Cyl("Suporte da mangueira", details, new Vector3(2.6f, 1.35f, AzMin + 0.13f), 0.12f, 0.05f, steel, new Vector3(90f, 0f, 0f));
        Piece("Ralo", details, 3.09f, FinishOffset, -1.71f, 3.31f, 0.008f, -1.49f, drain);
        Piece("Quadro de avisos", details, AxMin, 1.2f, 1.55f, AxMin + 0.02f, 1.8f, 2.45f, cardboard);
        Piece("Aviso", details, AxMin + 0.02f, 1.3f, 1.65f, AxMin + 0.024f, 1.6f, 1.9f, paper);
        Piece("Aviso", details, AxMin + 0.02f, 1.45f, 2f, AxMin + 0.024f, 1.75f, 2.3f, paper);
        Cyl("Relogio", details, new Vector3(AxMin + 0.02f, 2.65f, 0f), 0.3f, 0.04f, whitePlastic, new Vector3(0f, 0f, 90f));
        Piece("Ponteiro", details, AxMin + 0.042f, 2.64f, -0.006f, AxMin + 0.046f, 2.75f, 0.006f, rubber);
        Piece("Ponteiro", details, AxMin + 0.042f, 2.645f, -0.004f, AxMin + 0.046f, 2.655f, 0.12f, rubber);

        // Stretcher bumpers where trolleys rub the walls.
        Piece("Protetor de parede", details, -1.35f, 0.82f, AzMin, 4.45f, 0.96f, AzMin + 0.03f, wallGuard);
        Piece("Protetor de parede", details, 3.95f, 0.82f, AzMax - 0.03f, 7.35f, 0.96f, AzMax, wallGuard);
        Piece("Protetor de parede", details, AxMax - 0.03f, 0.82f, -3.35f, AxMax, 0.96f, -0.65f, wallGuard);

        Transform tr = Solid("Carrinho de instrumental", p, 4.6f, -1.75f, 5.4f, -1.25f, 0f, 0.92f);
        foreach (float x in new[] { 4.62f, 5.38f })
            foreach (float z in new[] { -1.73f, -1.27f })
            {
                Cyl("Coluna", tr, new Vector3(x, 0.5f, z), 0.025f, 0.84f, steel);
                Part("Rodizio", tr, PrimitiveType.Sphere, new Vector3(x, 0.04f, z), Vector3.one * 0.07f, Quaternion.identity, rubber);
            }
        Piece("Bandeja", tr, 4.6f, 0.42f, -1.75f, 5.4f, 0.44f, -1.25f, steel);
        Piece("Bandeja", tr, 4.6f, 0.88f, -1.75f, 5.4f, 0.9f, -1.25f, steel);
        Piece("Campo", tr, 4.7f, 0.9f, -1.68f, 5.25f, 0.905f, -1.32f, sheet);
        for (int i = 0; i < 4; i++)
        {
            float x = 4.78f + i * 0.1f;
            Piece("Instrumento", tr, x, 0.905f, -1.62f, x + 0.015f, 0.915f, -1.42f, steel);
        }

        Transform stool = Solid("Banco da autopsia", p, 1.37f, -2.18f, 1.83f, -1.72f, 0f, 0.62f);
        Cyl("Base", stool, new Vector3(1.6f, 0.02f, -1.95f), 0.46f, 0.04f, steel);
        Cyl("Coluna", stool, new Vector3(1.6f, 0.31f, -1.95f), 0.045f, 0.56f, steel);
        Cyl("Assento", stool, new Vector3(1.6f, 0.6f, -1.95f), 0.36f, 0.05f, rubber);

        Transform g = Solid("Maca de transporte", p, 6.6f, -2.75f, 7.35f, -0.65f, 0f, 0.98f);
        Piece("Leito", g, 6.6f, 0.78f, -2.75f, 7.35f, 0.82f, -0.65f, steelWorn);
        Piece("Colchonete", g, 6.63f, 0.82f, -2.7f, 7.32f, 0.86f, -0.7f, rubber);
        Piece("Prateleira", g, 6.63f, 0.25f, -2.6f, 7.32f, 0.27f, -0.8f, steel);
        Piece("Grade", g, 6.6f, 0.86f, -2.4f, 6.62f, 0.98f, -1f, steel);
        Piece("Grade", g, 7.33f, 0.86f, -2.4f, 7.35f, 0.98f, -1f, steel);
        Piece("Lencol dobrado", g, 6.75f, 0.86f, -2.6f, 7.2f, 0.92f, -2.2f, sheet);
        foreach (float x in new[] { 6.64f, 7.31f })
            foreach (float z in new[] { -2.71f, -0.69f })
            {
                Cyl("Coluna", g, new Vector3(x, 0.45f, z), 0.03f, 0.7f, steel);
                Part("Rodizio", g, PrimitiveType.Sphere, new Vector3(x, 0.05f, z), Vector3.one * 0.1f, Quaternion.identity, rubber);
            }
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

    // Six surface-mounted fluorescent fixtures (one aged tube, one burnt out) and a surgical lamp over the table.
    // Only the two fixtures flanking the table cast shadows, so the shadow atlas keeps its resolution.
    // Bounce light comes from the baked indirect lighting (UnclaimedMorgueGenerator.Iluminacao.cs).
    private static void AutopsyLighting(Transform ceiling)
    {
        var fixtures = new[] { (0f, -1.9f, 0), (2.75f, -1.9f, 0), (5.5f, -1.9f, 1), (0f, 1.4f, 2), (2.75f, 1.4f, 0), (5.5f, 1.4f, 0) };
        for (int i = 0; i < fixtures.Length; i++)
        {
            var (x, z, state) = fixtures[i];
            CeilingFixture(ceiling, "Luz - autopsia - luminaria " + (i + 1), "Luminaria " + (i + 1), x, z, state, 5.6f, 5600f,
                Mathf.Approximately(x, 2.75f));
        }

        Transform lamp = Sub(ceiling, "Foco cirurgico");
        Cyl("Fixacao", lamp, new Vector3(2.7f, 3.17f, -0.45f), 0.22f, 0.06f, whiteMetal);
        Cyl("Haste", lamp, new Vector3(2.7f, 2.74f, -0.45f), 0.05f, 0.8f, whiteMetal);
        Cyl("Cupula", lamp, new Vector3(2.7f, 2.3f, -0.45f), 0.62f, 0.1f, whiteMetal);
        Cyl("Lente", lamp, new Vector3(2.7f, 2.247f, -0.45f), 0.46f, 0.008f, surgicalGlass);
        SpotLight("Luz - autopsia - foco cirurgico", new Vector3(2.7f, 2.23f, -0.45f), 55f, 30f, 4f, 3f, 4500f, true);
    }

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
            return;
        }
        Debug.LogWarning("Unclaimed: no autopsy reflection probe in the open scenes.");
    }
}
