// Places the stage-4 systems (Daniel gets in the way) in the morgue map: every Sala volume with its neighbours,
// the action points, the autopsy tap he leaves running, the figure behind the receiving-room visor, the chain on
// the glass door, the safety latch inside the cold room and the breathing sheet.
// Idempotent: running it again updates the same objects instead of duplicating them.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class UnclaimedSistemasEtapa4
{
    const string Raiz = "Unclaimed - Necroterio (planta)";
    const string Grupo = "Sistemas - etapa 4";

    [MenuItem("Tools/Unclaimed/Sistemas/Colocar etapa 4 (Daniel atrapalha)")]
    public static void Colocar()
    {
        GameObject g = GameObject.Find(Raiz);
        if (g == null) { Debug.LogWarning($"Unclaimed: '{Raiz}' not found in the active scene."); return; }
        Transform raiz = g.transform;
        Undo.SetCurrentGroupName("Place stage 4 systems");

        Salas(raiz);
        Pontos(raiz, Filho(raiz, Grupo));
        Lencol(raiz);

        EditorSceneManager.MarkSceneDirty(g.scene);
        Debug.Log("Unclaimed: stage 4 systems placed.");
    }

    // ------------------------------------------------------------------ rooms

    // Rectangles from UnclaimedMorgueGenerator.Zones (floor to ceiling). Rooms made of several rectangles get one
    // volume per rectangle, all with the same id and neighbours. The circulation is split in two: Corredor (the legs
    // from the reception to the autopsy door) and CorredorFundos (the leg past the technical room plus the service
    // vestibule).
    static readonly (string id, Rect[] partes)[] Planta =
    {
        ("Recepcao", new[] { R(-14.75f, -12.375f, -6.5f, -7.125f), R(-10.375f, -7.125f, -6.5f, -6f) }),
        ("WC", new[] { R(-14.75f, -7.125f, -10.375f, -3.875f) }),
        ("Arquivo", new[] { R(-6.5f, -10.25f, 3.5f, -6f), R(-2f, -6f, 3.5f, -3.5f) }),
        ("Descanso", new[] { R(-13f, -3.875f, -5.25f, 0.875f) }),
        ("Deposito", new[] { R(-8f, 0.875f, -4.5f, 6f) }),
        ("Tecnica", new[] { R(-4.5f, 5.75f, 3.25f, 9f) }),
        ("Autopsia", new[] { R(-2f, -3.5f, 7.5f, 3.625f) }),
        ("CamaraFria", new[] { R(7.5f, -3.5f, 15f, 5.25f) }),
        ("Recebimento", new[] { R(3.25f, 5.25f, 12.75f, 11.5f) }),
        ("Corredor", new[] { R(-10.375f, -6f, -2f, -3.875f), R(-5.25f, -3.875f, -2f, 0.875f), R(-4.5f, 0.875f, -2f, 3.625f) }),
        ("CorredorFundos", new[] { R(-4.5f, 3.625f, 3.25f, 5.75f), R(3.25f, 3.625f, 7.5f, 5.25f) }),
        ("Patio", new[] { R(12.75f, 5.25f, 21f, 15.5f), R(9.25f, 11.5f, 12.75f, 13.25f) }),
    };

    // Door (group under "Portas") between two rooms; null = open passage.
    static readonly (string porta, string a, string b)[] Ligacoes =
    {
        ("Porta recepcao - circulacao", "Recepcao", "Corredor"),
        ("Porta WC", "Recepcao", "WC"),
        ("Porta recepcao - arquivo", "Recepcao", "Arquivo"),
        ("Porta arquivo - circulacao", "Arquivo", "Corredor"),
        ("Porta descanso", "Descanso", "Corredor"),
        ("Porta deposito", "Deposito", "Corredor"),
        ("Porta deposito", "Deposito", "CorredorFundos"),   // the door straddles the bend of the two legs
        ("Porta sala tecnica", "Tecnica", "CorredorFundos"),
        ("Porta autopsia - circulacao", "Autopsia", "Corredor"),
        ("Porta autopsia - camara fria", "Autopsia", "CamaraFria"),
        ("Porta camara fria - recebimento", "CamaraFria", "Recebimento"),
        ("Porta vestibulo - recebimento", "CorredorFundos", "Recebimento"),
        ("Porta dupla recebimento - patio", "Recebimento", "Patio"),
        (null, "Corredor", "CorredorFundos"),
    };

    /// <summary>All the Sala volumes of the map (stage 2 calls this too, for the three around the table).</summary>
    public static void Salas(Transform raiz)
    {
        Transform grupo = Filho(raiz, "Sistemas - salas");
        var vizinhas = new Dictionary<string, List<Sala.Vizinha>>();
        foreach (var s in Planta) vizinhas[s.id] = new List<Sala.Vizinha>();
        foreach (var l in Ligacoes)
        {
            FolhaDePorta[] folhas = l.porta == null ? new FolhaDePorta[] { null } : Folhas(raiz, l.porta);
            if (folhas.Length == 0) { Debug.LogWarning($"Unclaimed: door '{l.porta}' not found for the Sala volumes."); continue; }
            foreach (FolhaDePorta f in folhas)   // one entry per leaf: either leaf open counts
            {
                vizinhas[l.a].Add(new Sala.Vizinha { sala = l.b, porta = f });
                vizinhas[l.b].Add(new Sala.Vizinha { sala = l.a, porta = f });
            }
        }

        var usados = new HashSet<Transform>();
        foreach (var s in Planta)
            for (int i = 0; i < s.partes.Length; i++)
            {
                Transform t = SalaVolume(grupo, s.id, i, s.partes[i], vizinhas[s.id].ToArray());
                usados.Add(t);
            }
        // Leftover volumes from older layouts would keep answering for their old area.
        foreach (Transform t in grupo.Cast<Transform>().ToArray())
            if (!usados.Contains(t)) Undo.DestroyObjectImmediate(t.gameObject);
    }

    static Transform SalaVolume(Transform grupo, string id, int parte, Rect r, Sala.Vizinha[] vizinhas)
    {
        Transform t = Filho(grupo, parte == 0 ? "Sala - " + id : $"Sala - {id} ({parte + 1})");
        Undo.RecordObject(t, id);
        t.gameObject.layer = 2;   // Ignore Raycast: aiming and photos pass through
        t.SetPositionAndRotation(new Vector3(r.center.x, 1.6f, r.center.y), Quaternion.identity);
        t.localScale = Vector3.one;
        var box = Garantir<BoxCollider>(t.gameObject);
        Undo.RecordObject(box, id);
        box.isTrigger = true; box.center = Vector3.zero; box.size = new Vector3(r.width, 3.2f, r.height);
        var sala = Garantir<Sala>(t.gameObject);
        Undo.RecordObject(sala, id);
        sala.id = id; sala.vizinhas = vizinhas;
        EditorUtility.SetDirty(sala);
        return t;
    }

    // ------------------------------------------------------------------ action points

    static void Pontos(Transform raiz, Transform grupo)
    {
        FolhaDePorta[] Porta(string nome) => Folhas(raiz, nome);
        Torneira torneiraWC = Object.FindObjectsByType<Torneira>(FindObjectsInactive.Include)
            .FirstOrDefault(x => x.transform.IsChildOf(raiz) && x.GetComponentsInParent<Transform>(true).Any(p => p.name == "WC"));
        Descarga descarga = Object.FindObjectsByType<Descarga>(FindObjectsInactive.Include).FirstOrDefault(x => x.transform.IsChildOf(raiz));

        // Presence (S4): heard or glimpsed from another room.
        var p = Ponto(grupo, "Autopsia/Torneira", TipoDeAcao.Presenca, new Vector3(6.35f, 1.1f, -3.15f));
        p.torneira = TorneiraDaAutopsia(raiz);   // P08: he leaves it running when he gets off the table

        p = Ponto(grupo, "WC/Descarga", TipoDeAcao.Presenca, new Vector3(-14.2f, 0.5f, -4.0f));
        p.descarga = descarga;
        p = Ponto(grupo, "WC/Torneira", TipoDeAcao.Presenca, new Vector3(-10.48f, 0.9f, -5.25f));
        p.torneira = torneiraWC;

        p = Ponto(grupo, "Corredor/Passos", TipoDeAcao.Presenca, new Vector3(-3.25f, 0.1f, 2.2f));
        p.som = Garantir<AudioSource>(p.gameObject);   // clip comes with the sound pass
        p.som.playOnAwake = false; p.som.spatialBlend = 1f; p.som.maxDistance = 18f;

        p = Ponto(grupo, "Descanso/Porta", TipoDeAcao.Presenca, new Vector3(-5.6f, 1f, -0.25f));
        p.porta = Porta("Porta descanso").FirstOrDefault(f => f.name == "Folha");
        p = Ponto(grupo, "Deposito/Porta", TipoDeAcao.Presenca, new Vector3(-4.8f, 1f, 3.5f));
        p.porta = Porta("Porta deposito").FirstOrDefault(f => f.name == "Folha");

        p = Ponto(grupo, "Recebimento/Visor", TipoDeAcao.Presenca, new Vector3(5.0f, 1.5f, 5.9f));
        p.mostrar = Vulto(p.transform, new Vector3(4.98f, 0f, 5.68f));
        p.segundosVisivel = 2.5f;

        // Locked doors (S2): the two ends of the autopsy - cold room - receiving circuit, never both at once.
        var autopsiaCamara = Porta("Porta autopsia - camara fria");
        var camaraRecebimento = Porta("Porta camara fria - recebimento");
        p = Ponto(grupo, "Autopsia/PortaCamara", TipoDeAcao.TrancarPorta, new Vector3(7.2f, 1.2f, 0.25f));
        Portas(p, autopsiaCamara); p.grupo = "circuito"; p.segundosTrancada = 150f;
        p = Ponto(grupo, "Recebimento/PortaCamara", TipoDeAcao.TrancarPorta, new Vector3(9.75f, 1.2f, 5.7f));
        Portas(p, camaraRecebimento); p.grupo = "circuito"; p.segundosTrancada = 150f;

        // One-off sabotage (obstruction phase).
        p = Ponto(grupo, "Tecnica/Fusivel", TipoDeAcao.Apagao, new Vector3(-1.95f, 1.2f, 8.6f));
        p.repetivel = false; p.naSombra = false;

        p = Ponto(grupo, "CamaraFria/Porta", TipoDeAcao.PrenderCamaraFria, new Vector3(8.0f, 1.2f, 0.25f));
        Portas(p, autopsiaCamara.Concat(camaraRecebimento).ToArray());
        p.repetivel = false; p.naSombra = false;
        p.luzes = raiz.GetComponentsInChildren<Light>(true).Where(l => l.name.StartsWith("Luz - Camara fria")).ToArray();
        Trava(raiz, grupo, p);

        var vidro = Porta("Porta de vidro - entrada");
        p = Ponto(grupo, "Recepcao/Corrente", TipoDeAcao.Corrente, new Vector3(-11.4f, 1.1f, -12.2f));
        Portas(p, vidro);
        p.repetivel = false; p.naSombra = false;
        p.mostrar = Corrente(p.transform);

        // Where he shows up for the confrontation: the archive, at its door to the corridor.
        p = Ponto(grupo, "Arquivo/Porta", TipoDeAcao.Aparicao, new Vector3(-1.0f, 0f, -4.75f));
        p.repetivel = false;
    }

    static PontoDeAcao Ponto(Transform grupo, string id, TipoDeAcao tipo, Vector3 pos)
    {
        Transform t = Filho(grupo, "Ponto - " + id.Replace("/", " - "));
        Undo.RecordObject(t, id);
        t.position = pos;
        t.gameObject.layer = 2;
        var p = Garantir<PontoDeAcao>(t.gameObject);
        Undo.RecordObject(p, id);
        p.id = id; p.tipo = tipo;
        p.repetivel = tipo == TipoDeAcao.Presenca || tipo == TipoDeAcao.TrancarPorta;
        p.naSombra = true; p.naObstrucao = true; p.grupo = "";
        p.porta = null; p.outrasPortas = new FolhaDePorta[0];
        EditorUtility.SetDirty(p);
        return p;
    }

    static void Portas(PontoDeAcao p, FolhaDePorta[] folhas)
    {
        p.porta = folhas.Length > 0 ? folhas[0] : null;
        p.outrasPortas = folhas.Skip(1).ToArray();
        if (folhas.Length == 0) Debug.LogWarning($"Unclaimed: no door leaves for '{p.id}'.");
    }

    // ------------------------------------------------------------------ props

    // Knee-operated scrub sink: the spout ends 0.246 m out from the wall at 1.145 m; the bowl floor is near 0.71 m.
    static Torneira TorneiraDaAutopsia(Transform raiz)
    {
        Transform lav = raiz.Find("Ambientes/Autopsia/Mobiliario/Lavatorio de joelho");
        if (lav == null) { Debug.LogWarning("Unclaimed: autopsy scrub sink not found."); return null; }
        Transform t = Filho(lav, "Torneira");
        Undo.RecordObject(t, "Torneira");
        t.SetPositionAndRotation(new Vector3(6.35f, 0f, -3.154f), Quaternion.identity);
        t.localScale = Vector3.one;

        var tap = Garantir<Torneira>(t.gameObject);
        Undo.RecordObject(tap, "Torneira");
        var pts = new List<Vector3>();
        for (int i = 0; i <= 12; i++) { float k = i / 12f; pts.Add(new Vector3(0f, 1.139f - 0.424f * k * k, 0.004f * k)); }
        tap.trajetoria = pts.ToArray();
        tap.botao = null;

        Transform agua = Filho(t, "Agua");
        Garantir<MeshFilter>(agua.gameObject);
        var r = Garantir<MeshRenderer>(agua.gameObject);
        Torneira modelo = Object.FindObjectsByType<Torneira>(FindObjectsInactive.Include).FirstOrDefault(x => x != tap && x.fluxo != null);
        if (modelo != null) r.sharedMaterial = modelo.fluxo.GetComponent<Renderer>().sharedMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tap.fluxo = agua.GetComponent<MeshFilter>();

        var box = Garantir<BoxCollider>(t.gameObject);   // aim target around the spout and the knee paddle
        box.center = new Vector3(0f, 0.95f, -0.05f); box.size = new Vector3(0.36f, 0.6f, 0.3f);
        EditorUtility.SetDirty(tap);
        return tap;
    }

    // A dark standing figure right behind the left leaf's visor, seen from the vestibule for a moment.
    static GameObject Vulto(Transform ponto, Vector3 pe)
    {
        Transform v = Filho(ponto, "Vulto");
        Undo.RecordObject(v.gameObject, "Vulto");
        v.SetPositionAndRotation(pe, Quaternion.Euler(0f, 180f, 0f));
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/UnclaimedPrototype/Recepcao/Plastico preto.mat");
        Peca(v, "Corpo", PrimitiveType.Capsule, new Vector3(0f, 0.82f, 0f), new Vector3(0.46f, 0.82f, 0.3f), mat);
        Peca(v, "Ombros", PrimitiveType.Capsule, new Vector3(0f, 1.42f, 0f), new Vector3(0.5f, 0.2f, 0.28f), mat, 90f);
        Peca(v, "Cabeca", PrimitiveType.Sphere, new Vector3(0f, 1.66f, 0.02f), new Vector3(0.2f, 0.24f, 0.22f), mat);
        v.gameObject.SetActive(false);
        return v.gameObject;
    }

    static void Peca(Transform pai, string nome, PrimitiveType tipo, Vector3 pos, Vector3 escala, Material mat, float giroZ = 0f)
    {
        Transform t = pai.Find(nome);
        if (t == null)
        {
            var go = GameObject.CreatePrimitive(tipo);
            Undo.RegisterCreatedObjectUndo(go, nome);
            go.name = nome;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(pai, false);
            t = go.transform;
        }
        t.localPosition = pos; t.localRotation = Quaternion.Euler(0f, 0f, giroZ); t.localScale = escala;
        t.gameObject.layer = 2;
        var r = t.GetComponent<Renderer>();
        if (mat != null) r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
    }

    // Daniel chains the glass door from inside: a run of links between the two pull handles (closed leaves meet at
    // x -11.41; handles about 0.1 m either side, mid height 1.1 m) with the padlock hanging from the middle.
    static GameObject Corrente(Transform ponto)
    {
        Transform velho = ponto.Find("Corrente na porta");
        if (velho != null) Undo.DestroyObjectImmediate(velho.gameObject);
        var c = new GameObject("Corrente na porta");
        Undo.RegisterCreatedObjectUndo(c, "Corrente");
        c.transform.SetParent(ponto, false);
        c.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        Material aco = AssetDatabase.LoadAssetAtPath<Material>("Assets/Modelos/Recepcao/Materiais/Aco - fecho e arco.mat");
        const float z = -12.27f, xa = -11.56f, xb = -11.27f, y = 1.16f, flecha = 0.05f;
        const int elos = 16;
        for (int i = 0; i < elos; i++)
        {
            float t = (i + 0.5f) / elos;
            var pos = new Vector3(Mathf.Lerp(xa, xb, t), y - flecha * 4f * t * (1f - t), z);
            float inclinacao = -Mathf.Atan(flecha * 4f * (1f - 2f * t) / (xb - xa)) * Mathf.Rad2Deg;
            var giro = Quaternion.Euler(i % 2 == 0 ? 0f : 90f, 0f, 90f + inclinacao);
            var elo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.DestroyImmediate(elo.GetComponent<Collider>());
            elo.name = "Elo";
            elo.transform.SetParent(c.transform, false);
            elo.transform.SetPositionAndRotation(pos, giro);
            elo.transform.localScale = new Vector3(0.013f, 0.012f, 0.006f);
            if (aco != null) elo.GetComponent<Renderer>().sharedMaterial = aco;
        }
        // Two turns round each handle.
        foreach (float x in new[] { xa, xb })
            for (int k = 0; k < 2; k++)
            {
                var volta = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.DestroyImmediate(volta.GetComponent<Collider>());
                volta.name = "Volta no puxador";
                volta.transform.SetParent(c.transform, false);
                volta.transform.position = new Vector3(x, y + 0.012f - k * 0.024f, z - 0.03f);
                volta.transform.localScale = new Vector3(0.05f, 0.006f, 0.075f);
                if (aco != null) volta.GetComponent<Renderer>().sharedMaterial = aco;
            }
        if (UnclaimedExteriorFinishes.Available("Cadeado_Chave_Corrente"))
        {
            GameObject cadeado = UnclaimedExteriorFinishes.Place("Cadeado_Chave_Corrente", c.transform,
                new Vector3((xa + xb) / 2f, y - flecha - 0.02f, z + 0.01f), Vector3.forward, "Cadeado");
            foreach (var col in cadeado.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
        }
        foreach (var r in c.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        foreach (var t in c.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
        c.SetActive(false);
        return c;
    }

    // Safety latch inside the cold room, on the wall beside the autopsy door.
    static void Trava(Transform raiz, Transform grupo, PontoDeAcao armadilha)
    {
        var existente = Object.FindObjectsByType<TravaDeSeguranca>(FindObjectsInactive.Include).FirstOrDefault(x => x.transform.IsChildOf(raiz));
        TravaDeSeguranca trava = existente;
        if (trava == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Sistemas/Trava de seguranca.prefab");
            if (prefab == null) { Debug.LogWarning("Unclaimed: safety latch prefab not found."); return; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, grupo);
            Undo.RegisterCreatedObjectUndo(go, "Trava");
            trava = go.GetComponent<TravaDeSeguranca>();
        }
        // The model's front is local -Z and its back sits on local z = 0: yaw -90 puts it flat on the wall face (x 7.60)
        // looking into the room. Plate bottom at 0.9 m, grip near 0.97 m, sign at about 1.25 - 1.34 m.
        Undo.RecordObject(trava.transform, "Trava");
        trava.transform.SetPositionAndRotation(new Vector3(7.601f, 0.9f, 1.45f), Quaternion.Euler(0f, -90f, 0f));
        trava.transform.localScale = Vector3.one;
        Undo.RecordObject(trava, "Trava");
        trava.armadilha = armadilha;

        // The prefab's red block gives way to the modelled release (Blender/Sistemas/Trava/modelar_trava.py).
        var bloco = trava.GetComponent<MeshRenderer>();
        if (bloco != null) { Undo.RecordObject(bloco, "Trava"); bloco.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(bloco); }
        var col = trava.GetComponent<BoxCollider>();
        if (col != null)
        {
            Undo.RecordObject(col, "Trava");
            col.center = new Vector3(0f, 0.2f, -0.035f); col.size = new Vector3(0.16f, 0.32f, 0.07f);   // plate and lever, not the sign
            PrefabUtility.RecordPrefabInstancePropertyModifications(col);
        }
        Material inox = AssetDatabase.LoadAssetAtPath<Material>("Assets/UnclaimedPrototype/Autopsia/Aco inox fosco.mat");
        Material escuro = AssetDatabase.LoadAssetAtPath<Material>("Assets/UnclaimedPrototype/Recepcao/Plastico preto.mat");
        Material pintura = Lit("Trava - pintura cinza", new Color(0.42f, 0.43f, 0.43f), 0.35f, 0.2f);
        Material vermelho = Lit("Trava - vermelho", new Color(0.62f, 0.05f, 0.04f), 0.5f, 0.1f);
        Material borracha = Lit("Trava - borracha", new Color(0.035f, 0.035f, 0.035f), 0.18f, 0f);
        Material placa = Lit("Trava - placa", Color.white, 0.3f, 0f, "Assets/Modelos/CamaraFria/Texturas/TravaPlaca.png");
        Modelo(trava.transform, "Modelo - base", "TravaSegurancaBase", Vector3.zero,
            new Dictionary<string, Material> { { "inox", inox }, { "escuro", escuro }, { "pintura", pintura }, { "placa", placa } });
        trava.alavanca = Modelo(trava.transform, "Modelo - alavanca", "TravaSegurancaAlavanca", new Vector3(0f, 0.235f, -0.034f),
            new Dictionary<string, Material> { { "vermelho", vermelho }, { "borracha", borracha } });
        PrefabUtility.RecordPrefabInstancePropertyModifications(trava);
        PrefabUtility.RecordPrefabInstancePropertyModifications(trava.transform);
    }

    // Materials go by the imported slot name ("Trava - <key>"): the FBX importer does not keep Blender's slot order.
    static Transform Modelo(Transform pai, string nome, string fbx, Vector3 pos, Dictionary<string, Material> mats)
    {
        Transform velho = pai.Find(nome);
        if (velho != null) Undo.DestroyObjectImmediate(velho.gameObject);
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Modelos/CamaraFria/" + fbx + ".fbx");
        if (asset == null) { Debug.LogWarning($"Unclaimed: model '{fbx}' not found."); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, pai);
        Undo.RegisterCreatedObjectUndo(go, nome);
        go.name = nome;
        go.transform.localPosition = pos; go.transform.localRotation = Quaternion.identity; go.transform.localScale = Vector3.one;
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            r.sharedMaterials = r.sharedMaterials.Select(m => m != null && mats.TryGetValue(m.name.Replace("Trava - ", ""), out Material novo) ? novo : m).ToArray();
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        return go.transform;
    }

    static Material Lit(string nome, Color cor, float suavidade, float metalico, string textura = null)
    {
        string caminho = "Assets/Modelos/CamaraFria/Materiais/" + nome + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, caminho);
        }
        m.SetColor("_BaseColor", cor);
        m.SetFloat("_Smoothness", suavidade);
        m.SetFloat("_Metallic", metalico);
        if (textura != null) m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(textura));
        EditorUtility.SetDirty(m);
        return m;
    }

    // Daniel breathes under the sheet (head at -X; chest about 0.6 m in from the sheet's head end).
    static void Lencol(Transform raiz)
    {
        Transform l = raiz.Find("Ambientes/Autopsia/Mobiliario/Estado da mesa de autopsia/Corpo - autopsia/Lencol");
        if (l == null) { Debug.LogWarning("Unclaimed: autopsy sheet not found."); return; }
        var mf = l.GetComponent<MeshFilter>();
        string caminho = mf != null && mf.sharedMesh != null ? AssetDatabase.GetAssetPath(mf.sharedMesh) : null;
        if (!string.IsNullOrEmpty(caminho) && AssetImporter.GetAtPath(caminho) is ModelImporter imp && !imp.isReadable)
        {
            imp.isReadable = true;   // the sheet's vertices move at runtime
            imp.SaveAndReimport();
        }
        var r = Garantir<RespiracaoDoLencol>(l.gameObject);
        Undo.RecordObject(r, "Respiracao");
        r.centroLocal = new Vector3(-0.62f, 1.1f, 0f);
        r.raio = 0.34f;
        EditorUtility.SetDirty(r);
    }

    // ------------------------------------------------------------------ helpers

    static FolhaDePorta[] Folhas(Transform raiz, string porta) => Object.FindObjectsByType<FolhaDePorta>(FindObjectsInactive.Include)
        .Where(f => f.transform.IsChildOf(raiz) && f.GetComponentsInParent<Transform>(true).Any(t => t.name == porta))
        .OrderBy(f => f.name).ToArray();

    static Rect R(float x0, float z0, float x1, float z1) => Rect.MinMaxRect(x0, z0, x1, z1);

    static T Garantir<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : Undo.AddComponent<T>(go);
    }

    static Transform Filho(Transform pai, string nome)
    {
        Transform t = pai.Find(nome);
        if (t != null) return t;
        var go = new GameObject(nome);
        Undo.RegisterCreatedObjectUndo(go, nome);
        go.transform.SetParent(pai, false);
        return go.transform;
    }
}
