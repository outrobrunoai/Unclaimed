using UnityEngine;
using System.Collections;

public class VultoNaPorta : MonoBehaviour
{
    [Tooltip("Objeto do vulto, desativado enquanto não está atravessando o corredor.")]
    public Transform vulto;
    [Tooltip("Uma das extremidades do caminho do vulto, em posição de repouso do passo.")]
    public Transform pontoA;
    [Tooltip("A outra extremidade do caminho do vulto, em posição de repouso do passo.")]
    public Transform pontoB;
    [Tooltip("Alvo para o qual o vulto olha ao parar. Opcional; vazio mantém a direção atual.")]
    public Transform olharPara;

    [Tooltip("Intervalo mínimo e máximo, em segundos, entre o fim de uma aparição e a próxima.")]
    public Vector2 intervalo = new Vector2(18f, 40f);
    [Tooltip("Espera, em segundos, antes da primeira aparição ao ativar o componente.")]
    public float primeiraAparicao = 9f;
    [Tooltip("Velocidade da travessia em metros por segundo.")]
    public float velocidade = 1.1f;
    [Tooltip("Probabilidade de parar no meio do caminho, de zero a um.")]
    public float chanceDeParar = 0.45f;
    [Tooltip("Tempo mínimo e máximo, em segundos, durante o qual o vulto fica parado.")]
    public Vector2 tempoParado = new Vector2(0.8f, 2.2f);
    [Tooltip("Probabilidade de voltar para a origem depois da parada, de zero a um.")]
    public float chanceDeVoltar = 0.3f;
    [Tooltip("Amplitude da oscilação vertical dos passos, em metros. Zero desliga o balanço.")]
    public float balanco = 0.03f;

    Coroutine rotina;
    bool avisouReferencias;
    float tempoAndando;

    void OnEnable()
    {
        if (vulto != null) vulto.gameObject.SetActive(false);

        if (vulto == null || pontoA == null || pontoB == null)
        {
            if (!avisouReferencias)
            {
                Debug.LogWarning("VultoNaPorta precisa das referências vulto, pontoA e pontoB.", this);
                avisouReferencias = true;
            }
            return;
        }

        rotina = StartCoroutine(RotinaDeAparicoes());
    }

    void OnDisable()
    {
        if (rotina != null)
        {
            StopCoroutine(rotina);
            rotina = null;
        }
        if (vulto != null) vulto.gameObject.SetActive(false);
    }

    IEnumerator RotinaDeAparicoes()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, primeiraAparicao));

        while (true)
        {
            bool parteDeA = Random.value < 0.5f;
            Vector3 origem = parteDeA ? pontoA.position : pontoB.position;
            Vector3 destino = parteDeA ? pontoB.position : pontoA.position;
            Vector3 meio = (origem + destino) * 0.5f;
            bool vaiParar = Random.value < Mathf.Clamp01(chanceDeParar);

            tempoAndando = 0f;
            vulto.position = origem;
            VirarPara(destino - origem);
            vulto.gameObject.SetActive(true);

            if (vaiParar)
            {
                yield return Caminhar(origem, meio);
                FiltroVHS.Falha(0.35f, 0.25f);

                float restante = SortearTempo(tempoParado);
                while (restante > 0f)
                {
                    if (olharPara != null)
                    {
                        Vector3 direcao = olharPara.position - vulto.position;
                        direcao.y = 0f;
                        if (direcao.sqrMagnitude > 0.000001f)
                        {
                            float angulo = Mathf.Atan2(direcao.x, direcao.z) * Mathf.Rad2Deg;
                            float atual = vulto.eulerAngles.y;
                            float suavidade = 1f - Mathf.Exp(-5f * Time.deltaTime);
                            vulto.rotation = Quaternion.Euler(0f, Mathf.LerpAngle(atual, angulo, suavidade), 0f);
                        }
                    }
                    restante -= Time.deltaTime;
                    yield return null;
                }

                if (Random.value < Mathf.Clamp01(chanceDeVoltar)) destino = origem;
                yield return Caminhar(meio, destino);
            }
            else
            {
                yield return Caminhar(origem, destino);
            }

            vulto.gameObject.SetActive(false);
            yield return new WaitForSeconds(SortearTempo(intervalo));
        }
    }

    IEnumerator Caminhar(Vector3 origem, Vector3 destino)
    {
        VirarPara(destino - origem);
        float distancia = Vector3.Distance(origem, destino);
        float percorrido = 0f;

        while (percorrido < distancia)
        {
            percorrido = Mathf.Min(distancia, percorrido + Mathf.Max(0.01f, velocidade) * Time.deltaTime);
            tempoAndando += Time.deltaTime;
            Vector3 posicao = Vector3.Lerp(origem, destino, percorrido / distancia);
            posicao.y += Mathf.Sin(tempoAndando * 1.8f * Mathf.PI * 2f) * balanco;
            vulto.position = posicao;
            yield return null;
        }

        // Remove o balanço ao chegar, inclusive durante a parada no visor.
        vulto.position = destino;
    }

    void VirarPara(Vector3 direcao)
    {
        direcao.y = 0f;
        if (direcao.sqrMagnitude <= 0.000001f) return;
        float angulo = Mathf.Atan2(direcao.x, direcao.z) * Mathf.Rad2Deg;
        vulto.rotation = Quaternion.Euler(0f, angulo, 0f);
    }

    float SortearTempo(Vector2 limites)
    {
        float minimo = Mathf.Max(0f, Mathf.Min(limites.x, limites.y));
        float maximo = Mathf.Max(minimo, Mathf.Max(limites.x, limites.y));
        return Random.Range(minimo, maximo);
    }
}
