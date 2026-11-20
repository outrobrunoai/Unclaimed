using UnityEngine;

/// <summary>
/// Respiração sutil do corpo sob o lençol da mesa de autópsia. O peito sobe e desce devagar
/// quando o jogador não está olhando direto; ao encarar, a respiração para suavemente.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
public class RespiracaoDoLencol : MonoBehaviour
{
    public Vector3 centroLocal;                        // ponto do peito, no espaço deste objeto
    [Min(0.05f)] public float raio = 0.35f;            // alcance da deformação em volta do centro (m, espaço local)
    [Min(0f)] public float amplitude = 0.012f;         // subida máxima (m)
    [Min(0.5f)] public float ciclo = 4.2f;             // segundos por respiração
    [Range(1f, 60f)] public float anguloOlhando = 14f; // dentro deste ângulo do centro da visão = olhando
    [Min(0.5f)] public float distanciaMaxima = 9f;     // mais longe que isso conta como não olhando
    [Min(0.05f)] public float suavizar = 0.6f;         // segundos para parar/voltar
    public string fatoQueEncerra = "daniel_saiu";      // com o fato, para de vez

    MeshFilter filtro;
    Mesh malhaClonada;
    Mesh ultimaInlegivel;
    Vector3[] verticesBase;
    Vector3[] verticesAtuais;
    float[] pesosVertices;
    Vector3 centroAplicado;
    float raioAplicado;

    float peso = 1f;
    float ultimoDeslocamento;
    bool encerrado;

    public static float Deslocamento(float tempo, float ciclo, float amplitude, float peso)
    {
        if (ciclo <= 0f) return 0f;
        return amplitude * peso * (0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * tempo / ciclo));
    }

    public static float Queda(float distancia, float raio)
    {
        if (raio <= 0f || distancia >= raio) return 0f;
        if (distancia <= 0f) return 1f;
        float u = distancia / raio;
        float t = 1f - u * u;
        return t * t;
    }

    public static bool Olhando(Vector3 olhos, Vector3 frente, Vector3 ponto, float angulo, float distanciaMaxima)
    {
        if (distanciaMaxima <= 0f || frente.sqrMagnitude < 1e-8f) return false;
        Vector3 para = ponto - olhos;
        float distSq = para.sqrMagnitude;
        if (distSq > distanciaMaxima * distanciaMaxima) return false;
        if (distSq < 1e-8f) return true;
        return Vector3.Angle(frente, para) <= angulo;
    }

    void Start()
    {
        filtro = GetComponent<MeshFilter>();
        if (FatoEncerrou())
        {
            encerrado = true;
            peso = 0f;
            return;
        }
        PrepararMalha();
    }

    void Update()
    {
        if (encerrado) return;

        if (FatoEncerrou())
        {
            encerrado = true;
            peso = 0f;
            if (malhaClonada != null && verticesBase != null) Aplicar(0f);
            return;
        }

        PrepararMalha();
        if (malhaClonada == null || verticesBase == null) return;

        if (centroLocal != centroAplicado || !Mathf.Approximately(raio, raioAplicado))
            RecalcularPesos();

        bool olhando = false;
        Transform olhos = Jogador.Olhos;
        if (olhos != null)
        {
            Vector3 pontoMundo = transform.TransformPoint(centroLocal);
            olhando = Olhando(olhos.position, olhos.forward, pontoMundo, anguloOlhando, distanciaMaxima);
        }

        float alvoPeso = olhando ? 0f : 1f;
        peso = Mathf.MoveTowards(peso, alvoPeso, Time.deltaTime / Mathf.Max(0.05f, suavizar));

        float deslocamento = Deslocamento(Time.time, ciclo, amplitude, peso);
        Aplicar(deslocamento);
    }

    void OnDestroy() => DestruirClone();

    bool FatoEncerrou() => !string.IsNullOrEmpty(fatoQueEncerra) && EstadoDaNoite.Tem(fatoQueEncerra);

    void PrepararMalha()
    {
        if (filtro == null) filtro = GetComponent<MeshFilter>();
        if (filtro == null) return;

        Mesh atual = filtro.sharedMesh;
        if (atual == malhaClonada) return;

        // Outro script trocou a malha (ou primeira configuração no Start).
        DestruirClone();
        if (atual == null) return;

        if (!atual.isReadable)
        {
            if (ultimaInlegivel != atual)
            {
                ultimaInlegivel = atual;
                Debug.LogWarning($"[RespiracaoDoLencol] A malha '{atual.name}' em '{name}' não é legível (habilite Read/Write).", this);
            }
            return;
        }

        ultimaInlegivel = null;
        malhaClonada = Instantiate(atual);
        malhaClonada.name = atual.name + " (Respiracao)";
        malhaClonada.MarkDynamic();
        filtro.sharedMesh = malhaClonada;

        verticesBase = malhaClonada.vertices;
        verticesAtuais = new Vector3[verticesBase.Length];
        pesosVertices = new float[verticesBase.Length];
        RecalcularPesos();
        ultimoDeslocamento = 0f;
    }

    void RecalcularPesos()
    {
        centroAplicado = centroLocal;
        raioAplicado = raio;
        for (int i = 0; i < verticesBase.Length; i++)
            pesosVertices[i] = Queda(Vector3.Distance(verticesBase[i], centroLocal), raio);
        ultimoDeslocamento = float.NaN;
    }

    void Aplicar(float deslocamento)
    {
        if (Mathf.Abs(deslocamento - ultimoDeslocamento) <= 1e-5f) return;
        ultimoDeslocamento = deslocamento;
        for (int i = 0; i < verticesBase.Length; i++)
        {
            float p = pesosVertices[i];
            verticesAtuais[i] = p > 0f ? verticesBase[i] + Vector3.up * (p * deslocamento) : verticesBase[i];
        }
        malhaClonada.vertices = verticesAtuais;
    }

    void DestruirClone()
    {
        if (malhaClonada != null)
        {
            if (Application.isPlaying) Destroy(malhaClonada);
            else DestroyImmediate(malhaClonada);
            malhaClonada = null;
        }
        verticesBase = null;
        verticesAtuais = null;
        pesosVertices = null;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.3f, 0.9f, 0.6f, 0.6f);
        Gizmos.DrawWireSphere(centroLocal, raio);
    }
}
