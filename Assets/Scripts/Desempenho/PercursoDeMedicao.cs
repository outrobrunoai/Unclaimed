using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-1000)]
public sealed class PercursoDeMedicao : MonoBehaviour
{
    [Serializable] public sealed class Acao
    {
        public string tipo;
        public string alvo;
        [NonSerialized] public Component componente;
    }

    [Serializable] public sealed class Passo
    {
        public float t;
        public float[] pos;
        public float yaw;
        public float pitch;
        public string segmento;
        public Acao[] acoes;
        [NonSerialized] public int indiceSegmento;
        public Vector3 Posicao => new Vector3(pos[0], pos[1], pos[2]);
    }

    [Serializable] public sealed class Percurso
    {
        public float aquecimento;
        public Passo[] passos;
    }

    [Serializable] public sealed class EstatisticaContador
    {
        public string categoria;
        public string nome;
        public string unidade;
        public double media;
        public double maximo;
    }

    [Serializable] public sealed class Estatistica
    {
        public string segmento;
        public int quadros;
        public double duracaoSegundos;
        public double fpsMedio;
        public double quadroMediaMs;
        public double quadroP50Ms;
        public double quadroP95Ms;
        public double quadroP99Ms;
        public double quadroMaxMs;
        public double umPorCentoLowFps;
        public int picos;
        public double cpuMediaMs;
        public double mainMediaMs;
        public double renderMediaMs;
        public double gpuMediaMs;
        public int cpuAmostras;
        public int mainAmostras;
        public int renderAmostras;
        public int gpuAmostras;
        public bool gcAlocacaoDisponivel;
        public long gcBytesAlocados;
        public double gcMediaBytesPorQuadro;
        public long gcColecoes;
        public EstatisticaContador[] contadores;
    }

    [Serializable] public sealed class Resumo
    {
        public string rotulo;
        public string dataHora;
        public string graphicsDeviceType;
        public string graphicsDeviceName;
        public string resolucao;
        public int vSyncCount;
        public int targetFrameRate;
        public bool isDebugBuild;
        public bool isEditor;
        public string versaoUnity;
        public string motivoTermino;
        public string[] contadoresIndisponiveis;
        public Estatistica total;
        public Estatistica[] segmentos;
    }

    struct Quadro
    {
        public double tempo;
        public int segmento;
        public double deltaMs;
        public double cpu, main, render, gpu;
        public int colecoes;
    }

    sealed class Contador
    {
        public ProfilerCategory categoria;
        public string nomeCategoria, nome, unidade;
        public ProfilerRecorder recorder;
        public bool disponivel;

        public Contador(ProfilerCategory categoria, string nomeCategoria, string nome, string unidade)
        {
            this.categoria = categoria;
            this.nomeCategoria = nomeCategoria;
            this.nome = nome;
            this.unidade = unidade;
        }
    }

    const int Capacidade = 200000;   // ~150 s a mais de 1000 FPS
    static readonly CultureInfo Invariante = CultureInfo.InvariantCulture;
    static PercursoDeMedicao instancia;
    public static bool Concluido { get; private set; }
    public static string UltimoResumoJson { get; private set; }
    public static string UltimoCaminho { get; private set; }

    Percurso percurso;
    Movimento movimento;
    CharacterController controlador;
    Transform jogador, cameraJogador;
    Quadro[] quadros;
    long[] valores;
    Contador[] contadores;
    readonly FrameTiming[] timing = new FrameTiming[1];
    readonly List<string> segmentos = new List<string>();
    string pasta, rotulo, dataHora;
    string arquivoProfiler;
    readonly List<Vector2> janelasProfiler = new List<Vector2>();
    double aquecimentoDecorrido, tempo;
    int proximaAcao, quantidade, gcAnterior;
    bool gravando, ativo, sair, terminar;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar()
    {
        instancia = null;
        Concluido = false;
        UltimoResumoJson = null;
        UltimoCaminho = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void IniciarPelaLinhaDeComando()
    {
        string[] args = Environment.GetCommandLineArgs();
        if (Array.IndexOf(args, "-percurso-medicao") < 0) return;
        AplicarVariantes(Argumento(args, "-percurso-variante"));
        var medicao = Iniciar(Argumento(args, "-percurso-saida"), Argumento(args, "-percurso-rotulo"));
        if (medicao == null) return;
        medicao.sair = Array.IndexOf(args, "-percurso-sair") >= 0;
        // Build Development: grava o profiler só nas janelas pedidas ("49-53,68.5-71.5"), para achar picos
        // sem o arquivo de vários GB do percurso inteiro.
        medicao.arquivoProfiler = Argumento(args, "-percurso-profiler");
        string janelas = Argumento(args, "-percurso-profiler-janelas");
        if (medicao.arquivoProfiler != null && janelas != null)
            foreach (string janela in janelas.Split(','))
            {
                string[] limites = janela.Split('-');
                medicao.janelasProfiler.Add(new Vector2(float.Parse(limites[0], Invariante), float.Parse(limites[1], Invariante)));
            }
    }

    /// <summary>
    /// Desliga partes da cena só para diagnóstico (atribuir custo comparando com a medição normal).
    /// Lista separada por vírgulas; nada é salvo.
    /// </summary>
    public static void AplicarVariantes(string lista)
    {
        if (string.IsNullOrEmpty(lista)) return;
        var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        foreach (string v in lista.Split(','))
        {
            switch (v.Trim())
            {
                case "sem_sombras_adicionais":
                    foreach (Light l in FindObjectsByType<Light>()) if (l.type != LightType.Directional) l.shadows = LightShadows.None;
                    break;
                case "sem_sombra_lua":
                    foreach (Light l in FindObjectsByType<Light>()) if (l.type == LightType.Directional) l.shadows = LightShadows.None;
                    break;
                case "lua_2cascatas": if (urp) urp.shadowCascadeCount = 2; break;
                case "sem_opaque": if (urp) urp.supportsCameraOpaqueTexture = false; break;
                case "ceu_textura": if (RenderSettings.skybox) RenderSettings.skybox.EnableKeyword("_NUVENS_TEXTURA"); break;
                case "sem_vegetacao": Desligar("Unclaimed - Necroterio (planta)/Area externa/Vegetacao"); break;
                case "sem_gramado": Desligar("Unclaimed - Necroterio (planta)/Area externa/Vegetacao/Gramado volumetrico"); break;
                case "sem_arvores": Desligar("Unclaimed - Necroterio (planta)/Area externa/Vegetacao/Arvores"); break;
                case "sem_silhuetas": Desligar("Unclaimed - Necroterio (planta)/Area externa/Vegetacao/Silhuetas distantes"); break;
                case "sem_forracao": Desligar("Unclaimed - Necroterio (planta)/Area externa/Vegetacao/Forracao"); break;
                case "sem_vizinhanca": Desligar("Unclaimed - Necroterio (planta)/Area externa/Vizinhanca"); break;
                case "sem_chuva": Desligar("Chuva leve"); break;
                default: Debug.LogWarning("Variante de medição desconhecida: " + v); break;
            }
        }
        Debug.Log("Variantes de medição: " + lista);
    }

    static void Desligar(string caminho)
    {
        GameObject alvo = Encontrar(caminho);
        if (alvo != null) alvo.SetActive(false);
        else Debug.LogWarning("Variante: objeto não encontrado: " + caminho);
    }

    static string Argumento(string[] args, string chave)
    {
        int indice = Array.IndexOf(args, chave);
        return indice >= 0 && indice + 1 < args.Length && !args[indice + 1].StartsWith("-percurso-")
            ? args[indice + 1] : null;
    }

    public static PercursoDeMedicao Iniciar(string pastaSaida, string rotulo)
    {
        if (instancia != null) return instancia;
        Concluido = false;
        UltimoResumoJson = null;
        UltimoCaminho = null;
        var objeto = new GameObject("Percurso de medição");
        var medicao = objeto.AddComponent<PercursoDeMedicao>();
        instancia = medicao;
        try
        {
            medicao.Preparar(pastaSaida, rotulo);
            return medicao;
        }
        catch (Exception erro)
        {
            Debug.LogError("Não foi possível iniciar o percurso de medição: " + erro);
            medicao.Liberar();
            instancia = null;
            Destroy(objeto);
            return null;
        }
    }

    void Preparar(string pastaSaida, string nome)
    {
        var recurso = Resources.Load<TextAsset>("PercursoMedicao");
        if (recurso == null) throw new InvalidOperationException("Recurso PercursoMedicao não encontrado.");
        percurso = JsonUtility.FromJson<Percurso>(recurso.text);
        if (percurso == null || percurso.passos == null || percurso.passos.Length < 2 ||
            !Finito(percurso.aquecimento) || percurso.aquecimento < 0)
            throw new InvalidOperationException("Percurso ou aquecimento inválido.");
        for (int i = 0; i < percurso.passos.Length; i++)
        {
            Passo passo = percurso.passos[i];
            if (passo == null || passo.pos == null || passo.pos.Length != 3 || !Finito(passo.t) ||
                !Finito(passo.yaw) || !Finito(passo.pitch) || !Finito(passo.pos[0]) ||
                !Finito(passo.pos[1]) || !Finito(passo.pos[2]) ||
                (i == 0 ? passo.t != 0 : passo.t <= percurso.passos[i - 1].t))
                throw new InvalidOperationException("Passo inválido: " + i);
            string segmento = passo.segmento ?? "sem segmento";
            int indice = segmentos.IndexOf(segmento);
            if (indice < 0) { indice = segmentos.Count; segmentos.Add(segmento); }
            passo.indiceSegmento = indice;
            if (passo.acoes != null)
                foreach (Acao acao in passo.acoes) PrepararAcao(acao);
        }
        foreach (GameObject raiz in SceneManager.GetActiveScene().GetRootGameObjects())
            if (raiz.name == "Player") { jogador = raiz.transform; break; }
        if (jogador == null) throw new InvalidOperationException("Raiz Player não encontrada.");
        movimento = jogador.GetComponent<Movimento>();
        controlador = jogador.GetComponent<CharacterController>();
        if (movimento == null || movimento.camera == null || controlador == null)
            throw new InvalidOperationException("Player precisa de Movimento, câmera e CharacterController.");
        cameraJogador = movimento.camera;
        pasta = string.IsNullOrWhiteSpace(pastaSaida) ? Application.persistentDataPath : pastaSaida;
        rotulo = string.IsNullOrWhiteSpace(nome) ? "medicao" : nome;
        dataHora = DateTimeOffset.Now.ToString("o", Invariante);
        Directory.CreateDirectory(pasta);
        PrepararContadores();
        quadros = new Quadro[Capacidade];
        valores = new long[Capacidade * contadores.Length];
        gcAnterior = GC.CollectionCount(0);
        ativo = true;
        BloquearMovimento();
        AplicarPose(0);
    }

    static bool Finito(float valor) => !float.IsNaN(valor) && !float.IsInfinity(valor);

    void PrepararContadores()
    {
        var lista = new List<Contador>();
        foreach (string nome in new[] { "SetPass Calls Count", "Draw Calls Count", "Batches Count", "Triangles Count",
            "Vertices Count", "Shadow Casters Count", "Visible Skinned Meshes Count" })
            lista.Add(new Contador(ProfilerCategory.Render, "Render", nome, "contagem"));
        foreach (string nome in new[] { "GC Allocated In Frame", "GC Used Memory", "GC Reserved Memory", "Total Used Memory",
            "System Used Memory", "Texture Memory", "Mesh Memory", "Render Textures Bytes" })
            lista.Add(new Contador(ProfilerCategory.Memory, "Memory", nome, "bytes"));
        lista.Add(new Contador(ProfilerCategory.Internal, "Internal", "Main Thread", "ms"));
        contadores = lista.ToArray();
        foreach (Contador contador in contadores)
        {
            try
            {
                contador.recorder = ProfilerRecorder.StartNew(contador.categoria, contador.nome, 1);
                contador.disponivel = contador.recorder.Valid;
            }
            catch (Exception) { contador.disponivel = false; }
        }
    }

    void Update()
    {
        if (!ativo) return;
        BloquearMovimento();
        if (!gravando)
        {
            AplicarPose(0);
            // Quadros de carregamento (o primeiro traz o load da cena) não contam como aquecimento.
            if (Time.unscaledDeltaTime < 0.25f) aquecimentoDecorrido += Time.unscaledDeltaTime;
            if (aquecimentoDecorrido < percurso.aquecimento) return;
            gravando = true;
            gcAnterior = GC.CollectionCount(0);
            DispararAcoes();
            // O primeiro intervalo medido começa depois do aquecimento.
            return;
        }
        tempo += Time.unscaledDeltaTime;
        DispararAcoes();
        BloquearMovimento();
        AplicarPose(tempo);
        AtualizarProfiler();
        terminar = tempo >= percurso.passos[percurso.passos.Length - 1].t;
    }

    void LateUpdate()
    {
        if (!ativo) return;
        BloquearMovimento();
        if (!gravando || tempo <= 0) return;
        GravarQuadro();
        if (terminar || quantidade == Capacidade)
            Concluir(terminar ? "percurso concluído" : "capacidade de quadros atingida");
    }

    void AtualizarProfiler()
    {
        if (janelasProfiler.Count == 0) return;
        bool dentro = false;
        foreach (Vector2 janela in janelasProfiler) dentro |= tempo >= janela.x && tempo <= janela.y;
        if (dentro == UnityEngine.Profiling.Profiler.enabled) return;
        if (dentro && !UnityEngine.Profiling.Profiler.enableBinaryLog)
        {
            UnityEngine.Profiling.Profiler.maxUsedMemory = 256 * 1024 * 1024;
            UnityEngine.Profiling.Profiler.logFile = arquivoProfiler;
            UnityEngine.Profiling.Profiler.enableBinaryLog = true;
        }
        UnityEngine.Profiling.Profiler.enabled = dentro;
    }

    void BloquearMovimento()
    {
        if (movimento != null) movimento.enabled = false;
        if (controlador != null) controlador.enabled = false;
    }

    int ProximoPasso(double instante)
    {
        int indice = 1;
        while (indice < percurso.passos.Length - 1 && instante >= percurso.passos[indice].t) indice++;
        return indice;
    }

    void AplicarPose(double instante)
    {
        int indice = ProximoPasso(instante);
        Passo anterior = percurso.passos[indice - 1], seguinte = percurso.passos[indice];
        float fator = Mathf.Clamp01((float)((instante - anterior.t) / (seguinte.t - anterior.t)));
        Quaternion yaw = Quaternion.Euler(0, Mathf.LerpAngle(anterior.yaw, seguinte.yaw, fator), 0);
        jogador.SetPositionAndRotation(Vector3.Lerp(anterior.Posicao, seguinte.Posicao, fator) -
            yaw * cameraJogador.localPosition, yaw);
        cameraJogador.localRotation = Quaternion.Euler(Mathf.Lerp(anterior.pitch, seguinte.pitch, fator), 0, 0);
    }

    void PrepararAcao(Acao acao)
    {
        if (acao == null) return;
        GameObject alvo = Encontrar(acao.alvo);
        if (alvo != null)
        {
            switch (acao.tipo)
            {
                case "abrirPorta": case "fecharPorta": case "destravarPorta":
                    acao.componente = alvo.GetComponentInChildren<FolhaDePorta>(true); break;
                case "abrirPeca": case "fecharPeca":
                    acao.componente = alvo.GetComponentInChildren<PecaDeMovel>(true); break;
                case "inspecionar": case "encerrarInspecao": acao.componente = alvo.GetComponent<ObjetoInspecionavel>(); break;
                case "abrirInterface": case "fecharInterface": acao.componente = alvo.GetComponent<InterfaceDeTela>(); break;
            }
        }
        if (acao.componente == null) Debug.LogWarning("Ação de medição indisponível: " + acao.tipo + " em " + acao.alvo);
    }

    static GameObject Encontrar(string caminho)
    {
        if (string.IsNullOrEmpty(caminho)) return null;
        int barra = caminho.IndexOf('/');
        string raizNome = barra < 0 ? caminho : caminho.Substring(0, barra);
        foreach (GameObject raiz in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (raiz.name != raizNome) continue;
            Transform alvo = barra < 0 ? raiz.transform : raiz.transform.Find(caminho.Substring(barra + 1));
            return alvo != null ? alvo.gameObject : null;
        }
        return null;
    }

    void DispararAcoes()
    {
        while (proximaAcao < percurso.passos.Length && tempo >= percurso.passos[proximaAcao].t)
        {
            Acao[] acoes = percurso.passos[proximaAcao++].acoes;
            if (acoes == null) continue;
            foreach (Acao acao in acoes)
            {
                if (acao == null || acao.componente == null) continue;
                try
                {
                    switch (acao.tipo)
                    {
                        case "abrirPorta": ((FolhaDePorta)acao.componente).Abrir(); break;
                        case "fecharPorta": ((FolhaDePorta)acao.componente).Fechar(); break;
                        case "destravarPorta": ((FolhaDePorta)acao.componente).Destravar(); break;
                        case "abrirPeca": ((PecaDeMovel)acao.componente).Abrir(); break;
                        case "fecharPeca": ((PecaDeMovel)acao.componente).Fechar(); break;
                        case "inspecionar": ((ObjetoInspecionavel)acao.componente).IniciarInspecao(); break;
                        case "encerrarInspecao": ((ObjetoInspecionavel)acao.componente).EncerrarInspecao(); break;
                        case "abrirInterface": ((InterfaceDeTela)acao.componente).AbrirInterface(); break;
                        case "fecharInterface": ((InterfaceDeTela)acao.componente).FecharInterface(); break;
                    }
                }
                catch (Exception erro) { Debug.LogWarning("Falha na ação de medição " + acao.tipo + ": " + erro.Message); }
            }
        }
    }

    void GravarQuadro()
    {
        FrameTimingManager.CaptureFrameTimings();
        // Os timings e LastValue correspondem ao último quadro disponibilizado pelo Unity.
        bool temTiming = FrameTimingManager.GetLatestTimings(1, timing) > 0;
        int gcAtual = GC.CollectionCount(0);
        quadros[quantidade] = new Quadro
        {
            tempo = tempo,
            segmento = percurso.passos[ProximoPasso(tempo)].indiceSegmento,
            deltaMs = Time.unscaledDeltaTime * 1000.0,
            cpu = temTiming ? timing[0].cpuFrameTime : 0,
            main = temTiming ? timing[0].cpuMainThreadFrameTime : 0,
            render = temTiming ? timing[0].cpuRenderThreadFrameTime : 0,
            gpu = temTiming ? timing[0].gpuFrameTime : 0,
            colecoes = Math.Max(0, gcAtual - gcAnterior)
        };
        gcAnterior = gcAtual;
        for (int i = 0; i < contadores.Length; i++)
            if (contadores[i].disponivel) valores[quantidade * contadores.Length + i] = contadores[i].recorder.LastValue;
        quantidade++;
    }

    void Concluir(string motivo)
    {
        ativo = false;
        try
        {
            var indisponiveis = new List<string>();
            foreach (Contador contador in contadores)
                if (!contador.disponivel) indisponiveis.Add(contador.nomeCategoria + "/" + contador.nome);
            var resumo = new Resumo
            {
                rotulo = rotulo, dataHora = dataHora,
                graphicsDeviceType = SystemInfo.graphicsDeviceType.ToString(), graphicsDeviceName = SystemInfo.graphicsDeviceName,
                resolucao = Screen.width + "×" + Screen.height, vSyncCount = QualitySettings.vSyncCount,
                targetFrameRate = Application.targetFrameRate, isDebugBuild = Debug.isDebugBuild,
                isEditor = Application.isEditor, versaoUnity = Application.unityVersion, motivoTermino = motivo,
                contadoresIndisponiveis = indisponiveis.ToArray(), total = Calcular(-1), segmentos = new Estatistica[segmentos.Count]
            };
            // O total de picos usa o limiar de cada segmento, assim como os subtotais.
            resumo.total.picos = 0;
            for (int i = 0; i < segmentos.Count; i++)
            {
                resumo.segmentos[i] = Calcular(i);
                resumo.total.picos += resumo.segmentos[i].picos;
            }
            string nomeSeguro = NomeSeguro(rotulo);
            string baseCaminho = Path.Combine(pasta, "percurso_" + nomeSeguro + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", Invariante));
            // Evita sobrescrever uma execução iniciada no mesmo segundo.
            if (File.Exists(baseCaminho + ".json") || File.Exists(baseCaminho + ".csv"))
                throw new IOException("Já existe uma medição com este rótulo e horário: " + baseCaminho);
            string json = JsonUtility.ToJson(resumo, true);
            EscreverCsv(baseCaminho + ".csv");
            File.WriteAllText(baseCaminho + ".json", json, new UTF8Encoding(false));
            UltimoResumoJson = json;
            UltimoCaminho = baseCaminho + ".json";
            Concluido = true;
            Debug.Log("Medição: " + UltimoCaminho + " | FPS médio total: " + resumo.total.fpsMedio.ToString("F2", Invariante));
        }
        catch (Exception erro) { Debug.LogError("Falha ao salvar medição: " + erro); }
        finally
        {
            Liberar();
            instancia = null;
            Destroy(gameObject);
        }
        if (sair) Application.Quit(Concluido ? 0 : 1);
    }

    Estatistica Calcular(int segmento)
    {
        var resultado = new Estatistica { segmento = segmento < 0 ? "total" : segmentos[segmento] };
        var tempos = new List<double>();
        double[] somas = new double[contadores.Length], maximos = new double[contadores.Length];
        for (int i = 0; i < quantidade; i++)
        {
            Quadro q = quadros[i];
            if (segmento >= 0 && q.segmento != segmento) continue;
            resultado.quadros++;
            resultado.duracaoSegundos += q.deltaMs / 1000;
            resultado.gcColecoes += q.colecoes;
            tempos.Add(q.deltaMs);
            if (q.cpu > 0) { resultado.cpuMediaMs += q.cpu; resultado.cpuAmostras++; }
            if (q.main > 0) { resultado.mainMediaMs += q.main; resultado.mainAmostras++; }
            if (q.render > 0) { resultado.renderMediaMs += q.render; resultado.renderAmostras++; }
            if (q.gpu > 0 && q.gpu < 1000) { resultado.gpuMediaMs += q.gpu; resultado.gpuAmostras++; }   // o Vulkan às vezes devolve lixo
            for (int c = 0; c < contadores.Length; c++)
            {
                long valor = valores[i * contadores.Length + c];
                double convertido = contadores[c].unidade == "ms" ? valor / 1000000.0 : valor;
                somas[c] += convertido;
                maximos[c] = Math.Max(maximos[c], convertido);
                if (contadores[c].nome == "GC Allocated In Frame" && contadores[c].disponivel)
                {
                    resultado.gcAlocacaoDisponivel = true;
                    resultado.gcBytesAlocados += valor;
                }
            }
        }
        resultado.cpuMediaMs /= Math.Max(1, resultado.cpuAmostras);
        resultado.mainMediaMs /= Math.Max(1, resultado.mainAmostras);
        resultado.renderMediaMs /= Math.Max(1, resultado.renderAmostras);
        resultado.gpuMediaMs /= Math.Max(1, resultado.gpuAmostras);
        var estatisticas = new List<EstatisticaContador>();
        for (int c = 0; c < contadores.Length; c++)
            if (contadores[c].disponivel)
                estatisticas.Add(new EstatisticaContador { categoria = contadores[c].nomeCategoria, nome = contadores[c].nome,
                    unidade = contadores[c].unidade, media = somas[c] / Math.Max(1, resultado.quadros), maximo = maximos[c] });
        resultado.contadores = estatisticas.ToArray();
        if (resultado.quadros == 0) return resultado;
        tempos.Sort();
        resultado.fpsMedio = resultado.duracaoSegundos > 0 ? resultado.quadros / resultado.duracaoSegundos : 0;
        resultado.quadroMediaMs = resultado.duracaoSegundos * 1000 / resultado.quadros;
        resultado.quadroP50Ms = Percentil(tempos, 0.50);
        resultado.quadroP95Ms = Percentil(tempos, 0.95);
        resultado.quadroP99Ms = Percentil(tempos, 0.99);
        resultado.quadroMaxMs = tempos[tempos.Count - 1];
        int piores = Math.Max(1, (int)Math.Ceiling(tempos.Count * 0.01));
        double somaPiores = 0;
        for (int i = tempos.Count - piores; i < tempos.Count; i++) somaPiores += tempos[i];
        resultado.umPorCentoLowFps = somaPiores > 0 ? 1000 * piores / somaPiores : 0;
        foreach (double ms in tempos)
            if (ms > 2 * resultado.quadroP50Ms && ms > 1000.0 / 60) resultado.picos++;
        resultado.gcMediaBytesPorQuadro = (double)resultado.gcBytesAlocados / resultado.quadros;
        return resultado;
    }

    static double Percentil(List<double> ordenados, double proporcao)
    {
        double indice = (ordenados.Count - 1) * proporcao;
        int inferior = (int)indice;
        return ordenados[inferior] + (ordenados[Math.Min(inferior + 1, ordenados.Count - 1)] - ordenados[inferior]) * (indice - inferior);
    }

    static string NomeSeguro(string nome)
    {
        var texto = new StringBuilder(nome);
        char[] invalidos = Path.GetInvalidFileNameChars();
        for (int i = 0; i < texto.Length; i++)
            if (Array.IndexOf(invalidos, texto[i]) >= 0 || texto[i] == '/' || texto[i] == '\\' || char.IsControl(texto[i])) texto[i] = '_';
        return texto.ToString();
    }

    static string Csv(string texto) => "\"" + texto.Replace("\"", "\"\"") + "\"";
    static string Numero(double numero) => numero.ToString("R", Invariante);

    void EscreverCsv(string caminho)
    {
        using (var escritor = new StreamWriter(caminho, false, new UTF8Encoding(false)))
        {
            escritor.Write("indice,tempo_s,segmento,unscaledDeltaTime_ms,cpuFrameTime_ms,cpuMainThreadFrameTime_ms,cpuRenderThreadFrameTime_ms,gpuFrameTime_ms,gc_colecoes");
            foreach (Contador contador in contadores) escritor.Write("," + Csv(contador.nomeCategoria + "/" + contador.nome + " (" + contador.unidade + ")"));
            escritor.WriteLine();
            for (int i = 0; i < quantidade; i++)
            {
                Quadro q = quadros[i];
                escritor.Write(i.ToString(Invariante) + "," + Numero(q.tempo) + "," + Csv(segmentos[q.segmento]) + "," + Numero(q.deltaMs));
                foreach (double valor in new[] { q.cpu, q.main, q.render, q.gpu }) escritor.Write("," + (valor > 0 ? Numero(valor) : ""));
                escritor.Write("," + q.colecoes.ToString(Invariante));
                for (int c = 0; c < contadores.Length; c++)
                {
                    escritor.Write(',');
                    if (!contadores[c].disponivel) continue;
                    long valor = valores[i * contadores.Length + c];
                    escritor.Write(contadores[c].unidade == "ms" ? Numero(valor / 1000000.0) : valor.ToString(Invariante));
                }
                escritor.WriteLine();
            }
        }
    }

    void Liberar()
    {
        ativo = false;
        if (contadores != null)
            foreach (Contador contador in contadores) contador.recorder.Dispose();
        contadores = null;
        if (movimento != null) movimento.enabled = true;
        if (controlador != null) controlador.enabled = true;
    }

    void OnDisable()
    {
        Liberar();
        if (instancia == this) instancia = null;
    }
}
