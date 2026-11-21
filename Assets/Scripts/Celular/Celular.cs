using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Celular do jornalista (tecla T; fica no Player, cena Jogabilidade). O modelo 3D (CelularNaMao) sai do bolso
/// com uma animação; a interface (CelularTela) fica na própria tela do modelo, recortada nos cantos e girando
/// junto quando o celular deita. Apps: Mensagens, Câmera, Galeria e Redação (envio de provas).
/// No modo câmera o celular deita na frente do rosto e o jogador continua andando: clique ou F tira a foto,
/// Q ou botão direito volta aos apps, T guarda. A lanterna fica no painel rápido (toque na barra de cima) ou na
/// tecla L. Nenhuma dica de onde há sinal: o jogador descobre.
/// </summary>
public class Celular : PainelDoJogador, ISalvavel
{
    public string tecla = "T";
    public string teclaFoto = "F", teclaApps = "Q";
    [Tooltip("Prefab \"Celular na mao\" (Assets/Prefabs/Sistemas/Celular). Criado na câmera do jogador ao começar.")]
    public CelularNaMao prefabNaMao;
    public AudioSource fonte;
    public AudioClip somMensagem, somEnviado, somPuxar, somGuardar;

    [Header("Lanterna (tecla L ou no painel rápido do celular)")]
    public string teclaLanterna = "L";
    [Tooltip("Quanto a lanterna clareia. Pode mexer com o jogo rodando.")]
    [Range(0f, 30f)] public float intensidadeLanterna = 4f;
    [Tooltip("Até onde a luz chega (m).")]
    [Range(1f, 40f)] public float alcanceLanterna = 14f;
    [Tooltip("Abertura do facho (graus).")]
    [Range(10f, 120f)] public float anguloLanterna = 60f;
    [Tooltip("Parte central mais forte do facho (graus, menor que a abertura).")]
    [Range(1f, 120f)] public float anguloInternoLanterna = 30f;
    public Color corLanterna = new Color(1f, 0.96f, 0.88f);
    public LightShadows sombrasLanterna = LightShadows.Soft;
    [Tooltip("Textura opcional do facho (cookie), para dar o desenho de LED.")]
    public Texture cookieLanterna;
    public AudioClip somLanterna;

    /// <summary>A lanterna do celular está acesa (Daniel enxerga a luz de longe).</summary>
    public static bool LanternaLigada { get; private set; }
    public static Celular Instancia => instancia;

    static Celular instancia;
    static readonly RaycastHit[] acertosLanterna = new RaycastHit[32];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar()
    {
        LanternaLigada = false;
        instancia = null;
    }

    /// <summary>Verdadeiro quando a lanterna do celular está acesa e ilumina o ponto sem obstáculo sólido no caminho.</summary>
    public static bool LanternaIlumina(Vector3 ponto, float distanciaMaxima)
    {
        if (instancia == null || !LanternaLigada || instancia.luzLanterna == null || !instancia.luzLanterna.enabled) return false;
        if (distanciaMaxima < 0f) return false;
        Transform luz = instancia.luzLanterna.transform;
        Vector3 para = ponto - luz.position;
        float distancia = para.magnitude;
        if (distancia > distanciaMaxima) return false;
        if (distancia <= 0.0001f) return true;
        if (Vector3.Angle(luz.forward, para) > instancia.luzLanterna.spotAngle * 0.5f) return false;

        int qtd = Physics.RaycastNonAlloc(luz.position, para / distancia, acertosLanterna, distancia, ~(1 << 2), QueryTriggerInteraction.Ignore);
        for (int i = 0; i < qtd; i++)
        {
            Collider col = acertosLanterna[i].collider;
            if (col == null) continue;
            if (col.transform.IsChildOf(instancia.transform)) continue;
            if (instancia.naMao != null && col.transform.IsChildOf(instancia.naMao.transform)) continue;
            if (col.GetComponentInParent<Movimento>() != null) continue;
            if (distancia - acertosLanterna[i].distance < 0.15f) continue;
            return false;
        }
        return true;
    }

    public override string TeclaDoPainel => tecla;
    // O celular não pausa nada: o jogador anda com ele na mão (só o olhar fica com o mouse na tela).
    protected override bool TravaOJogador => false;
    protected override bool PodeAbrir =>
        !modoCamera && !TelaDeFinal.Ativa && !DanielPerseguidor.Capturando && !IntroChegada.Ativa && !Esconderijo.JogadorEscondido;

    public bool Lanterna => lanterna;
    public bool ModoCamera => modoCamera;
    public CelularNaMao NaMao => naMao;
    public CameraFotografica CameraDoCelular => cameraDoCelular;
    public float DisparoEm => disparoEm;

    /// <summary>Mensagens já vistas na conversa (para o contador de não lidas).</summary>
    public readonly HashSet<string> lidas = new HashSet<string>();

    bool lanterna, modoCamera, guardandoParaFechar, guardadoNaHora, montado;
    float disparoEm = -9f, avisoAte, dicaCameraAte;
    int vezesNaCamera;
    string aviso = "";
    Light luzLanterna;
    CelularNaMao naMao;
    CelularTela telaDoCelular;
    CameraFotografica cameraDoCelular;

    void Start() => GarantirMontado();

    void GarantirMontado()
    {
        if (montado) return;
        montado = true;
        CelularLogica.ReceberIniciais();
        cameraDoCelular = GetComponent<CameraFotografica>();
        UiMenu.GarantirEventSystem();
        if (prefabNaMao != null)
        {
            naMao = Instantiate(prefabNaMao);
            telaDoCelular = naMao.gameObject.AddComponent<CelularTela>();
            telaDoCelular.Construir(this, naMao);
        }
    }

    void OnEnable()
    {
        instancia = this;
        CelularLogica.AoReceber += AoReceber;
        CelularLogica.AoEnviar += AoEnviar;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        CelularLogica.AoReceber -= AoReceber;
        CelularLogica.AoEnviar -= AoEnviar;
        if (instancia == this) instancia = null;
        LanternaLigada = false;
    }

    void OnDestroy()
    {
        if (naMao != null) Destroy(naMao.gameObject);
    }

    // Com o celular no bolso, só a vibração avisa: o jogador decide se olha.
    void AoReceber(DadosDoCelular.Mensagem m)
    {
        if (IntroChegada.SilenciarAvisos) return;
        Tocar(somMensagem);
        if (!EstaAberta && !IntroChegada.Ativa) Avisar("O celular vibrou.");
    }

    void AoEnviar(string id) => Tocar(somEnviado);

    void Avisar(string texto)
    {
        aviso = texto;
        avisoAte = Time.unscaledTime + 2.5f;
    }

    void Tocar(AudioClip c)
    {
        if (c == null) return;
        if (fonte != null) fonte.PlayOneShot(c);
        else AudioSource.PlayClipAtPoint(c, Jogador.Posicao, 0.6f);
    }

    // ------------------------------------------------------------------ fluxo

    protected override void AoAbrir()
    {
        guardandoParaFechar = false;
        guardadoNaHora = false;
        if (Jogador.Movimento != null) Jogador.Movimento.ComCelularNaMao = true;
        bool vindoDaCamera = modoCamera;
        modoCamera = false;
        if (!vindoDaCamera)
        {
            Tocar(somPuxar);
            if (telaDoCelular != null) telaDoCelular.IrParaInicio();
        }
        if (naMao != null) naMao.IrPara(CelularNaMao.Pose.Mao);
    }

    // Esc e T: o celular desce (para o bolso, ou para a pose de lanterna) e só então a interface fecha.
    protected override void PedirFechar()
    {
        if (IntroChegada.Ativa) return;
        IniciarGuarda();
    }

    void IniciarGuarda()
    {
        if (guardandoParaFechar) return;
        guardandoParaFechar = true;
        Tocar(somGuardar);
        if (naMao != null) naMao.IrPara(PoseDeDescanso);
    }

    /// <summary>Abre o celular já na conversa deste contato, mesmo durante a introdução.</summary>
    public void MostrarConversa(string contato)
    {
        GarantirMontado();
        if (!EstaAberta) AbrirInterface();
        else
        {
            guardandoParaFechar = false;
            if (naMao != null) naMao.IrPara(CelularNaMao.Pose.Mao);
        }
        if (EstaAberta && telaDoCelular != null) telaDoCelular.AbrirConversa(contato);
        foreach (DadosDoCelular.Mensagem m in CelularLogica.Conversa(contato)) lidas.Add(m.id);
    }

    /// <summary>Guarda o celular com a animação (o mesmo que T ou Esc com ele aberto).</summary>
    public void Guardar() { if (EstaAberta) IniciarGuarda(); }

    /// <summary>Fecha a interface (se aberta), sai do modo câmera e guarda o modelo na hora.</summary>
    public void GuardarJa()
    {
        modoCamera = false;
        guardandoParaFechar = false;
        if (EstaAberta)
        {
            guardadoNaHora = true;
            FecharInterface();
        }
        if (naMao != null) naMao.GuardarJa();
    }

    CelularNaMao.Pose PoseDeDescanso => lanterna ? CelularNaMao.Pose.Lanterna : CelularNaMao.Pose.Guardado;

    // Fechada por fora (sem passar pela animação): o celular volta ao descanso, a não ser que vá para a câmera.
    protected override void AoFechar()
    {
        if (Jogador.Movimento != null) Jogador.Movimento.ComCelularNaMao = false;
        if (!modoCamera && !guardadoNaHora && naMao != null && naMao.Destino != PoseDeDescanso) naMao.IrPara(PoseDeDescanso);
        guardandoParaFechar = false;
        guardadoNaHora = false;
    }

    protected override void AtualizarAberta()
    {
        if (!IntroChegada.Ativa) base.AtualizarAberta();
        if (guardandoParaFechar && (naMao == null || !naMao.Animando)) FecharInterface();
    }

    /// <summary>App Câmera: o celular deita na frente do rosto e o jogador volta a andar.</summary>
    public void AbrirCamera()
    {
        if (IntroChegada.Ativa || TelaDeFinal.Ativa || DanielPerseguidor.Capturando || Esconderijo.JogadorEscondido) return;
        modoCamera = true;
        vezesNaCamera++;
        if (vezesNaCamera <= 2) dicaCameraAte = Time.unscaledTime + 5f;
        if (naMao != null) naMao.IrPara(CelularNaMao.Pose.Camera);
        FecharInterface();
    }

    /// <summary>Abre uma foto da galeria: o celular deita e a foto ocupa a tela dele.</summary>
    public void AbrirFoto(int indice)
    {
        if (indice < 0 || indice >= AlbumDeFotos.Fotos.Count) return;
        if (telaDoCelular != null) telaDoCelular.AbrirFoto(indice);
        if (naMao != null) naMao.IrPara(CelularNaMao.Pose.Foto);
    }

    /// <summary>Volta da foto para a galeria (o celular fica em pé de novo).</summary>
    public void FecharFoto()
    {
        if (naMao != null) naMao.IrPara(CelularNaMao.Pose.Mao);
    }

    void GuardarDaCamera()
    {
        modoCamera = false;
        Tocar(somGuardar);
        if (naMao != null) naMao.IrPara(PoseDeDescanso);
    }

    public void AlternarLanterna()
    {
        if (IntroChegada.Ativa) return;
        DefinirLanterna(!lanterna);
    }

    public void DefinirLanterna(bool ligar)
    {
        if ((IntroChegada.Ativa || TelaDeFinal.Ativa || DanielPerseguidor.Capturando) && ligar) return;
        if (lanterna == ligar) return;
        lanterna = ligar;
        Tocar(somLanterna);
        if (naMao == null || EstaAberta || modoCamera) return;
        naMao.IrPara(PoseDeDescanso);
    }

    protected override void Update()
    {
        base.Update();
        bool comSinal = Sinal.Tem;
        CelularLogica.Avancar(Time.deltaTime, comSinal);
        CelularLogica.ReceberPendentes(comSinal);

        if (TelaDeFinal.Ativa || DanielPerseguidor.Capturando)
        {
            if (EstaAberta || modoCamera || (naMao != null && naMao.Visivel)) GuardarJa();
            return;
        }

        if (Esconderijo.JogadorEscondido && (EstaAberta || modoCamera))
            GuardarJa();

        // Inspeção de pista: o modelo some na hora (a luz da lanterna continua).
        if (ObjetoInspecionavel.InspecionandoAlgo && (modoCamera || (naMao != null && naMao.Visivel && !EstaAberta)))
        {
            modoCamera = false;
            if (naMao != null) naMao.GuardarJa();
        }

        if (IntroChegada.Ativa) return;

        bool livre = !EstaAberta && !MenuPausa.Pausado && !InterfaceDeTela.AlgumaAberta && !ObjetoInspecionavel.InspecionandoAlgo &&
                     !TelaDeFinal.Ativa && !DanielPerseguidor.Capturando && Cursor.lockState == CursorLockMode.Locked;
        if (livre && lanterna && !modoCamera && naMao != null && naMao.Destino == CelularNaMao.Pose.Guardado) naMao.IrPara(CelularNaMao.Pose.Lanterna);
        // A lanterna liga também com o celular aberto na mão ou escondido.
        if ((livre || (EstaAberta && !MenuPausa.Pausado)) && MiraDeInteracao.TeclaApertada(teclaLanterna)) AlternarLanterna();

        if (!modoCamera || !livre || Esconderijo.JogadorEscondido) return;
        if (MiraDeInteracao.TeclaApertada(tecla)) { GuardarDaCamera(); return; }
        if (MiraDeInteracao.TeclaApertada(teclaApps) || BotaoDoMouse(1)) { AbrirInterface(); return; }
        if (MiraDeInteracao.TeclaApertada(teclaFoto) || BotaoDoMouse(0)) TirarFoto();
    }

    // A base (InterfaceDeTela) conclui o fechamento no LateUpdate: tem que ser chamada.
    protected override void LateUpdate()
    {
        base.LateUpdate();
        AtualizarLuzDaLanterna();
    }

    /// <summary>Foto pelo visor (só no modo câmera, com o celular parado na pose e a lanterna desligada).</summary>
    public bool TirarFoto()
    {
        if (!modoCamera || naMao == null || naMao.Animando || cameraDoCelular == null) return false;
        if (lanterna)
        {
            MiraDeInteracao.Avisar("Desligue a lanterna para tirar a foto.", 2.5f);
            return false;
        }
        cameraDoCelular.Fotografar(naMao);
        disparoEm = Time.unscaledTime;
        return true;
    }

    static bool BotaoDoMouse(int botao)
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current == null) return false;
        return (botao == 0 ? Mouse.current.leftButton : Mouse.current.rightButton).wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(botao);
#endif
    }

    void AtualizarLuzDaLanterna()
    {
        bool acesa = lanterna && !TelaDeFinal.Ativa && !DanielPerseguidor.Capturando && naMao != null && (naMao.Visivel || ObjetoInspecionavel.InspecionandoAlgo);
        LanternaLigada = acesa;
        if (luzLanterna == null)
        {
            if (!acesa) return;
            var go = new GameObject("Lanterna do celular");
            go.layer = 0;   // a câmera principal descarta luzes na camada do celular ("Inspecao")
            luzLanterna = go.AddComponent<Light>();
            luzLanterna.type = LightType.Spot;
            int camada = LayerMask.NameToLayer("Inspecao");
            if (camada >= 0) luzLanterna.cullingMask = ~(1 << camada);   // não ilumina o próprio celular
            // Todas as camadas de luz: salas isoladas (ex.: a autópsia, na camada 1) também recebem a lanterna.
            luzLanterna.renderingLayerMask = ~0;
            var urp = go.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
            if (urp == null) urp = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
            urp.renderingLayers = uint.MaxValue;
        }
        Transform ponto = naMao != null ? naMao.PontoDaLuz : null;
        if (ponto != null && luzLanterna.transform.parent != ponto)
        {
            luzLanterna.transform.SetParent(ponto, false);
            luzLanterna.transform.localPosition = Vector3.zero;
        }
        // A traseira do celular olha para a frente do jogador: o facho sai para a frente.
        if (naMao != null) luzLanterna.transform.rotation = Quaternion.LookRotation(naMao.transform.forward, naMao.transform.up);
        luzLanterna.enabled = acesa;
        luzLanterna.intensity = intensidadeLanterna;
        luzLanterna.range = alcanceLanterna;
        luzLanterna.spotAngle = anguloLanterna;
        luzLanterna.innerSpotAngle = Mathf.Min(anguloInternoLanterna, anguloLanterna);
        luzLanterna.color = corLanterna;
        luzLanterna.shadows = sombrasLanterna;
        luzLanterna.cookie = cookieLanterna;
    }

    // ------------------------------------------------------------------ save

    [System.Serializable]
    struct EstadoSalvo
    {
        public string[] lidas;
        public string[] fila;
    }

    public string Salvar()
    {
        var idsLidas = new List<string>(lidas);
        idsLidas.Sort(System.StringComparer.Ordinal);
        var idsFila = new List<string>(CelularLogica.Fila.Count);
        foreach (CelularLogica.ItemNaFila item in CelularLogica.Fila)
            if (item != null && !string.IsNullOrEmpty(item.id)) idsFila.Add(item.id);
        return JsonUtility.ToJson(new EstadoSalvo { lidas = idsLidas.ToArray(), fila = idsFila.ToArray() });
    }

    public void Carregar(string estado)
    {
        lidas.Clear();
        CelularLogica.Limpar();
        if (string.IsNullOrEmpty(estado)) return;
        EstadoSalvo salvo;
        try { salvo = JsonUtility.FromJson<EstadoSalvo>(estado); }
        catch (System.ArgumentException) { return; }
        if (salvo.lidas != null)
            foreach (string id in salvo.lidas)
                if (!string.IsNullOrEmpty(id)) lidas.Add(id);
        if (salvo.fila != null)
            foreach (string id in salvo.fila)
                if (!string.IsNullOrEmpty(id)) CelularLogica.Enfileirar(id);
    }

    // ------------------------------------------------------------------ o que fica fora da tela do celular

    // A interface mora na tela 3D (CelularTela); aqui só a vibração e, nas primeiras vezes, os controles da câmera.
    protected override void Desenhar() { }

    protected override void OnGUI()
    {
        useGUILayout = false;
        float s = DesenhoUi.S;
        if (modoCamera && !EstaAberta && Time.unscaledTime < dicaCameraAte)
        {
            float a = Mathf.Clamp01((dicaCameraAte - Time.unscaledTime) / 1f) * 0.6f;
            GUI.Label(new Rect(0, Screen.height - 46 * s, Screen.width, 30 * s),
                $"Clique: foto     {teclaApps}: apps     {tecla}: guardar",
                DesenhoUi.Estilo(15, new Color(1, 1, 1, a), false, TextAnchor.UpperCenter));
        }
        if (!string.IsNullOrEmpty(aviso) && Time.unscaledTime < avisoAte && !InterfaceDeTela.AlgumaAberta)
        {
            float a = Mathf.Clamp01((avisoAte - Time.unscaledTime) / 0.6f) * 0.75f;
            GUI.Label(new Rect(0, Screen.height * 0.82f, Screen.width, 30 * s), aviso,
                DesenhoUi.Estilo(17, new Color(1, 1, 1, a), false, TextAnchor.UpperCenter));
        }
    }
}
