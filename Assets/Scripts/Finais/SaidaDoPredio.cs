using UnityEngine;

/// <summary>
/// Saída do prédio (porta da recepção e portão do pátio). Mirar e apertar E pergunta se o jogador quer ir
/// embora; confirmando, mostra o epílogo. Durante a perseguição não pergunta nada: chegar perto já é a fuga.
///
/// A entrada principal recebe "fatoQueBloqueia" = "corrente": depois que Daniel passa a corrente (S5) ela não
/// abre, e o jogador precisa sair pelo pátio.
/// Prefab de José; Bruno coloca nas duas saídas.
/// </summary>
public class SaidaDoPredio : InterfaceDeTela
{
    public string tecla = "E";
    [Min(0.2f)] public float alcance = 2f;
    public string textoSair = "Ir embora";
    [Tooltip("Enquanto este fato existir, a saída está bloqueada (ex.: corrente).")]
    public string fatoQueBloqueia = "";
    public string textoBloqueada = "Uma corrente prende a porta.";
    [Tooltip("Na caça, chegar a esta distância já conta como fuga.")]
    [Min(0.5f)] public float raioDeFuga = 2.5f;

    const float DuracaoVinheta = 0.35f;
    const float AtrasoFrase = 0.15f;
    const float DuracaoFrase = 0.6f;
    const float AtrasoEscolhas = AtrasoFrase + 0.45f;
    const float DuracaoEscolhas = 0.4f;
    const float DuracaoHover = 0.15f;

    Epilogo.Textos textos;
    string frase;
    float abertaEm;
    float ultimoRepaint;
    float hoverVoltar;
    float hoverIr;
    Texture2D vinheta;

    public override float AlcanceDeInteracao => alcance;
    public override string Tecla => tecla;
    public bool Bloqueada => !string.IsNullOrEmpty(fatoQueBloqueia) && EstadoDaNoite.Tem(fatoQueBloqueia);
    public override string Indicacao => textoSair;

    public override void Interagir()
    {
        if (IntroChegada.Ativa) return;
        if (Bloqueada) { MiraDeInteracao.Avisar(textoBloqueada, 2f); return; }
        AbrirInterface();
    }

    protected override void AoAbrir()
    {
        textos = Epilogo.CarregarTextos();
        frase = Epilogo.FraseDeSaida(EstadoDaNoite.Tem, textos);
        abertaEm = Time.unscaledTime;
        ultimoRepaint = abertaEm;
        hoverVoltar = 0f;
        hoverIr = 0f;
        GarantirVinheta();
    }

    void OnDestroy()
    {
        if (vinheta != null)
        {
            Destroy(vinheta);
            vinheta = null;
        }
    }

    protected override void Update()
    {
        base.Update();
        if (IntroChegada.Ativa || EstadoDaNoite.Fase != FaseDeDaniel.Caca || Bloqueada || TelaDeFinal.Ativa) return;
        if (EstadoDaNoite.Tem("gravador_entregue")) return;
        if ((Jogador.Posicao - transform.position).sqrMagnitude <= raioDeFuga * raioDeFuga) Sair();
    }

    public void Sair()
    {
        if (EstaAberta) FecharInterface();
        EstadoDaNoite.Marcar("saiu_do_predio");
        TelaDeFinal.MostrarEpilogo();
    }

    protected override void Desenhar()
    {
        useGUILayout = false;
        if (textos == null)
        {
            textos = Epilogo.CarregarTextos();
            frase = Epilogo.FraseDeSaida(EstadoDaNoite.Tem, textos);
        }
        GarantirVinheta();

        float agora = Time.unscaledTime;
        float t = agora - abertaEm;
        float s = DesenhoUi.S;

        float alfaVinheta = Suave(t / DuracaoVinheta);
        Rect tela = new Rect(0f, 0f, Screen.width, Screen.height);
        DesenhoUi.Caixa(tela, new Color(0f, 0f, 0f, 0.25f * alfaVinheta));
        Color corAntes = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alfaVinheta);
        GUI.DrawTexture(tela, vinheta, ScaleMode.StretchToFill);
        GUI.color = corAntes;

        float pFrase = Suave((t - AtrasoFrase) / DuracaoFrase);
        float larguraFrase = Mathf.Min(920f * s, Screen.width - 120f * s);
        float yBaseFrase = Screen.height * 0.72f;
        float alturaFrase = 38f * s;

        if (pFrase > 0.001f)
        {
            GUIStyle estiloFrase = DesenhoUi.EstiloMaquina(27f, new Color(1f, 1f, 1f, pFrase), TextAnchor.MiddleCenter);
            GUIStyle estiloSombra = DesenhoUi.EstiloMaquina(27f, new Color(0f, 0f, 0f, 0.55f * pFrase), TextAnchor.MiddleCenter);

            alturaFrase = estiloFrase.CalcHeight(new GUIContent(frase), larguraFrase);
            float xFrase = (Screen.width - larguraFrase) * 0.5f;
            float yFrase = yBaseFrase + (1f - pFrase) * 14f * s;
            GUI.Label(new Rect(xFrase, yFrase + 2f * s, larguraFrase, alturaFrase), frase, estiloSombra);
            GUI.Label(new Rect(xFrase, yFrase, larguraFrase, alturaFrase), frase, estiloFrase);
        }

        float pEscolhas = Suave((t - AtrasoEscolhas) / DuracaoEscolhas);
        if (pEscolhas <= 0.001f) return;

        GUIStyle estiloMedida = DesenhoUi.EstiloMaquina(22f, Color.white, TextAnchor.MiddleCenter);
        Vector2 tamVoltar = estiloMedida.CalcSize(new GUIContent(textos.saidaVoltar));
        Vector2 tamIr = estiloMedida.CalcSize(new GUIContent(textos.saidaIr));
        float padX = 16f * s;
        float alturaEscolha = Mathf.Max(36f * s, Mathf.Max(tamVoltar.y, tamIr.y) + 14f * s);
        float wVoltar = tamVoltar.x + padX * 2f;
        float wIr = tamIr.x + padX * 2f;
        float espaco = 140f * s;
        float totalW = wVoltar + espaco + wIr;
        float xInicio = (Screen.width - totalW) * 0.5f;
        float yEscolhas = yBaseFrase + alturaFrase + 30f * s + (1f - pEscolhas) * 8f * s;

        Rect rVoltar = new Rect(xInicio, yEscolhas, wVoltar, alturaEscolha);
        Rect rIr = new Rect(xInicio + wVoltar + espaco, yEscolhas, wIr, alturaEscolha);

        if (Event.current.type == EventType.Repaint)
        {
            float dt = Mathf.Clamp(agora - ultimoRepaint, 0f, 0.1f);
            ultimoRepaint = agora;
            bool sobreVoltar = pEscolhas > 0.1f && rVoltar.Contains(Event.current.mousePosition);
            bool sobreIr = pEscolhas > 0.1f && rIr.Contains(Event.current.mousePosition);
            hoverVoltar = Mathf.MoveTowards(hoverVoltar, sobreVoltar ? 1f : 0f, dt / DuracaoHover);
            hoverIr = Mathf.MoveTowards(hoverIr, sobreIr ? 1f : 0f, dt / DuracaoHover);
        }

        if (DesenharEscolha(rVoltar, tamVoltar.x, textos.saidaVoltar, pEscolhas, hoverVoltar, s)) FecharInterface();
        if (DesenharEscolha(rIr, tamIr.x, textos.saidaIr, pEscolhas, hoverIr, s)) Sair();
    }

    static bool DesenharEscolha(Rect r, float larguraTexto, string texto, float alfa, float hover, float s)
    {
        Color cor = Color.Lerp(new Color(0.74f, 0.74f, 0.72f, alfa), new Color(1f, 1f, 1f, alfa), hover);
        GUI.Label(new Rect(r.x, r.y + 1.5f * s, r.width, r.height), texto,
            DesenhoUi.EstiloMaquina(22f, new Color(0f, 0f, 0f, 0.5f * alfa), TextAnchor.MiddleCenter));
        GUI.Label(r, texto, DesenhoUi.EstiloMaquina(22f, cor, TextAnchor.MiddleCenter));

        if (hover > 0.001f)
        {
            float wTraco = larguraTexto * Suave(hover);
            float xTraco = r.center.x - wTraco * 0.5f;
            float yTraco = r.yMax - 4f * s;
            DesenhoUi.Caixa(new Rect(xTraco, yTraco, wTraco, Mathf.Max(1f, 1.5f * s)),
                new Color(1f, 1f, 1f, 0.85f * alfa * hover));
        }

        return alfa > 0.1f && GUI.Button(r, GUIContent.none, GUIStyle.none);
    }

    void GarantirVinheta()
    {
        if (vinheta != null) return;
        const int tam = 128;
        vinheta = new Texture2D(tam, tam, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        var pixels = new Color[tam * tam];
        for (int y = 0; y < tam; y++)
        {
            float v = (y / (float)(tam - 1)) * 2f - 1f;
            for (int x = 0; x < tam; x++)
            {
                float u = (x / (float)(tam - 1)) * 2f - 1f;
                float d = Mathf.Sqrt(u * u + v * v);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1.25f, d)) * 0.8f;
                pixels[y * tam + x] = new Color(0f, 0f, 0f, a);
            }
        }
        vinheta.SetPixels(pixels);
        vinheta.Apply();
    }

    static float Suave(float x)
    {
        float c = Mathf.Clamp01(x);
        return 1f - (1f - c) * (1f - c) * (1f - c);
    }
}
