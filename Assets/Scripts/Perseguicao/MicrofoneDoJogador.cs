using UnityEngine;

/// <summary>
/// Ouve o microfone durante a caça (quando Daniel não vê o jogador ou ele está escondido).
/// Qualquer barulho alto acima do ruído de fundo (não é reconhecimento de fala) atrai Daniel (DanielPerseguidor.OuvirBarulho).
/// Nada é gravado em disco nem enviado; se instala sozinho no Play.
/// </summary>
public class MicrofoneDoJogador : MonoBehaviour
{
    const int Frequencia = 16000;
    const int Janela = 512;
    const float PisoMinimoDb = -80f;
    const float PisoInicialDb = -50f;
    const float MinimoAbsolutoDb = -42f;
    const float MeiaVidaDescer = 0.3f;
    const float MeiaVidaSubir = 4f;
    const float TempoCalibracao = 1.5f;
    const float TempoAcimaParaDisparar = 0.15f;
    const float TempoRecarga = 1.5f;
    const float TempoParaDesligar = 2f;
    const float DuracaoAlertaBarra = 0.6f;

    public static MicrofoneDoJogador Instancia { get; private set; }
    public static bool Ouvindo { get; private set; }
    public static float Nivel01 { get; private set; }

    AudioClip gravacao;
    float[] buffer;
    float pisoDb = PisoInicialDb;
    float calibrarAte;
    float tempoAcima;
    float recargaAte;
    float tempoSemCondicao;
    float alertaAte = -999f;
    float tentarLigarEm;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar()
    {
        Instancia = null;
        Ouvindo = false;
        Nivel01 = 0f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Instalar()
    {
        if (Instancia != null || FindAnyObjectByType<MicrofoneDoJogador>() != null) return;
        var go = new GameObject("Microfone do jogador");
        DontDestroyOnLoad(go);
        go.AddComponent<MicrofoneDoJogador>();
    }

    void Awake()
    {
        if (Instancia != null && Instancia != this) { Destroy(this); return; }
        Instancia = this;
    }

    void OnDisable() => Desligar();
    void OnApplicationQuit() => Desligar();

    void OnDestroy()
    {
        Desligar();
        if (Instancia == this)
        {
            Instancia = null;
            Ouvindo = false;
            Nivel01 = 0f;
        }
    }

    void Update()
    {
        bool deve = DeveEscutar();
        if (!deve)
        {
            Ouvindo = false;
            Nivel01 = 0f;
            tempoAcima = 0f;
            if (gravacao != null)
            {
                tempoSemCondicao += Time.unscaledDeltaTime;
                if (tempoSemCondicao >= TempoParaDesligar) Desligar();
            }
            return;
        }

        tempoSemCondicao = 0f;
        if (gravacao == null)
        {
            if (Time.unscaledTime < tentarLigarEm) return;
            if (!Ligar())
            {
                tentarLigarEm = Time.unscaledTime + 2f;
                return;
            }
        }

        Ouvindo = true;
        ProcessarAmostras();
    }

    static bool DeveEscutar()
    {
        if (!Configuracoes.UsarMicrofone || EstadoDaNoite.Fase != FaseDeDaniel.Caca) return false;
        if (TelaDeFinal.Ativa || MenuPausa.Pausado) return false;
        DanielPerseguidor daniel = DanielPerseguidor.Instancia;
        if (daniel == null) return false;
        DanielPerseguidor.Estado estado = daniel.Atual;
        if (estado == DanielPerseguidor.Estado.Inativo || estado == DanielPerseguidor.Estado.Parado || estado == DanielPerseguidor.Estado.Saindo)
            return false;
        if (!Esconderijo.JogadorEscondido && Time.time - daniel.UltimaVezQueViu <= 1f) return false;
        return Microphone.devices.Length > 0;
    }

    bool Ligar()
    {
        gravacao = Microphone.Start(null, true, 1, Frequencia);
        if (gravacao == null) return false;
        int total = gravacao.samples * gravacao.channels;
        if (total <= 0)
        {
            Desligar();
            return false;
        }
        if (buffer == null || buffer.Length != total) buffer = new float[total];
        pisoDb = PisoInicialDb;
        calibrarAte = Time.time + TempoCalibracao;
        tempoAcima = 0f;
        return true;
    }

    void Desligar()
    {
        if (gravacao != null || Microphone.IsRecording(null))
            Microphone.End(null);
        gravacao = null;
        Ouvindo = false;
        Nivel01 = 0f;
        tempoAcima = 0f;
        tempoSemCondicao = 0f;
    }

    void ProcessarAmostras()
    {
        int pos = Microphone.GetPosition(null);
        if (pos <= 0 || gravacao.samples <= 0) { Nivel01 = 0f; return; }

        int canais = Mathf.Max(1, gravacao.channels);
        int total = gravacao.samples * canais;
        if (buffer == null || buffer.Length != total) buffer = new float[total];
        if (!gravacao.GetData(buffer, 0)) { Nivel01 = 0f; return; }

        int quantidade = Mathf.Min(Janela * canais, total);
        int inicio = pos * canais - quantidade;
        float nivelDb = NivelDb(buffer, inicio, quantidade);

        float dt = Time.deltaTime;
        if (dt > 0f)
        {
            // Desce rápido no silêncio e sobe devagar no som para um barulho curto não virar piso.
            float meiaVida = nivelDb < pisoDb ? MeiaVidaDescer : MeiaVidaSubir;
            float fator = 1f - Mathf.Pow(0.5f, dt / meiaVida);
            pisoDb = Mathf.Lerp(pisoDb, nivelDb, fator);
        }

        float margem = Mathf.Lerp(28f, 10f, Configuracoes.SensibilidadeMicrofone);
        float limiar = Mathf.Max(pisoDb + margem, MinimoAbsolutoDb);
        float faixa = Mathf.Max(1f, limiar - pisoDb);
        Nivel01 = Mathf.Clamp01((nivelDb - pisoDb) / faixa);

        if (Time.time < calibrarAte)
        {
            tempoAcima = 0f;
            return;
        }

        if (AcimaDoLimiar(nivelDb, pisoDb, margem, MinimoAbsolutoDb))
        {
            tempoAcima += dt;
            if (tempoAcima >= TempoAcimaParaDisparar && Time.time >= recargaAte)
            {
                tempoAcima = 0f;
                recargaAte = Time.time + TempoRecarga;
                alertaAte = Time.time + DuracaoAlertaBarra;
                DanielPerseguidor daniel = DanielPerseguidor.Instancia;
                if (daniel != null) daniel.OuvirBarulho(Jogador.Posicao);
            }
        }
        else
        {
            tempoAcima = 0f;
        }
    }

    void OnGUI()
    {
        if (!Ouvindo || TelaDeFinal.Ativa || MenuPausa.Pausado) return;
        useGUILayout = false;
        // Desenha por cima da fresta escura do esconderijo.
        GUI.depth = -1;
        float s = DesenhoUi.S;
        float margem = 28f * s;
        float largura = 150f * s;
        float alturaBarra = 6f * s;
        float yBarra = Screen.height - margem - alturaBarra;
        bool alerta = Time.time < alertaAte;

        Color corTexto = alerta ? new Color(0.95f, 0.25f, 0.2f, 0.9f) : new Color(1f, 1f, 1f, 0.55f);
        GUI.Label(new Rect(margem, yBarra - 24f * s, largura, 22f * s), "Microfone",
            DesenhoUi.Estilo(15, corTexto, false, TextAnchor.LowerLeft));

        Color corFundo = alerta ? new Color(0.35f, 0.05f, 0.05f, 0.7f) : new Color(0f, 0f, 0f, 0.55f);
        DesenhoUi.Caixa(new Rect(margem, yBarra, largura, alturaBarra), corFundo);

        Color corBarra = alerta ? new Color(0.85f, 0.12f, 0.1f, 0.95f) : new Color(0.82f, 0.82f, 0.82f, 0.8f);
        float preenchido = largura * Nivel01;
        if (preenchido > 0.5f)
            DesenhoUi.Caixa(new Rect(margem, yBarra, preenchido, alturaBarra), corBarra);
    }

    /// <summary>Calcula o nível RMS em dBFS (mínimo -80) lendo circularmente a partir de inicio.</summary>
    public static float NivelDb(float[] amostras, int inicio, int quantidade)
    {
        if (amostras == null || amostras.Length == 0 || quantidade <= 0) return PisoMinimoDb;
        int n = amostras.Length;
        int indice = ((inicio % n) + n) % n;
        double soma = 0.0;
        for (int i = 0; i < quantidade; i++)
        {
            float v = amostras[indice];
            soma += v * v;
            indice++;
            if (indice >= n) indice = 0;
        }
        float rms = Mathf.Sqrt((float)(soma / quantidade));
        if (rms <= 1e-4f) return PisoMinimoDb;
        return Mathf.Max(PisoMinimoDb, 20f * Mathf.Log10(rms));
    }

    /// <summary>Verdadeiro quando o nível atinge ou passa o maior entre piso + margem e o mínimo absoluto.</summary>
    public static bool AcimaDoLimiar(float nivelDb, float pisoDb, float margemDb, float minimoDb)
    {
        return nivelDb >= Mathf.Max(pisoDb + margemDb, minimoDb);
    }
}
