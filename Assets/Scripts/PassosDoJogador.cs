using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Som dos passos do jogador, no ritmo do balanço da câmera (Movimento.AoDarPasso: um som por pé no chão).
/// Coloque no Player, junto do Movimento, e preencha os conjuntos de sons no Inspector.
///
/// A superfície é o chão sob o jogador (um raio para baixo), reconhecida nesta ordem:
///   1. um SuperficieDePasso no objeto atingido ou num pai dele (para marcar algo à mão);
///   2. o nome do Physics Material do collider;
///   3. o nome do material do Renderer atingido (ex.: "Asfalto", "Floor - clinico").
/// Um conjunto vale para a superfície se um dos seus "nomes" aparece nesse texto (sem diferenciar maiúsculas),
/// então "Floor" cobre todos os pisos internos e "Grama" cobre "Grama alta". Nenhum casou = conjunto padrão.
/// </summary>
[RequireComponent(typeof(Movimento))]
public class PassosDoJogador : MonoBehaviour
{
    [Serializable]
    public class ConjuntoDePassos
    {
        [Tooltip("Trechos de nome que escolhem este conjunto (ex.: \"Floor\", \"Piso\"). Vazio no conjunto padrão.")]
        public string[] nomes = Array.Empty<string>();
        [Tooltip("Variações do som; um diferente do anterior é sorteado a cada passo.")]
        public AudioClip[] sons = Array.Empty<AudioClip>();
        [Range(0f, 1f)] public float volume = 1f;
    }

    [Header("Sons")]
    [Tooltip("Usado quando nenhuma superfície abaixo casar (e se o chão não for reconhecido).")]
    public ConjuntoDePassos padrao = new ConjuntoDePassos();
    public List<ConjuntoDePassos> superficies = new List<ConjuntoDePassos>();

    [Header("Mixagem")]
    [Tooltip("Vazio = cria um AudioSource 2D neste objeto.")]
    public AudioSource fonte;
    [Tooltip("Opcional: grupo do Audio Mixer (ex.: Efeitos).")]
    public AudioMixerGroup saida;
    [Range(0f, 1f)] public float volumeGeral = 0.7f;
    [Range(0f, 1f)] public float volumeAgachado = 0.45f;
    [Tooltip("Variação aleatória de tom, para os passos não soarem idênticos.")]
    [Range(0f, 0.3f)] public float variacaoDeTom = 0.06f;
    [Range(0f, 0.3f)] public float variacaoDeVolume = 0.1f;

    [Header("Detecção do chão")]
    public float alcanceDoRaio = 2f;
    public LayerMask camadas = ~(1 << 2);   // ignora "Ignore Raycast"

    [Header("Depuração")]
    [Tooltip("Preenchido no Play: o texto do chão no último passo. Ande sobre cada piso e copie daqui os \"nomes\".")]
    [SerializeField] string ultimaSuperficie = "";
    public string UltimaSuperficie => ultimaSuperficie;

    Movimento movimento;
    CharacterController controle;
    readonly Dictionary<ConjuntoDePassos, int> ultimoSom = new Dictionary<ConjuntoDePassos, int>();

    void Awake()
    {
        movimento = GetComponent<Movimento>();
        controle = GetComponent<CharacterController>();
        if (fonte == null)
        {
            fonte = gameObject.AddComponent<AudioSource>();
            fonte.playOnAwake = false;
            fonte.spatialBlend = 0f;   // os próprios passos: sempre "na cabeça" do jogador
        }
        if (saida != null) fonte.outputAudioMixerGroup = saida;
    }

    void OnEnable() => movimento.AoDarPasso += Tocar;
    void OnDisable() => movimento.AoDarPasso -= Tocar;

    void Tocar(bool agachado)
    {
        string superficie = SuperficieAbaixo();
        ultimaSuperficie = superficie;
        ConjuntoDePassos conjunto = Escolher(superficie);
        AudioClip som = Sortear(conjunto);
        if (som == null) return;

        fonte.pitch = 1f + UnityEngine.Random.Range(-variacaoDeTom, variacaoDeTom);
        float volume = volumeGeral * conjunto.volume * (agachado ? volumeAgachado : 1f) *
                       (1f + UnityEngine.Random.Range(-variacaoDeVolume, variacaoDeVolume));
        fonte.PlayOneShot(som, Mathf.Clamp01(volume));
    }

    /// <summary>Texto que descreve o chão sob o jogador ("" se o raio não achou nada).</summary>
    public string SuperficieAbaixo()
    {
        Vector3 origem = controle != null ? transform.TransformPoint(controle.center) : transform.position + Vector3.up;
        if (!Physics.Raycast(origem, Vector3.down, out RaycastHit acerto, alcanceDoRaio, camadas, QueryTriggerInteraction.Ignore))
            return "";

        var marcada = acerto.collider.GetComponentInParent<SuperficieDePasso>();
        if (marcada != null) return marcada.superficie;
        if (acerto.collider.sharedMaterial != null) return acerto.collider.sharedMaterial.name;
        var renderer = acerto.collider.GetComponent<Renderer>();
        if (renderer != null && renderer.sharedMaterial != null) return renderer.sharedMaterial.name;
        return acerto.collider.name;
    }

    ConjuntoDePassos Escolher(string superficie)
    {
        if (!string.IsNullOrEmpty(superficie))
            foreach (ConjuntoDePassos c in superficies)
                foreach (string nome in c.nomes)
                    if (!string.IsNullOrEmpty(nome) && superficie.IndexOf(nome, StringComparison.OrdinalIgnoreCase) >= 0)
                        return c;
        return padrao;
    }

    // Nunca repete o mesmo som duas vezes seguidas (se houver mais de um).
    AudioClip Sortear(ConjuntoDePassos conjunto)
    {
        if (conjunto.sons == null || conjunto.sons.Length == 0)
            return conjunto != padrao ? Sortear(padrao) : null;
        if (conjunto.sons.Length == 1) return conjunto.sons[0];
        int i;
        if (ultimoSom.TryGetValue(conjunto, out int anterior))
        {
            i = UnityEngine.Random.Range(0, conjunto.sons.Length - 1);
            if (i >= anterior) i++;
        }
        else i = UnityEngine.Random.Range(0, conjunto.sons.Length);
        ultimoSom[conjunto] = i;
        return conjunto.sons[i];
    }
}
