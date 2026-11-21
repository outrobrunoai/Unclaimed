using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

public class BotaoSangueHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Componentes de UI")]
    public Image rastroSangueImage; // Arraste o seu objeto RastroSangue para cá

    [Header("Ajustes de Animação")]
    [Tooltip("Duração da transição do sangue se espalhando (em segundos)")]
    public float tempoTransicao = 0.15f; 

    [Header("Som (Opcional)")]
    public AudioSource somInteracao; // Um som rápido de corte, "splash" ou fricção

    private Coroutine corrotinaAtual;

    void Start()
    {
        // Garante que o sangue comece totalmente invisível
        if (rastroSangueImage != null)
        {
            rastroSangueImage.fillAmount = 0f;
        }
    }

    // Menu escondido (ex.: abriu as opções) com o mouse em cima: o OnPointerExit não chega, então apaga aqui
    void OnDisable()
    {
        corrotinaAtual = null;
        if (rastroSangueImage != null)
        {
            rastroSangueImage.fillAmount = 0f;
        }
    }

    // Chamado automaticamente quando o mouse entra no botão
    public void OnPointerEnter(PointerEventData eventData)
    {
        TocarSom();
        IniciarTransicao(1f); // Alvo: 100% visível (1)
    }

    // Chamado automaticamente quando o mouse sai do botão
    public void OnPointerExit(PointerEventData eventData)
    {
        IniciarTransicao(0f); // Alvo: 0% visível (0)
    }

    private void IniciarTransicao(float valorAlvo)
    {
        // Se já houver uma animação rodando, para ela antes de começar a outra
        if (corrotinaAtual != null)
        {
            StopCoroutine(corrotinaAtual);
        }

        corrotinaAtual = StartCoroutine(AnimarSangue(valorAlvo));
    }

    private IEnumerator AnimarSangue(float valorAlvo)
    {
        if (rastroSangueImage == null) yield break;

        float valorInicial = rastroSangueImage.fillAmount;
        float tempoPassado = 0f;

        while (tempoPassado < tempoTransicao)
        {
            tempoPassado += Time.unscaledDeltaTime;
            float progresso = tempoPassado / tempoTransicao;

            // Usa suavização (Lerp) para a transição não ser seca
            rastroSangueImage.fillAmount = Mathf.Lerp(valorInicial, valorAlvo, progresso);
            
            yield return null; // Espera o próximo frame
        }

        rastroSangueImage.fillAmount = valorAlvo; // Garante que cravou no valor final
    }

    private void TocarSom()
    {
        if (somInteracao != null)
        {
            somInteracao.PlayOneShot(somInteracao.clip);
        }
    }
}