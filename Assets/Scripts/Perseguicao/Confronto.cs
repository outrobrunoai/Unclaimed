using UnityEngine;

/// <summary>
/// "Deixa o gravador. Pode ir." Alguns segundos depois de o jogador pegar o gravador (ou logo depois de ouvir a
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
    public string fala = "Deixa o gravador. Pode ir.";
    [Min(0f)] public float esperaAntesDaCaca = 1.2f;

    public override float AlcanceDeInteracao => 0f;
    public override string Indicacao => "";
    public override void Interagir() { }

    float contado, ouviuEm = -1f, cacaEm = -1f;

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
        if (pronto) Comecar();
    }

    public void Comecar()
    {
        PontoDeAcao p = PontoDeAcao.Buscar(pontoDeAparicao);
        if (DanielPerseguidor.Instancia != null)
        {
            Vector3 pos = p != null ? p.transform.position : Jogador.Posicao + Jogador.Movimento.transform.forward * 4f;
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

    protected override void Desenhar()
    {
        float s = DesenhoUi.S;
        DesenhoUi.Caixa(new Rect(0, Screen.height * 0.62f, Screen.width, Screen.height * 0.38f), new Color(0, 0, 0, 0.7f));
        GUI.Label(new Rect(160 * s, Screen.height * 0.64f, Screen.width - 320 * s, 60 * s), "Daniel: " + fala,
            DesenhoUi.Estilo(30, Color.white, false, TextAnchor.UpperCenter));
        float w = 340 * s, y = Screen.height - 160 * s;
        if (DesenhoUi.Botao(new Rect(Screen.width / 2f - w - 20 * s, y, w, 60 * s), "Entregar o gravador", true, 19)) Escolher(true);
        if (DesenhoUi.Botao(new Rect(Screen.width / 2f + 20 * s, y, w, 60 * s), "Ficar com ele e correr", true, 19)) Escolher(false);
    }
}
