using UnityEngine;
using System.Collections;

public class FlickerFluorescente : MonoBehaviour
{
    [Header("Configurações da Luz (Física)")]
    public Light luzFluorescente;
    public float intensidadeMinima = 0.1f;
    public float intensidadeMaxima = 1.5f;

    [Header("Configurações do Material (Difusor)")]
    public Renderer difusorRenderer;
    public Color corEmissiva = Color.white;
    public float multiplicadorEmissao = 2f;
    private Material materialDifusor;

    [Header("Cadência (Ritmo)")]
    [Tooltip("Tempo MÍNIMO que a luz fica normal antes de dar defeito")]
    public float tempoLuzEstavelMin = 3f;
    [Tooltip("Tempo MÁXIMO que a luz fica normal antes de dar defeito")]
    public float tempoLuzEstavelMax = 10f;

    [Header("Som (Opcional)")]
    public AudioSource somChoqueEletrico;

    void Start()
    {
        if (luzFluorescente == null) luzFluorescente = GetComponent<Light>();
        
        if (difusorRenderer != null)
        {
            // Isso cria uma cópia única do material para esta lâmpada específica
            materialDifusor = difusorRenderer.material;
            materialDifusor.EnableKeyword("_EMISSION");
        }
        
        StartCoroutine(RotinaDeFlicker());
    }

    IEnumerator RotinaDeFlicker()
    {
        while (true)
        {
            // 1. ESTADO NORMAL: Fica acesa e estável por vários segundos
            AplicarIntensidade(intensidadeMaxima, multiplicadorEmissao);
            
            float tempoAcesa = Random.Range(tempoLuzEstavelMin, tempoLuzEstavelMax);
            yield return new WaitForSeconds(tempoAcesa);

            // 2. ESTADO DE SURTO: Dá uma sequência rápida de 2 a 5 piscadas
            int totalPiscadasNoSurto = Random.Range(2, 6);

            for (int i = 0; i < totalPiscadasNoSurto; i++)
            {
                float sorteio = Random.Range(0f, 100f);
                
                if (sorteio < 30f)
                {
                    // Falha grave (quase apaga)
                    AplicarIntensidade(intensidadeMinima, 0f);
                    TocarSomZumbido(0.023f);
                    yield return new WaitForSeconds(Random.Range(0.1f, 0.4f));
                }
                else
                {
                    // Falha leve (pisca na metade da força)
                    AplicarIntensidade(Random.Range(intensidadeMinima, intensidadeMaxima / 2), multiplicadorEmissao * 0.3f);
                    TocarSomZumbido(0.055f);
                    yield return new WaitForSeconds(Random.Range(0.05f, 0.15f));
                }
                
                // Pequena pausa rápida com ela acesa entre as piscadas do mesmo surto
                AplicarIntensidade(intensidadeMaxima, multiplicadorEmissao);
                yield return new WaitForSeconds(Random.Range(0.05f, 0.15f));
            }
        }
    }

    // Função para não repetir código toda hora
    void AplicarIntensidade(float intensidadeLuz, float intensidadeMat)
    {
        luzFluorescente.intensity = intensidadeLuz;
        if (materialDifusor != null)
        {
            materialDifusor.SetColor("_EmissionColor", corEmissiva * intensidadeMat);
        }
    }

    void TocarSomZumbido(float volume)
    {
        if (somChoqueEletrico != null && !somChoqueEletrico.isPlaying)
        {
            somChoqueEletrico.volume = volume;
            somChoqueEletrico.Play();
        }
    }
}