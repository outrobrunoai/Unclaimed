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

    protected override void Desenhar()
    {
        float s = S;
        Caixa(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, 0.6f));
        GUI.Label(new Rect(80 * s, 70 * s, Screen.width - 160 * s, 40 * s), gravacao.titulo, Estilo(24, new Color(1, 1, 1, 0.8f), true));
        float p = Mathf.Clamp01(Tempo / Mathf.Max(0.1f, Duracao));
        Rect barra = new Rect(80 * s, 120 * s, Screen.width - 160 * s, 6 * s);
        Caixa(barra, new Color(1, 1, 1, 0.15f));
        Caixa(new Rect(barra.x, barra.y, barra.width * p, barra.height), new Color(0.9f, 0.3f, 0.25f));

        Gravacao.Fala fala = gravacao.Em(Tempo);
        if (fala != null)
        {
            string texto = string.IsNullOrEmpty(fala.voz) ? fala.texto : fala.voz + ": " + fala.texto;
            GUI.Label(new Rect(160 * s, Screen.height - 260 * s, Screen.width - 320 * s, 160 * s), texto,
                Estilo(28, Color.white, false, TextAnchor.LowerCenter));
        }
        GUI.Label(new Rect(80 * s, Screen.height - 70 * s, Screen.width - 160 * s, 30 * s), $"{tecla} ou Esc para parar",
            Estilo(15, new Color(1, 1, 1, 0.5f)));
    }
}
