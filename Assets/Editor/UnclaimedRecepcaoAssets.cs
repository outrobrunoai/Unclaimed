// Builds the URP materials and prefabs of the reception furniture modelled in Blender
// (source and scripts in Blender/Recepcao; run scripts/gerar_tudo.py there first).
// Run Tools > Unclaimed > Build Recepcao Prefabs after the FBX files or baked textures change.
// Each asset has one mesh and one material on a baked atlas (BaseMap, MetallicSmoothness with
// smoothness in alpha, Normal, Occlusion); its prefab carries simple box colliders (none for the desk
// kit and the register book, which rest on the counter's collider).
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;

public static class UnclaimedRecepcaoAssets
{
    public const string Folder = "Assets/Modelos/Recepcao";
    // Bathroom fittings and door (Blender/WC, same pipeline).
    public const string WCFolder = "Assets/Modelos/WC";

    private readonly struct Asset
    {
        public readonly string Dir, File, Prefab;
        public readonly int Atlas;
        public readonly Bounds[] Colliders; // prefab-local, metres; null = one box around the mesh, empty = none

        public Asset(string file, string prefab, int atlas, params Bounds[] colliders) : this(Folder, file, prefab, atlas, colliders) { }

        public Asset(string dir, string file, string prefab, int atlas, params Bounds[] colliders)
        {
            Dir = dir; File = file; Prefab = prefab; Atlas = atlas; Colliders = colliders;
        }
    }

    // Prefab-local frame: origin on the floor at the centre of the reserved footprint, front towards -Z.
    private static readonly Asset[] Assets =
    {
        // Moving parts first: the counter and cabinet prefabs below take their materials.
        new Asset("GavetaBalcao1", null, 1024),
        new Asset("GavetaBalcao2", null, 1024),
        new Asset("GavetaBalcao3", null, 1024),
        new Asset("PortaArmarioEsquerda", null, 512),
        new Asset("PortaArmarioDireita", null, 512),
        // Public part up to the ledge (1.10 m, to the riser at z = -0.275), staff part up to the worktop (0.76 m) and
        // the drawer fronts' plane (z = 0.10), plus the worktop's lip over the drawers: the drawers carry their own
        // colliders in front of the body's, and the computer on the worktop stays reachable by the aim.
        new Asset("BalcaoRecepcao", "Balcao de recepcao", 2048,
            new Bounds(new Vector3(0f, 0.55f, -0.4375f), new Vector3(2.5f, 1.1f, 0.325f)),
            new Bounds(new Vector3(0f, 0.38f, -0.0875f), new Vector3(2.5f, 0.76f, 0.375f)),
            new Bounds(new Vector3(0f, 0.7475f, 0.11f), new Vector3(2.5f, 0.025f, 0.02f))),
        new Asset("CadeiraEspera", "Cadeira de espera", 1024,
            new Bounds(new Vector3(0f, 0.42f, 0.015f), new Vector3(0.47f, 0.84f, 0.48f))),
        new Asset("CadeiraAtendente", "Cadeira do atendente", 1024,
            new Bounds(new Vector3(0f, 0.51f, 0f), new Vector3(0.62f, 1.02f, 0.62f))),
        // Body as panels (top, sides, back, base, shelf) behind the doors' own colliders: the inside stays open to
        // the aim, so the key on the shelf can be reached only with a door open.
        new Asset("ArmarioApoio", "Armario de apoio", 1024,
            new Bounds(new Vector3(0f, 0.9375f, 0f), new Vector3(0.9f, 0.025f, 0.45f)),
            new Bounds(new Vector3(-0.441f, 0.4625f, 0.009f), new Vector3(0.018f, 0.925f, 0.432f)),
            new Bounds(new Vector3(0.441f, 0.4625f, 0.009f), new Vector3(0.018f, 0.925f, 0.432f)),
            new Bounds(new Vector3(0f, 0.5025f, 0.214f), new Vector3(0.864f, 0.845f, 0.018f)),
            new Bounds(new Vector3(0f, 0.049f, 0.009f), new Vector3(0.9f, 0.098f, 0.432f)),
            new Bounds(new Vector3(0f, 0.509f, 0.008f), new Vector3(0.864f, 0.018f, 0.39f))),
        new Asset("LongarinaEspera", "Longarina de espera", 1024, null),
        new Asset("EquipamentosBalcao", "Equipamentos do balcao", 2048),
        new Asset("LivroRegistro", "Livro de registro", 512),
        new Asset("Bebedouro", "Bebedouro", 1024, null),
        new Asset("Lixeira", "Lixeira", 512, null),
        // Baked parts of the composite prefabs below (no prefab of their own).
        new Asset("LuminariaSobrepor", null, 1024),
        new Asset("LuminariaExterna", null, 512),
        new Asset("PortaVidroFixos", null, 512),
        new Asset("PortaVidroFolha", null, 1024),
        // Restricted circulation door (frame with the fixed leaf, active leaf): composite prefab below.
        new Asset("PortaCirculacaoFixos", null, 1024),
        new Asset("PortaCirculacaoFolha", null, 1024),
        new Asset("PortaArquivoFixos", null, 1024),
        new Asset("PortaArquivoFolha", null, 1024),
        new Asset("PortaArquivoTrinco", null, 1024),
        // Entrance frame (aluminium lining, casings, sill): thin colliders on the jambs only.
        new Asset("BatenteEntrada", EntranceFramePrefab, 1024,
            new Bounds(new Vector3(-0.985f, 1.1f, 0f), new Vector3(0.03f, 2.2f, 0.21f)),
            new Bounds(new Vector3(0.985f, 1.1f, 0f), new Vector3(0.03f, 2.2f, 0.21f))),
        // Bathroom: floor fittings with simple colliders, wall fittings without (they stay out of the way).
        new Asset(WCFolder, "VasoSanitario", "Vaso sanitario", 1024,
            // Bowl box as tall as the tank: at seat height (0.40) the player's capsule rode up its corner.
            new Bounds(new Vector3(0f, 0.385f, -0.36f), new Vector3(0.36f, 0.77f, 0.60f)),
            new Bounds(new Vector3(0f, 0.57f, -0.11f), new Vector3(0.40f, 0.40f, 0.20f))),
        new Asset(WCFolder, "Lavatorio", "Lavatorio", 1024,
            new Bounds(new Vector3(0f, 0.71f, -0.21f), new Vector3(0.50f, 0.18f, 0.42f))),
        new Asset(WCFolder, "Saboneteira", "Saboneteira", 512, new Bounds[0]),
        new Asset(WCFolder, "ToalheiroPapel", "Toalheiro de papel", 512, new Bounds[0]),
        new Asset(WCFolder, "PapeleiraRolao", "Papeleira de rolao", 512, new Bounds[0]),
        new Asset(WCFolder, "BarraApoio", "Barra de apoio", 512, new Bounds[0]),
        new Asset(WCFolder, "PortaWCFixos", null, 1024),
        new Asset(WCFolder, "PortaWCFolha", null, 1024),
        new Asset(WCFolder, "Ralo", "Ralo", 512, new Bounds[0]),
        new Asset(WCFolder, "EscovaSanitaria", "Escova sanitaria", 512,
            new Bounds(new Vector3(0f, 0.0625f, 0f), new Vector3(0.105f, 0.125f, 0.105f))),
        new Asset(WCFolder, "Exaustor", "Exaustor", 512, new Bounds[0]),
    };

    // Counter drawers (Blender GAVETAS): slide out along +Z by 0.40 m. Support cabinet doors (Blender ARMARIO):
    // hinge axes at x = -+0.45, z = -0.207 (door back face), panel and pull in the hinge frame.
    private const float DrawerTravel = 0.40f, CabinetDoorOpen = 100f;
    private static readonly Vector3 CabinetHinge = new Vector3(0.45f, 0f, -0.207f);
    // Self-closing tap (Blender LAVATORIO, Unity axes): spout outlet and its direction.
    private static readonly Vector3 SpoutTip = new Vector3(0f, 0.862f, -0.125f), SpoutDir = new Vector3(0f, -0.013f, -0.025f);

    public const string EntranceFramePrefab = "Batente da entrada", WCDoorPrefab = "Porta do WC", MirrorPrefab = "Espelho";
    // Bathroom door (Blender PORTA_WC, Unity axes): hinge axis and leaf box in the leaf frame.
    private static readonly Vector3 WCPivot = new Vector3(0.469f, 0f, 0.017f);
    private static readonly Vector3 WCLeafCenter = new Vector3(-0.469f, 1.0875f, -0.0195f), WCLeafSize = new Vector3(0.934f, 2.159f, 0.035f);

    // Composite prefabs: surface ceiling fitting, entrance wall lamp, glass entrance door.
    public const string FittingPrefab = "Luminaria de sobrepor", WallLampPrefab = "Arandela externa",
        DoorPrefab = "Porta de vidro - entrada";
    // Light parts of the fittings: diffuser (slot 0) and emissive tubes / LED plate (slot 1).
    public const string LightPartName = "Difusor e lampadas";
    public const string DiffuserLit = "Difusor prismatico aceso", DiffuserOff = "Difusor prismatico apagado";
    private const string Textures = "Assets/UnclaimedPrototype/Recepcao/Texturas/";
    private const string TubeMaterials = "Assets/UnclaimedPrototype/Autopsia/";
    // Door (Blender PORTA, mapped to Unity: x, z -> y, y -> z): left pivot axis and leaf size. The leaves
    // carry FolhaDePorta (Assets/Scripts): open/close by interaction, obstruction test, initial state.
    public const float DoorPivotX = -0.909f, DoorOpenAngle = 88f;
    private static readonly Vector3 LeafCenter = new Vector3(0.426f, 1.09f, 0f), LeafSize = new Vector3(0.968f, 2.16f, 0.05f);

    // Lit monitor screen of the desk kit (prefab-local; Blender KIT dimensions, screen tilted back 6 degrees).
    // The monitor shows the idle desktop of the reception computer (Blender/Recepcao/scripts/texturas_puzzle.py).
    private const string ScreenTexture = "Assets/UnclaimedPrototype/Computador/Tela_Area_de_Trabalho.png";
    private const string ComputerFolder = "Assets/UnclaimedPrototype/Computador/";
    private const string PuzzleTextures = "Assets/UnclaimedPrototype/Recepcao/Texturas/";
    private static readonly Vector3 ScreenCenter = new Vector3(0.15f, 1.0085f, -0.196f);
    private static readonly Vector3 ScreenNormal = new Vector3(0f, Mathf.Sin(6f * Mathf.Deg2Rad), Mathf.Cos(6f * Mathf.Deg2Rad));
    private static readonly Vector2 ScreenSize = new Vector2(0.506f, 0.293f);

    public static string PrefabPath(string prefab) => Folder + "/" + prefab + ".prefab";
    public static string WCPrefabPath(string prefab) => WCFolder + "/" + prefab + ".prefab";

    // Material of a light part by fitting state (0 lit, 1 aged tube, 2 burnt out): diffuser and tube.
    public static Material[] LightMaterials(int state)
    {
        string tube = state == 0 ? "Difusor aceso" : state == 1 ? "Difusor gasto" : "Difusor apagado";
        return new[]
        {
            AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materiais/" + (state == 2 ? DiffuserOff : DiffuserLit) + ".mat"),
            AssetDatabase.LoadAssetAtPath<Material>(TubeMaterials + tube + ".mat"),
        };
    }

    [MenuItem("Tools/Unclaimed/Build Recepcao Prefabs")]
    public static void BuildAll()
    {
        foreach (Asset a in Assets) Build(a);
        BuildFittings();
        BuildDoor();
        BuildMirror();
        BuildWCDoor();
        BuildCirculationDoor();
        BuildArchiveDoor();
        AssetDatabase.SaveAssets();
        Debug.Log("Unclaimed: reception and bathroom prefabs rebuilt in " + Folder + " and " + WCFolder + ".");
    }

    private static void Build(Asset a)
    {
        string fbx = a.Dir + "/" + a.File + ".fbx", tex = a.Dir + "/Texturas/" + a.File;
        if (AssetImporter.GetAtPath(fbx) is ModelImporter model)
        {
            model.globalScale = 1f;
            model.materialImportMode = ModelImporterMaterialImportMode.None;
            model.importAnimation = false;
            model.importCameras = model.importLights = false;
            model.importBlendShapes = false;
            model.meshCompression = ModelImporterMeshCompression.Off;
            model.importNormals = ModelImporterNormals.Import;
            model.importTangents = ModelImporterTangents.CalculateMikk;
            model.SaveAndReimport();
        }
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(fbx);
        if (mesh == null)
        {
            Debug.LogWarning("Unclaimed: missing " + fbx + "; run the Blender gerar_tudo.py (Recepcao or WC) first.");
            return;
        }

        string matFolder = a.Dir + "/Materiais", matPath = matFolder + "/" + (a.Prefab ?? a.File) + ".mat";
        if (!AssetDatabase.IsValidFolder(matFolder)) AssetDatabase.CreateFolder(a.Dir, "Materiais");
        var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = a.Prefab ?? a.File };
            AssetDatabase.CreateAsset(material, matPath);
        }
        material.SetColor("_BaseColor", Color.white);
        material.SetTexture("_BaseMap", Tex(tex + "_BaseMap.png", a.Atlas, srgb: true));
        material.SetTexture("_MetallicGlossMap", Tex(tex + "_MetallicSmoothness.png", a.Atlas, srgb: false));
        material.SetFloat("_Metallic", 1f);
        // The entrance frame's satin aluminium faces the dark street through the glass: blur it a little more
        // so the reveal reads as metal rather than black.
        material.SetFloat("_Smoothness", a.File == "BatenteEntrada" ? 0.55f : 1f);
        material.SetFloat("_SmoothnessTextureChannel", 0f);
        material.SetTexture("_BumpMap", Tex(tex + "_Normal.png", a.Atlas, srgb: false, normal: true));
        material.SetFloat("_BumpScale", 1f);
        material.SetTexture("_OcclusionMap", Tex(tex + "_Occlusion.png", a.Atlas, srgb: false));
        material.SetFloat("_OcclusionStrength", 1f);
        BaseShaderGUI.SetMaterialKeywords(material, LitGUI.SetMaterialKeywords);
        EditorUtility.SetDirty(material);

        if (a.Prefab == null) return;
        var root = new GameObject(a.Prefab);
        try
        {
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            root.AddComponent<MeshRenderer>().sharedMaterial = material;
            foreach (Bounds b in a.Colliders ?? new[] { mesh.bounds })
            {
                var box = root.AddComponent<BoxCollider>();
                box.center = b.center;
                box.size = b.size;
            }
            if (a.File == "EquipamentosBalcao") AddScreen(root.transform, matFolder);
            Complement(a.File, root.transform, mesh);
            root.isStatic = true;   // the moving children (drawers, doors, buttons, water) stay non-static
            PrefabUtility.SaveAsPrefabAsset(root, a.Dir + "/" + a.Prefab + ".prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // Interactive parts added to a prefab after its body: counter drawers, cabinet doors, tap, flush.
    private static void Complement(string file, Transform root, Mesh body)
    {
        switch (file)
        {
            case "BalcaoRecepcao":
                for (int i = 1; i <= 3; i++)
                {
                    PecaDeMovel drawer = MovingPart(root, "Gaveta " + i, "GavetaBalcao" + i, Vector3.zero, "gaveta", null);
                    if (drawer == null) continue;
                    drawer.tipo = PecaDeMovel.TipoDePeca.Gaveta;
                    drawer.eixo = Vector3.forward;   // towards the clerk (Blender +Y)
                    drawer.curso = DrawerTravel;
                    drawer.duracao = 0.55f;
                    drawer.passoDeTeste = 0.01f;
                }
                break;
            case "ArmarioApoio":
                var cabinetDoors = new PecaDeMovel[2];
                foreach (var (name, asset, sx) in new[] { ("Porta esquerda", "PortaArmarioEsquerda", -1f), ("Porta direita", "PortaArmarioDireita", 1f) })
                {
                    var hinge = new Vector3(sx * CabinetHinge.x, 0f, CabinetHinge.z);
                    // Door panel (18 mm, 0.4485 wide from 1.5 mm off the hinge edge) and the pull, in the hinge frame.
                    var panel = new Bounds(new Vector3(-sx * 0.225f, 0.4735f, -0.009f), new Vector3(0.447f, 0.836f, 0.018f));
                    var pull = new Bounds(new Vector3(-sx * 0.415f, 0.62f, -0.032f), new Vector3(0.018f, 0.15f, 0.028f));
                    PecaDeMovel door = MovingPart(root, name, asset, hinge, "armário", new[] { panel, pull });
                    if (door == null) continue;
                    door.tipo = PecaDeMovel.TipoDePeca.Porta;
                    door.eixo = Vector3.up;
                    door.curso = -sx * CabinetDoorOpen;   // the free edge swings out to the front (-Z)
                    door.duracao = 0.7f;
                    door.passoDeTeste = 1f;
                    door.textoTrancada = "Armário trancado — cadeado";
                    door.avisoTrancada = "O cadeado prende as duas portas.";
                    cabinetDoors[sx < 0 ? 0 : 1] = door;
                }
                AddPadlockAndKey(root, cabinetDoors);
                break;
            case "Lavatorio":
                BuildTap(root, body);
                break;
            case "VasoSanitario":
                Transform flushButton = ButtonPart(root, "Botao de descarga", "VasoBotao");
                var flush = root.gameObject.AddComponent<Descarga>();
                flush.botao = flushButton;
                flush.fonte = AddAudio(root.gameObject);
                break;
        }
    }

    // A moving part as a child of the furniture: pivot object (hinge axis, or the closed pose for a drawer) with
    // the collider, a kinematic body, PecaDeMovel and an audio source; the mesh, modelled in the furniture's
    // frame, sits in a child offset back by the pivot. colliders: boxes in the pivot frame (null = mesh bounds).
    // First puzzle, on the support cabinet (Blender CADEADO, CHAVE; Unity axes = Blender x, z, y): the hasp (staple on
    // the left door, strap and its hinge leaf on the right one), the 4-dial padlock hung on it, locking both doors,
    // and the key of the restricted circulation on the shelf.
    // Blender CADEADO: origin on the shackle's plane, in front of the strap; shackle_open, lift_out.
    private static readonly Vector3 PadlockOrigin = new Vector3(-0.039f, 0.783f, -0.2355f);
    private static readonly Vector3 HaspStrapAxis = new Vector3(0.072f, 0.80f, -0.2286f);
    private const float DialPitch = 0.0068f, DialHeight = -0.034f, DialDepth = -0.006f;
    private const float ShackleOpen = 0.015f, PadlockLiftOut = 0.0125f;
    public const string CirculationKeyId = "chave_circulacao";

    // The hasp (staple on doors[0], strap on doors[1]) and the 4-dial padlock, as on the support cabinet, moved by offset
    // (root frame) so other furniture with a pair of doors meeting at x 0 can share it. Rest: where it lies once open.
    public static CadeadoCombinacao AddPadlock(Transform root, PecaDeMovel[] doors, Vector3 offset, string code,
                                               Vector3 restPosition, Quaternion restRotation)
    {
        Material steel = PuzzleMetal("Aco - fecho e arco", new Color(0.72f, 0.73f, 0.74f), 0.72f);
        Material brass = PuzzleMetal("Latao - cadeado", new Color(0.80f, 0.63f, 0.33f), 0.68f);
        Material dials = LitMaterial("Discos do cadeado", Color.white, 0.45f, false, Tex(PuzzleTextures + "Cadeado_Digitos.png", 1024, srgb: true));
        Mesh M(string f) => MeshOf(f);
        if (M("CadeadoCorpo") == null || M("FechoBase") == null) { Debug.LogWarning("Unclaimed: missing padlock meshes."); return null; }

        // Staple on the left door, hinge leaf and strap on the right door (meshes in the cabinet's frame).
        Transform left = doors[0] != null ? doors[0].transform : root, right = doors[1] != null ? doors[1].transform : root;
        Fixed("Fecho - grampo", left, M("FechoBase"), steel, root, offset);
        Fixed("Fecho - suporte da aba", right, M("FechoSuporte"), steel, root, offset);
        var strap = new GameObject("Fecho - aba").transform;
        strap.SetParent(right, false);
        strap.localPosition = HaspStrapAxis + offset - right.localPosition;
        MeshObject("Malha", strap, M("FechoAba"), steel).transform.localPosition = -HaspStrapAxis;

        // Padlock: body, shackle and dials under "Modelo" (what the inspection copies), in the padlock's frame
        // (origin at the top of the body); logic, aim collider and inspection on the "Cadeado" object.
        var padlock = new GameObject("Cadeado").transform;
        padlock.SetParent(root, false);
        padlock.localPosition = PadlockOrigin + offset;
        var model = new GameObject("Modelo").transform;
        model.SetParent(padlock, false);
        MeshObject("Corpo", model, M("CadeadoCorpo"), brass);
        Transform shackle = MeshObject("Arco", model, M("CadeadoArco"), steel).transform;
        var dialParts = new Transform[4];
        for (int i = 0; i < 4; i++)
        {
            dialParts[i] = MeshObject("Disco " + (i + 1), model, M("CadeadoDisco"), dials).transform;
            dialParts[i].localPosition = new Vector3((i - 1.5f) * DialPitch, DialHeight, DialDepth);
        }
        foreach (Renderer r in padlock.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var aim = padlock.gameObject.AddComponent<BoxCollider>();   // body and shackle, a little generous for the aim
        aim.center = new Vector3(0f, -0.012f, -0.002f);
        aim.size = new Vector3(0.05f, 0.08f, 0.03f);
        var rest = new GameObject("Cadeado - pouso aberto").transform;
        rest.SetParent(root, false);
        rest.SetLocalPositionAndRotation(restPosition, restRotation);
        // Examined with the clue inspection: the padlock's own model in front of the camera, dials manipulated there.
        var inspect = padlock.gameObject.AddComponent<ObjetoInspecionavel>();
        inspect.nome = "Cadeado de combinação";
        inspect.descricao = "";
        inspect.distanciaMaxima = 1.6f;
        inspect.modeloInspecao = model.gameObject;
        inspect.distanciaDaCamera = 0.6f;
        inspect.tamanhoNaTela = 0.42f;
        inspect.limiteDistancia = new Vector2(0.4f, 0.9f);
        inspect.posicaoNaTela = new Vector2(0f, 0f);
        inspect.orientacao = OrientacaoNaInspecao.FaceParaOJogador;
        inspect.faceVoltadaParaJogador = EixoLocal.Tras;   // dials face -Z
        inspect.topoNaTela = EixoLocal.Cima;
        inspect.travarMovimentoDoJogador = true;
        inspect.ocultarOriginal = true;
        var lockLogic = padlock.gameObject.AddComponent<CadeadoCombinacao>();
        lockLogic.combinacao = code;
        lockLogic.modelo = model;
        lockLogic.discos = dialParts;
        lockLogic.arco = shackle;
        lockLogic.cursoDoArco = new Vector3(0f, ShackleOpen, 0f);
        lockLogic.subidaParaSoltar = PadlockLiftOut;
        lockLogic.frente = Vector3.back;
        lockLogic.abaDoFecho = strap;
        lockLogic.pousoAberto = rest;
        lockLogic.portas = doors;
        lockLogic.fonte = AddAudio(padlock.gameObject);   // no clips in the project yet
        foreach (PecaDeMovel d in doors) if (d != null) d.trancada = true;
        return lockLogic;
    }


    private static void AddPadlockAndKey(Transform root, PecaDeMovel[] doors)
    {
        // Lying on the cabinet top (0.95), dials up: the body's back (4 mm behind the origin) rests on the laminate.
        if (AddPadlock(root, doors, Vector3.zero, "2317", new Vector3(-0.30f, 0.9545f, -0.10f), Quaternion.Euler(90f, 25f, 0f)) == null) return;
        Material nickel = PuzzleMetal("Niquel - chave", new Color(0.82f, 0.82f, 0.80f), 0.78f);
        Material tag = LitMaterial("Etiqueta da chave", Color.white, 0.35f, false, Tex(PuzzleTextures + "Etiqueta_Circulacao.png", 512, srgb: true));
        Mesh M(string f) => MeshOf(f);
        if (M("ChaveMetal") == null) { Debug.LogWarning("Unclaimed: missing key meshes."); return; }

        // Key of the restricted circulation on the shelf, with its ring and tag.
        var key = new GameObject("Chave da circulacao").transform;
        key.SetParent(root, false);
        MeshObject("Chave", key, M("ChaveMetal"), nickel);
        MeshObject("Etiqueta", key, M("ChaveEtiqueta"), tag);
        Bounds kb = M("ChaveMetal").bounds;
        kb.Encapsulate(M("ChaveEtiqueta").bounds);
        var keyBox = key.gameObject.AddComponent<BoxCollider>();
        keyBox.center = kb.center + new Vector3(0f, 0.008f, 0f);
        keyBox.size = kb.size + new Vector3(0.01f, 0.016f, 0.01f);
        var item = key.gameObject.AddComponent<ItemColetavel>();
        item.id = CirculationKeyId;
        item.textoAcao = "Pegar chave da circulação";
        item.aviso = "Chave da circulação guardada.";
        item.alcance = 1.6f;
    }

    // A part modelled in the furniture's frame, fixed to a moving piece (or the root): the mesh is offset back by the
    // piece's pivot so it lands where it was modelled.
    private static void Fixed(string name, Transform parent, Mesh mesh, Material material, Transform root, Vector3 offset = default)
    {
        if (mesh == null) return;
        var obj = MeshObject(name, parent, mesh, material);
        obj.transform.localPosition = (parent == root ? Vector3.zero : -parent.localPosition) + offset;
        obj.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private static Material PuzzleMetal(string name, Color color, float smoothness)
    {
        Material m = LitMaterial(name, color, smoothness, false);
        m.SetFloat("_Metallic", 1f);
        return m;
    }

    private static PecaDeMovel MovingPart(Transform root, string name, string file, Vector3 pivot, string noun, Bounds[] colliders)
    {
        Mesh mesh = MeshOf(file);
        if (mesh == null) { Debug.LogWarning("Unclaimed: missing " + file + " mesh."); return null; }
        var part = new GameObject(name).transform;
        part.SetParent(root, false);
        part.localPosition = pivot;
        MeshObject("Malha", part, mesh, BakedMaterial(file)).transform.localPosition = -pivot;
        foreach (Bounds b in colliders ?? new[] { new Bounds(mesh.bounds.center - pivot, mesh.bounds.size) })
        {
            var box = part.gameObject.AddComponent<BoxCollider>();
            box.center = b.center;
            box.size = b.size;
        }
        var body = part.gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        var piece = part.gameObject.AddComponent<PecaDeMovel>();
        piece.textoAbrir = "Abrir " + noun;
        piece.textoFechar = "Fechar " + noun;
        piece.movel = root;
        piece.alcance = 1.8f;
        piece.fonte = AddAudio(part.gameObject);   // no clips in the project yet
        return piece;
    }

    private static AudioSource AddAudio(GameObject obj)
    {
        var audio = obj.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 1f;
        audio.minDistance = 0.5f;
        audio.maxDistance = 10f;
        return audio;
    }

    private static Material WCChrome()
    {
        if (!AssetDatabase.IsValidFolder(WCFolder + "/Materiais")) AssetDatabase.CreateFolder(WCFolder, "Materiais");
        Material chrome = LitMaterial("Cromado - presilhas", new Color(0.86f, 0.86f, 0.85f), 0.9f, false, dir: WCFolder);
        chrome.SetFloat("_Metallic", 1f);
        return chrome;
    }

    // Pressable chrome button (unbaked mesh in its fitting's frame), as a child at the fitting's origin.
    private static Transform ButtonPart(Transform root, string name, string file)
    {
        Mesh mesh = MeshOf(file, WCFolder);
        if (mesh == null) { Debug.LogWarning("Unclaimed: missing " + file + " mesh."); return null; }
        var button = MeshObject(name, root, mesh, WCChrome());
        button.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return button.transform;
    }

    // Tap: the button, Torneira, and the water: a stream mesh (filled at runtime along a trajectory found here by
    // dropping the water from the spout onto the basin mesh), a foam ring and splash particles at the impact.
    private static void BuildTap(Transform root, Mesh basin)
    {
        const string tex = "Assets/UnclaimedPrototype/WC/Texturas/";
        Transform button = ButtonPart(root, "Botao da torneira", "LavatorioBotao");

        var probe = new GameObject("__cuba");
        probe.transform.SetParent(root, false);
        var surface = probe.AddComponent<MeshCollider>();
        surface.sharedMesh = basin;
        Physics.SyncTransforms();
        var path = new System.Collections.Generic.List<Vector3>();
        Vector3 p = SpoutTip + SpoutDir.normalized * 0.002f, v = SpoutDir.normalized * 0.3f, g = Physics.gravity;
        Vector3 hitPoint = p, hitNormal = Vector3.up;
        path.Add(p);
        const float dt = 0.004f;
        for (int i = 0; i < 400; i++)
        {
            Vector3 next = p + v * dt + 0.5f * g * dt * dt;
            v += g * dt;
            Vector3 step = next - p;
            if (surface.Raycast(new Ray(root.TransformPoint(p), step.normalized), out RaycastHit hit, step.magnitude))
            {
                hitPoint = root.InverseTransformPoint(hit.point);
                hitNormal = root.InverseTransformDirection(hit.normal);
                path.Add(hitPoint);
                break;
            }
            p = next;
            if ((i & 3) == 3) path.Add(p);
        }
        Object.DestroyImmediate(probe);
        if (path.Count < 2 || hitPoint == path[0]) Debug.LogWarning("Unclaimed: the tap's water did not reach the basin.");

        Material water = LitMaterial("Agua da torneira", new Color(0.82f, 0.9f, 0.95f, 0.75f), 0.95f, true,
            Tex(tex + "Agua_Fluxo.png", 256, srgb: true), dir: WCFolder);
        var stream = new GameObject("Agua");
        stream.transform.SetParent(root, false);
        var filter = stream.AddComponent<MeshFilter>();
        var streamRenderer = stream.AddComponent<MeshRenderer>();
        streamRenderer.sharedMaterial = water;
        streamRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        streamRenderer.receiveShadows = false;

        Quaternion facing = Quaternion.FromToRotation(Vector3.up, hitNormal);
        Material foamMat = LitMaterial("Espuma da torneira", new Color(1f, 1f, 1f, 0.4f), 0.6f, true,
            Tex(tex + "Agua_Espuma.png", 256, srgb: true), dir: WCFolder);
        var foam = new GameObject("Espuma");
        foam.transform.SetParent(root, false);
        foam.transform.SetLocalPositionAndRotation(hitPoint + hitNormal * 0.0015f, facing);
        const float half = 0.028f;
        var quad = new Mesh
        {
            name = "Espuma",
            vertices = new[] { new Vector3(-half, 0, -half), new Vector3(-half, 0, half), new Vector3(half, 0, half), new Vector3(half, 0, -half) },
            uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) },
            normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up },
            triangles = new[] { 0, 1, 2, 0, 2, 3 },
        };
        quad.RecalculateBounds();
        quad.RecalculateTangents();
        AssetDatabase.DeleteAsset(WCFolder + "/Espuma.asset");
        AssetDatabase.CreateAsset(quad, WCFolder + "/Espuma.asset");
        foam.AddComponent<MeshFilter>().sharedMesh = quad;
        var foamRenderer = foam.AddComponent<MeshRenderer>();
        foamRenderer.sharedMaterial = foamMat;
        foamRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var splash = new GameObject("Respingos");
        splash.transform.SetParent(root, false);
        splash.transform.SetLocalPositionAndRotation(hitPoint + hitNormal * 0.002f, Quaternion.LookRotation(hitNormal));
        var plane = new GameObject("Plano da cuba").transform;   // particles bounce on the basin's surface
        plane.SetParent(root, false);
        plane.SetLocalPositionAndRotation(hitPoint, facing);
        var ps = splash.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.0018f, 0.004f);
        main.startColor = new Color(0.92f, 0.96f, 1f, 0.7f);
        main.gravityModifier = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 120;
        var emission = ps.emission;
        emission.rateOverTime = 90f;
        emission.enabled = false;   // Torneira turns it on while the water reaches the basin
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 55f;
        shape.radius = 0.003f;
        var collision = ps.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.Planes;
        collision.SetPlane(0, plane);
        collision.bounce = 0.15f;
        collision.dampen = 0.6f;
        collision.lifetimeLoss = 0.5f;
        var psRenderer = splash.GetComponent<ParticleSystemRenderer>();
        psRenderer.sharedMaterial = DropletMaterial(tex + "Gota.png");
        psRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var tap = root.gameObject.AddComponent<Torneira>();
        tap.botao = button;
        tap.trajetoria = path.ToArray();
        tap.fluxo = filter;
        tap.respingos = ps;
        tap.espuma = foamRenderer;
        tap.fonte = AddAudio(root.gameObject);
    }

    private static Material DropletMaterial(string texture)
    {
        string path = WCFolder + "/Materiais/Respingo.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "Respingo" };
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetTexture("_BaseMap", Tex(texture, 32, srgb: true));
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
        return m;
    }

    // Unlit quad over the baked (dark) screen so the monitor reads as switched on.
    private static void AddScreen(Transform root, string matFolder)
    {
        string path = matFolder + "/Tela do sistema.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Tela do sistema" };
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetTexture("_BaseMap", Tex(ScreenTexture, 2048, srgb: true));
        material.SetColor("_BaseColor", new Color(0.8f, 0.8f, 0.8f));
        EditorUtility.SetDirty(material);
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        quad.name = "Tela ligada";
        quad.transform.SetParent(root, false);
        // Unity's quad faces -Z, so its -Z axis is turned to the screen's normal.
        quad.transform.localPosition = ScreenCenter + ScreenNormal * 0.0012f;
        quad.transform.localRotation = Quaternion.LookRotation(-ScreenNormal, Vector3.up);
        quad.transform.localScale = new Vector3(ScreenSize.x, ScreenSize.y, 1f);
        var renderer = quad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // The computer: aiming at the monitor opens the station's interface (ComputadorRecepcao).
        var computer = new GameObject("Computador").transform;
        computer.SetParent(root, false);
        computer.localPosition = ScreenCenter - ScreenNormal * 0.02f;
        computer.localRotation = Quaternion.LookRotation(-ScreenNormal, Vector3.up);
        var box = computer.gameObject.AddComponent<BoxCollider>();
        box.size = new Vector3(ScreenSize.x + 0.03f, ScreenSize.y + 0.03f, 0.05f);
        var pc = computer.gameObject.AddComponent<ComputadorRecepcao>();
        pc.conteudo = AssetDatabase.LoadAssetAtPath<TextAsset>(ComputerFolder + "EstacaoRecepcao.json");
        // Interface (Blender/Recepcao/scripts/estacao_interface.py): wallpaper, icons and glyphs, found by name;
        // plus the station's pictures (attachments) in Imagens/.
        pc.papelDeParede = Tex(ComputerFolder + "Interface/papel_de_parede.png", 2048, srgb: true);
        var pictures = new System.Collections.Generic.List<Texture2D>();
        foreach (string folder in new[] { ComputerFolder + "Interface", ComputerFolder + "Imagens" })
        {
            if (!AssetDatabase.IsValidFolder(folder)) continue;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string file = AssetDatabase.GUIDToAssetPath(guid);
                if (file.EndsWith("papel_de_parede.png")) continue;
                pictures.Add(UiTexture(file));
            }
        }
        pc.imagens = pictures.ToArray();
        pc.telaDoMonitor = renderer;
        pc.aspectoDoMonitor = ScreenSize.x / ScreenSize.y;
        pc.fonte = AssetDatabase.LoadAssetAtPath<Font>(ComputerFolder + "Fontes/NotoSans-Regular.ttf");
        pc.fonteNegrito = AssetDatabase.LoadAssetAtPath<Font>(ComputerFolder + "Fontes/NotoSans-Bold.ttf");
        pc.fonteMono = AssetDatabase.LoadAssetAtPath<Font>(ComputerFolder + "Fontes/NotoSansMono-Regular.ttf");
    }

    // Interface textures: sRGB, no mipmaps, clamped and uncompressed so icons and text in pictures stay crisp.
    private static Texture2D UiTexture(string path)
    {
        if (AssetImporter.GetAtPath(path) is TextureImporter ti)
        {
            bool changed = ti.mipmapEnabled || ti.wrapMode != TextureWrapMode.Clamp || ti.textureCompression != TextureImporterCompression.Uncompressed
                           || !ti.alphaIsTransparency || ti.npotScale != TextureImporterNPOTScale.None;
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.mipmapEnabled = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.alphaIsTransparency = true;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.maxTextureSize = 2048;
            if (changed) ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Material BakedMaterial(string file, string dir = Folder) =>
        AssetDatabase.LoadAssetAtPath<Material>(dir + "/Materiais/" + file + ".mat");

    private static Mesh MeshOf(string file, string dir = Folder) => AssetDatabase.LoadAssetAtPath<Mesh>(dir + "/" + file + ".fbx");

    private static GameObject MeshObject(string name, Transform parent, Mesh mesh, params Material[] materials)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<MeshFilter>().sharedMesh = mesh;
        obj.AddComponent<MeshRenderer>().sharedMaterials = materials;
        return obj;
    }

    // URP Lit material created or updated in Materiais; transparent ones keep specular highlights and
    // reflections (glass, acrylic).
    private static Material LitMaterial(string name, Color color, float smoothness, bool transparent,
        Texture2D baseMap = null, Texture2D normalMap = null, Texture2D glossMap = null, Color? emission = null, Vector2? tiling = null,
        string dir = Folder)
    {
        string path = dir + "/Materiais/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", color);
        m.SetTexture("_BaseMap", baseMap);
        m.SetTextureScale("_BaseMap", tiling ?? Vector2.one);
        m.SetTexture("_BumpMap", normalMap);
        m.SetFloat("_BumpScale", 1f);
        m.SetTexture("_MetallicGlossMap", glossMap);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Surface", transparent ? 1f : 0f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_BlendModePreserveSpecular", 1f);
        m.SetColor("_EmissionColor", emission ?? Color.black);
        m.globalIlluminationFlags = emission.HasValue ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        BaseShaderGUI.SetMaterialKeywords(m, LitGUI.SetMaterialKeywords);
        if (emission.HasValue) m.EnableKeyword("_EMISSION"); else m.DisableKeyword("_EMISSION");
        EditorUtility.SetDirty(m);
        return m;
    }

    // Ceiling fitting and entrance lamp: baked body on the root, light parts in a child whose materials
    // the map generator sets per fitting state. The lights themselves stay in the generator.
    private static void BuildFittings()
    {
        var prism = Tex(Textures + "Difusor_Prismatico_Normal.png", 128, srgb: false, normal: true);
        // Milky prismatic acrylic: lets the tubes show as brighter bands and glows a little itself.
        var tiling = new Vector2(38f, 7.5f);   // 32 mm tile over the 1.25 x 0.24 m diffuser
        LitMaterial(DiffuserLit, new Color(0.94f, 0.95f, 0.95f, 0.8f), 0.55f, true, null, prism, null, new Color(0.62f, 0.61f, 0.58f), tiling);
        LitMaterial(DiffuserOff, new Color(0.86f, 0.87f, 0.87f, 0.85f), 0.55f, true, null, prism, null, null, tiling);
        foreach (var (prefab, body, light) in new[] { (FittingPrefab, "LuminariaSobrepor", "LuminariaSobrepor_Luz"),
                                                    (WallLampPrefab, "LuminariaExterna", "LuminariaExterna_Luz") })
        {
            Mesh bodyMesh = MeshOf(body), lightMesh = MeshOf(light);
            if (bodyMesh == null || lightMesh == null) { Debug.LogWarning("Unclaimed: missing " + body + " meshes."); continue; }
            var root = new GameObject(prefab);
            try
            {
                root.AddComponent<MeshFilter>().sharedMesh = bodyMesh;
                root.AddComponent<MeshRenderer>().sharedMaterial = BakedMaterial(body);
                MeshObject(LightPartName, root.transform, lightMesh, LightMaterials(0));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(prefab));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }

    // Glass door: fixed parts on the root; each leaf is a pivot transform on its pivot axis carrying the
    // fittings, the glass and its collider (the right leaf mirrors the left one inside a child, so the
    // collider never has a negative scale). The prefab is saved closed; FolhaDePorta applies the initial
    // state (open, on the floor stops) on Awake and the generator applies it in the Editor.
    private static void BuildDoor()
    {
        Mesh fixedMesh = MeshOf("PortaVidroFixos"), leafMesh = MeshOf("PortaVidroFolha"), glassMesh = MeshOf("PortaVidroVidro");
        if (fixedMesh == null || leafMesh == null || glassMesh == null) { Debug.LogWarning("Unclaimed: missing door meshes."); return; }
        Material glass = LitMaterial("Vidro temperado", Color.white, 1f, true,
            Tex(Textures + "Vidro_Porta_Base.png", 1024, srgb: true), null, Tex(Textures + "Vidro_Porta_MetallicSmoothness.png", 1024, srgb: false));
        var root = new GameObject(DoorPrefab);
        try
        {
            root.AddComponent<MeshFilter>().sharedMesh = fixedMesh;
            root.AddComponent<MeshRenderer>().sharedMaterial = BakedMaterial("PortaVidroFixos");
            foreach (var (name, side) in new[] { ("Folha esquerda", 1f), ("Folha direita", -1f) })
            {
                var leaf = new GameObject(name).transform;
                leaf.SetParent(root.transform, false);
                leaf.localPosition = new Vector3(side * DoorPivotX, 0f, 0f);
                var box = leaf.gameObject.AddComponent<BoxCollider>();
                box.center = new Vector3(side * LeafCenter.x, LeafCenter.y, 0f);
                box.size = LeafSize;
                // Moving leaf: kinematic body, opens outwards (left +, right -) up to the floor stops.
                var body = leaf.gameObject.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                var audio = leaf.gameObject.AddComponent<AudioSource>();
                audio.playOnAwake = false;
                audio.spatialBlend = 1f;
                audio.minDistance = 1f;
                audio.maxDistance = 15f;
                var door = leaf.gameObject.AddComponent<FolhaDePorta>();
                door.sentido = side > 0 ? 1 : -1;
                door.anguloMaximo = DoorOpenAngle;
                door.duracao = 1f;
                door.alcance = 2f;
                door.iniciarAberta = true;
                door.aberturaInicial = 1f;
                door.fonte = audio;   // clips: none in the project yet
                door.textoAbrir = "Abrir porta";
                door.textoFechar = "Fechar porta";
                Transform geometry = leaf;
                if (side < 0)
                {
                    geometry = new GameObject("Espelhada").transform;
                    geometry.SetParent(leaf, false);
                    geometry.localScale = new Vector3(-1f, 1f, 1f);
                }
                MeshObject("Ferragens", geometry, leafMesh, BakedMaterial("PortaVidroFolha"));
                var pane = MeshObject("Vidro", geometry, glassMesh, glass);
                pane.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(DoorPrefab));
        }
        finally { Object.DestroyImmediate(root); }
    }

    // Bathroom mirror (not baked): reflective face lit by the bathroom's box-projected reflection probe,
    // polished glass edge, chrome clips.
    private static void BuildMirror()
    {
        Mesh mesh = MeshOf("Espelho", WCFolder);
        if (mesh == null) { Debug.LogWarning("Unclaimed: missing Espelho mesh."); return; }
        const string tex = "Assets/UnclaimedPrototype/WC/Texturas/";
        if (!AssetDatabase.IsValidFolder(WCFolder + "/Materiais")) AssetDatabase.CreateFolder(WCFolder, "Materiais");
        Material edge = LitMaterial("Borda de vidro", new Color(0.55f, 0.68f, 0.64f), 0.85f, false, dir: WCFolder);
        Material chrome = LitMaterial("Cromado - presilhas", new Color(0.86f, 0.86f, 0.85f), 0.9f, false, dir: WCFolder);
        chrome.SetFloat("_Metallic", 1f);
        // Planar reflection (EspelhoPlanar + shader "Unclaimed/Espelho planar"), with the wipe marks of the old
        // gloss map as smudges; the room's probe stands in when the reflection camera is off.
        Material mirror = LitMaterial("Espelho", new Color(0.9f, 0.92f, 0.92f), 1f, false, dir: WCFolder);
        mirror.shader = Shader.Find("Unclaimed/Espelho planar");
        mirror.SetColor("_BaseColor", new Color(0.9f, 0.92f, 0.92f));
        mirror.SetTexture("_ManchasTex", Tex(tex + "WC_Espelho_MS.png", 512, srgb: false));
        mirror.SetFloat("_Manchas", 0.6f);
        EditorUtility.SetDirty(mirror);
        var root = new GameObject(MirrorPrefab);
        try
        {
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            // The FBX does not keep the slot order: tell the parts apart by thickness (flat reflective face,
            // 4 mm glass, 9 mm clips).
            var materials = new Material[mesh.subMeshCount];
            int face = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                float t = mesh.GetSubMesh(i).bounds.size.z;
                materials[i] = t < 0.001f ? mirror : t < 0.006f ? edge : chrome;
                if (t < 0.001f) face = i;
            }
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            var planar = root.AddComponent<EspelhoPlanar>();
            planar.espelho = renderer;
            planar.submalha = face;
            planar.pontoLocal = mesh.GetSubMesh(face).bounds.center;
            planar.normalLocal = Vector3.back;   // the face looks out of the wall (prefab front, -Z)
            PrefabUtility.SaveAsPrefabAsset(root, WCPrefabPath(MirrorPrefab));
        }
        finally { Object.DestroyImmediate(root); }
    }

    // Bathroom door: painted frame (colliders on the jambs) and the leaf on its hinge axis with the
    // approved door mechanic (FolhaDePorta): opens into the bathroom up to 95 degrees, starts open.
    private static void BuildWCDoor()
    {
        Mesh frame = MeshOf("PortaWCFixos", WCFolder), leafMesh = MeshOf("PortaWCFolha", WCFolder);
        if (frame == null || leafMesh == null) { Debug.LogWarning("Unclaimed: missing bathroom door meshes."); return; }
        var root = new GameObject(WCDoorPrefab);
        try
        {
            root.AddComponent<MeshFilter>().sharedMesh = frame;
            root.AddComponent<MeshRenderer>().sharedMaterial = BakedMaterial("PortaWCFixos", WCFolder);
            foreach (float sx in new[] { -1f, 1f })
            {
                var jamb = root.AddComponent<BoxCollider>();
                jamb.center = new Vector3(sx * 0.485f, 1.1f, 0f);
                jamb.size = new Vector3(0.03f, 2.2f, 0.21f);
            }
            var leaf = new GameObject("Folha").transform;
            leaf.SetParent(root.transform, false);
            leaf.localPosition = WCPivot;
            MeshObject("Malha", leaf, leafMesh, BakedMaterial("PortaWCFolha", WCFolder));
            var box = leaf.gameObject.AddComponent<BoxCollider>();
            box.center = WCLeafCenter;
            box.size = WCLeafSize;
            var body = leaf.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var audio = leaf.gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            audio.minDistance = 1f;
            audio.maxDistance = 12f;
            var door = leaf.gameObject.AddComponent<FolhaDePorta>();
            door.sentido = 1;          // leaf along -X turns towards +Z (into the bathroom)
            door.anguloMaximo = 95f;   // the floor stop sits at 97.5 degrees
            door.duracao = 1.1f;
            door.alcance = 2f;
            door.iniciarAberta = true;
            door.aberturaInicial = 1f;
            door.fonte = audio;        // no clips in the project yet
            door.textoAbrir = "Abrir porta";
            door.textoFechar = "Fechar porta";
            PrefabUtility.SaveAsPrefabAsset(root, WCPrefabPath(WCDoorPrefab));
        }
        finally { Object.DestroyImmediate(root); }
    }

    // Restricted circulation door (Blender PORTA_CIRC, Unity axes): pressed-steel frame with a rebate and the
    // secondary leaf (colliders on the jambs, split at the rebate so none overlaps the leaf, and on the secondary
    // leaf), and the active leaf on its hinge axis just outside the corridor face, locked until the player has the
    // circulation key. The leaves are flush with the frame on the corridor (swing) side.
    public const string CirculationDoorPrefab = "Porta da circulacao";
    private static readonly Vector3 CircPivot = new Vector3(-0.7185f, 0f, 0.1215f);
    private static readonly Vector3 CircLeafCenter = new Vector3(0.4315f, 1.0895f, -0.0265f), CircLeafSize = new Vector3(0.86f, 2.155f, 0.04f);

    private static void BuildCirculationDoor()
    {
        Mesh frame = MeshOf("PortaCirculacaoFixos"), leafMesh = MeshOf("PortaCirculacaoFolha");
        if (frame == null || leafMesh == null) { Debug.LogWarning("Unclaimed: missing circulation door meshes."); return; }
        var root = new GameObject(CirculationDoorPrefab);
        try
        {
            root.AddComponent<MeshFilter>().sharedMesh = frame;
            root.AddComponent<MeshRenderer>().sharedMaterial = BakedMaterial("PortaCirculacaoFixos");
            foreach (float sx in new[] { -1f, 1f })
            {
                var stop = root.AddComponent<BoxCollider>();      // stop part of the jamb (reception side of the leaves)
                stop.center = new Vector3(sx * 0.7275f, 1.1f, -0.02f);
                stop.size = new Vector3(0.045f, 2.2f, 0.19f);
                var rebate = root.AddComponent<BoxCollider>();    // rebate part, beside the leaf edge
                rebate.center = new Vector3(sx * 0.735f, 1.1f, 0.095f);
                rebate.size = new Vector3(0.03f, 2.2f, 0.04f);
            }
            var fixedLeaf = root.AddComponent<BoxCollider>();   // secondary leaf, held by surface bolts: part of the frame
            fixedLeaf.center = new Vector3(0.432f, 1.0895f, 0.095f);
            fixedLeaf.size = new Vector3(0.57f, 2.155f, 0.04f);
            var leaf = new GameObject("Folha").transform;
            leaf.SetParent(root.transform, false);
            leaf.localPosition = CircPivot;
            MeshObject("Malha", leaf, leafMesh, BakedMaterial("PortaCirculacaoFolha"));
            var box = leaf.gameObject.AddComponent<BoxCollider>();
            box.center = CircLeafCenter;
            box.size = CircLeafSize;
            var body = leaf.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var door = leaf.gameObject.AddComponent<FolhaDePorta>();
            door.sentido = -1;         // leaf along +X turns towards +Z (into the corridor)
            door.anguloMaximo = 95f;   // the floor stop sits at 97.5 degrees
            door.duracao = 1.1f;
            door.alcance = 2f;
            door.iniciarAberta = false;
            door.fonte = AddAudio(leaf.gameObject);   // no clips in the project yet
            door.textoAbrir = "Abrir porta";
            door.textoFechar = "Fechar porta";
            door.trancada = true;
            door.chaveNecessaria = CirculationKeyId;
            door.textoTrancada = "Porta trancada — chave da circulação";
            door.textoDestrancar = "Destrancar com a chave da circulação";
            door.avisoDestrancada = "Porta destrancada.";
            door.avisoSemChave = "Trancada. Precisa da chave da circulação.";
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(CirculationDoorPrefab));
        }
        finally { Object.DestroyImmediate(root); }
    }

    public const string ArchiveDoorPrefab = "Porta do arquivo";

    private static void BuildArchiveDoor()
    {
        Mesh frame = MeshOf("PortaArquivoFixos"), leafMesh = MeshOf("PortaArquivoFolha"), boltMesh = MeshOf("PortaArquivoTrinco");
        if (frame == null || leafMesh == null || boltMesh == null) { Debug.LogWarning("Unclaimed: missing archive door meshes."); return; }
        var root = new GameObject(ArchiveDoorPrefab);
        try
        {
            root.AddComponent<MeshFilter>().sharedMesh = frame;
            root.AddComponent<MeshRenderer>().sharedMaterial = BakedMaterial("PortaArquivoFixos");
            foreach (float sx in new[] { -1f, 1f })
            {
                var stop = root.AddComponent<BoxCollider>();
                stop.center = new Vector3(sx * 0.7275f, 1.1f, -0.02f);
                stop.size = new Vector3(0.045f, 2.2f, 0.19f);
                var rebate = root.AddComponent<BoxCollider>();
                rebate.center = new Vector3(sx * 0.735f, 1.1f, 0.095f);
                rebate.size = new Vector3(0.03f, 2.2f, 0.04f);
            }
            var fixedLeaf = root.AddComponent<BoxCollider>();
            fixedLeaf.center = new Vector3(0.432f, 1.0895f, 0.095f);
            fixedLeaf.size = new Vector3(0.57f, 2.155f, 0.04f);
            var leaf = new GameObject("Folha").transform;
            leaf.SetParent(root.transform, false);
            leaf.localPosition = CircPivot;
            MeshObject("Malha", leaf, leafMesh, BakedMaterial("PortaArquivoFolha"));
            var box = leaf.gameObject.AddComponent<BoxCollider>();
            box.center = CircLeafCenter;
            box.size = CircLeafSize;
            var body = leaf.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var door = leaf.gameObject.AddComponent<FolhaDePorta>();
            door.sentido = -1;         // local +Z points into the archive with yaw +90
            door.anguloMaximo = 95f;
            door.duracao = 1.1f;
            door.alcance = 2f;
            door.iniciarAberta = false;
            door.fonte = AddAudio(leaf.gameObject);
            door.textoAbrir = "Abrir porta";
            door.textoFechar = "Fechar porta";
            door.trancada = true;
            door.chaveNecessaria = "";
            door.textoTrancada = "Porta trancada";
            door.avisoSemChave = "Está trancada. Parece haver um ferrolho do outro lado.";

            // Bolt meshes share the leaf's hinge frame.
            var bolt = new GameObject("Ferrolho");
            bolt.transform.SetParent(leaf, false);
            var boltBox = bolt.AddComponent<BoxCollider>();
            boltBox.center = new Vector3(0.7715f, 1.45f, 0.019f);
            boltBox.size = new Vector3(0.20f, 0.08f, 0.06f);
            var bar = MeshObject("Trinco", bolt.transform, boltMesh, BakedMaterial("PortaArquivoTrinco"));
            var latch = bolt.AddComponent<FerrolhoDePorta>();
            latch.porta = door;
            latch.trinco = bar.transform;
            latch.deslocamento = new Vector3(-0.05f, 0f, 0f);
            latch.alcance = 2f;
            latch.fonte = AddAudio(bolt);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(ArchiveDoorPrefab));
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static Texture2D Tex(string path, int size, bool srgb, bool normal = false)
    {
        if (AssetImporter.GetAtPath(path) is TextureImporter importer)
        {
            var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (importer.textureType != type || importer.sRGBTexture != srgb || importer.maxTextureSize != size)
            {
                importer.textureType = type;
                importer.sRGBTexture = srgb;
                importer.maxTextureSize = size;
                importer.SaveAndReimport();
            }
        }
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null) Debug.LogWarning("Unclaimed: missing texture " + path);
        return texture;
    }
}
