using UnityEngine;

/// <summary>
/// "Põe o gravador no chão e vai embora. Ninguém precisa se machucar esta noite." Alguns segundos depois de o jogador pegar o gravador (ou logo depois de ouvir a
/// gravação), Daniel aparece no PontoDeAcao de aparição (Arquivo/Porta) e o jogador escolhe:
///   Entregar: "gravador_entregue"; Daniel pega e vai embora, sem perseguição.
///   Ficar com ele: "caca"; a perseguição começa.
/// Fica no objeto "Sistemas" da cena Jogabilidade.
/// </summary>
public class Confronto : InterfaceDeTela
{
    public string pontoDeAparicao = "Arquivo/Porta";
    [Tooltip("Segundos (sem contar leitura e telas) entre pegar o gravador e Daniel aparecer.")]
    [Min(0f)] public float espera = 25f;
    [Tooltip("Depois de ouvir a gravação inteira, ele aparece logo.")]
    [Min(0f)] public float esperaDepoisDeOuvir = 2f;
    [TextArea] public string fala = "Põe o gravador no chão e vai embora.\nNinguém precisa se machucar esta noite.";
    [Min(0f)] public float esperaAntesDaCaca = 1.2f;

    public override float AlcanceDeInteracao => 0f;
    public override string Indicacao => "";
    public override void Interagir() { }

    float contado, ouviuEm = -1f, cacaEm = -1f;
    bool apareceu;

    protected override void Update()
    {
        base.Update();
        if (cacaEm > 0f && Time.time >= cacaEm)
        {
            cacaEm = -1f;
            if (DanielPerseguidor.Instancia != null) DanielPerseguidor.Instancia.ComecarCaca();
        }
        if (EstaAberta || TelaDeFinal.Ativa || !EstadoDaNoite.Tem("gravador_pego") || EstadoDaNoite.Tem("confronto_feito")) return;
        if (Jogador.Ocupado) return;
        contado += Time.deltaTime;
        if (EstadoDaNoite.Tem("gravacao_ouvida") && ouviuEm < 0f) ouviuEm = contado;
        bool pronto = contado >= espera || (ouviuEm >= 0f && contado - ouviuEm >= esperaDepoisDeOuvir);
        if (!pronto) return;
        if (InterfaceDeTela.AlgumaAberta)
        {
            if (InterfaceDeTela.Aberta is Celular celular) celular.GuardarJa();
            return;
        }
        Comecar();
    }

    public void Comecar()
    {
        if (EstaAberta || InterfaceDeTela.AlgumaAberta) return;
        if (Celular.Instancia != null && (Celular.Instancia.ModoCamera || (Celular.Instancia.NaMao != null && Celular.Instancia.NaMao.Visivel)))
            Celular.Instancia.GuardarJa();
        if (!apareceu && DanielPerseguidor.Instancia != null)
        {
            apareceu = true;
            PontoDeAcao p = PontoDeAcao.Buscar(pontoDeAparicao);
            Vector3 frente = Jogador.Movimento != null ? Jogador.Movimento.transform.forward : Vector3.forward;
            Vector3 pos = p != null ? p.transform.position : Jogador.Posicao + frente * 4f;
            Vector3 olhar = Jogador.Posicao - pos;
            olhar.y = 0f;
            DanielPerseguidor.Instancia.Aparecer(pos, olhar.sqrMagnitude > 0.01f ? Quaternion.LookRotation(olhar) : Quaternion.identity);
        }
        AbrirInterface();
    }

    // Esc não foge da escolha.
    protected override void PedirFechar() { }

    void Escolher(bool entregar)
    {
        EstadoDaNoite.Marcar("confronto_feito");
        if (entregar)
        {
            EstadoDaNoite.Marcar("gravador_entregue");
            if (DanielPerseguidor.Instancia != null) DanielPerseguidor.Instancia.IrEmbora();
            MiraDeInteracao.Avisar("Ele pegou o gravador e sumiu pelo corredor.", 3f);
        }
        else
        {
            EstadoDaNoite.Marcar("caca");
            cacaEm = Time.time + esperaAntesDaCaca;
        }
        FecharInterface();
    }

    // ------------------------------------------------------------------ tela

    // Encenação: a câmera vira para o rosto de Daniel, faixas de cinema fecham a tela, a fala dele surge letra a
    // letra como legenda e, depois, as duas respostas do jogador aparecem na máquina de escrever (a voz do
    // jornalista, como na pergunta de saída e nos finais).
    const float DuracaoGiro = 0.7f;
    const float DuracaoFaixas = 0.5f;
    const float AtrasoFala = 0.55f;
    const float LetrasPorSegundo = 26f;
    const float PausaAntesDasEscolhas = 0.6f;
    const float DuracaoEscolhas = 0.45f;
    const float DuracaoHover = 0.15f;
    const float FracaoFaixa = 0.115f;

    static readonly Color CorDoNome = new Color(0.78f, 0.34f, 0.30f);

    float abertaEm, ultimoRepaint, hoverEntregar, hoverFicar;
    Quaternion giroInicial, giroAlvo;
    float inclinacaoInicial, inclinacaoAlvo;
    bool girando;
    Texture2D vinheta;

    protected override void AoAbrir()
    {
        abertaEm = Time.unscaledTime;
        ultimoRepaint = abertaEm;
        hoverEntregar = hoverFicar = 0f;
        GarantirVinheta();
        PrepararGiro();
    }

    protected override void AoFechar()
    {
        // Devolve o olhar ao Movimento já virado para Daniel (ele guarda a inclinação por conta própria).
        Movimento m = Jogador.Movimento;
        if (girando && m != null)
            m.Teleportar(m.transform.position, giroAlvo.eulerAngles.y, inclinacaoAlvo, m.Agachado);
        girando = false;
    }

    void PrepararGiro()
    {
        girando = false;
        Movimento m = Jogador.Movimento;
        DanielPerseguidor d = DanielPerseguidor.Instancia;
        if (m == null || m.camera == null || d == null) return;
        Transform cabeca = d.animator != null && d.animator.isHuman ? d.animator.GetBoneTransform(HumanBodyBones.Head) : null;
        Vector3 alvo = cabeca != null ? cabeca.position : d.transform.position + Vector3.up * 1.6f;
        Vector3 dir = alvo - m.camera.position;
        Vector3 plano = new Vector3(dir.x, 0f, dir.z);
        if (plano.sqrMagnitude < 0.01f) return;
        giroInicial = m.transform.rotation;
        giroAlvo = Quaternion.LookRotation(plano);
        inclinacaoInicial = Mathf.DeltaAngle(0f, m.camera.localEulerAngles.x);
        inclinacaoAlvo = Mathf.Clamp(-Mathf.Atan2(dir.y, plano.magnitude) * Mathf.Rad2Deg, -60f, 60f);
        girando = true;
    }

    protected override void AtualizarAberta()
    {
        if (!girando) return;
        Movimento m = Jogador.Movimento;
        if (m == null) return;
        float p = Suave((Time.unscaledTime - abertaEm) / DuracaoGiro);
        m.transform.rotation = Quaternion.Slerp(giroInicial, giroAlvo, p);
        m.camera.localRotation = Quaternion.Euler(Mathf.Lerp(inclinacaoInicial, inclinacaoAlvo, p), 0f, 0f);
    }

    void OnDestroy()
    {
        if (vinheta != null) { Destroy(vinheta); vinheta = null; }
    }

    protected override void Desenhar()
    {
        useGUILayout = false;
        GarantirVinheta();
        float agora = Time.unscaledTime;
        float t = agora - abertaEm;
        float s = DesenhoUi.S;
        float W = Screen.width, H = Screen.height;

        // Vinheta e faixas de cinema.
        float pFaixas = Suave(t / DuracaoFaixas);
        Color antes = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, pFaixas);
        GUI.DrawTexture(new Rect(0f, 0f, W, H), vinheta, ScaleMode.StretchToFill);
        GUI.color = antes;
        float faixa = H * FracaoFaixa * pFaixas;
        DesenhoUi.Caixa(new Rect(0f, 0f, W, faixa), Color.black);
        DesenhoUi.Caixa(new Rect(0f, H - faixa, W, faixa), Color.black);

        // Fala de Daniel, letra a letra.
        float tFala = t - AtrasoFala;
        if (tFala <= 0f) return;
        int letras = Mathf.Clamp(Mathf.FloorToInt(tFala * LetrasPorSegundo), 0, fala.Length);
        float fimDaFala = fala.Length / LetrasPorSegundo;
        float pNome = Suave(tFala / 0.3f);

        float largura = Mathf.Min(1000f * s, W - 160f * s);
        float x = (W - largura) * 0.5f;
        GUIStyle texto = DesenhoUi.Estilo(31f, Color.white, false, TextAnchor.UpperCenter, true);
        float alturaFala = texto.CalcHeight(new GUIContent(fala), largura);
        // Fala logo acima da faixa de baixo (o rosto de Daniel fica livre no centro); respostas dentro da faixa.
        float yFala = H * (1f - FracaoFaixa) - alturaFala - 22f * s;
        GUIStyle nome = DesenhoUi.Estilo(15f, new Color(CorDoNome.r, CorDoNome.g, CorDoNome.b, pNome), true, TextAnchor.LowerCenter);
        GUI.Label(new Rect(x, yFala - 34f * s, largura, 26f * s), "D A N I E L", nome);
        DesenhoUi.Caixa(new Rect(W * 0.5f - 18f * s * pNome, yFala - 6f * s, 36f * s * pNome, Mathf.Max(1f, 1f * s)),
            new Color(CorDoNome.r, CorDoNome.g, CorDoNome.b, 0.7f * pNome));

        // O texto inteiro é medido e só a parte já "dita" fica visível, para a linha não pular de lugar.
        string dita = fala.Substring(0, letras);
        string resto = fala.Substring(letras);
        string rich = "<color=#ffffffff>" + dita + "</color><color=#ffffff00>" + resto + "</color>";
        GUIStyle sombra = DesenhoUi.Estilo(31f, new Color(0f, 0f, 0f, 0.6f), false, TextAnchor.UpperCenter);
        GUI.Label(new Rect(x + 1.5f * s, yFala + 2f * s, largura, alturaFala), dita, sombra);
        GUI.Label(new Rect(x, yFala, largura, alturaFala), rich, texto);

        // Respostas do jogador.
        float pEscolhas = Suave((tFala - fimDaFala - PausaAntesDasEscolhas) / DuracaoEscolhas);
        if (pEscolhas <= 0.001f) return;
        const string entregar = "Entregar o gravador", ficar = "Ficar com ele e correr";
        GUIStyle medida = DesenhoUi.EstiloMaquina(22f, Color.white, TextAnchor.MiddleCenter);
        Vector2 tamE = medida.CalcSize(new GUIContent(entregar)), tamF = medida.CalcSize(new GUIContent(ficar));
        float pad = 18f * s, alt = Mathf.Max(38f * s, Mathf.Max(tamE.y, tamF.y) + 14f * s), espaco = 120f * s;
        float wE = tamE.x + pad * 2f, wF = tamF.x + pad * 2f;
        float x0 = (W - (wE + espaco + wF)) * 0.5f;
        float y = H * (1f - FracaoFaixa * 0.5f) - alt * 0.5f + (1f - pEscolhas) * 6f * s;
        Rect rE = new Rect(x0, y, wE, alt), rF = new Rect(x0 + wE + espaco, y, wF, alt);
        DesenhoUi.Caixa(new Rect(W * 0.5f - 0.5f * s, y + alt * 0.2f, Mathf.Max(1f, 1f * s), alt * 0.6f),
            new Color(1f, 1f, 1f, 0.25f * pEscolhas));

        if (Event.current.type == EventType.Repaint)
        {
            float dt = Mathf.Clamp(agora - ultimoRepaint, 0f, 0.1f);
            ultimoRepaint = agora;
            Vector2 mouse = Event.current.mousePosition;
            hoverEntregar = Mathf.MoveTowards(hoverEntregar, pEscolhas > 0.1f && rE.Contains(mouse) ? 1f : 0f, dt / DuracaoHover);
            hoverFicar = Mathf.MoveTowards(hoverFicar, pEscolhas > 0.1f && rF.Contains(mouse) ? 1f : 0f, dt / DuracaoHover);
        }
        if (Escolha(rE, tamE.x, entregar, pEscolhas, hoverEntregar, s)) Escolher(true);
        if (Escolha(rF, tamF.x, ficar, pEscolhas, hoverFicar, s)) Escolher(false);
    }

    static bool Escolha(Rect r, float larguraTexto, string texto, float alfa, float hover, float s)
    {
        Color cor = Color.Lerp(new Color(0.66f, 0.66f, 0.64f, alfa), new Color(1f, 1f, 1f, alfa), hover);
        GUI.Label(new Rect(r.x, r.y + 1.5f * s, r.width, r.height), texto,
            DesenhoUi.EstiloMaquina(22f, new Color(0f, 0f, 0f, 0.5f * alfa), TextAnchor.MiddleCenter));
        GUI.Label(r, texto, DesenhoUi.EstiloMaquina(22f, cor, TextAnchor.MiddleCenter));
        if (hover > 0.001f)
        {
            float w = larguraTexto * Suave(hover);
            DesenhoUi.Caixa(new Rect(r.center.x - w * 0.5f, r.yMax - 4f * s, w, Mathf.Max(1f, 1.5f * s)),
                new Color(1f, 1f, 1f, 0.85f * alfa * hover));
        }
        return alfa > 0.1f && GUI.Button(r, GUIContent.none, GUIStyle.none);
    }

    void GarantirVinheta()
    {
        if (vinheta != null) return;
        const int tam = 128;
        vinheta = new Texture2D(tam, tam, TextureFormat.RGBA32, false)
        { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[tam * tam];
        for (int j = 0; j < tam; j++)
            for (int i = 0; i < tam; i++)
            {
                float u = i / (tam - 1f) * 2f - 1f, v = j / (tam - 1f) * 2f - 1f;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 1.3f, Mathf.Sqrt(u * u + v * v))) * 0.85f;
                px[j * tam + i] = new Color(0.02f, 0f, 0f, a);
            }
        vinheta.SetPixels(px);
        vinheta.Apply();
    }

    static float Suave(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
}
