using UnityEngine;

/// <summary>
/// Lugar para se esconder na caça (armários do descanso, gavetas vazias C-05 a C-07). Mirar e apertar E
/// coloca o jogador dentro (agachado, olhar parado, tela escurecida); E de novo sai pelo ponto de saída.
/// Daniel não vê quem está escondido, a menos que tenha visto o jogador entrar.
/// Prefab de José (Esconderijo.prefab); Bruno coloca no móvel e ajusta os pontos "Dentro" e "Saida".
/// </summary>
public class Esconderijo : AlvoDeInteracao
{
    public string tecla = "E";
    [Min(0.2f)] public float alcance = 1.6f;
    public string textoEntrar = "Esconder-se";
    [Tooltip("Onde fica o pé do jogador escondido (de frente para a fresta: eixo Z azul).")]
    public Transform dentro;
    [Tooltip("Onde ele volta ao sair. Vazio = onde estava ao entrar.")]
    public Transform saida;
    [Tooltip("Só na caça? (fora dela, entrar não tem sentido e não aparece).")]
    public bool soNaCaca = true;
    [Tooltip("Porta do compartimento; vazio = não mexe.")]
    public PecaDeMovel porta;
    [Tooltip("Abertura deixada ao se esconder (nas unidades da peça: graus numa porta).")]
    [Min(0f)] public float fresta = 8f;
    [Tooltip("Escondido agachado (gaveta, móvel baixo) ou em pé (armário alto).")]
    public bool agachado = true;
    [Tooltip("Inclinação do olhar escondido, em graus (positivo = para baixo).")]
    public float inclinacao = 10f;
    public AudioClip somEntrar, somSair;

    public static Esconderijo Ocupado { get; private set; }
    public static float EntrouEm { get; private set; } = -999f;
    public static bool JogadorEscondido => Ocupado != null;

    Vector3 voltarPara;
    float voltarGiro;
    float aberturaAntes;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar() { Ocupado = null; EntrouEm = -999f; }

    public override float AlcanceDeInteracao => alcance;
    public override string Tecla => tecla;
    public override string Indicacao =>
        Ocupado != null || (soNaCaca && EstadoDaNoite.Fase != FaseDeDaniel.Caca) ? "" : textoEntrar;

    public override void Interagir() => Entrar();

    public void Entrar()
    {
        Movimento m = Jogador.Movimento;
        if (m == null || Ocupado != null) return;
        if (Celular.Instancia != null &&
            (Celular.Instancia.EstaAberta || Celular.Instancia.ModoCamera ||
             (!Celular.Instancia.Lanterna && Celular.Instancia.NaMao != null && Celular.Instancia.NaMao.Visivel)))
            Celular.Instancia.GuardarJa();
        voltarPara = m.transform.position;
        voltarGiro = m.transform.eulerAngles.y;
        Transform d = dentro != null ? dentro : transform;
        m.Teleportar(d.position, d.eulerAngles.y, inclinacao, agachado);
        m.enabled = false;
        Ocupado = this;
        EntrouEm = Time.time;
        if (porta != null)
        {
            aberturaAntes = porta.Abertura;
            porta.IrPara(Mathf.Min(fresta, aberturaAntes), true);
        }
        if (somEntrar != null) AudioSource.PlayClipAtPoint(somEntrar, d.position);
    }

    public void Sair()
    {
        Movimento m = Jogador.Movimento;
        if (Ocupado != this) return;
        Ocupado = null;
        if (m == null) return;
        if (porta != null) porta.IrPara(Mathf.Max(aberturaAntes, 60f), true);
        m.enabled = true;
        if (saida != null) m.Teleportar(saida.position, saida.eulerAngles.y, 0f, false);
        else m.Teleportar(voltarPara, voltarGiro, 0f, false);
        MiraDeInteracao.BloquearNoProximoQuadro();
        if (somSair != null) AudioSource.PlayClipAtPoint(somSair, m.transform.position);
    }

    protected override void Update()
    {
        if (Ocupado == this)
        {
            if (MenuPausa.Pausado || InterfaceDeTela.AlgumaAberta || TelaDeFinal.Ativa) return;
            if (Time.time - EntrouEm > 0.3f && MiraDeInteracao.TeclaApertada(tecla)) Sair();
            return;
        }
        base.Update();
    }

    // O menu de pausa liga o Movimento de novo ao despausar: escondido, ele continua desligado.
    void LateUpdate()
    {
        if (Ocupado == this && Jogador.Movimento != null && Jogador.Movimento.enabled && !TelaDeFinal.Ativa)
            Jogador.Movimento.enabled = false;
    }

    void OnDisable() { if (Ocupado == this) Sair(); }

    protected override void OnGUI()
    {
        if (Ocupado != this) { base.OnGUI(); return; }
        useGUILayout = false;
        float s = DesenhoUi.S, W = Screen.width, H = Screen.height;
        // A porta encosta: o escuro fecha das bordas até sobrar a fresta, que "respira" de leve com o jogador.
        float dentroHa = Time.time - EntrouEm;
        float fecha = DesenhoUi.Suave(dentroHa / 0.45f);
        float fresta = Mathf.Lerp(H, H * 0.13f + Mathf.Sin(Time.time * 1.3f) * H * 0.006f, fecha);
        float borda = 70f * s;   // degradê nas beiradas da fresta
        float alto = (H - fresta) * 0.5f;
        Color escuro = new Color(0f, 0f, 0f, 0.95f);
        DesenhoUi.Caixa(new Rect(0, 0, W, Mathf.Max(0f, alto - borda)), escuro);
        DesenhoUi.Degrade(new Rect(0, alto - borda, W, borda), 0.95f, true);
        DesenhoUi.Degrade(new Rect(0, alto + fresta, W, borda), 0.95f, false);
        DesenhoUi.Caixa(new Rect(0, alto + fresta + borda, W, Mathf.Max(0f, alto - borda)), escuro);
        DesenhoUi.Vinheta(fecha);
        // Dica discreta, que some depois de alguns segundos e fica só um sussurro.
        float a = Mathf.Lerp(0.5f, 0.18f, DesenhoUi.Suave((dentroHa - 3f) / 1.5f)) * DesenhoUi.Suave((dentroHa - 0.4f) / 0.4f);
        GUI.Label(new Rect(0, H - 64 * s, W, 26 * s), $"{tecla.ToUpper()} para sair do esconderijo",
            DesenhoUi.EstiloMaquina(16, new Color(1, 1, 1, a), TextAnchor.UpperCenter));
    }

    void OnDrawGizmosSelected()
    {
        if (dentro != null) { Gizmos.color = Color.green; Gizmos.DrawWireSphere(dentro.position, 0.2f); Gizmos.DrawRay(dentro.position, dentro.forward * 0.5f); }
        if (saida != null) { Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(saida.position, 0.2f); }
    }
}
