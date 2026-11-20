// Archive / office finish, baked furniture and narrative papers. The room's approved envelope is unchanged.
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class UnclaimedMorgueGenerator
{
    private const string AdministracaoFolder = BaseFolder + "/Administracao";
    // Clear the structural half-wall, finish, skirting and a further centimetre.
    private const float AdminInset = WallThickness / 2 + FinishOffset + SkirtingThickness + .01f;
    private const float AdminEast = 3.5f - AdminInset;
    private const float AdminSouth = -10.25f + AdminInset;
    private const float AdminNorth = -3.5f - AdminInset;

    private static ZoneFinish ArquivoFinish()
    {
        var f = RecepcaoFinish();
        Color paint = new Color(.66f, .69f, .64f);
        f.Wainscot = Mat(AdministracaoFolder, "Barrado tinta lavavel", paint, .42f, 0,
            RecepcaoTextures + "Recepcao_Pintura_Base.png", RecepcaoTextures + "Recepcao_Pintura_Normal.png", 2f,
            glossMap: RecepcaoTextures + "Recepcao_Pintura_MS.png");
        f.Friso = Mat(AdministracaoFolder, "Friso do barrado", paint * .86f, .5f);
        return f;
    }

    private static bool HasAdminPrefab(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(UnclaimedAdministracaoAssets.PrefabPath(name)) != null;

    private static Transform AdminProp(Transform parent, string prefab, string name, Vector3 pos, float yaw, bool reception = false)
    {
        if (parent == null) return null;
        string path = reception ? UnclaimedRecepcaoAssets.PrefabPath(prefab) : UnclaimedAdministracaoAssets.PrefabPath(prefab);
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) { Debug.LogWarning("Unclaimed: missing " + path); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.name = name; go.transform.localPosition = pos; go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        return go.transform;
    }

    private static void BuildArquivo(Transform room)
    {
        var previous = room.Find("Mobiliario");
        if (previous != null) Undo.DestroyObjectImmediate(previous.gameObject);
        Transform g = Sub(room, "Mobiliario");
        AdminProp(g, "Mesa do servidor", "Mesa do servidor", new Vector3(-2.625f, 0, -8f), 0);
        if (HasAdminPrefab("Mesa do servidor")) AdminProp(g, "Cadeira do atendente", "Cadeira do atendente", new Vector3(-2.72f, 0, -7.05f), 8, true);
        AdminProp(g, "Lixeira", "Lixeira", new Vector3(-1.45f, 0, -8.3f), 0, true);
        AdminProp(g, "Mesa de apoio", "Mesa de apoio", new Vector3(-5.55f, 0, AdminSouth + .252f), 180);
        AdminProp(g, "Impressora", "Impressora", new Vector3(-5.72f, .74f, AdminSouth + .252f), 177);
        AdminProp(g, "Pastas de manuais", "Pastas de manuais", new Vector3(-5.27f, .74f, AdminSouth + .252f), 180);
        AdminProp(g, "Armario de pertences", "Armario de pertences", new Vector3(AdminEast - .25f, 0, -9.40f), 90);
        AdminProp(g, "Estante de aco", "Estante de aco", new Vector3(AdminEast - .20f, 0, -8.05f), 90);
        AdminProp(g, "Estante da coordenacao", "Estante da coordenacao", new Vector3(AdminEast - .20f, 0, -7.07f), 90);
        for (int i = 1; i <= 3; i++)
        {
            var cabinet = AdminProp(g, "Arquivo de aco", "Arquivo " + i, new Vector3(AdminEast - .31f, 0, -6.10f + .50f * i), 90);
            if (cabinet != null) UnclaimedAdministracaoAssets.ApplyLabels(cabinet, "Arquivo de aco", i.ToString());
        }
        AdminProp(g.Find("Arquivo 2"), "Pasta do contrato", "Pasta do contrato", new Vector3(0, 1.33f, 0), 12);
        Transform benchProp = AdminProp(g, "Bancada", "Bancada", new Vector3(.55f, 0, AdminNorth - .30f), 0);
        // Two boxes waiting on the free end of the bench, away from the charger cable.
        AdminProp(benchProp, "Caixa arquivo", "Caixa na bancada 1", new Vector3(.62f, .90f, .02f), 3);
        AdminProp(benchProp, "Caixa arquivo", "Caixa na bancada 2", new Vector3(.79f, .90f, .04f), -5);
        // The floor box stays south of the shelving, away from the locker door's swing.
        AdminProp(g, "Caixa arquivo", "Caixa arquivo no chao", new Vector3(2.86f, 0, -8.70f), 86);
        AdminProp(g.Find("Armario de pertences"), "Caixa arquivo", "Caixa sobre o armario", new Vector3(.05f, 1.85f, .01f), -7);
        PlaceArquivoDocuments(room);
    }

    // Idempotent placement: each paper / prop has an owned name under its supporting furniture or moving part.
    // Local positions are in the closed pose, so papers continue to follow drawers and doors during play.
    public static void PlaceArquivoDocuments(Transform room)
    {
        Transform g = room.Find("Mobiliario");
        if (g == null) { Debug.LogWarning("Unclaimed: administration furniture not found; generate the room first."); return; }
        foreach (var t in g.GetComponentsInChildren<Transform>(true))
            if (t != null && (t.name.StartsWith("Documento admin - ") || t.name.StartsWith("Pertence admin - ") || t.name.StartsWith("Pilha - ")))
                Undo.DestroyObjectImmediate(t.gameObject);
        Transform desk = g.Find("Mesa do servidor"), manuals = g.Find("Pastas de manuais"), coord = g.Find("Estante da coordenacao"), bench = g.Find("Bancada");
        // Upper letter tray over the pedestal (centre x .60, z -.12): modelled dummy paper tops at .847 m, all three
        // readable sheets sit above it. Fanned 45 mm apart towards the back (-Z), so each form keeps a strip the aim can reach.
        AdminPaper(desk, "ficha_081", new Vector3(.597f, .848f, -.165f), 90, 177);
        AdminPaper(desk, "ficha_082", new Vector3(.605f, .853f, -.12f), 90, 182);
        AdminPaper(desk, "d01_copia_arquivo", new Vector3(.60f, .858f, -.075f), 90, 180);
        // Crossword in front of the keyboard's right hand side, Nina's list in front of the tray.
        AdminPaper(desk, "palavras_cruzadas", new Vector3(.22f, .761f, .19f), 90, 173, reception: true);
        AdminPaper(desk, "pendencias_nina", new Vector3(.58f, .761f, .20f), 90, 184);
        AdminPaper(g.Find("Impressora"), "bilhete_impressora", new Vector3(.015f, .336f, .02f), 90, -4);
        // Grey manual k=2 starts at x=-.018, ends at -.002; its top is .274. Exposed paper tops = .304.
        // Printed faces look along -X, parallel to the covers; the exposed 30 mm strips remain aimable.
        AdminPaper(manuals, "h03_comprovante", new Vector3(-.012f, .229f, -.015f), 0, 90);
        // Irene's note sticks out past the receipt's back edge and 25 mm above it.
        AdminPaper(manuals, "h06_recado_irene", new Vector3(-.007f, .224f, .030f), 0, 90);
        // Limiar's 041 transport receipt between them: the old case's papers stay together in the grey folder.
        AdminPaper(manuals, "h08_recibo_041", new Vector3(-.0095f, .2265f, .008f), 0, 90);
        Transform middle = g.Find("Arquivo 2");
        // Loose on top of the hanging folders (their tabs end at .31), clear of drawer 3's bottom (.335) when closed,
        // at the front half so it is clear of the cabinet when the drawer is open.
        AdminPaper(middle != null ? middle.Find("Gaveta 4") : null, "h02_pedido_irene", new Vector3(-.01f, .318f, .0f), 90, 4);
        string[] stack = { "proposta_transferencia", "desculpas_riscadas", "h04_relatorio", "h07_autorizacao", "ordem_conserto" };
        for (int i = 0; i < stack.Length; i++)
            // Stepped 37.5 mm towards the back: each lower sheet shows a front strip; the repair order lies on top, at the back.
            AdminPaper(coord, stack[i], new Vector3(-.27f + (i % 2) * .006f, .856f + i * .005f, -.085f + i * .0375f), 90, 90 + (i - 2) * 1.2f);
        Transform contract = middle != null ? middle.Find("Pasta do contrato") : null;
        AdminPaper(contract, "d11_cobranca_784", new Vector3(-.045f, .023f, -.035f), 90, -6);
        AdminPaper(contract, "d12_pessoas_contrato", new Vector3(.05f, .029f, -.005f), 90, 5);
        // Leo's own findings on top of the contract papers: the printed messages, the warehouse photo, João's note on it.
        AdminPaper(contract, "d15_mensagens", new Vector3(-.02f, .035f, .0f), 90, 3);
        AdminPaper(contract, "d12_foto_galpao", new Vector3(.035f, .040f, -.035f), 90, -8);
        AdminPaper(contract, "d12_bilhete_joao", new Vector3(.06f, .042f, -.075f), 90, -3);
        AdminPaper(bench, "d09_retirada", new Vector3(-.70f, .901f, .16f), 90, -4);
        AdminPaper(bench, "d10_recibo_pasta", new Vector3(-.42f, .931f, -.085f), 90, 90);
        Transform locker = g.Find("Armario de pertences");
        if (locker != null)
        {
            Bag(locker, false, 1, "P24", "1"); Bag(locker, true, 1, "P25", "2"); Bag(locker, false, 3, "P26", "3");
            float floor = UnclaimedAdministracaoAssets.CellBase(2);
            AdminProp(locker, "Envelope de guarda", "Pertence admin - envelope", new Vector3(.2225f, floor + .004f, -.005f), 0);
            // Leo's recorder is sealed inside the envelope (child 'Gravador de Leo' of its prefab).
            AdminPaper(locker, "d10_recibo_objeto", new Vector3(.08f, floor + .001f, -.07f), 90, 0);
            AdminPaper(locker.Find("Porta A1"), "legenda_armario", new Vector3(.2055f, .23f, -.019f), 0, 0);
        }
        AdminStack(desk, "bandeja", "d01_copia_arquivo", "ficha_082", "ficha_081");
        AdminStack(manuals, "pasta cinza", "h06_recado_irene", "h08_recibo_041", "h03_comprovante");
        AdminStack(coord, "pasta AGO", "ordem_conserto", "h07_autorizacao", "h04_relatorio", "desculpas_riscadas", "proposta_transferencia");
        AdminStack(contract, "contrato", "d12_bilhete_joao", "d12_foto_galpao", "d15_mensagens", "d12_pessoas_contrato", "d11_cobranca_784");
    }

    private static void AdminStack(Transform parent, string name, params string[] ids)
    {
        if (parent == null) { Debug.LogWarning("Unclaimed: missing parent for document stack " + name); return; }
        var go = new GameObject("Pilha - " + name);
        go.transform.SetParent(parent, false);
        var stack = go.AddComponent<PilhaDeDocumentos>();
        stack.folhas = new ObjetoInspecionavel[ids.Length];
        var descendants = parent.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < ids.Length; i++)
        {
            foreach (var t in descendants)
                if (t.name == "Documento admin - " + ids[i])
                {
                    // Support inspection components on either the prefab root or a child.
                    stack.folhas[i] = t.GetComponentInChildren<ObjetoInspecionavel>(true);
                    break;
                }
            if (stack.folhas[i] == null)
                Debug.LogWarning("Unclaimed: missing inspectable document " + ids[i] + " for stack " + name);
        }
        Undo.RegisterCreatedObjectUndo(go, "Create document stack " + name);
    }

    private static void Bag(Transform locker, bool b, int level, string id, string variant)
    {
        var bag = AdminProp(locker, "Bolsa de pertences", "Pertence admin - " + id,
            new Vector3(UnclaimedAdministracaoAssets.CellX(b), UnclaimedAdministracaoAssets.CellBase(level) + .004f, -.015f), 0);
        if (bag != null) UnclaimedAdministracaoAssets.ApplyLabels(bag, "Bolsa de pertences", variant);
        if (bag != null && id == "P26") UnclaimedAdministracaoAssets.Remendo(bag);   // Leo's bag: the patched seam
    }

    private static void AdminPaper(Transform parent, string id, Vector3 pos, float tilt, float yaw, bool reception = false)
    {
        if (parent == null) return;
        string path = reception ? UnclaimedDocumentosRecepcao.PrefabPath(id) : UnclaimedDocumentosAdministracao.PrefabPath(id);
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) { Debug.LogWarning("Unclaimed: missing " + path); return; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        Undo.RegisterCreatedObjectUndo(go, "Place " + id);
        go.name = "Documento admin - " + id;
        go.transform.localPosition = pos; go.transform.localRotation = Quaternion.Euler(tilt, yaw, 0);
    }
}
