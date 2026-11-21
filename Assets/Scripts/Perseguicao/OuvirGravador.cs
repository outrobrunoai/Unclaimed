using UnityEngine;

/// <summary>
/// Ouvir a gravação de Léo (tecla G, depois de pegar o gravador). Toca o áudio (se houver) com legenda
/// "Voz 1 / Voz 2". Ouvir até o fim coleta D13. Enquanto ouve, Daniel não age; na caça não dá para ouvir.
/// Fica no Player (cena Jogabilidade).
/// </summary>
public class OuvirGravador : PainelDoJogador
{
    public const string TeclaPadrao = "G";
    public string tecla = TeclaPadrao;
    [Tooltip("Áudio da gravação (opcional: sem ele, só as legendas no tempo do JSON).")]
    public AudioClip audioDaGravacao;
    public AudioSource fonte;
    public string idEvidencia = "D13";

    public override string TeclaDoPainel => tecla;
    protected override bool PodeAbrir => Gravador.ComOJogador && EstadoDaNoite.Fase != FaseDeDaniel.Caca;

    Gravacao gravacao;
    float inicio;

    protected override void AoAbrir()
    {
        if (gravacao == null) gravacao = Gravacao.Carregar();
        inicio = Time.unscaledTime;
        if (audioDaGravacao != null)
        {
            if (fonte == null) fonte = gameObject.AddComponent<AudioSource>();
            fonte.spatialBlend = 0f;
            fonte.ignoreListenerPause = true;
            fonte.clip = audioDaGravacao;
            fonte.Play();
        }
    }

    protected override void AoFechar()
    {
        if (fonte != null) fonte.Stop();
    }

    float Tempo => fonte != null && fonte.isPlaying ? fonte.time : Time.unscaledTime - inicio;
    float Duracao => audioDaGravacao != null ? audioDaGravacao.length : gravacao.Duracao;

    protected override void AtualizarAberta()
    {
        base.AtualizarAberta();
        if (Tempo >= Duracao - 0.05f || (fonte != null && audioDaGravacao != null && !fonte.isPlaying && Time.unscaledTime - inicio > 1f))
        {
            Caderno.Coletar(idEvidencia);
            EstadoDaNoite.Marcar("gravacao_ouvida");
            FecharInterface();
        }
    }

    // ------------------------------------------------------------------ tela

    // Como ouvir num gravador de mão no escuro: a tela escurece, um ponto vermelho marca a reprodução, um medidor de
    // nível pulsa enquanto alguém fala e a legenda entra e sai com cada fala, com o nome de quem fala em cima.
    const float DuracaoEntrada = 0.45f;
    const float FadeDaFala = 0.25f;
    const int Barras = 48;
    static readonly Color CorVoz1 = new Color(0.62f, 0.70f, 0.78f);
    static readonly Color CorVoz2 = new Color(0.80f, 0.36f, 0.31f);
    static readonly Color Vermelho = new Color(0.86f, 0.22f, 0.18f);
    readonly float[] nivel = new float[Barras];
    float ultimoQuadro;

    protected override void Desenhar()
    {
        useGUILayout = false;
        float s = S, W = Screen.width, H = Screen.height;
        float agora = Time.unscaledTime;
        float e = DesenhoUi.Suave((agora - inicio) / DuracaoEntrada);
        float t = Tempo, dur = Mathf.Max(0.1f, Duracao);

        Caixa(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.72f * e));
        DesenhoUi.Vinheta(e);

        // Cabeçalho: ponto vermelho piscando, "REPRODUZINDO" e o título na máquina de escrever.
        float x = 90f * s, y = 76f * s;
        float pisca = 0.55f + 0.45f * Mathf.Sin(agora * 4.2f);
        Caixa(new Rect(x, y + 6f * s, 12f * s, 12f * s), new Color(Vermelho.r, Vermelho.g, Vermelho.b, e * pisca));
        GUI.Label(new Rect(x + 24f * s, y, 400f * s, 24f * s), "R E P R O D U Z I N D O",
            Estilo(14, new Color(1f, 1f, 1f, 0.55f * e), true));
        GUI.Label(new Rect(x, y + 34f * s, W - 2f * x, 36f * s), gravacao.titulo,
            DesenhoUi.EstiloMaquina(24f, new Color(1f, 1f, 1f, 0.85f * e)));

        // Medidor de nível no centro: sobe com quem está falando, cai no silêncio.
        Gravacao.Fala fala = gravacao.Em(t);
        float dt = Mathf.Clamp(agora - ultimoQuadro, 0f, 0.1f);
        if (Event.current.type == EventType.Repaint) ultimoQuadro = agora;
        float larg = Mathf.Min(720f * s, W - 200f * s), passo = larg / Barras, alturaMax = 90f * s;
        float x0 = (W - larg) * 0.5f, yMeio = H * 0.42f;
        Color corNivel = fala == null ? new Color(1f, 1f, 1f, 0.25f) : (EhVoz2(fala) ? CorVoz2 : CorVoz1);
        for (int i = 0; i < Barras; i++)
        {
            float alvo = 0.04f;
            if (fala != null)
            {
                float envelope = Mathf.Sin(Mathf.PI * (i + 0.5f) / Barras);
                alvo = Mathf.Lerp(0.08f, 1f, Mathf.PerlinNoise(i * 0.37f, t * 6.5f)) * (0.35f + 0.65f * envelope);
            }
            if (Event.current.type == EventType.Repaint) nivel[i] = Mathf.MoveTowards(nivel[i], alvo, dt * 6f);
            float hb = Mathf.Max(2f * s, nivel[i] * alturaMax);
            Caixa(new Rect(x0 + i * passo + passo * 0.25f, yMeio - hb * 0.5f, passo * 0.5f, hb),
                new Color(corNivel.r, corNivel.g, corNivel.b, (fala == null ? 0.25f : 0.75f) * e));
        }

        // Legenda: nome de quem fala e a frase, entrando e saindo com a fala.
        if (fala != null)
        {
            float a = e * Mathf.Min(DesenhoUi.Suave((t - fala.inicio) / FadeDaFala), DesenhoUi.Suave((fala.fim - t) / FadeDaFala));
            float lw = Mathf.Min(1000f * s, W - 200f * s), lx = (W - lw) * 0.5f;
            GUIStyle texto = Estilo(29, new Color(1f, 1f, 1f, a), false, TextAnchor.UpperCenter);
            float lh = texto.CalcHeight(new GUIContent(fala.texto), lw);
            float ly = H - 150f * s - lh;
            if (!string.IsNullOrEmpty(fala.voz))
            {
                Color cv = EhVoz2(fala) ? CorVoz2 : CorVoz1;
                GUI.Label(new Rect(lx, ly - 32f * s, lw, 24f * s), Espacar(fala.voz.ToUpperInvariant()),
                    Estilo(14, new Color(cv.r, cv.g, cv.b, a), true, TextAnchor.LowerCenter));
            }
            GUI.Label(new Rect(lx + 1.5f * s, ly + 2f * s, lw, lh), fala.texto, Estilo(29, new Color(0f, 0f, 0f, 0.6f * a), false, TextAnchor.UpperCenter));
            GUI.Label(new Rect(lx, ly, lw, lh), fala.texto, texto);
        }

        // Linha do tempo fina com os minutos, embaixo.
        float bx = 90f * s, bw = W - 180f * s, by = H - 78f * s;
        Caixa(new Rect(bx, by, bw, Mathf.Max(1f, 2f * s)), new Color(1f, 1f, 1f, 0.15f * e));
        Caixa(new Rect(bx, by, bw * Mathf.Clamp01(t / dur), Mathf.Max(1f, 2f * s)), new Color(Vermelho.r, Vermelho.g, Vermelho.b, 0.9f * e));
        GUIStyle tempo = DesenhoUi.EstiloMaquina(15f, new Color(1f, 1f, 1f, 0.5f * e));
        GUI.Label(new Rect(bx, by + 10f * s, 200f * s, 22f * s), Relogio(t) + " / " + Relogio(dur), tempo);
        GUIStyle dica = DesenhoUi.EstiloMaquina(15f, new Color(1f, 1f, 1f, 0.35f * e), TextAnchor.UpperRight);
        GUI.Label(new Rect(bx, by + 10f * s, bw, 22f * s), $"{tecla} ou Esc para parar", dica);
    }

    // Voz 2 é Daniel: a cor dele puxa para o vermelho do confronto (sem dizer quem é).
    static bool EhVoz2(Gravacao.Fala f) => f.voz != null && f.voz.EndsWith("2");
    static string Espacar(string t) => string.Join(" ", t.ToCharArray());
    static string Relogio(float seg) { int x = Mathf.Max(0, Mathf.FloorToInt(seg)); return $"{x / 60}:{x % 60:00}"; }
}
