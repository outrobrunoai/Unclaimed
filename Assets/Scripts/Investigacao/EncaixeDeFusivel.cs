using UnityEngine;

/// <summary>
/// Encaixe de fusível no quadro geral: permite inspecionar a falta do fusível e recolocar o fusível reserva.
/// </summary>
public class EncaixeDeFusivel : AlvoDeInteracao
{
    public string circuito = RedeEletrica.Geral;
    public string itemFusivel = "fusivel_reserva";
    [Min(0.2f)] public float alcance = 1.6f;
    public AudioSource fonte;
    public AudioClip somEncaixe;

    public override float AlcanceDeInteracao => alcance;

    public override string Indicacao
    {
        get
        {
            RedeEletrica.Circuito c = RedeEletrica.Buscar(circuito);
            if (c == null || c.fusivel) return "";
            if (!Chaveiro.Tem(itemFusivel)) return "Ver o encaixe do fusível";
            string nome = string.IsNullOrEmpty(c.nome) ? circuito : c.nome.ToLower();
            return "Recolocar o fusível da " + nome;
        }
    }

    public override void Interagir()
    {
        RedeEletrica.Circuito c = RedeEletrica.Buscar(circuito);
        if (c == null || c.fusivel) return;

        if (!Chaveiro.Tem(itemFusivel))
        {
            string nome = string.IsNullOrEmpty(c.nome) ? circuito : c.nome.ToLower();
            MiraDeInteracao.Avisar("O fusível da " + nome + " foi retirado. É preciso um fusível reserva.");
            return;
        }

        if (RedeEletrica.ColocarFusivel(circuito))
        {
            Tocar(somEncaixe);
            MiraDeInteracao.Avisar("Fusível no lugar.");
        }
    }

    void Tocar(AudioClip clip)
    {
        if (clip == null) return;
        if (fonte != null) fonte.PlayOneShot(clip);
        else AudioSource.PlayClipAtPoint(clip, transform.position);
    }
}
