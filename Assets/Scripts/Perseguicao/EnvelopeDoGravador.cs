using UnityEngine;

/// <summary>
/// Envelope de guarda lacrado contendo o gravador de Léo (compartimento G-B2).
/// Na inspeção: clicar abre a aba, desliza o gravador para fora e o traz em foco para o centro da câmera;
/// em foco, arrastar gira o gravador e um clique curto o pega (mesma lógica do componente Gravador).
/// </summary>
[RequireComponent(typeof(ObjetoInspecionavel))]
public class EnvelopeDoGravador : MonoBehaviour, IManipulavelNaInspecao, ISalvavel
{
    public Transform aba;              // filho "Aba": pivô na dobradiça; gira em torno do X local
    public Gravador gravador;          // filho "Gravador de Leo" (componente Gravador já existente)
    public float anguloAberta = -140f; // graus em X local da aba aberta
    public Vector3 deslize = new Vector3(0f, 0f, 0.10f); // deslocamento local do gravador meio para fora
    public float escalaDoFoco = 2.2f;  // quanto o gravador cresce em foco (relativo ao tamanho na cópia)
    public float duracaoAbrir = 0.9f, duracaoDeslizar = 0.9f, duracaoFoco = 0.8f;

    const string FatoGravadorPego = "gravador_pego";
    const float DuracaoTremor = 0.15f;
    const float QuedaEnvelope = 0.35f;
    const float InclinacaoFoco = 15f;
    const float LimiteCliqueCurtoPx = 8f;

    enum Estado { Lacrado, Aberto, Vazio }
    enum Fase { Parado, Abrindo, Deslizando, Foco, Olhando }

    Estado estado = Estado.Lacrado;
    bool inicializado;
    Quaternion abaRotInicial = Quaternion.identity;
    Vector3 gravPosInicial;
    Quaternion gravRotInicial = Quaternion.identity;
    Vector3 gravEscInicial = Vector3.one;

    // Estado transitório da inspeção (cópia)
    Fase fase = Fase.Parado;
    float tempoFase;
    Transform copia, copiaAba, copiaGravador;
    Vector3 copiaPosBase;
    Vector3 focoGravPosDe, focoGravEscDe, focoGravEscPara;
    Quaternion focoGravRotDe, focoGravRotPara;
    Vector3 focoEnvPosDe, focoEnvPosPara;
    bool botaoAntes;
    bool apertouNoOlhando;
    Vector2 posAperto;
    float maxDeslocamento;

    void Awake()
    {
        // A cópia criada pelo ObjetoInspecionavel traz este componente; ela não deve alterar a própria pose.
        if (ObjetoInspecionavel.InspecionandoAlgo)
        {
            enabled = false;
            return;
        }
        Inicializar();
    }

    /// <summary>Guarda a pose inicial fechada e aplica o estado atual (também usado em testes EditMode).</summary>
    public void Inicializar()
    {
        if (inicializado) return;
        inicializado = true;

        if (aba == null) aba = Achar(transform, "Aba");
        if (gravador == null)
        {
            Transform tGrav = Achar(transform, "Gravador de Leo");
            if (tGrav != null) gravador = tGrav.GetComponent<Gravador>();
        }

        if (aba != null) abaRotInicial = aba.localRotation;
        if (gravador != null)
        {
            Transform tg = gravador.transform;
            gravPosInicial = tg.localPosition;
            gravRotInicial = tg.localRotation;
            gravEscInicial = tg.localScale;
        }

        if (EstadoDaNoite.Tem(FatoGravadorPego))
            estado = Estado.Vazio;

        Aplicar(transform, estado);
    }

    // ---------- Pose (original ou cópia) ----------

    void Aplicar(Transform raizAlvo, Estado e)
    {
        if (raizAlvo == null) return;
        bool eOriginal = raizAlvo == transform;
        Transform abaAlvo = eOriginal ? aba : Achar(raizAlvo, NomeAba());
        Transform gravAlvo = eOriginal
            ? (gravador != null ? gravador.transform : null)
            : Achar(raizAlvo, NomeGravador());

        Quaternion rotAba = e == Estado.Lacrado
            ? abaRotInicial
            : abaRotInicial * Quaternion.Euler(anguloAberta, 0f, 0f);
        if (abaAlvo != null) abaAlvo.localRotation = rotAba;

        if (gravAlvo == null) return;

        if (e == Estado.Vazio)
        {
            DefinirRenderers(gravAlvo, false);
            if (eOriginal)
            {
                DefinirColliders(gravAlvo, false);
                if (gravador != null) gravador.enabled = false;
                gravAlvo.gameObject.SetActive(false);
            }
            else
            {
                DesativarInteracaoNaCopia(gravAlvo);
                gravAlvo.gameObject.SetActive(false);
            }
            return;
        }

        if (eOriginal && !gravAlvo.gameObject.activeSelf)
            gravAlvo.gameObject.SetActive(true);
        else if (!eOriginal && !gravAlvo.gameObject.activeSelf)
            gravAlvo.gameObject.SetActive(true);

        bool aberto = e == Estado.Aberto;
        gravAlvo.localPosition = aberto ? gravPosInicial + deslize : gravPosInicial;
        gravAlvo.localRotation = gravRotInicial;
        gravAlvo.localScale = gravEscInicial;
        DefinirRenderers(gravAlvo, aberto);

        if (eOriginal)
        {
            DefinirColliders(gravAlvo, aberto);
            if (gravador != null) gravador.enabled = aberto;
        }
        else
        {
            DesativarInteracaoNaCopia(gravAlvo);
        }
    }

    string NomeAba() => aba != null ? aba.name : "Aba";
    string NomeGravador() => gravador != null ? gravador.name : "Gravador de Leo";

    static Transform Achar(Transform raiz, string nome)
    {
        if (raiz == null) return null;
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
            if (t != raiz && t.name == nome) return t;
        return null;
    }

    static void DefinirRenderers(Transform raiz, bool ativo)
    {
        if (raiz == null) return;
        foreach (Renderer r in raiz.GetComponentsInChildren<Renderer>(true))
            r.enabled = ativo;
    }

    static void DefinirColliders(Transform raiz, bool ativo)
    {
        if (raiz == null) return;
        foreach (Collider c in raiz.GetComponentsInChildren<Collider>(true))
            c.enabled = ativo;
    }

    static void DesativarInteracaoNaCopia(Transform gravAlvo)
    {
        DefinirColliders(gravAlvo, false);
        foreach (Gravador g in gravAlvo.GetComponentsInChildren<Gravador>(true))
            g.enabled = false;
    }

    // ---------- Inspeção (IManipulavelNaInspecao) ----------

    public string Instrucoes
    {
        get
        {
            if (fase == Fase.Olhando) return "Arraste: girar   Clique: pegar o gravador";
            if (fase != Fase.Parado) return "";
            if (EstadoDaNoite.Tem(FatoGravadorPego) || estado == Estado.Vazio) return "";
            return estado == Estado.Lacrado ? "Clique: abrir o envelope" : "Clique: tirar o gravador";
        }
    }

    public void AoIniciarInspecao(Transform copiaRaiz, Camera camera)
    {
        Inicializar();
        if (EstadoDaNoite.Tem(FatoGravadorPego))
            estado = Estado.Vazio;

        copia = copiaRaiz;
        if (copia != null)
        {
            foreach (EnvelopeDoGravador clone in copia.GetComponentsInChildren<EnvelopeDoGravador>(true))
                clone.enabled = false;
        }

        copiaAba = Achar(copia, NomeAba());
        copiaGravador = Achar(copia, NomeGravador());
        Aplicar(copia, estado);

        fase = Fase.Parado;
        tempoFase = 0f;
        apertouNoOlhando = false;
        botaoAntes = true; // ignora botão já pressionado ao abrir
    }

    public UsoDaEntrada ProcessarEntrada(Camera camera, Vector2 mouse, bool botao, float scroll)
    {
        if (copia == null) return UsoDaEntrada.Nenhum;

        bool apertou = botao && !botaoAntes;
        bool soltou = !botao && botaoAntes;
        UsoDaEntrada uso = UsoDaEntrada.Nenhum;

        switch (fase)
        {
            case Fase.Parado:
                if (apertou)
                {
                    if (estado == Estado.Lacrado)
                    {
                        IniciarAbertura();
                        uso = UsoDaEntrada.Ponteiro | UsoDaEntrada.Rolagem;
                    }
                    else if (estado == Estado.Aberto)
                    {
                        IniciarFoco(camera);
                        uso = UsoDaEntrada.Ponteiro | UsoDaEntrada.Rolagem;
                    }
                }
                break;

            case Fase.Abrindo:
                AvancarAbertura();
                uso = UsoDaEntrada.Ponteiro | UsoDaEntrada.Rolagem;
                break;

            case Fase.Deslizando:
                AvancarDeslize(camera);
                uso = UsoDaEntrada.Ponteiro | UsoDaEntrada.Rolagem;
                break;

            case Fase.Foco:
                AvancarFoco();
                uso = UsoDaEntrada.Ponteiro | UsoDaEntrada.Rolagem;
                break;

            case Fase.Olhando:
                uso = UsoDaEntrada.Nenhum;
                if (apertou)
                {
                    apertouNoOlhando = true;
                    posAperto = mouse;
                    maxDeslocamento = 0f;
                }
                else if (botao && apertouNoOlhando)
                {
                    float d = Vector2.Distance(mouse, posAperto);
                    if (d > maxDeslocamento) maxDeslocamento = d;
                }
                else if (soltou && apertouNoOlhando)
                {
                    apertouNoOlhando = false;
                    float d = Vector2.Distance(mouse, posAperto);
                    if (d > maxDeslocamento) maxDeslocamento = d;
                    if (maxDeslocamento < LimiteCliqueCurtoPx)
                    {
                        Pegar();
                        uso = UsoDaEntrada.Ponteiro | UsoDaEntrada.Rolagem;
                    }
                }
                break;
        }

        botaoAntes = botao;
        return uso;
    }

    public void AoFinalizarInspecao()
    {
        copia = null;
        copiaAba = null;
        copiaGravador = null;
        fase = Fase.Parado;
        tempoFase = 0f;
        apertouNoOlhando = false;
        botaoAntes = false;

        if (EstadoDaNoite.Tem(FatoGravadorPego))
            estado = Estado.Vazio;

        Aplicar(transform, estado);
    }

    void IniciarAbertura()
    {
        fase = Fase.Abrindo;
        tempoFase = 0f;
        copiaPosBase = copia.localPosition;
    }

    void AvancarAbertura()
    {
        tempoFase += Time.unscaledDeltaTime;

        // Tremor curto antes da aba abrir (fita rasgando).
        if (tempoFase < DuracaoTremor)
        {
            float queda = 1f - tempoFase / DuracaoTremor;
            float amp = 0.0025f * queda;
            Vector3 sacode = new Vector3(Mathf.Sin(tempoFase * 95f) * amp, Mathf.Cos(tempoFase * 130f) * amp * 0.5f, 0f);
            if (copia.parent != null)
                copia.localPosition = copiaPosBase + copia.parent.InverseTransformVector(copia.TransformVector(sacode));
            return;
        }

        copia.localPosition = copiaPosBase;
        float dur = Mathf.Max(0.01f, duracaoAbrir);
        float t = Mathf.Clamp01((tempoFase - DuracaoTremor) / dur);
        float s = Mathf.SmoothStep(0f, 1f, t);

        if (copiaAba != null)
            copiaAba.localRotation = abaRotInicial * Quaternion.Euler(anguloAberta * s, 0f, 0f);

        if (t >= 1f)
            IniciarDeslize();
    }

    void IniciarDeslize()
    {
        fase = Fase.Deslizando;
        tempoFase = 0f;
        if (copiaGravador != null)
        {
            copiaGravador.localPosition = gravPosInicial;
            copiaGravador.localRotation = gravRotInicial;
            copiaGravador.localScale = gravEscInicial;
            DefinirRenderers(copiaGravador, true);
        }
    }

    void AvancarDeslize(Camera camera)
    {
        tempoFase += Time.unscaledDeltaTime;
        float dur = Mathf.Max(0.01f, duracaoDeslizar);
        float t = Mathf.Clamp01(tempoFase / dur);
        float s = Mathf.SmoothStep(0f, 1f, t);

        if (copiaGravador != null)
            copiaGravador.localPosition = Vector3.Lerp(gravPosInicial, gravPosInicial + deslize, s);

        if (t >= 1f)
        {
            estado = Estado.Aberto;
            IniciarFoco(camera);
        }
    }

    void IniciarFoco(Camera camera)
    {
        fase = Fase.Foco;
        tempoFase = 0f;

        Transform pivo = copia.parent;
        focoEnvPosDe = copia.localPosition;
        Vector3 quedaMundo = camera != null ? -camera.transform.up * QuedaEnvelope : Vector3.down * QuedaEnvelope;
        focoEnvPosPara = pivo != null
            ? focoEnvPosDe + pivo.InverseTransformVector(quedaMundo)
            : focoEnvPosDe + quedaMundo;

        if (copiaGravador != null)
        {
            if (pivo != null) copiaGravador.SetParent(pivo, true);
            focoGravPosDe = copiaGravador.localPosition;
            focoGravRotDe = copiaGravador.localRotation;
            focoGravEscDe = copiaGravador.localScale;
            focoGravEscPara = focoGravEscDe * escalaDoFoco;

            Vector3 eixoX = (pivo != null && camera != null)
                ? pivo.InverseTransformDirection(camera.transform.right).normalized
                : Vector3.right;
            focoGravRotPara = Quaternion.AngleAxis(InclinacaoFoco, eixoX) * focoGravRotDe;
        }
    }

    void AvancarFoco()
    {
        tempoFase += Time.unscaledDeltaTime;
        float dur = Mathf.Max(0.01f, duracaoFoco);
        float t = Mathf.Clamp01(tempoFase / dur);
        float s = Mathf.SmoothStep(0f, 1f, t);

        if (copiaGravador != null)
        {
            copiaGravador.localPosition = Vector3.Lerp(focoGravPosDe, Vector3.zero, s);
            copiaGravador.localRotation = Quaternion.Slerp(focoGravRotDe, focoGravRotPara, s);
            copiaGravador.localScale = Vector3.Lerp(focoGravEscDe, focoGravEscPara, s);
        }
        copia.localPosition = Vector3.Lerp(focoEnvPosDe, focoEnvPosPara, s);

        if (t >= 1f)
        {
            // O gravador já é filho do pivô, então desliga só os renderers do envelope e da aba.
            DefinirRenderers(copia, false);
            fase = Fase.Olhando;
            apertouNoOlhando = false;
        }
    }

    void Pegar()
    {
        if (gravador != null)
        {
            gravador.enabled = true;
            gravador.Interagir();
        }
        estado = Estado.Vazio;
        ObjetoInspecionavel inspecao = GetComponent<ObjetoInspecionavel>();
        if (inspecao != null) inspecao.EncerrarInspecao();
    }

    // ---------- Save (ISalvavel) ----------

    public string Salvar()
    {
        if (EstadoDaNoite.Tem(FatoGravadorPego))
            estado = Estado.Vazio;
        return ((int)estado).ToString();
    }

    public void Carregar(string estadoSalvo)
    {
        Inicializar();
        if (EstadoDaNoite.Tem(FatoGravadorPego) || estadoSalvo == "2")
            estado = Estado.Vazio;
        else if (estadoSalvo == "1")
            estado = Estado.Aberto;
        else
            estado = Estado.Lacrado;

        Aplicar(transform, estado);
    }
}
