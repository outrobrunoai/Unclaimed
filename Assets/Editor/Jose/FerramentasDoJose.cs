// Ferramentas de José (sistemas). Menu Tools > Unclaimed > José:
//   Criar prefabs de mecânica       Assets/Prefabs/Sistemas/*.prefab (Bruno coloca no mapa, sem editar o prefab)
//   Instalar sistemas na Jogabilidade   câmera, álbum, celular e gravador no Player; objeto "Sistemas" (diretor,
//                                        confronto, checkpoints) e Daniel provisório na cena Jogabilidade
//   Criar cena de testes               Assets/Scenes/Testes/Jose.unity, com uma sala de cada sistema e NavMesh
//   Verificar evidências               quais ids do catálogo já estão em prefabs e quais faltam
// Tudo também roda em batchmode: -executeMethod FerramentasDoJose.TudoEmLote
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class FerramentasDoJose
{
    public const string PastaPrefabs = "Assets/Prefabs/Sistemas";
    const string PastaMateriais = PastaPrefabs + "/Materiais";
    const string CenaJogabilidade = "Assets/Scenes/Jogabilidade.unity";
    const string CenaTestes = "Assets/Scenes/Testes/Jose.unity";

    static string Prefab(string nome) => PastaPrefabs + "/" + nome + ".prefab";

    public static void TudoEmLote()
    {
        CriarPrefabs();
        InstalarNaJogabilidade();
        CriarCenaDeTestes();
        VerificarEvidencias();
    }

    // ================================================================== prefabs

    [MenuItem("Tools/Unclaimed/José/Criar prefabs de mecânica")]
    public static void CriarPrefabs()
    {
        GarantirPasta(PastaPrefabs);
        GarantirPasta(PastaMateriais);
        Material cinza = Mat("Painel", new Color(0.45f, 0.47f, 0.46f), 0.3f);
        Material escuro = Mat("Plastico escuro", new Color(0.08f, 0.08f, 0.09f), 0.5f);
        Material vermelho = Mat("Trava vermelha", new Color(0.6f, 0.08f, 0.06f), 0.4f);
        Material papel = Mat("Papel", new Color(0.93f, 0.92f, 0.88f), 0.1f);
        Material daniel = Mat("Daniel provisorio", new Color(0.18f, 0.2f, 0.24f), 0.2f);

        Salvar("Documento legivel", () =>
        {
            GameObject go = Caixa("Documento legivel", new Vector3(0.21f, 0.002f, 0.297f), papel);
            var o = go.AddComponent<ObjetoInspecionavel>();
            o.nome = "Documento";
            o.descricao = "Texto do documento (transcrição mostrada na inspeção).";
            o.idEvidencia = "";
            o.orientacao = OrientacaoNaInspecao.FaceParaOJogador;
            return go;
        });

        Salvar("Painel de energia", () =>
        {
            GameObject go = Caixa("Painel de energia", new Vector3(0.5f, 0.7f, 0.14f), cinza);
            go.AddComponent<PainelDeEnergia>();
            return go;
        });

        Salvar("Gravador", () =>
        {
            GameObject go = Caixa("Gravador", new Vector3(0.07f, 0.025f, 0.12f), escuro);
            go.AddComponent<Gravador>();
            return go;
        });

        Salvar("Copia do audio", () =>
        {
            GameObject go = Caixa("Copia do audio", new Vector3(0.5f, 0.32f, 0.05f), escuro);
            go.AddComponent<CopiaDoAudio>();
            return go;
        });

        Salvar("Trava de seguranca", () =>
        {
            GameObject go = Caixa("Trava de seguranca", new Vector3(0.06f, 0.18f, 0.06f), vermelho);
            go.AddComponent<TravaDeSeguranca>();
            return go;
        });

        Salvar("Esconderijo", () =>
        {
            // Collider sólido dentro do móvel: com a porta aberta, o jogador mira o interior e se esconde.
            var go = new GameObject("Esconderijo");
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.9f, 0f);
            box.size = new Vector3(0.6f, 1.8f, 0.5f);
            var e = go.AddComponent<Esconderijo>();
            e.dentro = Filho(go, "Dentro", new Vector3(0f, 0f, 0f));
            e.saida = Filho(go, "Saida", new Vector3(0f, 0f, 0.9f));
            return go;
        });

        Salvar("Saida do predio", () =>
        {
            var go = new GameObject("Saida do predio");
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.05f, 0f);
            box.size = new Vector3(1.2f, 2.1f, 0.1f);
            go.AddComponent<SaidaDoPredio>();
            return go;
        });

        Salvar("Zona de sinal", () =>
        {
            var go = new GameObject("Zona de sinal");
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(6f, 3f, 6f);
            box.center = new Vector3(0f, 1.5f, 0f);
            go.AddComponent<ZonaDeSinal>();
            return go;
        });

        Salvar("Sala", () =>
        {
            var go = new GameObject("Sala");
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(5f, 3f, 5f);
            box.center = new Vector3(0f, 1.5f, 0f);
            go.AddComponent<Sala>();
            return go;
        });

        Salvar("Ponto de acao", () =>
        {
            var go = new GameObject("Ponto de acao");
            go.AddComponent<PontoDeAcao>();
            return go;
        });

        Salvar("Alvo fotografico", () =>
        {
            var go = new GameObject("Alvo fotografico");
            go.AddComponent<AlvoFotografico>();
            return go;
        });

        Salvar("Consumidor de energia", () =>
        {
            var go = new GameObject("Consumidor de energia");
            go.AddComponent<ConsumidorDeEnergia>();
            return go;
        });

        Salvar("Daniel (provisorio)", () =>
        {
            var go = new GameObject("Daniel (provisorio)");
            var agente = go.AddComponent<NavMeshAgent>();
            agente.radius = 0.32f;
            agente.height = 1.8f;
            agente.acceleration = 14f;
            agente.angularSpeed = 540f;
            agente.stoppingDistance = 0.4f;
            agente.enabled = false;   // liga no confronto (DanielPerseguidor.Aparecer)
            var corpo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            corpo.name = "Corpo";
            Object.DestroyImmediate(corpo.GetComponent<Collider>());
            corpo.transform.SetParent(go.transform, false);
            corpo.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            corpo.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);
            corpo.GetComponent<Renderer>().sharedMaterial = daniel;
            var fonte = go.AddComponent<AudioSource>();
            fonte.spatialBlend = 1f;
            fonte.playOnAwake = false;
            var p = go.AddComponent<DanielPerseguidor>();
            p.corpo = corpo;
            p.fonte = fonte;
            return go;
        });

        CriarCelularNaMao();
        AssetDatabase.SaveAssets();
        Debug.Log("José: prefabs de mecânica em " + PastaPrefabs);
    }

    static void Salvar(string nome, System.Func<GameObject> criar)
    {
        GameObject go = criar();
        try { PrefabUtility.SaveAsPrefabAsset(go, Prefab(nome)); }
        finally { Object.DestroyImmediate(go); }
    }

    static GameObject Caixa(string nome, Vector3 tamanho, Material m)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = nome;
        go.transform.localScale = tamanho;
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }

    static Transform Filho(GameObject pai, string nome, Vector3 local)
    {
        var t = new GameObject(nome).transform;
        t.SetParent(pai.transform, false);
        t.localPosition = local;
        return t;
    }

    static Material Mat(string nome, Color cor, float liso, float metal = 0f)
    {
        string caminho = PastaMateriais + "/" + nome + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, caminho);
        }
        m.SetColor("_BaseColor", cor);
        m.SetFloat("_Smoothness", liso);
        m.SetFloat("_Metallic", metal);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void GarantirPasta(string caminho)
    {
        if (AssetDatabase.IsValidFolder(caminho)) return;
        string pai = Path.GetDirectoryName(caminho).Replace('\\', '/');
        GarantirPasta(pai);
        AssetDatabase.CreateFolder(pai, Path.GetFileName(caminho));
    }

    // ================================================================== Jogabilidade

    [MenuItem("Tools/Unclaimed/José/Instalar sistemas na Jogabilidade")]
    public static void InstalarNaJogabilidade()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene cena = EditorSceneManager.OpenScene(CenaJogabilidade, OpenSceneMode.Single);
        Movimento jogador = Object.FindAnyObjectByType<Movimento>();
        if (jogador == null) { Debug.LogError("José: não achei o Player (Movimento) na Jogabilidade."); return; }

        Garantir<CameraFotografica>(jogador.gameObject);
        // O álbum virou o app Galeria do celular: tira o componente antigo (script apagado).
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(jogador.gameObject);
        var celular = Garantir<Celular>(jogador.gameObject);
        celular.prefabNaMao = AssetDatabase.LoadAssetAtPath<CelularNaMao>(PastaCelular + "/Celular na mao.prefab");
        if (celular.prefabNaMao == null) Debug.LogWarning("José: prefab do celular não encontrado; rode Criar prefabs de mecânica.");
        EditorUtility.SetDirty(celular);
        Garantir<OuvirGravador>(jogador.gameObject);

        GameObject sistemas = cena.GetRootGameObjects().FirstOrDefault(g => g.name == "Sistemas") ?? new GameObject("Sistemas");
        Garantir<SistemasDaNoite>(sistemas);
        Garantir<DiretorDeDaniel>(sistemas);
        Garantir<Confronto>(sistemas);

        if (Object.FindAnyObjectByType<DanielPerseguidor>(FindObjectsInactive.Include) == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab("Daniel (provisorio)"));
            if (prefab != null)
            {
                var d = (GameObject)PrefabUtility.InstantiatePrefab(prefab, cena);
                d.transform.position = jogador.transform.position + Vector3.down * 50f;   // escondido até o confronto
            }
        }

        EditorSceneManager.MarkSceneDirty(cena);
        EditorSceneManager.SaveScene(cena);
        Debug.Log("José: sistemas instalados na Jogabilidade.");
    }

    // ================================================================== celular 3D

    public const string PastaCelular = PastaPrefabs + "/Celular";
    const string FbxCelular = PastaCelular + "/Celular.fbx";

    /// <summary>
    /// Monta "Celular na mao.prefab" a partir do FBX do Blender (Blender/Sistemas/Celular/modelar_celular.py):
    /// materiais URP no lugar dos do FBX, modelo girado para a tela olhar para -Z (para o jogador) com o topo em +Y,
    /// sinais da UV da tela e uma luz fraca da tela (o brilho no rosto, no escuro).
    /// </summary>
    public static void CriarCelularNaMao()
    {
        var importador = AssetImporter.GetAtPath(FbxCelular) as ModelImporter;
        if (importador == null) { Debug.LogWarning("José: falta " + FbxCelular + " (rode o script do Blender)."); return; }
        var mats = new Dictionary<string, Material>
        {
            ["Corpo"] = Mat("Celular - corpo", new Color(0.11f, 0.12f, 0.135f), 0.55f, 0.6f),
            ["Vidro"] = Mat("Celular - vidro", new Color(0.02f, 0.02f, 0.025f), 0.92f),
            ["Lente"] = Mat("Celular - lente", new Color(0.02f, 0.025f, 0.04f), 0.95f, 0.3f),
            ["Flash"] = Mat("Celular - flash", new Color(0.95f, 0.9f, 0.75f), 0.6f),
            ["Botao"] = Mat("Celular - botao", new Color(0.16f, 0.17f, 0.19f), 0.6f, 0.7f),
        };
        Material telaMat = AssetDatabase.LoadAssetAtPath<Material>(PastaMateriais + "/Celular - tela.mat");
        if (telaMat == null)
        {
            telaMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(telaMat, PastaMateriais + "/Celular - tela.mat");
        }
        mats["Tela"] = telaMat;

        importador.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importador.importCameras = false;
        importador.importLights = false;
        importador.importAnimation = false;
        importador.globalScale = 1f;
        foreach (var par in mats) importador.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), par.Key), par.Value);
        importador.SaveAndReimport();

        var fonte = AssetDatabase.LoadAssetAtPath<GameObject>(FbxCelular);
        var raiz = new GameObject("Celular na mao");
        try
        {
            var modelo = (GameObject)PrefabUtility.InstantiatePrefab(fonte);
            modelo.name = "Modelo";
            modelo.transform.SetParent(raiz.transform, false);
            Transform telaT = modelo.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Tela");
            if (telaT == null) { Debug.LogError("José: o FBX não tem o objeto Tela."); return; }
            Mesh malha = telaT.GetComponent<MeshFilter>().sharedMesh;

            // Normal da tela e eixo do comprimento, no espaço do modelo.
            Vector3 normal = Vector3.zero;
            foreach (Vector3 n in malha.normals) normal += n;
            normal = modelo.transform.InverseTransformDirection(telaT.TransformDirection(normal.normalized)).normalized;
            Vector3 tam = malha.bounds.size;
            Vector3 eixoAlto = tam.x >= tam.y && tam.x >= tam.z ? Vector3.right : tam.y >= tam.z ? Vector3.up : Vector3.forward;
            eixoAlto = modelo.transform.InverseTransformDirection(telaT.TransformDirection(eixoAlto)).normalized;
            // Gira o modelo: normal da tela → -Z (para o jogador), comprimento → +Y.
            modelo.transform.localRotation = Quaternion.Inverse(Quaternion.LookRotation(-normal, eixoAlto));
            Vector3 centro = raiz.transform.InverseTransformPoint(telaT.GetComponent<Renderer>().bounds.center);
            modelo.transform.localPosition = -new Vector3(centro.x, centro.y, 0f);

            // UV: U deve crescer para +X (direita do jogador) e V para +Y (cima).
            Vector3[] v = malha.vertices;
            Vector2[] uv = malha.uv;
            float cx = 0f, cy = 0f;
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 p = raiz.transform.InverseTransformPoint(telaT.TransformPoint(v[i]));
                cx += (uv[i].x - 0.5f) * p.x;
                cy += (uv[i].y - 0.5f) * p.y;
            }

            var na = raiz.AddComponent<CelularNaMao>();
            na.tela = telaT.GetComponent<Renderer>();
            na.tela.sharedMaterial = telaMat;
            na.sinalUV = new Vector2(cx >= 0f ? 1f : -1f, cy >= 0f ? 1f : -1f);
            na.imagens = ImagensDoCelular();

            var luz = new GameObject("Brilho da tela").AddComponent<Light>();
            luz.transform.SetParent(raiz.transform, false);
            luz.transform.localPosition = new Vector3(0f, 0f, -0.08f);
            luz.type = LightType.Point;
            luz.range = 0.9f;
            luz.intensity = 0.35f;
            luz.color = new Color(0.65f, 0.75f, 1f);
            luz.shadows = LightShadows.None;
            na.brilhoDaTela = luz;

            Vector3 tamFinal = raiz.GetComponentsInChildren<Renderer>().Aggregate(new Bounds(raiz.transform.position, Vector3.zero),
                (b, r) => { b.Encapsulate(r.bounds); return b; }).size;
            Debug.Log($"José: celular montado. Tamanho {tamFinal.x:F3} x {tamFinal.y:F3} x {tamFinal.z:F3} m; UV {na.sinalUV}; normal da tela no FBX {normal}.");
            PrefabUtility.SaveAsPrefabAsset(raiz, PastaCelular + "/Celular na mao.prefab");
        }
        finally { Object.DestroyImmediate(raiz); }
    }

    /// <summary>Ícones da interface (Blender/Sistemas/Celular/icones.py), importados lisos e sem repetir a borda.</summary>
    static Texture2D[] ImagensDoCelular()
    {
        var lista = new List<Texture2D>();
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { PastaCelular + "/Icones" }))
        {
            string caminho = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetImporter.GetAtPath(caminho) is TextureImporter ti)
            {
                ti.textureType = TextureImporterType.Default;
                ti.alphaIsTransparency = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.mipmapEnabled = true;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport();
            }
            lista.Add(AssetDatabase.LoadAssetAtPath<Texture2D>(caminho));
        }
        return lista.ToArray();
    }

    static T Garantir<T>(GameObject go) where T : Component => go.GetComponent<T>() ?? go.AddComponent<T>();

    // ================================================================== cena de testes

    [MenuItem("Tools/Unclaimed/José/Criar cena de testes")]
    public static void CriarCenaDeTestes()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        // O Player vem da Jogabilidade, na posição do mapa: a área de testes é montada em volta dele.
        Scene jog = EditorSceneManager.OpenScene(CenaJogabilidade, OpenSceneMode.Single);
        Movimento m = Object.FindAnyObjectByType<Movimento>();
        Vector3 origem = Vector3.zero;
        float giro = 0f;
        if (m != null)
        {
            var cc = m.GetComponent<CharacterController>();
            origem = m.transform.position;
            if (cc != null) origem.y += cc.center.y - cc.height * 0.5f;
            giro = m.transform.eulerAngles.y;
        }

        GarantirPasta("Assets/Scenes/Testes");
        Scene cena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var raiz = new GameObject("Testes do José").transform;
        raiz.SetPositionAndRotation(origem, Quaternion.Euler(0f, giro, 0f));

        var carregar = new GameObject("Carregar jogabilidade");
        carregar.AddComponent<CarregarJogabilidade>();

        var sol = new GameObject("Luz ambiente");
        var luzSol = sol.AddComponent<Light>();
        luzSol.type = LightType.Directional;
        luzSol.intensity = 0.15f;
        sol.transform.rotation = Quaternion.Euler(50f, 30f, 0f);
        RenderSettings.ambientLight = new Color(0.08f, 0.08f, 0.1f);

        Material piso = Mat("Teste piso", new Color(0.35f, 0.36f, 0.37f), 0.2f);
        Material parede = Mat("Teste parede", new Color(0.7f, 0.72f, 0.7f), 0.1f);

        // Corredor no meio (o jogador começa nele), salas dos dois lados.
        Bloco(raiz, "Piso", new Vector3(0f, -0.1f, 10f), new Vector3(24f, 0.2f, 32f), piso);
        Bloco(raiz, "Parede esquerda", new Vector3(-12f, 1.5f, 10f), new Vector3(0.2f, 3f, 32f), parede);
        Bloco(raiz, "Parede direita", new Vector3(12f, 1.5f, 10f), new Vector3(0.2f, 3f, 32f), parede);
        Bloco(raiz, "Parede fundo", new Vector3(0f, 1.5f, 26f), new Vector3(24f, 3f, 0.2f), parede);
        Bloco(raiz, "Parede frente", new Vector3(0f, 1.5f, -6f), new Vector3(24f, 3f, 0.2f), parede);
        // Divisórias do corredor (x = ±3) com um vão de porta de 1,4 m no meio de cada sala (z = -2, 6, 14).
        float[] trechos = { -6f, -2.7f, -1.3f, 5.3f, 6.7f, 13.3f, 14.7f, 18f };
        foreach (float x in new[] { -3f, 3f })
            for (int i = 0; i < trechos.Length; i += 2)
                Bloco(raiz, "Divisoria", new Vector3(x, 1.5f, (trechos[i] + trechos[i + 1]) / 2f), new Vector3(0.2f, 3f, trechos[i + 1] - trechos[i]), parede);
        // Paredes entre as salas laterais; a do fundo (z = 18) separa as salas do pátio.
        foreach (float z in new[] { 2f, 10f, 18f })
            foreach (float x in new[] { -7.5f, 7.5f })
                Bloco(raiz, "Parede entre salas", new Vector3(x, 1.5f, z), new Vector3(9f, 3f, 0.2f), parede);

        // Salas (nomes combinados).
        SalaDeTeste(raiz, "Corredor", new Vector3(0f, 0f, 10f), new Vector3(6f, 3f, 32f));
        SalaDeTeste(raiz, "Recepcao", new Vector3(-7.5f, 0f, -2f), new Vector3(9f, 3f, 8f));
        SalaDeTeste(raiz, "Tecnica", new Vector3(7.5f, 0f, -2f), new Vector3(9f, 3f, 8f));
        SalaDeTeste(raiz, "Autopsia", new Vector3(-7.5f, 0f, 6f), new Vector3(9f, 3f, 8f));
        SalaDeTeste(raiz, "CamaraFria", new Vector3(7.5f, 0f, 6f), new Vector3(9f, 3f, 8f));
        SalaDeTeste(raiz, "Arquivo", new Vector3(-7.5f, 0f, 14f), new Vector3(9f, 3f, 8f));
        SalaDeTeste(raiz, "Descanso", new Vector3(7.5f, 0f, 14f), new Vector3(9f, 3f, 8f));
        SalaDeTeste(raiz, "Patio", new Vector3(0f, 0f, 22f), new Vector3(24f, 3f, 8f));

        // Luzes por circuito.
        LuzDoCircuito(raiz, "Luz geral corredor", new Vector3(0f, 2.8f, 4f), RedeEletrica.Geral, false, new Color(1f, 0.96f, 0.9f), 2.5f);
        LuzDoCircuito(raiz, "Luz geral salas", new Vector3(0f, 2.8f, 14f), RedeEletrica.Geral, false, new Color(1f, 0.96f, 0.9f), 2.5f);
        LuzDoCircuito(raiz, "Luz de emergencia", new Vector3(0f, 2.6f, 10f), RedeEletrica.Geral, true, new Color(1f, 0.2f, 0.15f), 0.8f);
        LuzDoCircuito(raiz, "Luz de exame", new Vector3(-7.5f, 2.5f, 6f), RedeEletrica.Exame, false, new Color(0.85f, 0.95f, 1f), 4f);

        // Recepção: sinal, saída e corrente.
        Instanciar("Zona de sinal", raiz, new Vector3(-7.5f, 0f, -2f), 0f);
        Instanciar("Saida do predio", raiz, new Vector3(-7.5f, 0f, -5.8f), 0f).GetComponent<SaidaDoPredio>().fatoQueBloqueia = "corrente";
        var corrente = Instanciar("Ponto de acao", raiz, new Vector3(-7.5f, 1f, -5.6f), 0f).GetComponent<PontoDeAcao>();
        corrente.id = "Recepcao/Corrente";
        corrente.tipo = TipoDeAcao.Corrente;
        corrente.repetivel = false;
        corrente.mostrar = Bloco(raiz, "Corrente (visual)", new Vector3(-7.5f, 1f, -5.75f), new Vector3(1.2f, 0.06f, 0.06f), Mat("Corrente", new Color(0.3f, 0.3f, 0.32f), 0.8f));
        corrente.mostrar.SetActive(false);
        Documento(raiz, "Documento D08", new Vector3(-9f, 1f, -2f), "D08", "Aviso sobre retiradas à noite");

        // Técnica: painel de energia, fusível reserva e o ponto do apagão.
        Instanciar("Painel de energia", raiz, new Vector3(11.8f, 1.4f, -2f), -90f);
        var apagao = Instanciar("Ponto de acao", raiz, new Vector3(11f, 1.4f, -2f), 0f).GetComponent<PontoDeAcao>();
        apagao.id = "Tecnica/Fusivel";
        apagao.tipo = TipoDeAcao.Apagao;
        apagao.repetivel = false;
        var fusivel = Bloco(raiz, "Fusivel reserva", new Vector3(9f, 1f, -5f), new Vector3(0.08f, 0.08f, 0.12f), Mat("Fusivel", new Color(0.8f, 0.6f, 0.2f), 0.4f));
        var item = fusivel.AddComponent<ItemColetavel>();
        item.id = "fusivel_reserva";
        item.textoAcao = "Pegar fusível reserva";
        item.aviso = "Fusível reserva guardado.";

        // Autópsia: a mesa (F01) com duas poses do braço, torneira de Daniel.
        var mesa = Bloco(raiz, "Mesa de autopsia", new Vector3(-7.5f, 0.45f, 6f), new Vector3(0.9f, 0.9f, 2.1f), Mat("Inox", new Color(0.7f, 0.72f, 0.74f), 0.8f));
        var ocupado = new GameObject("Ocupado");
        ocupado.transform.SetParent(mesa.transform.parent, false);
        ocupado.transform.position = mesa.transform.position;
        var corpo = Bloco(ocupado.transform, "Corpo sob o lencol", Vector3.up * 0.55f, new Vector3(0.6f, 0.25f, 1.8f), Mat("Lencol", new Color(0.9f, 0.9f, 0.88f), 0.1f), true);
        var bracoA = Bloco(ocupado.transform, "Braco pose A", new Vector3(0.38f, 0.55f, 0.2f), new Vector3(0.12f, 0.12f, 0.6f), Mat("Pele", new Color(0.75f, 0.62f, 0.55f), 0.2f), true);
        var bracoB = Bloco(ocupado.transform, "Braco pose B", new Vector3(0.42f, 0.5f, -0.35f), new Vector3(0.12f, 0.12f, 0.6f), Mat("Pele", new Color(0.75f, 0.62f, 0.55f), 0.2f), true);
        bracoB.transform.localRotation = Quaternion.Euler(0f, 35f, 0f);
        var alvo = ocupado.AddComponent<AlvoFotografico>();
        alvo.id = "mesa_autopsia";
        alvo.idEvidencia = "F01";
        alvo.legenda = "A pessoa na mesa de autópsia";
        alvo.centro = corpo.transform;
        alvo.poses = new[] { bracoA, bracoB };
        var vazia = Bloco(raiz, "Lencol no chao (P08)", new Vector3(-6.5f, 0.02f, 7.5f), new Vector3(1.2f, 0.02f, 0.9f), Mat("Lencol", new Color(0.9f, 0.9f, 0.88f), 0.1f));
        vazia.SetActive(false);
        var estadoMesa = mesa.AddComponent<EstadoMesaAutopsia>();
        var so = new SerializedObject(estadoMesa);
        so.FindProperty("ocupado").objectReferenceValue = ocupado;
        so.FindProperty("p08").objectReferenceValue = vazia;
        so.ApplyModifiedPropertiesWithoutUndo();
        var torneira = Instanciar("Ponto de acao", raiz, new Vector3(-11f, 1f, 8f), 0f).GetComponent<PontoDeAcao>();
        torneira.id = "Autopsia/Torneira";
        torneira.tipo = TipoDeAcao.Presenca;
        torneira.som = SomDeTeste(torneira.gameObject);

        // Câmara fria: corpo em C-02 (F02), esconderijo na gaveta vazia, armadilha e trava de segurança.
        var c02 = Bloco(raiz, "Gaveta C-02 (Leo)", new Vector3(11f, 0.9f, 4f), new Vector3(1.6f, 0.5f, 0.7f), Mat("Inox", new Color(0.7f, 0.72f, 0.74f), 0.8f));
        var alvoC02 = c02.AddComponent<AlvoFotografico>();
        alvoC02.id = "gaveta_c02";
        alvoC02.idEvidencia = "F02";
        alvoC02.legenda = "O corpo na gaveta 2, com a tala";
        var esconderijo = Instanciar("Esconderijo", raiz, new Vector3(11f, 0f, 8.5f), -90f);
        esconderijo.name = "Esconderijo (gaveta C-05)";
        var armadilha = Instanciar("Ponto de acao", raiz, new Vector3(3.5f, 1f, 6f), 0f).GetComponent<PontoDeAcao>();
        armadilha.id = "CamaraFria/Porta";
        armadilha.tipo = TipoDeAcao.PrenderCamaraFria;
        armadilha.repetivel = false;
        armadilha.porta = Porta(raiz, "Porta da camara fria", new Vector3(3f, 0f, 5.3f), parede);
        var luzCamara = LuzDoCircuito(raiz, "Luz da camara fria", new Vector3(7.5f, 2.6f, 6f), RedeEletrica.Refrigeracao, false, new Color(0.75f, 0.85f, 1f), 1.5f);
        armadilha.luzes = new[] { luzCamara.GetComponent<Light>() };
        Instanciar("Trava de seguranca", raiz, new Vector3(3.3f, 1.1f, 7f), 90f);

        // Arquivo: gravador, cópia do áudio, aparição de Daniel, documento D09.
        var mesaArquivo = Bloco(raiz, "Mesa do arquivo", new Vector3(-9f, 0.4f, 14f), new Vector3(1.2f, 0.8f, 0.6f), Mat("Madeira", new Color(0.4f, 0.3f, 0.2f), 0.2f));
        Instanciar("Gravador", raiz, mesaArquivo.transform.localPosition + new Vector3(-0.2f, 0.42f, 0f), 0f);
        Instanciar("Copia do audio", raiz, mesaArquivo.transform.localPosition + new Vector3(0.3f, 0.58f, 0.15f), 180f);
        var aparicao = Instanciar("Ponto de acao", raiz, new Vector3(-3.8f, 0f, 14f), 0f).GetComponent<PontoDeAcao>();
        aparicao.id = "Arquivo/Porta";
        aparicao.tipo = TipoDeAcao.Aparicao;
        Documento(raiz, "Documento D09", new Vector3(-10f, 0.81f, 14.2f), "D09", "Lista de pertences de Léo");

        // Descanso: armário para se esconder; passos no corredor.
        Instanciar("Esconderijo", raiz, new Vector3(11f, 0f, 14f), -90f).name = "Esconderijo (armario)";
        var passos = Instanciar("Ponto de acao", raiz, new Vector3(0f, 0.2f, 18f), 0f).GetComponent<PontoDeAcao>();
        passos.id = "Corredor/Passos";
        passos.som = SomDeTeste(passos.gameObject);

        // Pátio: saída dos fundos.
        Instanciar("Saida do predio", raiz, new Vector3(0f, 0f, 25.8f), 0f);
        Instanciar("Zona de sinal", raiz, new Vector3(0f, 0f, 22f), 0f);

        // NavMesh para a perseguição.
        var nav = raiz.gameObject.AddComponent<NavMeshSurface>();
        nav.collectObjects = CollectObjects.Children;
        nav.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        nav.BuildNavMesh();
        EditorSceneManager.SaveScene(cena, CenaTestes);
        if (nav.navMeshData != null)
        {
            string caminho = "Assets/Scenes/Testes/Jose/NavMesh.asset";
            GarantirPasta("Assets/Scenes/Testes/Jose");
            AssetDatabase.DeleteAsset(caminho);
            AssetDatabase.CreateAsset(nav.navMeshData, caminho);
            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("José: cena de testes em " + CenaTestes + ". Abra e dê Play (a Jogabilidade carrega junto).");
    }

    static GameObject Bloco(Transform raiz, string nome, Vector3 local, Vector3 tamanho, Material m, bool semCollider = false)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = nome;
        go.transform.SetParent(raiz, false);
        go.transform.localPosition = local;
        go.transform.localScale = tamanho;
        go.GetComponent<Renderer>().sharedMaterial = m;
        if (semCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static void SalaDeTeste(Transform raiz, string id, Vector3 centro, Vector3 tamanho)
    {
        var go = new GameObject("Sala " + id);
        go.transform.SetParent(raiz, false);
        go.transform.localPosition = centro;
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = new Vector3(0f, tamanho.y / 2f, 0f);
        box.size = tamanho;
        go.AddComponent<Sala>().id = id;
    }

    static GameObject LuzDoCircuito(Transform raiz, string nome, Vector3 local, string circuito, bool inverter, Color cor, float intensidade)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(raiz, false);
        go.transform.localPosition = local;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = 12f;
        l.color = cor;
        l.intensity = intensidade;
        var c = go.AddComponent<ConsumidorDeEnergia>();
        c.circuito = circuito;
        c.inverter = inverter;
        c.luzes = new[] { l };
        return go;
    }

    static GameObject Instanciar(string prefab, Transform raiz, Vector3 local, float giro)
    {
        var p = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab(prefab));
        var go = (GameObject)PrefabUtility.InstantiatePrefab(p, raiz.gameObject.scene);
        go.transform.SetParent(raiz, false);
        go.transform.localPosition = local;
        go.transform.localRotation = Quaternion.Euler(0f, giro, 0f);
        return go;
    }

    static void Documento(Transform raiz, string nome, Vector3 local, string evidencia, string titulo)
    {
        var go = Instanciar("Documento legivel", raiz, local, 0f);
        go.name = nome;
        var o = go.GetComponent<ObjetoInspecionavel>();
        o.idEvidencia = evidencia;
        o.nome = titulo;
        Evidencia e = CatalogoDeEvidencias.Padrao.Buscar(evidencia);
        if (e != null) o.descricao = e.resumo;
    }

    static AudioSource SomDeTeste(GameObject go)
    {
        var a = go.AddComponent<AudioSource>();
        a.playOnAwake = false;
        a.spatialBlend = 1f;
        return a;
    }

    static FolhaDePorta Porta(Transform raiz, string nome, Vector3 dobradica, Material m)
    {
        var pivo = new GameObject(nome);
        pivo.transform.SetParent(raiz, false);
        pivo.transform.localPosition = dobradica;
        var folha = Bloco(pivo.transform, "Folha", new Vector3(0f, 1.05f, 0.7f), new Vector3(0.08f, 2.1f, 1.4f), m);
        folha.name = "Folha";
        var porta = pivo.AddComponent<FolhaDePorta>();
        porta.anguloMaximo = 100f;
        return porta;
    }

    // ================================================================== verificação

    [MenuItem("Tools/Unclaimed/José/Verificar evidências")]
    public static void VerificarEvidencias()
    {
        var catalogo = CatalogoDeEvidencias.Padrao;
        var achadas = new Dictionary<string, List<string>>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string caminho = AssetDatabase.GUIDToAssetPath(guid);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(caminho);
            if (go == null) continue;
            foreach (var o in go.GetComponentsInChildren<ObjetoInspecionavel>(true)) Anotar(achadas, o.idEvidencia, caminho);
            foreach (var a in go.GetComponentsInChildren<AlvoFotografico>(true)) Anotar(achadas, a.idEvidencia, caminho);
        }
        var linhas = new List<string> { "Evidências do catálogo e onde aparecem (prefabs):" };
        foreach (Evidencia e in catalogo.evidencias)
        {
            string onde = achadas.TryGetValue(e.id, out var l) ? string.Join(", ", l.Select(Path.GetFileNameWithoutExtension)) :
                e.id == "D04" ? "(painel de energia)" : e.id == "D07" ? "(computador da recepção)" : e.id == "D13" ? "(gravador)" :
                e.id.StartsWith("F") ? "(foto: AlvoFotografico no mapa)" : e.id.StartsWith("V") ? "(observação)" : "FALTA";
            linhas.Add($"  {e.id} {e.titulo}: {onde}");
        }
        foreach (var par in achadas)
            if (catalogo.Buscar(par.Key) == null) linhas.Add($"  ID DESCONHECIDO {par.Key}: {string.Join(", ", par.Value)}");
        Debug.Log(string.Join("\n", linhas));
    }

    static void Anotar(Dictionary<string, List<string>> d, string id, string caminho)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (!d.TryGetValue(id, out var l)) d[id] = l = new List<string>();
        if (!l.Contains(caminho)) l.Add(caminho);
    }
}
