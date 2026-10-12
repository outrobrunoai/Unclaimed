using UnityEngine;

/// <summary>
/// Base das interações miradas (portas, gavetas, armários, torneira, descarga): o objeto responde quando
/// é o alvo da MiraDeInteracao (primeira superfície sob o centro da tela, dentro do próprio alcance) e a
/// tecla é apertada. Só um alvo existe por frame, e com um alvo na mira as pistas do ObjetoInspecionavel
/// não respondem, então uma mesma tecla nunca aciona duas coisas. Inspeção, diário aberto e cursor solto
/// bloqueiam tudo (MiraDeInteracao.ControleCapturado).
/// O alvo é achado pelo Collider atingido: o componente fica no objeto do Collider ou num pai dele.
/// </summary>
public abstract class AlvoDeInteracao : MonoBehaviour
{
    /// <summary>Distância máxima (m) da câmera até o ponto mirado.</summary>
    public abstract float AlcanceDeInteracao { get; }

    /// <summary>Texto da indicação ("Abrir gaveta"). Vazio = sem ação agora (ex.: descarga em ciclo).</summary>
    public abstract string Indicacao { get; }

    /// <summary>Nome da tecla (Input System: Key; Input Manager: KeyCode).</summary>
    public virtual string Tecla => "E";

    /// <summary>A ação: chamada quando a tecla é apertada com este alvo na mira.</summary>
    public abstract void Interagir();

    protected virtual void Update()
    {
        if (MiraDeInteracao.Alvo == this && !string.IsNullOrEmpty(Indicacao) && MiraDeInteracao.TeclaApertada(Tecla))
            Interagir();
    }

    protected virtual void OnGUI()
    {
        if (MiraDeInteracao.Alvo == this) MiraDeInteracao.Indicar(Tecla, Indicacao);
    }
}
