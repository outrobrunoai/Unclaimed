using UnityEngine;

/// <summary>
/// Objeto que o jogador pega com a tecla de interação (ex.: chave). Só pode ser coletado quando é a primeira
/// superfície sob a mira (MiraDeInteracao), então portas fechadas e o próprio móvel impedem a coleta. Ao coletar:
/// registra a posse no Chaveiro, mostra um aviso curto e sai do cenário. Não coleta duas vezes.
/// </summary>
[DisallowMultipleComponent]
public class ItemColetavel : AlvoDeInteracao
{
    [Tooltip("Identificador da posse (o mesmo que a porta pede).")]
    public string id = "chave_circulacao";
    public string tecla = "E";
    [Min(0.2f)] public float alcance = 1.6f;
    public string textoAcao = "Pegar chave da circulação";
    public string aviso = "Chave da circulação guardada.";
    [Tooltip("Som opcional ao pegar (tocado no ponto do objeto).")]
    public AudioClip somColeta;

    public override float AlcanceDeInteracao => alcance;
    public override string Tecla => tecla;
    public override string Indicacao => Chaveiro.Tem(id) ? "" : textoAcao;

    public override void Interagir()
    {
        if (!Chaveiro.Adicionar(id)) return;
        if (somColeta != null) AudioSource.PlayClipAtPoint(somColeta, transform.position);
        MiraDeInteracao.Avisar(aviso);
        gameObject.SetActive(false);
    }
}
