using UnityEngine;

/// <summary>
/// Copiar a gravação no computador do arquivo (cerca de 40 s reais). O jogador liga a cópia e precisa ficar
/// perto; se se afastar, a cópia para e continua de onde parou quando ele voltar. Com Daniel por perto, é a
/// escolha arriscada da parte 4. Ao terminar marca "audio_copiado" (o celular passa a poder enviar D13).
/// Prefab/componente de José; Bruno põe no computador do arquivo.
/// </summary>
public class CopiaDoAudio : AlvoDeInteracao, ISalvavel
{
    public string tecla = "E";
    [Min(0.2f)] public float alcance = 1.6f;
    [Min(1f)] public float segundos = 40f;
    [Tooltip("Distância máxima do computador enquanto copia.")]
    [Min(0.5f)] public float raio = 3f;
    public AudioSource fonte;
    public AudioClip somTrabalhando, somPronto;

    float feito;
    bool copiando;
    float copiaDesde;

    public override float AlcanceDeInteracao => alcance;
    public override string Tecla => tecla;
    public override string Indicacao =>
        EstadoDaNoite.Tem(CelularLogica.FatoAudioCopiado) || !Gravador.ComOJogador ? "" :
        copiando ? "" : feito > 0f ? "Continuar a cópia da gravação" : "Copiar a gravação (cerca de 40 s)";

    public float Progresso => Mathf.Clamp01(feito / segundos);

    public override void Interagir()
    {
        if (!Gravador.ComOJogador || EstadoDaNoite.Tem(CelularLogica.FatoAudioCopiado)) return;
        copiando = true;
        copiaDesde = Time.unscaledTime;
        if (fonte != null && somTrabalhando != null) { fonte.clip = somTrabalhando; fonte.loop = true; fonte.Play(); }
        MiraDeInteracao.Avisar("Copiando. Fique perto do computador.");
    }

    protected override void Update()
    {
        base.Update();
        if (!copiando) return;
        if (!Gravador.ComOJogador) { Parar(); return; }
        bool perto = (Jogador.Posicao - transform.position).sqrMagnitude <= raio * raio;
        if (!perto) { Parar(); MiraDeInteracao.Avisar("A cópia parou. Volte ao computador para continuar."); return; }
        feito += Time.deltaTime;
        if (feito < segundos) return;
        Parar();
        EstadoDaNoite.Marcar(CelularLogica.FatoAudioCopiado);
        if (somPronto != null) AudioSource.PlayClipAtPoint(somPronto, transform.position);
        MiraDeInteracao.Avisar("Cópia pronta. Dá para enviar a gravação pelo celular.", 3f);
    }

    void Parar()
    {
        copiando = false;
        if (fonte != null) fonte.Stop();
    }

    protected override void OnGUI()
    {
        base.OnGUI();
        if (!copiando) return;
        // Barra fina de transferência, como no computador do arquivo, com a porcentagem ao lado.
        float s = DesenhoUi.S;
        float a = DesenhoUi.Suave((Time.unscaledTime - copiaDesde) / 0.35f);
        float w = 460f * s, x = (Screen.width - w) * 0.5f, y = Screen.height - 132f * s;
        DesenhoUi.Caixa(new Rect(x - 24f * s, y - 52f * s, w + 48f * s, 92f * s), new Color(0f, 0f, 0f, 0.55f * a));
        GUI.Label(new Rect(x, y - 40f * s, w, 26f * s), "Copiando a gravação…",
            DesenhoUi.EstiloMaquina(18f, new Color(1f, 1f, 1f, 0.9f * a)));
        GUI.Label(new Rect(x, y - 40f * s, w, 26f * s), $"{Mathf.RoundToInt(Progresso * 100)}%",
            DesenhoUi.EstiloMaquina(18f, new Color(1f, 1f, 1f, 0.9f * a), TextAnchor.UpperRight));
        float h = Mathf.Max(2f, 3f * s);
        DesenhoUi.Caixa(new Rect(x, y, w, h), new Color(1f, 1f, 1f, 0.15f * a));
        DesenhoUi.Caixa(new Rect(x, y, w * Progresso, h), new Color(0.86f, 0.86f, 0.80f, a));
        GUI.Label(new Rect(x, y + 10f * s, w, 22f * s), "Fique perto do computador até terminar.",
            DesenhoUi.Estilo(14f, new Color(1f, 1f, 1f, 0.45f * a)));
    }

    public string Salvar() => feito.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public void Carregar(string estado)
    {
        float.TryParse(estado, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out feito);
        copiando = false;
    }
}
