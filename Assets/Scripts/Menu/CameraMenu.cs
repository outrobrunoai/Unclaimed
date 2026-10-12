using UnityEngine;

public class CameraBreathing : MonoBehaviour
{
    [Header("Configurações de Movimento")]
    public float amplitude = 0.03f; // Quão longe a câmera move
    public float velocidadeRespiração = 0.5f; // Quão rápido o tempo passa

    [Header("O Segredo da Fluidez")]
    [Tooltip("Clique na curva verde no Inspector para moldar a desaceleração no topo e na base.")]
    public AnimationCurve curvaDeRespiracao;

    private Vector3 posicaoInicial;
    private float tempoAtual;

    void Start()
    {
        posicaoInicial = transform.localPosition;

        // Se a curva estiver vazia, cria uma curva em "S" padrão como base
        if (curvaDeRespiracao.keys.Length == 0)
        {
            curvaDeRespiracao = new AnimationCurve(
                new Keyframe(0f, -1f, 0f, 0f),   // Começa no fundo, velocidade zero
                new Keyframe(0.5f, 1f, 0f, 0f),  // Chega no topo, velocidade zero
                new Keyframe(1f, -1f, 0f, 0f)    // Volta pro fundo, velocidade zero
            );
        }

        // Faz a curva se repetir infinitamente de forma automática
        curvaDeRespiracao.preWrapMode = WrapMode.Loop;
        curvaDeRespiracao.postWrapMode = WrapMode.Loop;
    }

    void Update()
    {
        // O tempo avança
        tempoAtual += Time.deltaTime * velocidadeRespiração;

        // O valor Y segue exatamente o desenho da sua curva
        float swayY = curvaDeRespiracao.Evaluate(tempoAtual) * amplitude;
        
        // O valor X faz um movimento circular levemente dessincronizado do Y para parecer orgânico
        float swayX = Mathf.Cos(tempoAtual * Mathf.PI) * (amplitude * 0.5f);

        transform.localPosition = posicaoInicial + new Vector3(swayX, swayY, 0);
    }
}