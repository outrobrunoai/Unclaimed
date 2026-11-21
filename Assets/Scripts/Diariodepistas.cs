using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Caderno do jornalista. Coloque UM destes em um GameObject vazio da cena.
/// Tab abre/fecha. Duas abas: Evidências e Conclusões (do Caderno).
/// Interface feita com IMGUI (sem Canvas), com visual de bloco de repórter.
/// </summary>
public class DiarioDePistas : MonoBehaviour
{
    public static DiarioDePistas Instancia { get; private set; }
    public static bool Aberto => Instancia != null && Instancia.aberto;

    [Header("Controles")]
    public string teclaDiario = "Tab";
    [Tooltip("Se marcado, o jogo congela enquanto o diário está aberto.")]
    public bool pausarJogoAoAbrir = false;
    [Tooltip("Movimento do jogador, olhar com o mouse, lanterna etc.")]
    public Behaviour[] desativarComDiarioAberto;

    [Header("Aparência")]
    [Tooltip("Fonte dos títulos (máquina de escrever). Vazio = fonte padrão do Unity.")]
    public Font fonteTitulo;
    [Tooltip("Fonte do texto corrido.")]
    public Font fonteTexto;
    [Tooltip("Fonte dos rótulos, abas e botões.")]
    public Font fonteNegrito;
    [Tooltip("Letra de mão do jornalista: títulos, textos e itens da lista. Vazio = fontes acima.")]
    public Font fonteManuscrita, fonteManuscritaNegrito;
    [Tooltip("Duração (s) das animações de abrir e fechar. Usa tempo real, funciona com o jogo pausado.")]
    [Min(0.01f)] public float duracaoAbrir = 0.34f, duracaoFechar = 0.22f;
    [Tooltip("Texturas de papel (repetem lado a lado). Vazio = cor lisa.")]
    public Texture2D texturaPagina, texturaIndice, texturaCapa, texturaDivisoria, texturaNota;
    [Tooltip("Uma volta da espiral, repetida na vertical entre as páginas.")]
    public Texture2D texturaEspiral;

    [Header("Eventos")]
    public UnityEvent aoAbrirDiario;
    public UnityEvent aoFecharDiario;

    bool aberto;
    float animacao; // 0 = fora da tela, 1 = aberto; segue "aberto" com a duração configurada
    Vector2 scrollLista, scrollTexto;
    int aba;
    Evidencia evidenciaSelecionada;
    Comparacao conclusaoSelecionada;
    bool escolhendoComparacao;
    string mensagemComparacao;
    string textoComparacao;
    string avisoTexto;
    float avisoAte;

    CursorLockMode travaAnterior;
    bool cursorVisivelAnterior;
    bool pausou;
    float timeScaleAnterior = 1f;
    readonly List<Behaviour> desativados = new List<Behaviour>();

    GUIStyle sTitulo, sTexto, sItem, sRotulo, sEtiqueta, sAba, sBotao, sNotaTitulo, sCapa, sAviso, sRodape;

    void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }
        Instancia = this;
    }

    void OnEnable()
    {
        if (Instancia == this) EstadoDaNoite.AoMarcar += AoMarcarFato;
    }

    void AoMarcarFato(string fato)
    {
        if (!fato.StartsWith("ev:", StringComparison.Ordinal)) return;
        Evidencia evidencia = Caderno.Catalogo.Buscar(fato.Substring(3));
        if (evidencia == null) return;
        avisoTexto = $"Nova evidência no caderno: {evidencia.titulo}";
        avisoAte = Time.unscaledTime + 4f;
    }

    void OnDestroy()
    {
        if (sombra != null) Destroy(sombra);
        EstadoDaNoite.AoMarcar -= AoMarcarFato;
        if (Instancia == this) Instancia = null;
    }

    // ---------- API pública ----------

    /// <summary>Abre o caderno (se estiver fechado) já na aba pedida: 0 Evidências, 1 Conclusões.</summary>
    public static void AbrirNaAba(int aba)
    {
        if (Instancia == null) return;
        if (!Instancia.aberto)
        {
            if (!PodeAbrir) return;
            Instancia.Abrir();
        }
        Instancia.TrocarAba(aba);
    }

    // ---------- Abrir / fechar ----------

    /// <summary>O caderno só abre com o jogo rodando: sem pausa, inspeção, esconderijo, caça ou outra tela aberta.</summary>
    static bool PodeAbrir =>
        !MenuPausa.Pausado && !ObjetoInspecionavel.InspecionandoAlgo && !InterfaceDeTela.AlgumaAberta && !IntroChegada.Ativa &&
        !TelaDeFinal.Ativa && !DanielPerseguidor.Capturando && !Esconderijo.JogadorEscondido && !DecisoesDeDaniel.CacaEmAndamento;

    void Update()
    {
        if (aberto && (TelaDeFinal.Ativa || DanielPerseguidor.Capturando))
            Fechar();

        float alvo = aberto ? 1f : 0f;
        float duracao = aberto ? duracaoAbrir : duracaoFechar;
        animacao = Mathf.MoveTowards(animacao, alvo, Time.unscaledDeltaTime / Mathf.Max(0.01f, duracao));

        if (TeclaApertada(teclaDiario))
        {
            if (aberto) Fechar();
            else if (PodeAbrir) Abrir();
        }
        else if (aberto && TeclaApertada("Escape"))
        {
            Fechar();
        }
    }

    void Abrir()
    {
        aberto = true;

        desativados.Clear();
        foreach (var comp in desativarComDiarioAberto)
        {
            if (comp != null && comp.enabled)
            {
                comp.enabled = false;
                desativados.Add(comp);
            }
        }

        travaAnterior = Cursor.lockState;
        cursorVisivelAnterior = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        pausou = pausarJogoAoAbrir;
        if (pausou)
        {
            timeScaleAnterior = Time.timeScale;
            Time.timeScale = 0f;
        }

        aoAbrirDiario?.Invoke();
    }

    void Fechar()
    {
        aberto = false;

        foreach (var comp in desativados)
            if (comp != null && !(comp is Movimento && (TelaDeFinal.Ativa || DanielPerseguidor.Capturando)))
                comp.enabled = true;
        desativados.Clear();

        Cursor.lockState = travaAnterior;
        Cursor.visible = cursorVisivelAnterior;

        if (pausou)
        {
            if (!TelaDeFinal.Ativa) Time.timeScale = timeScaleAnterior;
            pausou = false;
        }

        aoFecharDiario?.Invoke();
    }

    void OnDisable()
    {
        EstadoDaNoite.AoMarcar -= AoMarcarFato;
        if (aberto) Fechar();
    }

    // ---------- Interface (IMGUI) ----------
    // Visual de bloco de repórter: capa escura, índice à esquerda, página pautada à direita e divisórias no topo.
    // Tudo é desenhado com retângulos e posições explícitas (GUI.*, sem GUILayout): o número de controles não
    // depende do passe de Layout, então trocar de aba ou comparar no meio de um clique não desalinha o IMGUI.

    static readonly Color corCapa = new Color(0.10f, 0.09f, 0.08f);
    static readonly Color corPapel = new Color(0.93f, 0.90f, 0.81f);
    static readonly Color corPapelIndice = new Color(0.87f, 0.83f, 0.73f);
    static readonly Color corAbaInativa = new Color(0.70f, 0.66f, 0.56f);
    static readonly Color corAbaSobre = new Color(0.79f, 0.75f, 0.65f);
    static readonly Color corPauta = new Color(0.40f, 0.50f, 0.62f, 0.20f);
    static readonly Color corMargem = new Color(0.72f, 0.22f, 0.20f, 0.45f);
    static readonly Color corTinta = new Color(0.11f, 0.13f, 0.20f);
    static readonly Color corTintaFraca = new Color(0.11f, 0.13f, 0.20f, 0.62f);
    static readonly Color corMarcador = new Color(0.96f, 0.80f, 0.25f, 0.55f);
    static readonly Color corNota = new Color(0.97f, 0.87f, 0.45f);
    static readonly Color corVermelho = new Color(0.68f, 0.16f, 0.14f);
    static readonly Color corArame = new Color(0.30f, 0.30f, 0.32f);

    static readonly string[] cabecalhos = { "EVIDÊNCIAS REUNIDAS", "CONCLUSÕES E HIPÓTESES" };

    readonly GUIContent conteudo = new GUIContent();
    readonly List<Evidencia> coletadas = new List<Evidencia>();
    readonly List<Comparacao> conclusoes = new List<Comparacao>();
    readonly string[] nomesDasAbas = new string[2];
    float escala = 1f; // 1 = tela de 1080 linhas
    int escalaDosEstilos;

    float S(float v) => v * escala;

    GUIStyle Estilo(Font fonte, float tamanho, Color cor, TextAnchor ancora = TextAnchor.UpperLeft, bool quebra = true)
    {
        var estilo = new GUIStyle
        {
            font = fonte,
            fontSize = Mathf.Max(9, Mathf.RoundToInt(S(tamanho))),
            wordWrap = quebra,
            alignment = ancora,
            richText = false,
        };
        estilo.normal.textColor = cor;
        return estilo;
    }

    void IniciarEstilos()
    {
        int chave = Mathf.RoundToInt(escala * 100f);
        if (sTitulo != null && chave == escalaDosEstilos) return;
        escalaDosEstilos = chave;

        // O que o jornalista escreveu sai em letra de mão; o que é impresso no caderno (abas, rótulos, botão) não.
        Font mao = fonteManuscrita != null ? fonteManuscrita : fonteTexto;
        Font maoNegrito = fonteManuscritaNegrito != null ? fonteManuscritaNegrito : (fonteManuscrita != null ? fonteManuscrita : fonteTitulo);
        bool manuscrita = fonteManuscrita != null;
        sTitulo = Estilo(maoNegrito, manuscrita ? 38 : 34, corTinta);
        sTexto = Estilo(mao, manuscrita ? 27 : 24, corTinta);
        sItem = Estilo(mao, manuscrita ? 24 : 21, corTinta, TextAnchor.MiddleLeft);
        sRotulo = Estilo(fonteNegrito, 15, corTintaFraca);
        sEtiqueta = Estilo(fonteNegrito, 13, corTintaFraca, TextAnchor.MiddleRight, false);
        sAba = Estilo(fonteNegrito, 19, corTinta, TextAnchor.MiddleCenter, false);
        sBotao = Estilo(fonteNegrito, 19, corTinta, TextAnchor.MiddleCenter, false);
        sNotaTitulo = Estilo(manuscrita ? maoNegrito : fonteNegrito, manuscrita ? 24 : 21, corTinta);
        sCapa = Estilo(fonteTitulo, 26, corPapel, TextAnchor.MiddleRight, false);
        sRodape = Estilo(fonteTexto, 16, new Color(0.82f, 0.80f, 0.72f, 0.85f), TextAnchor.MiddleCenter, false);
        sAviso = Estilo(mao, manuscrita ? 23 : 20, corTinta);
    }

    // ---------- Papel e sombras ----------

    const float LadoDoPapel = 640f; // tamanho de uma repetição da textura, em pixels de 1080p
    Texture2D sombra; // degradê horizontal: opaco à esquerda, transparente à direita

    /// <summary>Retângulo de papel: textura repetida (ancorada na tela, para o papel não "andar"), ou cor lisa.</summary>
    void Papel(Rect r, Texture2D textura, Color semTextura, Color? tom = null)
    {
        if (textura == null) { Caixa(r, semTextura); return; }
        float lado = S(LadoDoPapel);
        Color anterior = GUI.color;
        GUI.color = tom ?? Color.white;
        GUI.DrawTextureWithTexCoords(r, textura, new Rect(r.x / lado, -r.yMax / lado, r.width / lado, r.height / lado));
        GUI.color = anterior;
    }

    /// <summary>Sombra suave: escura no lado indicado e sumindo para o outro (0 esquerda, 1 direita).</summary>
    void Sombra(Rect r, float forca, bool escuraNaDireita)
    {
        if (sombra == null)
        {
            sombra = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            for (int x = 0; x < 64; x++)
            {
                float t = 1f - x / 63f;
                sombra.SetPixel(x, 0, new Color(0f, 0f, 0f, t * t));
            }
            sombra.Apply();
        }
        Color anterior = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, forca);
        GUI.DrawTextureWithTexCoords(r, sombra, escuraNaDireita ? new Rect(1, 0, -1, 1) : new Rect(0, 0, 1, 1));
        GUI.color = anterior;
    }

    static void Caixa(Rect r, Color cor)
    {
        Color anterior = GUI.color;
        GUI.color = cor;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = anterior;
    }

    static void Moldura(Rect r, Color cor, float e)
    {
        Caixa(new Rect(r.x, r.y, r.width, e), cor);
        Caixa(new Rect(r.x, r.yMax - e, r.width, e), cor);
        Caixa(new Rect(r.x, r.y, e, r.height), cor);
        Caixa(new Rect(r.xMax - e, r.y, e, r.height), cor);
    }

    bool Clique(Rect r) => aberto && GUI.Button(r, GUIContent.none, GUIStyle.none);

    float Altura(string texto, GUIStyle estilo, float largura)
    {
        conteudo.text = texto ?? "";
        return estilo.CalcHeight(conteudo, largura);
    }

    /// <summary>Um bloco de texto na página; devolve a altura ocupada. Com desenhar = false, só mede.</summary>
    float Bloco(float y, float largura, string texto, GUIStyle estilo, bool desenhar)
    {
        float a = Altura(texto, estilo, largura);
        if (desenhar) GUI.Label(new Rect(0, y, largura, a), texto ?? "", estilo);
        return a;
    }

    void OnGUI()
    {
        useGUILayout = false;
        escala = Mathf.Clamp(Screen.height / 1080f, 0.6f, 2.5f);
        IniciarEstilos();

        if (!aberto && Time.unscaledTime < avisoAte) DesenharAviso();
        if (animacao <= 0f) return;

        // Abrir: o caderno sobe de baixo da tela, passa um pouco do lugar e assenta.
        // Fechar: acelera para baixo. O fundo escurece junto.
        float deslocamento = aberto ? 1f - SaidaComRetorno(animacao) : EntradaCubica(1f - animacao);
        Caixa(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.72f * Mathf.SmoothStep(0f, 1f, animacao)));
        Matrix4x4 matrizAnterior = GUI.matrix;
        float escalaAnimada = Mathf.Lerp(1f, 0.94f, Mathf.Clamp01(deslocamento));
        var centro = new Vector2(Screen.width / 2f, Screen.height / 2f);
        GUI.matrix = Matrix4x4.Translate(new Vector3(0f, deslocamento * Screen.height * 0.85f, 0f)) * matrizAnterior;
        GUIUtility.ScaleAroundPivot(Vector2.one * escalaAnimada, centro);

        float hAbas = S(50), borda = S(14);
        float w = Mathf.Min(Screen.width * 0.92f, S(1500));
        float h = Mathf.Min(Screen.height * 0.80f, S(840));
        var folha = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f + hAbas * 0.5f, w, h);
        var capa = new Rect(folha.x - borda, folha.y - borda, folha.width + 2 * borda, folha.height + 2 * borda);
        Caixa(new Rect(capa.x + S(10), capa.y + S(14), capa.width, capa.height), new Color(0f, 0f, 0f, 0.45f));
        Papel(capa, texturaCapa, corCapa);
        // Espessura do bloco de folhas: tiras de papel um pouco mais escuras sob a borda de baixo.
        for (int i = 3; i >= 1; i--)
            Papel(new Rect(folha.x + S(2 * i), folha.yMax, folha.width - S(4 * i), S(2.5f) * i), texturaIndice, corPapelIndice,
                new Color(0.82f - i * 0.06f, 0.80f - i * 0.06f, 0.76f - i * 0.06f));

        AtualizarListas();
        DesenharAbas(folha, hAbas, borda);

        float lIndice = Mathf.Round(folha.width * 0.34f);
        var indice = new Rect(folha.x, folha.y, lIndice, folha.height);
        var pagina = new Rect(folha.x + lIndice, folha.y, folha.width - lIndice, folha.height);
        Papel(indice, texturaIndice, corPapelIndice);
        Papel(pagina, texturaPagina, corPapel);
        // Bordas externas levemente gastas e a curva das folhas junto à espiral.
        Sombra(new Rect(indice.x, indice.y, S(26), indice.height), 0.14f, false);
        Sombra(new Rect(pagina.xMax - S(26), pagina.y, S(26), pagina.height), 0.14f, true);
        Sombra(new Rect(indice.xMax - S(70), indice.y, S(70), indice.height), 0.30f, true);
        Sombra(new Rect(pagina.x, pagina.y, S(56), pagina.height), 0.26f, false);

        // Pautas, margem vermelha e espiral entre as duas páginas.
        float traco = Mathf.Max(1f, S(1.5f));
        for (float y = pagina.y + S(112); y < pagina.yMax - S(20); y += S(38))
            Caixa(new Rect(pagina.x + S(24), y, pagina.width - S(48), traco), corPauta);
        Caixa(new Rect(pagina.x + S(70), pagina.y, Mathf.Max(1f, S(2)), pagina.height), corMargem);
        if (texturaEspiral != null)
        {
            float le = S(128), ae = S(48);
            for (float y = folha.y + S(10); y < folha.yMax - ae; y += ae)
                GUI.DrawTexture(new Rect(pagina.x - le / 2f, y, le, ae), texturaEspiral);
        }
        else
            for (float y = folha.y + S(24); y < folha.yMax - S(24); y += S(40))
                Caixa(new Rect(pagina.x - S(17), y, S(34), S(8)), corArame);

        GUI.Label(new Rect(indice.x + S(30), indice.y + S(28), indice.width - S(60), S(26)), cabecalhos[aba], sRotulo);
        var areaLista = new Rect(indice.x + S(18), indice.y + S(68), indice.width - S(86), indice.height - S(88)); // deixa livre a faixa da espiral
        var areaPagina = new Rect(pagina.x + S(96), pagina.y + S(40), pagina.width - S(132), pagina.height - S(66));

        if (aba == 0) DesenharEvidencias(areaLista, areaPagina);
        else DesenharConclusoes(areaLista, areaPagina);

        GUI.Label(new Rect(0, folha.yMax + borda + S(10), Screen.width, S(26)),
            $"[{teclaDiario.ToUpper()}] ou [Esc] para fechar", sRodape);
        GUI.matrix = matrizAnterior;
    }

    // Curvas da animação (t de 0 a 1).
    static float SaidaComRetorno(float t)
    {
        const float c1 = 0.9f, c3 = c1 + 1f; // c1 controla o quanto passa do lugar antes de assentar
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    static float EntradaCubica(float t) => t * t * t;

    void AtualizarListas()
    {
        coletadas.Clear();
        coletadas.AddRange(Caderno.Coletadas());
        conclusoes.Clear();
        conclusoes.AddRange(Caderno.Conclusoes());
        if (!coletadas.Contains(evidenciaSelecionada)) evidenciaSelecionada = coletadas.Count > 0 ? coletadas[0] : null;
        if (!conclusoes.Contains(conclusaoSelecionada)) conclusaoSelecionada = conclusoes.Count > 0 ? conclusoes[0] : null;
        nomesDasAbas[0] = $"Evidências ({coletadas.Count})";
        nomesDasAbas[1] = $"Conclusões ({conclusoes.Count})";
    }

    void TrocarAba(int nova)
    {
        aba = Mathf.Clamp(nova, 0, 1);
        escolhendoComparacao = false;
        scrollLista = scrollTexto = Vector2.zero;
    }

    void DesenharAbas(Rect folha, float hAbas, float borda)
    {
        float largura = Mathf.Min(S(250), (folha.width - S(360)) / 2f), vao = S(8);
        for (int i = 0; i < 2; i++)
        {
            var r = new Rect(folha.x + S(26) + i * (largura + vao), folha.y - hAbas, largura, hAbas);
            bool ativa = i == aba;
            if (ativa)
            {
                Papel(r, texturaPagina, corPapel);
                Caixa(new Rect(r.x, r.y, r.width, S(5)), corVermelho);
            }
            else
            {
                bool sobre = r.Contains(Event.current.mousePosition);
                var cartolina = new Rect(r.x, r.y + S(8), r.width, r.height - S(8));
                Papel(cartolina, texturaDivisoria, sobre ? corAbaSobre : corAbaInativa,
                    sobre ? new Color(1.08f, 1.08f, 1.08f) : Color.white);
                Caixa(new Rect(cartolina.x, cartolina.yMax - S(3), cartolina.width, S(3)), new Color(0f, 0f, 0f, 0.18f));
            }
            sAba.normal.textColor = ativa ? corTinta : corTintaFraca;
            GUI.Label(new Rect(r.x, r.y + (ativa ? S(4) : S(8)), r.width, r.height - (ativa ? S(4) : S(8))), nomesDasAbas[i], sAba);
            if (!ativa && Clique(r)) TrocarAba(i);
        }
        GUI.Label(new Rect(folha.xMax - S(330), folha.y - hAbas, S(320), hAbas - borda), "CADERNO", sCapa);
    }

    // ---------- Índice (lista da esquerda) ----------

    float AlturaDaLinha(string titulo, float largura) =>
        Mathf.Max(S(48), Altura(titulo, sItem, largura - S(110)) + S(20));

    /// <summary>Uma linha do índice, em coordenadas da área de rolagem. Devolve true se foi clicada.</summary>
    bool Linha(ref float y, float largura, string titulo, string etiqueta, bool novo, bool selecionada)
    {
        float a = AlturaDaLinha(titulo, largura);
        var r = new Rect(0, y, largura, a);
        y += a;

        if (selecionada) Caixa(new Rect(r.x + S(4), r.y + S(5), r.width - S(8), r.height - S(10)), corMarcador);
        else if (r.Contains(Event.current.mousePosition)) Caixa(r, new Color(corTinta.r, corTinta.g, corTinta.b, 0.07f));
        if (novo) Caixa(new Rect(r.x + S(9), r.y + a / 2f - S(4), S(8), S(8)), corVermelho);
        GUI.Label(new Rect(r.x + S(26), r.y, largura - S(110), a), titulo, sItem);
        if (!string.IsNullOrEmpty(etiqueta)) GUI.Label(new Rect(r.xMax - S(82), r.y, S(72), a), etiqueta, sEtiqueta);
        Caixa(new Rect(r.x + S(8), r.yMax - 1f, r.width - S(16), 1f), corPauta);
        return Clique(r);
    }

    static string TipoPorExtenso(string tipo)
    {
        switch (tipo)
        {
            case "foto": return "FOTO";
            case "audio": return "GRAVAÇÃO";
            case "observacao": return "OBSERVAÇÃO";
            default: return "DOCUMENTO";
        }
    }

    static string TipoCurto(string tipo)
    {
        switch (tipo)
        {
            case "foto": return "FOTO";
            case "audio": return "ÁUDIO";
            case "observacao": return "VISTO";
            default: return "DOC";
        }
    }

    // ---------- Página (direita) ----------

    /// <summary>Rótulo pequeno, título e corpo. Devolve a altura total.</summary>
    float PaginaSimples(float largura, bool desenhar, string rotulo, string titulo, string corpo)
    {
        float y = 0f;
        y += Bloco(y, largura, rotulo, sRotulo, desenhar) + S(8);
        y += Bloco(y, largura, titulo, sTitulo, desenhar) + S(10);
        if (desenhar) Caixa(new Rect(0, y, S(90), Mathf.Max(2f, S(3))), corVermelho);
        y += S(22);
        y += Bloco(y, largura, corpo, sTexto, desenhar);
        return y;
    }

    void PaginaVazia(Rect area, string texto)
    {
        GUI.BeginGroup(area);
        Bloco(0, area.width, texto, sTexto, true);
        GUI.EndGroup();
    }

    void DesenharEvidencias(Rect areaLista, Rect areaPagina)
    {
        if (coletadas.Count == 0)
        {
            PaginaVazia(areaPagina, "Nenhuma evidência ainda.\nLeia documentos e tire fotos para reunir evidências.");
            return;
        }

        float largura = areaLista.width - S(18), total = 0f;
        foreach (Evidencia ev in coletadas) total += AlturaDaLinha(ev.titulo, largura);
        Evidencia clicada = null;
        scrollLista = GUI.BeginScrollView(areaLista, scrollLista, new Rect(0, 0, largura, total));
        float y = 0f;
        foreach (Evidencia ev in coletadas)
            if (Linha(ref y, largura, ev.titulo, TipoCurto(ev.tipo), false, ev == evidenciaSelecionada)) clicada = ev;
        GUI.EndScrollView();

        if (clicada != null)
        {
            if (escolhendoComparacao)
            {
                CompararEvidencias(evidenciaSelecionada.id, clicada.id);
                escolhendoComparacao = false;
            }
            else
            {
                evidenciaSelecionada = clicada;
                mensagemComparacao = textoComparacao = null;
            }
            scrollTexto = Vector2.zero;
        }

        float lp = areaPagina.width - S(18);
        float alt = PaginaDaEvidencia(lp, false);
        scrollTexto = GUI.BeginScrollView(areaPagina, scrollTexto, new Rect(0, 0, lp, alt));
        PaginaDaEvidencia(lp, true);
        GUI.EndScrollView();
    }

    float PaginaDaEvidencia(float largura, bool desenhar)
    {
        Evidencia ev = evidenciaSelecionada;
        string rotulo = TipoPorExtenso(ev.tipo) + (string.IsNullOrEmpty(ev.local) ? "" : "  ·  " + ev.local.ToUpper());
        float y = PaginaSimples(largura, desenhar, rotulo, ev.titulo, ev.resumo) + S(30);

        if (coletadas.Count < 2)
        {
            y += Bloco(y, largura, "Reúna outra evidência para poder comparar.", sRotulo, desenhar);
            return y;
        }

        // Botão de comparar: contorno em tinta; preenchido enquanto espera a segunda evidência.
        var botao = new Rect(0, y, Mathf.Min(largura, S(430)), S(52));
        if (desenhar)
        {
            bool sobre = botao.Contains(Event.current.mousePosition);
            if (escolhendoComparacao) Caixa(botao, corTinta);
            else
            {
                if (sobre) Caixa(botao, new Color(corTinta.r, corTinta.g, corTinta.b, 0.10f));
                Moldura(botao, corTinta, Mathf.Max(2f, S(2)));
            }
            sBotao.normal.textColor = escolhendoComparacao ? corPapel : corTinta;
            GUI.Label(botao, escolhendoComparacao ? "Cancelar comparação" : "Comparar com outra evidência", sBotao);
            if (Clique(botao))
            {
                escolhendoComparacao = !escolhendoComparacao;
                mensagemComparacao = textoComparacao = null;
            }
        }
        y += botao.height + S(16);

        if (escolhendoComparacao)
        {
            const string dica = "Escolha a segunda evidência na lista ao lado.";
            float a = Altura(dica, sNotaTitulo, largura - S(24)) + S(16);
            if (desenhar)
            {
                Caixa(new Rect(0, y, largura, a), corMarcador);
                GUI.Label(new Rect(S(12), y + S(8), largura - S(24), a - S(16)), dica, sNotaTitulo);
            }
            y += a + S(12);
        }

        if (!string.IsNullOrEmpty(mensagemComparacao))
        {
            // Resultado da comparação, como um bilhete colado na página.
            float pad = S(18), li = largura - 2 * pad - S(6);
            float hM = Altura(mensagemComparacao, sNotaTitulo, li);
            float hT = string.IsNullOrEmpty(textoComparacao) ? 0f : Altura(textoComparacao, sTexto, li) + S(8);
            var nota = new Rect(0, y, largura, pad * 2 + hM + hT);
            if (desenhar)
            {
                Caixa(new Rect(nota.x + S(4), nota.y + S(4), nota.width, nota.height), new Color(0f, 0f, 0f, 0.18f));
                Papel(nota, texturaNota, corNota);
                Caixa(new Rect(nota.x, nota.y, S(6), nota.height), corVermelho);
                GUI.Label(new Rect(nota.x + pad + S(6), nota.y + pad, li, hM), mensagemComparacao, sNotaTitulo);
                if (hT > 0f)
                    GUI.Label(new Rect(nota.x + pad + S(6), nota.y + pad + hM + S(8), li, hT - S(8)), textoComparacao, sTexto);
            }
            y += nota.height + S(12);
        }
        return y;
    }

    void CompararEvidencias(string a, string b)
    {
        ResultadoDaComparacao resultado = Caderno.Comparar(a, b, out Comparacao comparacao);
        textoComparacao = null;
        switch (resultado)
        {
            case ResultadoDaComparacao.Nova:
                mensagemComparacao = comparacao.tipo == "hipotese"
                    ? $"Nova hipótese: {comparacao.titulo}" : $"Nova conclusão: {comparacao.titulo}";
                textoComparacao = comparacao.texto;
                break;
            case ResultadoDaComparacao.JaRegistrada:
                mensagemComparacao = $"Você já registrou isto: {comparacao.titulo}";
                break;
            case ResultadoDaComparacao.SemRelacao:
                mensagemComparacao = "Estas duas evidências não têm ligação direta.";
                break;
            case ResultadoDaComparacao.FaltaEvidencia:
                mensagemComparacao = "Você precisa reunir as duas evidências antes de comparar.";
                break;
            default:
                mensagemComparacao = "Escolha duas evidências diferentes para comparar.";
                break;
        }
    }

    void DesenharConclusoes(Rect areaLista, Rect areaPagina)
    {
        if (conclusoes.Count == 0)
        {
            PaginaVazia(areaPagina, "Nenhuma conclusão ainda.\nCompare duas evidências na aba Evidências.");
            return;
        }

        float largura = areaLista.width - S(18), total = 0f;
        foreach (Comparacao c in conclusoes) total += AlturaDaLinha(c.titulo, largura);
        Comparacao clicada = null;
        scrollLista = GUI.BeginScrollView(areaLista, scrollLista, new Rect(0, 0, largura, total));
        float y = 0f;
        foreach (Comparacao c in conclusoes)
            if (Linha(ref y, largura, c.titulo, c.tipo == "hipotese" ? "HIPÓTESE" : null, false, c == conclusaoSelecionada))
                clicada = c;
        GUI.EndScrollView();
        if (clicada != null)
        {
            conclusaoSelecionada = clicada;
            scrollTexto = Vector2.zero;
        }

        Comparacao sel = conclusaoSelecionada;
        string rotulo = sel.tipo == "hipotese" ? "HIPÓTESE" : "CONCLUSÃO";
        float lp = areaPagina.width - S(18);
        float alt = PaginaSimples(lp, false, rotulo, sel.titulo, sel.texto);
        scrollTexto = GUI.BeginScrollView(areaPagina, scrollTexto, new Rect(0, 0, lp, alt));
        PaginaSimples(lp, true, rotulo, sel.titulo, sel.texto);
        GUI.EndScrollView();
    }

    /// <summary>Aviso de evidência nova: uma tira de papel no canto superior direito.</summary>
    void DesenharAviso()
    {
        float largura = S(480);
        float alt = Altura(avisoTexto, sAviso, largura - S(48)) + S(28);
        var r = new Rect(Screen.width - largura - S(28), S(28), largura, alt);
        Caixa(new Rect(r.x + S(4), r.y + S(4), r.width, r.height), new Color(0f, 0f, 0f, 0.35f));
        Papel(r, texturaPagina, corPapel);
        Caixa(new Rect(r.x, r.y, S(8), r.height), corVermelho);
        GUI.Label(new Rect(r.x + S(26), r.y + S(14), largura - S(48), alt - S(28)), avisoTexto, sAviso);
    }

    // ---------- Entrada (Input System novo e antigo) ----------

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
}