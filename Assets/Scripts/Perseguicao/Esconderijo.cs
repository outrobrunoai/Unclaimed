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
    public AudioClip somEntrar, somSair;

    public static Esconderijo Ocupado { get; private set; }
    public static float EntrouEm { get; private set; } = -999f;
    public static bool JogadorEscondido => Ocupado != null;

    Vector3 voltarPara;
    float voltarGiro;

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
        voltarPara = m.transform.position;
        voltarGiro = m.transform.eulerAngles.y;
        Transform d = dentro != null ? dentro : transform;
        m.Teleportar(d.position, d.eulerAngles.y, 10f, true);
        m.enabled = false;
        Ocupado = this;
        EntrouEm = Time.time;
        if (somEntrar != null) AudioSource.PlayClipAtPoint(somEntrar, d.position);
    }

    public void Sair()
    {
        Movimento m = Jogador.Movimento;
        if (Ocupado != this) return;
        Ocupado = null;
        if (m == null) return;
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
        float s = DesenhoUi.S;
        // Escuro com uma fresta no meio.
        float fresta = Screen.height * 0.12f;
        DesenhoUi.Caixa(new Rect(0, 0, Screen.width, (Screen.height - fresta) / 2f), new Color(0, 0, 0, 0.93f));
        DesenhoUi.Caixa(new Rect(0, (Screen.height + fresta) / 2f, Screen.width, (Screen.height - fresta) / 2f), new Color(0, 0, 0, 0.93f));
        GUI.Label(new Rect(0, Screen.height - 70 * s, Screen.width, 30 * s), $"{tecla.ToUpper()}: sair do esconderijo",
            DesenhoUi.Estilo(16, new Color(1, 1, 1, 0.5f), false, TextAnchor.UpperCenter));
    }

    void OnDrawGizmosSelected()
    {
        if (dentro != null) { Gizmos.color = Color.green; Gizmos.DrawWireSphere(dentro.position, 0.2f); Gizmos.DrawRay(dentro.position, dentro.forward * 0.5f); }
        if (saida != null) { Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(saida.position, 0.2f); }
    }
}
