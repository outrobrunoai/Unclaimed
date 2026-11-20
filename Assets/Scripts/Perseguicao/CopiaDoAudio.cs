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
        float s = DesenhoUi.S;
        Rect r = new Rect(Screen.width / 2f - 220 * s, Screen.height - 150 * s, 440 * s, 16 * s);
        DesenhoUi.Caixa(r, new Color(0, 0, 0, 0.6f));
        DesenhoUi.Caixa(new Rect(r.x, r.y, r.width * Progresso, r.height), new Color(0.4f, 0.8f, 0.45f));
        GUI.Label(new Rect(r.x, r.y - 34 * s, r.width, 30 * s), $"Copiando a gravação: {Mathf.RoundToInt(Progresso * 100)}%",
            DesenhoUi.Estilo(17, Color.white, true, TextAnchor.LowerCenter));
    }

    public string Salvar() => feito.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public void Carregar(string estado)
    {
        float.TryParse(estado, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out feito);
        copiando = false;
    }
}
