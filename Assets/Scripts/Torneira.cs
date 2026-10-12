using UnityEngine;

/// <summary>
/// Torneira de pressão (lavatório): E aperta o botão, que afunda e volta pela mola, e abre ou fecha a água.
/// O fluxo é um tubo fino gerado ao longo de uma trajetória fixa (bico → ponto de impacto na cuba, calculada no
/// Editor pelo construtor de prefabs), com textura de estrias rolando para baixo: ao abrir, a frente da água desce
/// em queda livre até a cuba; ao fechar, a cauda se solta do bico e cai. Respingos (ParticleSystem, colidindo com
/// o plano da cuba) e um anel de espuma no impacto só aparecem enquanto a água chega à cuba.
/// Fica na raiz do lavatório: mirar qualquer parte dele (cuba ou torneira) mostra "Abrir torneira".
/// </summary>
[DisallowMultipleComponent]
public class Torneira : AlvoDeInteracao
{
    [Header("Interação")]
    public string tecla = "E";
    [Min(0.2f)] public float alcance = 1.8f;
    public string textoAbrir = "Abrir torneira";
    public string textoFechar = "Fechar torneira";

    [Header("Botão")]
    public Transform botao;
    [Tooltip("Deslocamento local do botão apertado até o fim.")]
    public Vector3 cursoBotao = new Vector3(0f, -0.004f, 0f);

    [Header("Água")]
    [Tooltip("Trajetória do fluxo no espaço deste objeto, do bico ao impacto na cuba.")]
    public Vector3[] trajetoria;
    public MeshFilter fluxo;
    public float raioNoBico = 0.0042f, raioNoImpacto = 0.003f;
    [Tooltip("Velocidade (m/s) com que as estrias descem pelo fluxo.")]
    public float velocidadeTextura = 1.4f;
    public ParticleSystem respingos;
    public Renderer espuma;

    [Header("Som (opcional)")]
    public AudioSource fonte;
    public AudioClip somBotao;
    [Tooltip("Água correndo (em loop, numa segunda fonte criada sozinha).")]
    public AudioClip somAgua;

    public bool Aberta => aberta;
    /// <summary>Verdadeiro enquanto a água atinge a cuba.</summary>
    public bool AguaNaCuba => fluxoVisivel && fim >= comprimento - 1e-4f && inicio < comprimento;

    public override float AlcanceDeInteracao => alcance;
    public override string Tecla => tecla;
    public override string Indicacao => aberta ? textoFechar : textoAbrir;
    public override void Interagir() => Alternar();

    bool aberta, fluxoVisivel;
    float inicio, fim, tempoInicio, tempoFim;   // trecho visível (m ao longo da trajetória) e tempos de queda
    float comprimento, botaoT = 1f, rolagem;
    float[] acumulado;
    Vector3 botaoRepouso;
    Mesh malha;
    Material materialFluxo, materialEspuma;
    AudioSource fonteAgua;
    const int Lados = 8;
    const float Gravidade = 9.81f, VelocidadeSaida = 0.35f;

    void Awake()
    {
        if (botao != null) botaoRepouso = botao.localPosition;
        PrepararTrajetoria();
        if (fluxo != null)
        {
            malha = new Mesh { name = "Fluxo de agua" };
            malha.MarkDynamic();
            fluxo.sharedMesh = malha;
            var r = fluxo.GetComponent<Renderer>();
            materialFluxo = r.material;   // instância própria: a rolagem da textura é desta torneira
            r.enabled = false;
        }
        if (espuma != null) { materialEspuma = espuma.material; espuma.enabled = false; }
        if (respingos != null) { var e = respingos.emission; e.enabled = false; }
        if (somAgua != null)
        {
            fonteAgua = gameObject.AddComponent<AudioSource>();
            fonteAgua.clip = somAgua;
            fonteAgua.loop = true;
            fonteAgua.playOnAwake = false;
            fonteAgua.spatialBlend = 1f;
            fonteAgua.minDistance = 0.5f;
            fonteAgua.maxDistance = 8f;
        }
    }

    void PrepararTrajetoria()
    {
        if (trajetoria == null || trajetoria.Length < 2) { comprimento = 0f; return; }
        acumulado = new float[trajetoria.Length];
        for (int i = 1; i < trajetoria.Length; i++)
            acumulado[i] = acumulado[i - 1] + Vector3.Distance(trajetoria[i - 1], trajetoria[i]);
        comprimento = acumulado[trajetoria.Length - 1];
    }

    public void Alternar()
    {
        aberta = !aberta;
        botaoT = 0f;   // o botão afunda e volta a cada toque
        if (fonte != null && somBotao != null) fonte.PlayOneShot(somBotao);
        if (aberta)
        {
            // Nova coluna d'água a partir do bico (se a anterior ainda caía, ela é substituída).
            fluxoVisivel = true;
            inicio = fim = 0f;
            tempoInicio = tempoFim = 0f;
            if (fonteAgua != null) fonteAgua.Play();
        }
        else
        {
            tempoInicio = 0f;   // a cauda se solta do bico agora
            if (fonteAgua != null) fonteAgua.Stop();
        }
    }

    protected override void Update()
    {
        base.Update();
        float dt = Time.deltaTime;
        AnimarBotao(dt);
        if (!fluxoVisivel || comprimento <= 0f) { Mostrar(false); return; }

        // A frente desce em queda livre até a cuba; ao fechar, a cauda faz o mesmo.
        tempoFim += dt;
        fim = Mathf.Min(comprimento, VelocidadeSaida * tempoFim + 0.5f * Gravidade * tempoFim * tempoFim);
        if (!aberta)
        {
            tempoInicio += dt;
            inicio = Mathf.Min(comprimento, VelocidadeSaida * tempoInicio + 0.5f * Gravidade * tempoInicio * tempoInicio);
            if (inicio >= comprimento) { fluxoVisivel = false; Mostrar(false); return; }
        }

        rolagem = (rolagem + velocidadeTextura * dt) % 1000f;
        if (materialFluxo != null) materialFluxo.mainTextureOffset = new Vector2(0f, rolagem);
        Construir(inicio, fim);
        Mostrar(true);
    }

    void AnimarBotao(float dt)
    {
        if (botao == null || botaoT >= 1f) return;
        botaoT = Mathf.Min(1f, botaoT + dt / 0.35f);
        // Afunda rápido (primeiro quarto do tempo) e volta pela mola, desacelerando.
        float k = botaoT < 0.25f ? Mathf.SmoothStep(0f, 1f, botaoT / 0.25f) : 1f - Mathf.SmoothStep(0f, 1f, (botaoT - 0.25f) / 0.75f);
        botao.localPosition = botaoRepouso + cursoBotao * k;
    }

    void Mostrar(bool sim)
    {
        if (fluxo != null) fluxo.GetComponent<Renderer>().enabled = sim;
        bool impacto = sim && AguaNaCuba;
        if (respingos != null)
        {
            var e = respingos.emission;
            if (e.enabled != impacto) { e.enabled = impacto; if (impacto && !respingos.isPlaying) respingos.Play(); }
        }
        if (espuma != null)
        {
            espuma.enabled = impacto;
            if (impacto)
            {
                espuma.transform.Rotate(0f, 140f * Time.deltaTime, 0f, Space.Self);
                if (materialEspuma != null)
                {
                    Color c = materialEspuma.GetColor("_BaseColor");
                    c.a = 0.35f + 0.1f * Mathf.Sin(Time.time * 23f);
                    materialEspuma.SetColor("_BaseColor", c);
                }
            }
        }
    }

    // Tubo de Lados lados entre os comprimentos a e b da trajetória; afina com a queda, ondula de leve embaixo.
    void Construir(float a, float b)
    {
        if (malha == null || b - a < 0.001f) { malha?.Clear(); return; }
        int aneis = Mathf.Clamp(Mathf.CeilToInt((b - a) / 0.01f) + 1, 2, 64);
        var v = new Vector3[aneis * (Lados + 1)];
        var n = new Vector3[v.Length];
        var uv = new Vector2[v.Length];
        var t = new int[(aneis - 1) * Lados * 6];
        Transform tf = fluxo.transform;
        for (int i = 0; i < aneis; i++)
        {
            float s = Mathf.Lerp(a, b, i / (aneis - 1f));
            Vector3 p = PontoEm(s, out Vector3 dir);
            float f = comprimento > 0f ? s / comprimento : 0f;
            float raio = Mathf.Lerp(raioNoBico, raioNoImpacto, Mathf.Sqrt(f));
            // Ondulação: só no trecho de baixo, onde a coluna real começa a se desfazer.
            p += new Vector3(Mathf.Sin(Time.time * 37f + s * 90f), 0f, Mathf.Cos(Time.time * 29f + s * 70f)) * (0.0008f * f * f);
            Vector3 lado = Vector3.Cross(dir, Vector3.right).sqrMagnitude > 1e-4f ? Vector3.Cross(dir, Vector3.right).normalized : Vector3.forward;
            Vector3 outro = Vector3.Cross(dir, lado);
            for (int k = 0; k <= Lados; k++)
            {
                float ang = k / (float)Lados * Mathf.PI * 2f;
                Vector3 radial = lado * Mathf.Cos(ang) + outro * Mathf.Sin(ang);
                int idx = i * (Lados + 1) + k;
                v[idx] = tf.InverseTransformPoint(transform.TransformPoint(p + radial * raio));
                n[idx] = tf.InverseTransformDirection(transform.TransformDirection(radial));
                uv[idx] = new Vector2(k / (float)Lados, -s * 6f);
            }
        }
        int w = 0;
        for (int i = 0; i < aneis - 1; i++)
            for (int k = 0; k < Lados; k++)
            {
                int i0 = i * (Lados + 1) + k, i1 = i0 + 1, j0 = i0 + Lados + 1, j1 = j0 + 1;
                t[w++] = i0; t[w++] = j0; t[w++] = i1;
                t[w++] = i1; t[w++] = j0; t[w++] = j1;
            }
        malha.Clear();
        malha.vertices = v;
        malha.normals = n;
        malha.uv = uv;
        malha.triangles = t;
        malha.RecalculateBounds();
    }

    Vector3 PontoEm(float s, out Vector3 dir)
    {
        int i = 1;
        while (i < acumulado.Length - 1 && acumulado[i] < s) i++;
        float seg = acumulado[i] - acumulado[i - 1];
        float u = seg > 0f ? Mathf.Clamp01((s - acumulado[i - 1]) / seg) : 0f;
        dir = (trajetoria[i] - trajetoria[i - 1]).normalized;
        return Vector3.Lerp(trajetoria[i - 1], trajetoria[i], u);
    }

    void OnDrawGizmosSelected()
    {
        if (trajetoria == null) return;
        Gizmos.color = new Color(0.4f, 0.7f, 1f);
        for (int i = 1; i < trajetoria.Length; i++)
            Gizmos.DrawLine(transform.TransformPoint(trajetoria[i - 1]), transform.TransformPoint(trajetoria[i]));
    }
}
