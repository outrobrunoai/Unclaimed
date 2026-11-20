using UnityEngine;

/// <summary>
/// Chave seletora individual do painel de contingência: encaminha a interação mirada para o PainelDeContingencia.
/// </summary>
public class ChaveDoPainel : AlvoDeInteracao
{
    public PainelDeContingencia painel;
    public string circuito;
    [Min(0.2f)] public float alcance = 1.4f;

    public override float AlcanceDeInteracao => alcance;
    public override string Indicacao => painel != null ? painel.Indicacao(circuito) : "";

    public override void Interagir()
    {
        if (painel != null) painel.Acionar(circuito);
    }
}
