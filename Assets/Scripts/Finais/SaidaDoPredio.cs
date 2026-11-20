using UnityEngine;

/// <summary>
/// Saída do prédio (porta da recepção e portão do pátio). Mirar e apertar E pergunta se o jogador quer ir
/// embora; confirmando, mostra o epílogo. Durante a perseguição não pergunta nada: chegar perto já é a fuga.
///
/// A entrada principal recebe "fatoQueBloqueia" = "corrente": depois que Daniel passa a corrente (S5) ela não
/// abre, e o jogador precisa sair pelo pátio.
/// Prefab de José; Bruno coloca nas duas saídas.
/// </summary>
public class SaidaDoPredio : InterfaceDeTela
{
    public string tecla = "E";
    [Min(0.2f)] public float alcance = 2f;
    public string textoSair = "Ir embora";
    [Tooltip("Enquanto este fato existir, a saída está bloqueada (ex.: corrente).")]
    public string fatoQueBloqueia = "";
    public string textoBloqueada = "Uma corrente prende a porta.";
    [Tooltip("Na caça, chegar a esta distância já conta como fuga.")]
    [Min(0.5f)] public float raioDeFuga = 2.5f;

    public override float AlcanceDeInteracao => alcance;
    public override string Tecla => tecla;
    public bool Bloqueada => !string.IsNullOrEmpty(fatoQueBloqueia) && EstadoDaNoite.Tem(fatoQueBloqueia);
    public override string Indicacao => textoSair;

    public override void Interagir()
    {
        if (Bloqueada) { MiraDeInteracao.Avisar(textoBloqueada, 2f); return; }
        AbrirInterface();
    }

    protected override void Update()
    {
        base.Update();
        if (EstadoDaNoite.Fase != FaseDeDaniel.Caca || Bloqueada || TelaDeFinal.Ativa) return;
        if (EstadoDaNoite.Tem("gravador_entregue")) return;
        if ((Jogador.Posicao - transform.position).sqrMagnitude <= raioDeFuga * raioDeFuga) Sair();
    }

    public void Sair()
    {
        if (EstaAberta) FecharInterface();
        EstadoDaNoite.Marcar("saiu_do_predio");
        TelaDeFinal.MostrarEpilogo();
    }

    protected override void Desenhar()
    {
        float s = DesenhoUi.S;
        DesenhoUi.Caixa(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, 0.7f));
        float w = 760 * s, h = 300 * s;
        Rect r = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
        DesenhoUi.Caixa(r, new Color(0.1f, 0.1f, 0.12f, 0.97f));
        Epilogo.Resultado res = Epilogo.DaNoiteAtual();
        string resumo = res.incompleto
            ? "Você ainda não sabe o que aconteceu aqui. Se sair agora, a investigação fica pela metade."
            : res.provas == ProvasEnviadas.Nenhuma ? "Você não mandou nenhuma prova para a redação." : "A redação já tem parte do que você encontrou.";
        GUI.Label(new Rect(r.x + 30 * s, r.y + 26 * s, w - 60 * s, 40 * s), "Sair do prédio?", DesenhoUi.Estilo(28, Color.white, true));
        GUI.Label(new Rect(r.x + 30 * s, r.y + 80 * s, w - 60 * s, 100 * s), resumo, DesenhoUi.Estilo(19, new Color(0.85f, 0.85f, 0.82f)));
        if (DesenhoUi.Botao(new Rect(r.x + 30 * s, r.yMax - 80 * s, 240 * s, 50 * s), "Ficar", true, 18)) FecharInterface();
        if (DesenhoUi.Botao(new Rect(r.xMax - 270 * s, r.yMax - 80 * s, 240 * s, 50 * s), "Ir embora", true, 18)) Sair();
    }
}
