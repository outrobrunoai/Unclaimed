using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Trava de segurança do lado de dentro da câmara fria (saída da armadilha S3). No escuro ela só responde
/// depois que a lanterna do celular a iluminou por perto (ou com a luz da câmara acesa). Puxar destrava a porta
/// presa pelo PontoDeAcao de tipo PrenderCamaraFria e acende a luz de novo.
/// Prefab de José; Bruno coloca junto da porta frigorífica, do lado de dentro.
/// </summary>
public class TravaDeSeguranca : AlvoDeInteracao
{
    public string tecla = "E";
    [Min(0.2f)] public float alcance = 1.5f;
    public string textoPuxar = "Puxar trava de segurança";
    [Tooltip("Ponto da armadilha. Vazio = qualquer PrenderCamaraFria ativo.")]
    public PontoDeAcao armadilha;
    [FormerlySerializedAs("segundosAposFlash")]
    [Tooltip("Depois da luz da lanterna, por quantos segundos o jogador ainda acha a trava no escuro.")]
    [Min(1f)] public float segundosAposLuz = 8f;
    [FormerlySerializedAs("distanciaDoFlash")]
    [Tooltip("Distância máxima (m) para a lanterna revelar a trava no escuro.")]
    [Min(0.5f)] public float distanciaDaLuz = 6f;
    public AudioClip somTrava;

    bool achada;

    public override float AlcanceDeInteracao => alcance;
    public override string Tecla => tecla;
    public override string Indicacao => Visivel ? textoPuxar : "";

    PontoDeAcao Armadilha
    {
        get
        {
            if (armadilha != null) return armadilha;
            foreach (PontoDeAcao p in PontoDeAcao.Todos)
                if (p != null && p.tipo == TipoDeAcao.PrenderCamaraFria && p.Ativo) return p;
            return null;
        }
    }

    bool Visivel
    {
        get
        {
            PontoDeAcao a = Armadilha;
            if (a == null || !a.Ativo) return true;   // sem armadilha: a trava é só uma trava
            if (achada) return true;
            if (Celular.LanternaIlumina(transform.position, distanciaDaLuz)) achada = true;
            return achada;
        }
    }

    public override void Interagir()
    {
        PontoDeAcao a = Armadilha;
        if (somTrava != null) AudioSource.PlayClipAtPoint(somTrava, transform.position);
        if (a == null || !a.Ativo) return;
        a.Desfazer();
        if (a.porta != null) a.porta.Abrir();
        achada = false;
        EstadoDaNoite.Marcar("escapou_camara_fria");
        MiraDeInteracao.Avisar("A trava cedeu. A porta abriu.");
    }
}
