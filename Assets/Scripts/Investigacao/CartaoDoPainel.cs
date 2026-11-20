using UnityEngine;

/// <summary>
/// Cartão impresso no painel de contingência (evidência D04): ao ler, coleta a evidência e exibe a legenda na tela.
/// </summary>
public class CartaoDoPainel : AlvoDeInteracao
{
    public string idEvidencia = "D04";
    [Min(0.2f)] public float alcance = 1.4f;
    [TextArea]
    public string texto = "Legenda do painel. Capacidade disponível: 10. Refrigeração: 6 (prioridade). Luz geral: 3. Luminária de exame: 4.";

    public override float AlcanceDeInteracao => alcance;
    public override string Indicacao => "Ler o cartão do painel";

    public override void Interagir()
    {
        if (!string.IsNullOrEmpty(idEvidencia)) Caderno.Coletar(idEvidencia);
        MiraDeInteracao.Avisar(texto, 7f);
    }
}
