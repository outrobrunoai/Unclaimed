using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PortalDeChuva : MonoBehaviour
{
    [Min(0)] public float largura = 2f;
    public FolhaDePorta[] folhas;
    [Range(0, 1)] public float transmissaoFechada = 0.25f;
    public float corteFechada = 1400f;
    public float corteAberta = 22000f;
    [Min(0.001f)] public float raioReferencia = 1.2f;
    [Min(0)] public float amortecimentoPorMetro = 0.06f;
    [Tooltip("Corte do som que chega pela abertura a 8 m de caminho (absorção e difração gradual).")]
    public float corteDistante = 6000f;
    [Tooltip("Corte quando o caminho dobra uma esquina (sem visada da abertura).")]
    public float corteSemVisada = 3000f;
    [Tooltip("Além dos batentes, quanto do entorno da porta decide o lado pelo plano real (m).")]
    public float margemLateral = 1f;
    [Tooltip("Distância ao plano da entrada dentro da qual o lado é decidido pelo plano (m).")]
    public float alcancePlano = 2.5f;
    public AudioSource fonte;
    public AudioLowPassFilter filtro;

    MapaAbrigoChuva mapa;
    float[] distancias;
    Vector3 centro, direita, frente;
    float velocidadeGanho, velocidadeCorte, alvoCorteCaminho = 22000f, corteCaminho = 22000f, velocidadeCaminho, corteDaPorta = -1;

    // Fraction of the opening that is actually open: each leaf covers its own share of the doorway.
    public float Abertura01
    {
        get
        {
            float soma = 0;
            int n = 0;
            if (folhas != null)
                foreach (var folha in folhas)
                    if (folha) { soma += AberturaDa(folha); n++; }
            return n == 0 ? 1f : soma / n;
        }
    }

    static float AberturaDa(FolhaDePorta folha) => Mathf.Clamp01(folha.Abertura / Mathf.Max(0.001f, folha.anguloMaximo));

    // Lateral span of the doorway covered by a leaf: a double door splits it at the centre, on the hinge side.
    void TrechoDa(FolhaDePorta folha, int quantas, out float de, out float ate)
    {
        de = -largura * 0.5f; ate = largura * 0.5f;
        if (quantas < 2) return;
        if (Vector3.Dot(folha.transform.position - centro, direita) < 0) ate = 0; else de = 0;
    }

    /// True near this doorway; ladoInterno tells on which side of the real entrance plane the point is.
    public bool Perto(Vector3 p, out bool ladoInterno)
    {
        Vector3 d = p - transform.position;
        float lateral = Vector3.Dot(d, ProjetarHorizontal(transform.right)), plano = Vector3.Dot(d, ProjetarHorizontal(transform.forward));
        ladoInterno = plano > 0;
        return Mathf.Abs(lateral) <= largura * 0.5f + margemLateral && Mathf.Abs(plano) <= alcancePlano;
    }

    static Vector3 ProjetarHorizontal(Vector3 v) => Vector3.ProjectOnPlane(v, Vector3.up).normalized;

    void Awake()
    {
        centro = transform.position;
        direita = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        frente = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (!fonte)
        {
            var go = new GameObject("Audio portal");
            go.transform.SetParent(transform, false);
            fonte = go.AddComponent<AudioSource>();
        }
        if (!filtro) filtro = fonte.GetComponent<AudioLowPassFilter>();
        if (!filtro) filtro = fonte.gameObject.AddComponent<AudioLowPassFilter>();
        fonte.loop = true;
        fonte.spatialBlend = 1;
        fonte.dopplerLevel = 0;
        fonte.rolloffMode = AudioRolloffMode.Custom;
        fonte.SetCustomCurve(AudioSourceCurveType.CustomRolloff, AnimationCurve.Linear(0, 1, 1, 1));
        fonte.minDistance = 1;
        fonte.maxDistance = 500;
        fonte.playOnAwake = false;
        fonte.volume = 0;
        filtro.cutoffFrequency = Corte(Abertura01);
    }

    void Start()
    {
        var chuva = GetComponentInParent<ChuvaLeve>();
        if (!chuva) chuva = FindAnyObjectByType<ChuvaLeve>();
        mapa = chuva ? chuva.mapa : null;
        ConstruirCampo();
    }

    void OnDisable()
    {
        if (fonte) { fonte.Stop(); fonte.volume = 0; }
        velocidadeGanho = velocidadeCorte = 0;
    }

    void ConstruirCampo()
    {
        if (!mapa || mapa.largura <= 0 || mapa.altura <= 0 || mapa.celula <= 0) return;
        int w = mapa.largura, h = mapa.altura;
        distancias = new float[w * h];
        var internas = new List<int>();
        for (int z = 0; z < h; z++)
            for (int x = 0; x < w; x++)
            {
                int i = z * w + x;
                distancias[i] = float.PositiveInfinity;
                if (mapa.ProfundidadeNaCelula(x, z) <= 0) continue;
                internas.Add(i);
                Vector3 p = mapa.CentroCelula(x, z, centro.y) - centro;
                float dentro = Vector3.Dot(p, frente);
                if (Mathf.Abs(Vector3.Dot(p, direita)) <= largura * 0.5f && dentro >= 0 && dentro <= 0.4f)
                    distancias[i] = 0;
            }
        // Sweep only internal cells. Diagonal steps cannot cut across blocked corners.
        float diagonal = mapa.celula * 1.41421356f;
        bool mudou;
        do
        {
            mudou = false;
            for (int sweep = 0; sweep < 2; sweep++)
                for (int k = 0; k < internas.Count; k++)
                {
                    int i = internas[sweep == 0 ? k : internas.Count - 1 - k];
                    int x = i % w, z = i / w;
                    float melhor = distancias[i];
                    int dz = sweep == 0 ? -1 : 1, dx = sweep == 0 ? -1 : 1;
                    Relaxar(x, z, x + dx, z, mapa.celula, ref melhor);
                    Relaxar(x, z, x, z + dz, mapa.celula, ref melhor);
                    Relaxar(x, z, x - 1, z + dz, diagonal, ref melhor);
                    Relaxar(x, z, x + 1, z + dz, diagonal, ref melhor);
                    if (melhor < distancias[i] - 0.00001f) { distancias[i] = melhor; mudou = true; }
                }
        } while (mudou);
    }

    void Relaxar(int x, int z, int nx, int nz, float passo, ref float melhor)
    {
        if (mapa.ProfundidadeNaCelula(nx, nz) <= 0) return;
        if (nx != x && nz != z && (mapa.ProfundidadeNaCelula(nx, z) <= 0 || mapa.ProfundidadeNaCelula(x, nz) <= 0)) return;
        melhor = Mathf.Min(melhor, distancias[nz * mapa.largura + nx] + passo);
    }

    float Distancia(int x, int z)
    {
        if (distancias == null || x < 0 || z < 0 || x >= mapa.largura || z >= mapa.altura) return float.PositiveInfinity;
        return distancias[z * mapa.largura + x];
    }

    float Derivada(float menos, float mais, float atual)
    {
        bool m = !float.IsInfinity(menos), p = !float.IsInfinity(mais);
        if (m && p) return (mais - menos) / (2 * mapa.celula);
        if (p) return (mais - atual) / mapa.celula;
        if (m) return (atual - menos) / mapa.celula;
        return 0;
    }

    // The open part dominates the spectrum, so the filter follows the most open leaf.
    float AberturaMaxima()
    {
        float m = 0; int n = 0;
        if (folhas != null) foreach (var folha in folhas) if (folha) { m = Mathf.Max(m, AberturaDa(folha)); n++; }
        return n == 0 ? 1 : m;
    }

    float Corte(float abertura) => Mathf.Exp(Mathf.Lerp(Mathf.Log(Mathf.Max(10, corteFechada)),
        Mathf.Log(Mathf.Max(10, corteAberta)), abertura));

    public void Atualizar(Vector3 ouvinte, float dentro01, float volumeBase, float suavizacao)
    {
        if (!fonte) return;
        float abertura = AberturaMaxima();
        float ganho = 0;
        if (mapa && distancias != null && mapa.Celula(ouvinte, out int x, out int z))
        {
            float d = Distancia(x, z);
            if (!float.IsInfinity(d))
            {
                float raio = Mathf.Max(0.001f, raioReferencia);
                float att = Mathf.Min(1, raio / Mathf.Max(d, raio)) * Mathf.Exp(-amortecimentoPorMetro * Mathf.Max(0, d - raio));
                // Each leaf passes the open sound through its own share and glass transmission elsewhere;
                // the source sits on the nearest OPEN part of the doorway, or on the glass when all are shut.
                float porta = 0, lateral = Vector3.Dot(ouvinte - centro, direita), melhorLateral = 0, melhorErro = float.MaxValue;
                int quantas = 0;
                if (folhas != null) foreach (var folha in folhas) if (folha) quantas++;
                if (quantas == 0) { porta = 1; melhorLateral = Mathf.Clamp(lateral, -largura * 0.5f, largura * 0.5f); melhorErro = 0; }
                else foreach (var folha in folhas)
                {
                    if (!folha) continue;
                    float a = AberturaDa(folha);
                    porta += Mathf.Lerp(transmissaoFechada, 1, a) / quantas;
                    if (a < 0.05f) continue;
                    TrechoDa(folha, quantas, out float de, out float ate);
                    float l = Mathf.Clamp(lateral, de, ate);
                    if (Mathf.Abs(l - lateral) < melhorErro) { melhorErro = Mathf.Abs(l - lateral); melhorLateral = l; }
                }
                if (melhorErro == float.MaxValue) melhorLateral = Mathf.Clamp(lateral, -largura * 0.5f, largura * 0.5f);
                ganho = volumeBase * Mathf.Sqrt(Mathf.Clamp01(dentro01)) * att * porta;
                Vector3 ponto = centro + direita * melhorLateral;
                ponto.y = ouvinte.y;
                bool visada = d <= Vector3.Distance(ouvinte, ponto) * 1.15f + 0.3f;
                // Path colour: gently darker with walking distance, and more so around corners.
                alvoCorteCaminho = Mathf.Exp(Mathf.Lerp(Mathf.Log(corteAberta), Mathf.Log(corteDistante), Mathf.Clamp01((d - raio) / 8f)));
                if (!visada) alvoCorteCaminho = Mathf.Min(alvoCorteCaminho, corteSemVisada);
                if (visada) fonte.transform.position = ponto;
                else
                {
                    Vector3 dir = new Vector3(-Derivada(Distancia(x - 1, z), Distancia(x + 1, z), d), 0,
                        -Derivada(Distancia(x, z - 1), Distancia(x, z + 1), d));
                    // Equal-length routes can cancel the central gradient at a junction.
                    if (dir.sqrMagnitude < 0.000001f)
                    {
                        float melhor = d;
                        for (int dz = -1; dz <= 1; dz++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                if (dx == 0 && dz == 0) continue;
                                if (dx != 0 && dz != 0 && (mapa.ProfundidadeNaCelula(x + dx, z) <= 0 ||
                                    mapa.ProfundidadeNaCelula(x, z + dz) <= 0)) continue;
                                float vizinho = Distancia(x + dx, z + dz);
                                if (vizinho >= melhor) continue;
                                melhor = vizinho;
                                dir = new Vector3(dx, 0, dz);
                            }
                    }
                    fonte.transform.position = ouvinte + dir.normalized * d;
                }
                fonte.spread = Mathf.Lerp(140, 40, Mathf.Clamp01((d - 0.5f) / 4));
            }
        }
        fonte.volume = Mathf.SmoothDamp(fonte.volume, ganho, ref velocidadeGanho, suavizacao);
        // Slow smoothing for the path colour so walking never reads as a filter sweep.
        corteCaminho = Mathf.SmoothDamp(corteCaminho, alvoCorteCaminho, ref velocidadeCaminho, 0.6f);
        if (corteDaPorta < 0) corteDaPorta = Corte(abertura);
        corteDaPorta = Mathf.SmoothDamp(corteDaPorta, Corte(abertura), ref velocidadeCorte, suavizacao);
        if (filtro) filtro.cutoffFrequency = Mathf.Min(corteCaminho, corteDaPorta);
    }
}
