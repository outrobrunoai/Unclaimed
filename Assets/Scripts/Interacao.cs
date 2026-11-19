using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Coloque este script em qualquer objeto-pista (precisa de um Collider).
/// Ao apontar a mira (centro da tela) diretamente para ele, a até "distanciaMaxima" metros,
/// aparece o aviso "[E] Examinar". Paredes e outros objetos na frente bloqueiam a mira.
/// Ao apertar a tecla, uma cópia do objeto aparece na frente da câmera
/// e pode ser girada com clique + arraste do mouse (scroll = zoom).
/// A cópia é desenhada por cima de tudo (câmera overlay do URP), então nunca
/// atravessa paredes ou móveis. Isso exige a layer "Inspecao" (veja abaixo).
/// Funciona com o Input System novo e com o Input Manager antigo.
///
/// Peças manipuláveis (ex.: os discos de um cadeado): um componente no mesmo objeto que implemente
/// IManipulavelNaInspecao recebe a cópia ao abrir e a entrada a cada quadro; o que ele usar (clique/arraste,
/// scroll) não gira nem aproxima o objeto. Opcionalmente o original fica oculto durante a inspeção (sem
/// duplicata visível) e o Movimento do jogador é travado sem precisar ligá-lo no Inspector.
/// </summary>
public class ObjetoInspecionavel : MonoBehaviour, ISalvavel
{
    [Header("Informações da pista")]
    public string nome = "Pista";
    [TextArea] public string descricao;
    public string idEvidencia;

    [Header("Interação")]
    [Tooltip("Nome da tecla (ex.: E, F, Q).")]
    public string teclaInteracao = "E";
    [Tooltip("Até que distância (em metros) a mira alcança o objeto.")]
    public float distanciaMaxima = 2f;
    [Tooltip("Folga da mira (em metros) para facilitar apontar para objetos pequenos. 0 = o centro da tela precisa acertar o objeto exatamente.")]
    [Range(0f, 0.2f)] public float folgaDaMira = 0.04f;
    [Tooltip("Realça a pista enquanto está na mira.")]
    public bool realcarNaMira = true;
    [Tooltip("Força do realce branco na mira.")]
    [Range(0f, 1f)] public float intensidadeDoRealce = 1f;

    [Header("Objeto preso no lugar")]
    [Tooltip("Para o que está preso (quadro na parede, placa, papel colado): o objeto não sai do lugar nem gira. A câmera vai até a " +
             "frente da face escolhida em \"Face Voltada Para Jogador\" (topo = \"Topo Na Tela\"); scroll aproxima, e de perto o mouse move a vista.")]
    public bool fixoNoLugar = false;

    [Header("Visualização 360°")]
    [Tooltip("Opcional: um modelo mais detalhado só para a inspeção. Vazio = usa o próprio objeto.")]
    public GameObject modeloInspecao;
    public float distanciaDaCamera = 0.7f;
    [Tooltip("Tamanho (em metros) da maior dimensão do objeto durante a inspeção.")]
    public float tamanhoNaTela = 0.4f;
    public float sensibilidade = 0.4f;
    public float zoomPorScroll = 0.08f;
    public Vector2 limiteDistancia = new Vector2(0.3f, 1.2f);

    [Header("Posição ao inspecionar")]
    [Tooltip("Onde o objeto aparece na tela. (0,0) = centro. X: -1 = borda esquerda, +1 = borda direita. " +
             "Y: -1 = borda de baixo, +1 = borda de cima. Ex.: (-0.35, 0) deixa o objeto à esquerda. " +
             "Dica: selecione o objeto na cena para ver uma prévia (esfera verde-azulada) na frente da câmera do jogador.")]
    public Vector2 posicaoNaTela = Vector2.zero;
    [Tooltip("FaceParaOJogador = sempre mostra a face escolhida abaixo virada para o jogador, de qualquer posição. ManterDaCena = mantém a rotação que o objeto tem na cena. RotacaoManual = usa a Rotacao Inicial abaixo.")]
    public OrientacaoNaInspecao orientacao = OrientacaoNaInspecao.FaceParaOJogador;
    [Tooltip("Qual lado do objeto (nos eixos do próprio objeto) deve ficar virado para o jogador. Ex.: um livro deitado mostra a capa com Cima. Selecione o objeto na cena: a seta amarela mostra o lado escolhido.")]
    public EixoLocal faceVoltadaParaJogador = EixoLocal.Cima;
    [Tooltip("Qual lado do objeto deve ficar para cima na tela. Precisa ser um eixo diferente da face escolhida. A seta azul-clara mostra o lado escolhido.")]
    public EixoLocal topoNaTela = EixoLocal.Frente;
    [Tooltip("Gira o objeto no próprio plano da tela (em graus) para acertar a posição. Ex.: deixar o texto da capa reto.")]
    public float giroNaTela = 0f;
    [Tooltip("Só usado em RotacaoManual: rotação (em graus) relativa à câmera.")]
    public Vector3 rotacaoInicial = new Vector3(0f, 180f, 0f);

    [Header("Não atravessar paredes")]
    [Tooltip("Layer usada só durante a inspeção. Precisa existir em Project Settings > Tags and Layers (o script GarantirLayerInspecao cria sozinho). O script aplica ela na cópia automaticamente.")]
    public string nomeDaLayer = "Inspecao";

    [Header("Durante a inspeção")]
    [Tooltip("Arraste aqui o movimento do jogador e o script de olhar com o mouse para travá-los.")]
    public Behaviour[] desativarDuranteInspecao;
    [Tooltip("Também trava todo Movimento ativo na cena (útil em prefabs, que não referenciam o jogador).")]
    public bool travarMovimentoDoJogador = false;
    [Tooltip("Oculta o objeto original enquanto a cópia está na tela (sem duplicata visível).")]
    public bool ocultarOriginal = false;
    [Tooltip("Instruções no rodapé durante a inspeção. Vazio = as instruções padrão (ou as do manipulador).")]
    public string instrucoes = "";
    [Tooltip("Segundos da cópia saindo do lugar do objeto até a frente da câmera (e voltando ao fechar). 0 = aparece direto.")]
    public float duracaoAnimacao = 0f;

    [Header("Eventos")]
    public UnityEvent aoIniciarInspecao;
    [Tooltip("Dispara só na primeira vez (ideal para registrar a pista no diário usando o Inspector).")]
    public UnityEvent aoPrimeiraInspecao;
    public UnityEvent aoFinalizarInspecao;

    // --- estado compartilhado entre todas as pistas ---
    static ObjetoInspecionavel melhor;      // pista mais próxima neste frame
    static float melhorDistancia;
    static int frameAtual = -1;
    public static bool InspecionandoAlgo => emInspecao != null;
    static ObjetoInspecionavel emInspecao;  // pista sendo inspecionada agora
    static Material materialRealce;
    static bool avisouShaderRealce;
    static readonly int intensidadeRealceId = Shader.PropertyToID("_Intensidade");

    float realce;
    MaterialPropertyBlock propriedadesRealce;
    MeshRenderer[] renderersRealce;
    Mesh[] meshesRealce;
    bool avisouLoteEstatico;

    // --- estado da instância ---
    Transform cam;
    Transform pivot;
    float distanciaAtual;
    bool arrastando;
    Vector2 ultimaPosMouse;
    bool jaExaminado;
    public bool JaExaminado => jaExaminado;
    CursorLockMode travaAnterior;
    bool cursorVisivelAnterior;
    readonly System.Collections.Generic.List<Behaviour> desativados = new System.Collections.Generic.List<Behaviour>();
    readonly System.Collections.Generic.List<Renderer> ocultados = new System.Collections.Generic.List<Renderer>();
    IManipulavelNaInspecao manipulador;
    int quadroAbertura = -1;

    // animação de ida (do lugar do objeto até a frente da câmera) e de volta
    enum Animacao { Nenhuma, Vindo, Voltando }
    Animacao animacao;
    float inicioAnimacao;
    Transform copia;
    Vector3 origemPos, origemEsc, alvoLocalPos, alvoLocalEsc, deLocalPos, deLocalEsc;
    Quaternion origemRot, alvoLocalRot, deLocalRot;
    GUIStyle estilo;

    // vista no lugar (fixoNoLugar): a câmera do jogador vai até a frente do objeto e volta ao fechar
    bool vistaFixa;
    Vector3 camPosLocal, deP;
    Quaternion camRotLocal, deR;
    Vector3 vistaCentro, vistaNormal, vistaCima, vistaDireita;
    Vector2 vistaMeia;          // meia largura e meia altura do objeto no plano da vista (m)
    float vistaDistInicial, vistaDist;

    // câmera sobreposta (desenha o objeto por cima de tudo)
    Camera camPrincipal;
    Camera camSobreposta;
    UniversalAdditionalCameraData dadosCamPrincipal;
    int mascaraOriginal;
    bool mascaraAlterada;
    bool avisouLayer;

    // Cada pista confere se a mira está apontando para ela; só uma por vez recebe a interação.
    void Update()
    {
        if (Time.frameCount != frameAtual)
        {
            frameAtual = Time.frameCount;
            melhor = null;
            melhorDistancia = float.MaxValue;
        }

        if (emInspecao != null) return;
        // Diário, telas (computador, cadeado) e cursor solto bloqueiam; portas, gavetas etc. na mira têm
        // prioridade sobre a pista (mesma tecla).
        if (MiraDeInteracao.ControleCapturado || MiraDeInteracao.Alvo != null) return;

        Camera c = Camera.main;
        if (c == null) return;

        float dist;
        if (!EstaApontando(c, out dist)) return;

        if (dist < melhorDistancia)
        {
            melhorDistancia = dist;
            melhor = this;
        }
    }

    // A "mira" é um raio que sai do centro da câmera. Só vale se o primeiro objeto sólido
    // atingido for este (paredes e outros objetos na frente bloqueiam).
    // O raio é o mesmo para todas as pistas no frame: cada combinação de alcance e folga é lançada
    // uma vez só e as outras pistas reaproveitam os acertos.
    sealed class Lance
    {
        public int frame = -1, n;
        public float distancia, folga;
        public readonly RaycastHit[] acertos = new RaycastHit[32];
    }
    static readonly System.Collections.Generic.List<Lance> lances = new System.Collections.Generic.List<Lance>();

    static Lance Lancar(Ray raio, float distancia, float folga)
    {
        Lance lance = null;
        foreach (Lance l in lances)
            if (l.distancia == distancia && l.folga == folga) { lance = l; break; }
        if (lance == null) lances.Add(lance = new Lance { distancia = distancia, folga = folga });
        if (lance.frame != Time.frameCount)
        {
            lance.frame = Time.frameCount;
            lance.n = folga > 0f
                ? Physics.SphereCastNonAlloc(raio, folga, lance.acertos, distancia, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide)
                : Physics.RaycastNonAlloc(raio, lance.acertos, distancia, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        }
        return lance;
    }

    bool EstaApontando(Camera c, out float distancia)
    {
        var raio = new Ray(c.transform.position, c.transform.forward);

        // 1) Mira exata (centro da tela).
        if (Avaliar(Lancar(raio, distanciaMaxima, 0f), out distancia)) return true;

        // 2) Com folga, para facilitar a mira em objetos pequenos.
        if (folgaDaMira > 0f && Avaliar(Lancar(raio, distanciaMaxima, folgaDaMira), out distancia)) return true;

        distancia = 0f;
        return false;
    }

    // Entre tudo que o raio atravessou: este objeto precisa estar mais perto do que qualquer
    // coisa sólida. Triggers de outros objetos (zonas de evento etc.) não bloqueiam a mira.
    bool Avaliar(Lance lance, out float distancia)
    {
        float meu = float.MaxValue;
        float bloqueio = float.MaxValue;

        for (int i = 0; i < lance.n; i++)
        {
            RaycastHit h = lance.acertos[i];
            if (h.collider == null) continue;
            if (h.distance <= 0f) continue; // já estava sobreposto na origem (ex.: o próprio jogador)

            if (h.collider.transform.IsChildOf(transform))
                meu = Mathf.Min(meu, h.distance);
            else if (!h.collider.isTrigger)
                bloqueio = Mathf.Min(bloqueio, h.distance);
        }

        distancia = meu;
        return meu < float.MaxValue && meu <= bloqueio;
    }

    // LateUpdate roda depois de todos os Update, então "melhor" já está decidido.
    void LateUpdate()
    {
        if (emInspecao != null) realce = 0f;
        if (emInspecao == this && animacao == Animacao.Voltando)
        {
            // voltando ao lugar: sem entrada; fecha ao chegar
            float e = Progresso();
            if (vistaFixa) PoseDaCamera(e, true);
            else PoseEntre(deLocalPos, deLocalRot, deLocalEsc, e, true);
            if (e >= 1f) Fechar();
            return;
        }
        if (emInspecao == this)
        {
            if (animacao == Animacao.Vindo)
            {
                float e = Progresso();
                if (vistaFixa) PoseDaCamera(e, false);
                else PoseEntre(alvoLocalPos, alvoLocalRot, alvoLocalEsc, e, false);
                if (e >= 1f) animacao = Animacao.Nenhuma;
            }
            if (vistaFixa) { if (animacao == Animacao.Nenhuma) InspecionarNoLugar(); }
            else Inspecionar();
            if (emInspecao == this && animacao == Animacao.Nenhuma && Time.frameCount != quadroAbertura)
            {
                var pilha = PilhaDeDocumentos.De(this);
                if (pilha != null && pilha.Total > 1)
                {
                    int passo = TeclaApertada("LeftArrow") || TeclaApertada("A") ? -1
                        : TeclaApertada("RightArrow") || TeclaApertada("D") ? 1 : 0;
                    var vizinha = passo != 0 ? pilha.Vizinha(this, passo) : null;
                    if (vizinha != null && vizinha != this)
                    {
                        // Restaura o estado anterior antes de aplicar o da próxima folha.
                        Fechar();
                        vizinha.Abrir();
                        return;
                    }
                }
            }
            if (emInspecao == this && Time.frameCount != quadroAbertura && (TeclaApertada(teclaInteracao) || TeclaApertada("Escape")))
                PedirFechamento();
        }
        else if (emInspecao == null && melhor == this && TeclaApertada(teclaInteracao))
        {
            Abrir();
        }
        AtualizarRealce();
    }

    void AtualizarRealce()
    {
        if (!realcarNaMira || emInspecao != null)
        {
            realce = 0f;
            return;
        }

        realce = Mathf.MoveTowards(realce, melhor == this ? 1f : 0f, Time.unscaledDeltaTime / 0.12f);
        if (realce <= 0.001f) return;

        if (materialRealce == null)
        {
            if (avisouShaderRealce) return;
            Shader shader = Resources.Load<Shader>("Shaders/RealceMira");
            if (shader == null) shader = Shader.Find("Unclaimed/RealceMira");
            if (shader == null)
            {
                avisouShaderRealce = true;
                Debug.LogWarning("Shader Unclaimed/RealceMira não encontrado; realce da mira desativado.", this);
                return;
            }
            materialRealce = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        // Guarda os meshes uma vez; não altera os materiais originais.
        if (renderersRealce == null)
        {
            renderersRealce = GetComponentsInChildren<MeshRenderer>(true);
            meshesRealce = new Mesh[renderersRealce.Length];
            for (int i = 0; i < renderersRealce.Length; i++)
            {
                var filtro = renderersRealce[i].GetComponent<MeshFilter>();
                if (filtro != null) meshesRealce[i] = filtro.sharedMesh;
            }
        }

        if (propriedadesRealce == null) propriedadesRealce = new MaterialPropertyBlock();
        propriedadesRealce.SetFloat(intensidadeRealceId, realce * intensidadeDoRealce);
        for (int i = 0; i < renderersRealce.Length; i++)
        {
            var renderer = renderersRealce[i];
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.forceRenderingOff)
                continue;
            if (renderer.isPartOfStaticBatch)
            {
                if (!avisouLoteEstatico)
                {
                    avisouLoteEstatico = true;
                    Debug.LogWarning($"Realce ignorado em '{name}': '{renderer.name}' faz parte de um lote estático.", this);
                }
                continue;
            }
            Mesh mesh = meshesRealce[i];
            if (mesh == null) continue;

            var parametros = new RenderParams(materialRealce)
            {
                matProps = propriedadesRealce,
                layer = renderer.gameObject.layer,
                worldBounds = renderer.bounds,
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = false,
                camera = null
            };
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                Graphics.RenderMesh(in parametros, mesh, submesh, renderer.localToWorldMatrix);
        }
    }

    /// <summary>
    /// Abre a inspeção sem a mira (ex.: um papel escolhido com o mouse no quadro de avisos). Falso se outra
    /// inspeção já está aberta. Como na mira, a tecla que abriu não fecha no mesmo quadro.
    /// </summary>
    public bool IniciarInspecao()
    {
        if (emInspecao != null) return false;
        Abrir();
        return emInspecao == this;
    }

    // Save: já examinada (aoPrimeiraInspecao não dispara de novo depois de continuar o jogo).
    public string Salvar() => jaExaminado ? "1" : "0";
    public void Carregar(string estado) => jaExaminado = estado == "1";

    /// <summary>Encerra a inspeção deste objeto (se estiver aberta), devolvendo controles e cursor.</summary>
    public void EncerrarInspecao()
    {
        if (emInspecao == this) Fechar();
    }

    void Abrir()
    {
        quadroAbertura = Time.frameCount;
        camPrincipal = Camera.main;
        cam = camPrincipal.transform;
        emInspecao = this;
        realce = 0f;

        if (fixoNoLugar)
        {
            if (!AbrirNoLugar()) { emInspecao = null; return; }
            TravarJogadorEColetar(null);
            return;
        }

        GameObject fonte = modeloInspecao != null ? modeloInspecao : gameObject;
        GameObject clone = Instantiate(fonte, fonte.transform.position, fonte.transform.rotation);
        clone.name = nome + " (inspeção)";
        clone.transform.localScale = fonte.transform.lossyScale;

        // A cópia não pode ser interativa nem física.
        foreach (var s in clone.GetComponentsInChildren<ObjetoInspecionavel>())
        {
            s.enabled = false;
            Destroy(s);
        }
        foreach (var c in clone.GetComponentsInChildren<Collider>()) c.enabled = false;
        foreach (var r in clone.GetComponentsInChildren<Rigidbody>()) r.isKinematic = true;

        Renderer[] rends = clone.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0)
        {
            Destroy(clone);
            emInspecao = null;
            return;
        }

        // Mede o objeto sem rotação: assim tamanho e centro não dependem de como ele está virado na cena.
        Quaternion rotacaoOriginal = clone.transform.rotation;
        clone.transform.rotation = Quaternion.identity;
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        Vector3 centroLocal = clone.transform.InverseTransformPoint(b.center);
        float maior = Mathf.Max(b.size.x, b.size.y, b.size.z);

        // Pivô na frente da câmera, na posição escolhida.
        distanciaAtual = Mathf.Clamp(distanciaDaCamera, limiteDistancia.x, limiteDistancia.y);
        pivot = new GameObject("PivotInspecao").transform;
        pivot.SetParent(cam, false);
        pivot.localRotation = Quaternion.identity;
        pivot.localPosition = PosicaoLocal(camPrincipal, distanciaAtual);

        // Vira o objeto (de frente para o jogador, por padrão) e põe o centro dele exatamente no pivô.
        clone.transform.rotation = CalcularRotacao(rotacaoOriginal);
        clone.transform.position += pivot.position - clone.transform.TransformPoint(centroLocal);
        clone.transform.SetParent(pivot, true);

        if (maior > 0.0001f) pivot.localScale = Vector3.one * (tamanhoNaTela / maior);

        // Pose final da cópia no pivô; com animação, ela parte do lugar do original.
        copia = clone.transform;
        alvoLocalPos = copia.localPosition; alvoLocalRot = copia.localRotation; alvoLocalEsc = copia.localScale;
        origemPos = fonte.transform.position; origemRot = fonte.transform.rotation; origemEsc = fonte.transform.lossyScale;
        animacao = Animacao.Nenhuma;
        if (duracaoAnimacao > 0f)
        {
            animacao = Animacao.Vindo;
            inicioAnimacao = Time.unscaledTime;
            PoseEntre(alvoLocalPos, alvoLocalRot, alvoLocalEsc, 0f, false);
        }

        ConfigurarCameraSobreposta(clone);

        // O original some enquanto a cópia está na tela (a cópia já foi feita com os renderers ligados).
        ocultados.Clear();
        if (ocultarOriginal)
            foreach (var r in GetComponentsInChildren<Renderer>())
                if (r.enabled) { r.enabled = false; ocultados.Add(r); }

        TravarJogadorEColetar(clone);
    }

    // Trava o jogador, libera o cursor e registra a primeira inspeção (comum às duas formas de examinar).
    void TravarJogadorEColetar(GameObject clone)
    {
        desativados.Clear();
        if (desativarDuranteInspecao != null)
            foreach (var comp in desativarDuranteInspecao)
            {
                if (comp != null && comp.enabled)
                {
                    comp.enabled = false;
                    desativados.Add(comp);
                }
            }
        if (travarMovimentoDoJogador)
            foreach (var mov in FindObjectsByType<Movimento>())
                if (mov.enabled) { mov.enabled = false; desativados.Add(mov); }
        travaAnterior = Cursor.lockState;
        cursorVisivelAnterior = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        arrastando = false;

        manipulador = clone != null ? GetComponent<IManipulavelNaInspecao>() : null;
        manipulador?.AoIniciarInspecao(clone.transform, camPrincipal);

        aoIniciarInspecao?.Invoke();

        if (!jaExaminado)
        {
            jaExaminado = true;
            aoPrimeiraInspecao?.Invoke(); // Ideal para chamar mecânicas de diário ou som de nova pista!
            if (!string.IsNullOrEmpty(idEvidencia)) Caderno.Coletar(idEvidencia);
        }
    }

    // Vista no lugar: mede o objeto no plano da face escolhida e calcula a distância em que ele cabe inteiro na tela.
    bool AbrirNoLugar()
    {
        Renderer[] rends = GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return false;
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        vistaNormal = transform.TransformDirection(Eixo(faceVoltadaParaJogador)).normalized;
        vistaCima = Vector3.ProjectOnPlane(transform.TransformDirection(Eixo(topoNaTela)), vistaNormal).normalized;
        vistaDireita = Vector3.Cross(vistaCima, vistaNormal);   // direita de quem olha para a face
        Vector3 ext = b.extents;
        float Meia(Vector3 eixo) => Mathf.Abs(eixo.x) * ext.x + Mathf.Abs(eixo.y) * ext.y + Mathf.Abs(eixo.z) * ext.z;
        vistaMeia = new Vector2(Meia(vistaDireita), Meia(vistaCima));
        vistaCentro = b.center + vistaNormal * Meia(vistaNormal);   // na face, não no meio da espessura
        float tg = Mathf.Tan(camPrincipal.fieldOfView * 0.5f * Mathf.Deg2Rad);
        vistaDistInicial = Mathf.Max(vistaMeia.y / tg, vistaMeia.x / (tg * camPrincipal.aspect)) * 1.2f;
        vistaDistInicial = Mathf.Max(vistaDistInicial, camPrincipal.nearClipPlane + 0.08f);
        vistaDist = vistaDistInicial;

        camPosLocal = cam.localPosition; camRotLocal = cam.localRotation;
        deP = cam.position; deR = cam.rotation;
        vistaFixa = true;
        animacao = Animacao.Vindo;
        inicioAnimacao = Time.unscaledTime;
        return true;
    }

    Vector3 PosicaoDaVista(Vector2 deslocamento) =>
        vistaCentro + vistaNormal * vistaDist + vistaDireita * deslocamento.x + vistaCima * deslocamento.y;

    Quaternion RotacaoDaVista => Quaternion.LookRotation(-vistaNormal, vistaCima);

    // Câmera entre a pose do jogador e a vista do objeto (ida) ou da pose atual de volta ao jogador.
    void PoseDaCamera(float e, bool voltando)
    {
        if (cam == null) return;
        if (voltando)
        {
            Vector3 p = cam.parent != null ? cam.parent.TransformPoint(camPosLocal) : camPosLocal;
            Quaternion r = cam.parent != null ? cam.parent.rotation * camRotLocal : camRotLocal;
            cam.SetPositionAndRotation(Vector3.Lerp(deP, p, e), Quaternion.Slerp(deR, r, e));
        }
        else cam.SetPositionAndRotation(Vector3.Lerp(deP, PosicaoDaVista(Vector2.zero), e), Quaternion.Slerp(deR, RotacaoDaVista, e));
    }

    // Scroll aproxima; com zoom, o mouse leva a vista até a borda do objeto (sem zoom, ele fica inteiro e parado).
    void InspecionarNoLugar()
    {
        float scroll = Mathf.Clamp(ScrollVertical(), -1f, 1f);
        float minimo = Mathf.Max(camPrincipal.nearClipPlane + 0.05f, vistaDistInicial * 0.3f);
        if (Mathf.Abs(scroll) > 0.01f)
            vistaDist = Mathf.Clamp(vistaDist - scroll * zoomPorScroll * Mathf.Max(1f, vistaDistInicial), minimo, vistaDistInicial);
        Vector2 m = PosicaoMouse();
        Vector2 n = new Vector2(Mathf.Clamp(m.x / Mathf.Max(1, Screen.width) * 2f - 1f, -1f, 1f),
                                Mathf.Clamp(m.y / Mathf.Max(1, Screen.height) * 2f - 1f, -1f, 1f));
        float folga = 1f - vistaDist / vistaDistInicial;   // 0 sem zoom
        Vector3 alvo = PosicaoDaVista(Vector2.Scale(n, vistaMeia) * folga);
        float k = 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime);
        cam.SetPositionAndRotation(Vector3.Lerp(cam.position, alvo, k), RotacaoDaVista);
    }

    // Rotação (no mundo) que o objeto terá ao aparecer na inspeção.
    Quaternion CalcularRotacao(Quaternion rotacaoOriginal)
    {
        switch (orientacao)
        {
            case OrientacaoNaInspecao.FaceParaOJogador:
            {
                // Destino: o lado escolhido aponta para a câmera e o "topo" escolhido aponta para cima na tela.
                Quaternion alvo = Quaternion.LookRotation(-cam.forward, cam.up);
                Quaternion local = Quaternion.LookRotation(Eixo(faceVoltadaParaJogador), Eixo(topoNaTela));
                return Quaternion.AngleAxis(giroNaTela, cam.forward) * alvo * Quaternion.Inverse(local);
            }
            case OrientacaoNaInspecao.RotacaoManual:
                return cam.rotation * Quaternion.Euler(rotacaoInicial);
            default:
                return rotacaoOriginal;
        }
    }

    static Vector3 Eixo(EixoLocal eixo)
    {
        switch (eixo)
        {
            case EixoLocal.Cima: return Vector3.up;
            case EixoLocal.Baixo: return Vector3.down;
            case EixoLocal.Frente: return Vector3.forward;
            case EixoLocal.Tras: return Vector3.back;
            case EixoLocal.Direita: return Vector3.right;
            default: return Vector3.left;
        }
    }

    // Converte "posição na tela" (-1..1) + distância em uma posição local da câmera.
    // Assim o objeto fica no mesmo ponto da tela mesmo quando você dá zoom.
    Vector3 PosicaoLocal(Camera c, float distancia)
    {
        float meiaAltura = Mathf.Tan(c.fieldOfView * 0.5f * Mathf.Deg2Rad) * distancia;
        return new Vector3(
            posicaoNaTela.x * meiaAltura * c.aspect,
            posicaoNaTela.y * meiaAltura,
            distancia);
    }

    void Inspecionar()
    {
        if (pivot == null) return;

        Vector2 pos = PosicaoMouse();
        float scroll = Mathf.Clamp(ScrollVertical(), -1f, 1f);

        UsoDaEntrada uso = manipulador != null
            ? manipulador.ProcessarEntrada(camPrincipal, pos, BotaoEsquerdoPressionado(), scroll)
            : UsoDaEntrada.Nenhum;
        if (emInspecao != this || pivot == null) return;   // o manipulador pode ter encerrado a inspeção

        if ((uso & UsoDaEntrada.Ponteiro) != 0)
        {
            arrastando = false;   // clique/arraste numa peça: o objeto não gira
        }
        else if (BotaoEsquerdoPressionado())
        {
            if (arrastando)
            {
                Vector2 d = pos - ultimaPosMouse;
                // Gira em torno dos eixos da câmera, então o arraste sempre "parece certo".
                pivot.Rotate(cam.up, -d.x * sensibilidade, Space.World);
                pivot.Rotate(cam.right, d.y * sensibilidade, Space.World);
            }
            arrastando = true;
        }
        else
        {
            arrastando = false;
        }
        ultimaPosMouse = pos;

        if ((uso & UsoDaEntrada.Rolagem) == 0 && Mathf.Abs(scroll) > 0.01f)
        {
            distanciaAtual = Mathf.Clamp(distanciaAtual - scroll * zoomPorScroll, limiteDistancia.x, limiteDistancia.y);
            pivot.localPosition = PosicaoLocal(camPrincipal, distanciaAtual);
        }
    }

    /// <summary>Fecha a inspeção; com animação, a cópia volta antes ao lugar do original.</summary>
    void PedirFechamento()
    {
        if (vistaFixa)
        {
            deP = cam.position; deR = cam.rotation;
            animacao = Animacao.Voltando;
            inicioAnimacao = Time.unscaledTime;
            return;
        }
        if (duracaoAnimacao <= 0f || copia == null) { Fechar(); return; }
        deLocalPos = copia.localPosition; deLocalRot = copia.localRotation; deLocalEsc = copia.localScale;
        animacao = Animacao.Voltando;
        inicioAnimacao = Time.unscaledTime;
    }

    float Progresso()
    {
        float duracao = vistaFixa ? Mathf.Max(0.35f, duracaoAnimacao) : duracaoAnimacao;
        float k = Mathf.Clamp01((Time.unscaledTime - inicioAnimacao) / Mathf.Max(0.01f, duracao));
        return k * k * (3f - 2f * k);
    }

    /// <summary>
    /// Pose da cópia entre o lugar do original (no mundo, convertido para o pivô a cada quadro: girar ou aproximar
    /// durante a animação não a desloca) e uma pose no pivô. paraOrigem: de "outra" pose até o original.
    /// </summary>
    void PoseEntre(Vector3 outraPos, Quaternion outraRot, Vector3 outraEsc, float e, bool paraOrigem)
    {
        if (copia == null || pivot == null) return;
        Vector3 oPos = pivot.InverseTransformPoint(origemPos);
        Quaternion oRot = Quaternion.Inverse(pivot.rotation) * origemRot;
        Vector3 oEsc = origemEsc / Mathf.Max(1e-6f, pivot.lossyScale.x);
        float t = paraOrigem ? e : 1f - e;   // t = 1: no original
        copia.localPosition = Vector3.Lerp(outraPos, oPos, t);
        copia.localRotation = Quaternion.Slerp(outraRot, oRot, t);
        copia.localScale = Vector3.Lerp(outraEsc, oEsc, t);
    }

    void Fechar()
    {
        animacao = Animacao.Nenhuma;
        if (vistaFixa && cam != null) { cam.localPosition = camPosLocal; cam.localRotation = camRotLocal; }
        vistaFixa = false;
        copia = null;
        if (pivot != null) Destroy(pivot.gameObject);
        pivot = null;
        DesfazerCameraSobreposta();

        foreach (var r in ocultados)
            if (r != null) r.enabled = true;
        ocultados.Clear();

        foreach (var comp in desativados)
        {
            if (comp == null) continue;
            if (comp is Movimento mov) mov.olhar = Vector2.zero;   // sem giro acumulado durante a inspeção
            comp.enabled = true;
        }
        desativados.Clear();

        Cursor.lockState = travaAnterior;
        Cursor.visible = cursorVisivelAnterior;

        emInspecao = null;
        var m = manipulador;
        manipulador = null;
        m?.AoFinalizarInspecao();
        aoFinalizarInspecao?.Invoke();
    }

    // Desenha o objeto por cima de tudo com uma câmera "overlay" (URP camera stacking).
    // Assim ele nunca atravessa paredes, nem no zoom out.
    void ConfigurarCameraSobreposta(GameObject clone)
    {
        int camada = LayerMask.NameToLayer(nomeDaLayer);
        camPrincipal = Camera.main;
        dadosCamPrincipal = camPrincipal != null ? camPrincipal.GetUniversalAdditionalCameraData() : null;

        if (camada < 0 || dadosCamPrincipal == null || dadosCamPrincipal.renderType != CameraRenderType.Base)
        {
            if (!avisouLayer)
            {
                Debug.LogWarning($"ObjetoInspecionavel: a layer '{nomeDaLayer}' não existe ou a câmera principal não é uma Base Camera do URP. " +
                                 "O objeto pode atravessar paredes. Crie a layer em Project Settings > Tags and Layers.");
                avisouLayer = true;
            }
            dadosCamPrincipal = null;
            return;
        }

        // Coloca a cópia (e o pivô) na layer de inspeção.
        foreach (var t in clone.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = camada;
        pivot.gameObject.layer = camada;

        // A câmera principal para de desenhar essa layer durante a inspeção.
        mascaraOriginal = camPrincipal.cullingMask;
        camPrincipal.cullingMask = mascaraOriginal & ~(1 << camada);
        mascaraAlterada = true;

        // Câmera extra: desenha só a layer de inspeção, por cima da imagem final.
        var go = new GameObject("CameraInspecao");
        go.transform.SetParent(camPrincipal.transform, false);
        camSobreposta = go.AddComponent<Camera>();
        camSobreposta.fieldOfView = camPrincipal.fieldOfView;
        camSobreposta.nearClipPlane = 0.01f;
        camSobreposta.farClipPlane = 20f;
        camSobreposta.cullingMask = 1 << camada;

        var dados = camSobreposta.GetUniversalAdditionalCameraData();
        dados.renderType = CameraRenderType.Overlay;
        dados.renderPostProcessing = dadosCamPrincipal.renderPostProcessing;
        dadosCamPrincipal.cameraStack.Add(camSobreposta);
    }

    void DesfazerCameraSobreposta()
    {
        if (camSobreposta != null)
        {
            if (dadosCamPrincipal != null) dadosCamPrincipal.cameraStack.Remove(camSobreposta);
            Destroy(camSobreposta.gameObject);
            camSobreposta = null;
        }

        if (mascaraAlterada && camPrincipal != null)
            camPrincipal.cullingMask = mascaraOriginal;

        mascaraAlterada = false;
        dadosCamPrincipal = null;
    }

    void OnDisable()
    {
        realce = 0f;
        if (emInspecao == this) Fechar();
    }

    // Prévia no Scene: com o objeto selecionado, mostra onde ele vai aparecer e qual lado ficará virado para o jogador.
    void OnDrawGizmosSelected()
    {
        Camera c = Camera.main;
        if (c != null)
        {
            float d = Mathf.Clamp(distanciaDaCamera, limiteDistancia.x, limiteDistancia.y);
            Vector3 centro = c.transform.TransformPoint(PosicaoLocal(c, d));

            Gizmos.color = new Color(0.45f, 0.78f, 0.72f, 0.9f);
            Gizmos.DrawWireSphere(centro, tamanhoNaTela * 0.5f);
            Gizmos.DrawLine(c.transform.position, centro);

            // Alcance da mira
            Gizmos.color = new Color(1f, 1f, 1f, 0.4f);
            Gizmos.DrawRay(c.transform.position, c.transform.forward * distanciaMaxima);
        }

        if (orientacao == OrientacaoNaInspecao.FaceParaOJogador)
        {
            var colisor = GetComponentInChildren<Collider>();
            Vector3 origem = colisor != null ? colisor.bounds.center : transform.position;
            float comp = Mathf.Max(0.15f, tamanhoNaTela * 0.5f);

            Gizmos.color = Color.yellow; // lado que ficará virado para o jogador
            Gizmos.DrawLine(origem, origem + transform.TransformDirection(Eixo(faceVoltadaParaJogador)) * comp);
            Gizmos.color = Color.cyan;   // lado que ficará para cima na tela
            Gizmos.DrawLine(origem, origem + transform.TransformDirection(Eixo(topoNaTela)) * comp * 0.6f);
        }
    }

    // ---------- Interface simples (sem precisar criar Canvas) ----------
    void OnGUI()
    {
        useGUILayout = false;   // só GUI.*: sem o passe de Layout nem a alocação dele a cada frame
        bool mostrandoAviso = emInspecao == null && melhor == this;
        bool inspecionando = emInspecao == this;
        if (!mostrandoAviso && !inspecionando) return;

        if (estilo == null)
        {
            estilo = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            estilo.normal.textColor = Color.white;
        }

        if (mostrandoAviso)
        {
            HudInteracao.Interagir(teclaInteracao);
            var pilha = PilhaDeDocumentos.De(this);
            if (pilha != null && pilha.Total > 1)
            {
                int total = pilha.Total;
                int naoLidas = pilha.NaoLidas;
                string aviso = $"{total} papéis aqui";
                if (naoLidas == total) aviso += " · nenhum lido";
                else if (naoLidas > 0) aviso += $" · {naoLidas} não lidos";
                HudInteracao.Texto(aviso, 0.5f + 0.045f + 0.032f, 0.75f);
            }
        }
        else
        {
            string texto = !string.IsNullOrEmpty(instrucoes) ? instrucoes
                : manipulador != null && !string.IsNullOrEmpty(manipulador.Instrucoes) ? manipulador.Instrucoes
                : fixoNoLugar ? "Scroll: aproximar  |  Mouse: mover a vista de perto"
                : "Clique e arraste: girar  |  Scroll: zoom";
            var pilha = PilhaDeDocumentos.De(this);
            if (pilha != null && pilha.Total > 1)
                texto += $"  |  ← →: outro papel ({pilha.Indice(this) + 1}/{pilha.Total})";
            HudInteracao.Texto($"{texto}  |  {teclaInteracao.ToUpper()} ou Esc: fechar", 0.94f);
        }
    }

    // ---------- Entrada: funciona nos dois sistemas de input da Unity ----------
    static bool TeclaApertada(string nomeTecla)
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null) return false;
        if (!Enum.TryParse(nomeTecla, true, out Key k)) return false;
        return Keyboard.current[k].wasPressedThisFrame;
#else
        if (!Enum.TryParse(nomeTecla, true, out KeyCode k)) return false;
        return Input.GetKeyDown(k);
#endif
    }

    static bool BotaoEsquerdoPressionado()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
        return Input.GetMouseButton(0);
#endif
    }

    static Vector2 PosicaoMouse()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }

    static float ScrollVertical()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
#else
        return Input.mouseScrollDelta.y;
#endif
    }
}

/// <summary>O que o manipulador usou da entrada neste quadro (o resto continua girando/aproximando o objeto).</summary>
[Flags]
public enum UsoDaEntrada
{
    Nenhum = 0,
    Ponteiro = 1,   // clique/arraste
    Rolagem = 2     // scroll
}

/// <summary>
/// Peças manipuláveis durante a inspeção de um ObjetoInspecionavel (no mesmo objeto). A inspeção mostra uma cópia:
/// o manipulador a recebe ao abrir e é responsável por mantê-la igual ao original.
/// </summary>
public interface IManipulavelNaInspecao
{
    /// <summary>Chamado ao abrir, com a raiz da cópia (já na frente da câmera) e a câmera do jogador.</summary>
    void AoIniciarInspecao(Transform copia, Camera camera);

    /// <summary>A cada quadro da inspeção: posição do mouse (pixels), botão esquerdo e scroll (-1..1).</summary>
    UsoDaEntrada ProcessarEntrada(Camera camera, Vector2 mouse, bool botao, float scroll);

    /// <summary>Chamado ao fechar (a cópia já foi destruída).</summary>
    void AoFinalizarInspecao();

    /// <summary>Instruções no rodapé (vazio = as padrão).</summary>
    string Instrucoes { get; }
}

public enum OrientacaoNaInspecao
{
    FaceParaOJogador,
    ManterDaCena,
    RotacaoManual
}

public enum EixoLocal
{
    Cima,
    Baixo,
    Frente,
    Tras,
    Direita,
    Esquerda
}
