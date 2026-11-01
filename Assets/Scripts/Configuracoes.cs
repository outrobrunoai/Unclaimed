using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Opções do jogador (vídeo, áudio e controles), salvas em PlayerPrefs e aplicadas sozinhas ao iniciar o jogo.
/// A tela de opções (MenuOpcoes) só muda estes valores; quem depende deles lê daqui (o Movimento lê sensibilidade,
/// inverter Y e campo de visão) e pode ouvir Alterado para reaplicar na hora.
///
/// Brilho: um Volume global próprio (DontDestroyOnLoad, prioridade alta) com Color Adjustments > Post Exposure.
/// Só aparece em câmeras com Post Processing ligado; o Movimento liga na câmera do jogador.
/// </summary>
public static class Configuracoes
{
    public static readonly int[] LimitesDeFps = { 30, 60, 120, 144, 0 };   // 0 = sem limite

    public const float BrilhoPadrao = 0.5f, VolumePadrao = 0.8f, SensibilidadePadrao = 1f;
    public const float CampoDeVisaoPadrao = 60f, CampoDeVisaoMin = 50f, CampoDeVisaoMax = 90f;
    public const float SensibilidadeMin = 0.2f, SensibilidadeMax = 3f;
    public const float BalancoPadrao = 1f;
    // Brilho 0..1 vira -ExposicaoMaxima..+ExposicaoMaxima stops (0,5 = neutro).
    const float ExposicaoMaxima = 1.5f;

    /// <summary>Disparado depois de qualquer mudança aplicada.</summary>
    public static event Action Alterado;

    // --- vídeo ---
    public static int ResolucaoLargura { get; private set; }
    public static int ResolucaoAltura { get; private set; }
    public static FullScreenMode ModoDeTela { get; private set; }
    public static int Qualidade { get; private set; }
    public static bool VSync { get; private set; }
    public static int LimiteDeFps { get; private set; }
    public static float Brilho { get; private set; }
    // --- áudio ---
    public static float VolumeGeral { get; private set; }
    public static bool SomEmSegundoPlano { get; private set; }
    // --- controles ---
    public static float Sensibilidade { get; private set; }
    public static bool InverterY { get; private set; }
    public static float CampoDeVisao { get; private set; }
    /// <summary>Balanço da câmera ao andar: 0 = desligado, 1 = o configurado no Movimento.</summary>
    public static float IntensidadeBalanco { get; private set; } = BalancoPadrao;

    static bool carregado;
    static bool focado = true;
    static Volume volumeBrilho;
    static ColorAdjustments ajusteBrilho;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar()
    {
        carregado = false;
        focado = true;
        volumeBrilho = null;
        ajusteBrilho = null;
        Alterado = null;
        Application.focusChanged -= AoMudarFoco;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AoIniciarJogo()
    {
        Carregar();
        Application.focusChanged += AoMudarFoco;
        AplicarTudo();
    }

    /// <summary>Lê os valores salvos (ou os padrões). Seguro chamar várias vezes.</summary>
    public static void Carregar()
    {
        if (carregado) return;
        carregado = true;

        Resolution nativa = Screen.currentResolution;
        ResolucaoLargura = PlayerPrefs.GetInt("opc.largura", nativa.width);
        ResolucaoAltura = PlayerPrefs.GetInt("opc.altura", nativa.height);
        ModoDeTela = (FullScreenMode)PlayerPrefs.GetInt("opc.modoTela", (int)Screen.fullScreenMode);
        Qualidade = Mathf.Clamp(PlayerPrefs.GetInt("opc.qualidade", QualitySettings.GetQualityLevel()), 0, QualitySettings.names.Length - 1);
        VSync = PlayerPrefs.GetInt("opc.vsync", 1) == 1;
        LimiteDeFps = PlayerPrefs.GetInt("opc.fps", 0);
        Brilho = PlayerPrefs.GetFloat("opc.brilho", BrilhoPadrao);
        VolumeGeral = PlayerPrefs.GetFloat("opc.volume", VolumePadrao);
        SomEmSegundoPlano = PlayerPrefs.GetInt("opc.somFundo", 1) == 1;
        Sensibilidade = PlayerPrefs.GetFloat("opc.sensibilidade", SensibilidadePadrao);
        InverterY = PlayerPrefs.GetInt("opc.inverterY", 0) == 1;
        CampoDeVisao = PlayerPrefs.GetFloat("opc.fov", CampoDeVisaoPadrao);
        IntensidadeBalanco = PlayerPrefs.GetFloat("opc.balanco", BalancoPadrao);
    }

    public static void Salvar()
    {
        PlayerPrefs.SetInt("opc.largura", ResolucaoLargura);
        PlayerPrefs.SetInt("opc.altura", ResolucaoAltura);
        PlayerPrefs.SetInt("opc.modoTela", (int)ModoDeTela);
        PlayerPrefs.SetInt("opc.qualidade", Qualidade);
        PlayerPrefs.SetInt("opc.vsync", VSync ? 1 : 0);
        PlayerPrefs.SetInt("opc.fps", LimiteDeFps);
        PlayerPrefs.SetFloat("opc.brilho", Brilho);
        PlayerPrefs.SetFloat("opc.volume", VolumeGeral);
        PlayerPrefs.SetInt("opc.somFundo", SomEmSegundoPlano ? 1 : 0);
        PlayerPrefs.SetFloat("opc.sensibilidade", Sensibilidade);
        PlayerPrefs.SetInt("opc.inverterY", InverterY ? 1 : 0);
        PlayerPrefs.SetFloat("opc.fov", CampoDeVisao);
        PlayerPrefs.SetFloat("opc.balanco", IntensidadeBalanco);
        PlayerPrefs.Save();
    }

    /// <summary>Volta tudo ao padrão (resolução nativa, tela cheia sem borda) e aplica.</summary>
    public static void RestaurarPadroes()
    {
        Resolution nativa = Screen.currentResolution;
        ResolucaoLargura = nativa.width;
        ResolucaoAltura = nativa.height;
        ModoDeTela = FullScreenMode.FullScreenWindow;
        Qualidade = QualitySettings.names.Length - 1;
        VSync = true;
        LimiteDeFps = 0;
        Brilho = BrilhoPadrao;
        VolumeGeral = VolumePadrao;
        SomEmSegundoPlano = true;
        Sensibilidade = SensibilidadePadrao;
        InverterY = false;
        CampoDeVisao = CampoDeVisaoPadrao;
        IntensidadeBalanco = BalancoPadrao;
        AplicarTudo();
    }

    // --- alteração (cada uma aplica na hora) ---

    public static void DefinirResolucao(int largura, int altura)
    {
        ResolucaoLargura = largura; ResolucaoAltura = altura;
        AplicarTela();
        Avisar();
    }

    public static void DefinirModoDeTela(FullScreenMode modo)
    {
        ModoDeTela = modo;
        AplicarTela();
        Avisar();
    }

    public static void DefinirQualidade(int nivel)
    {
        Qualidade = Mathf.Clamp(nivel, 0, QualitySettings.names.Length - 1);
        QualitySettings.SetQualityLevel(Qualidade, true);
        AplicarTaxaDeQuadros();   // trocar de nível restaura o vSyncCount do nível
        Avisar();
    }

    public static void DefinirVSync(bool ligado) { VSync = ligado; AplicarTaxaDeQuadros(); Avisar(); }
    public static void DefinirLimiteDeFps(int fps) { LimiteDeFps = fps; AplicarTaxaDeQuadros(); Avisar(); }
    public static void DefinirBrilho(float valor) { Brilho = Mathf.Clamp01(valor); AplicarBrilho(); Avisar(); }
    public static void DefinirVolumeGeral(float valor) { VolumeGeral = Mathf.Clamp01(valor); AplicarAudio(); Avisar(); }
    public static void DefinirSomEmSegundoPlano(bool ligado) { SomEmSegundoPlano = ligado; AplicarAudio(); Avisar(); }
    public static void DefinirSensibilidade(float valor) { Sensibilidade = Mathf.Clamp(valor, SensibilidadeMin, SensibilidadeMax); Avisar(); }
    public static void DefinirInverterY(bool ligado) { InverterY = ligado; Avisar(); }
    public static void DefinirCampoDeVisao(float graus) { CampoDeVisao = Mathf.Clamp(graus, CampoDeVisaoMin, CampoDeVisaoMax); Avisar(); }
    public static void DefinirIntensidadeBalanco(float valor) { IntensidadeBalanco = Mathf.Clamp01(valor); Avisar(); }

    /// <summary>Resoluções do monitor sem repetir largura x altura (taxas diferentes), da menor para a maior.</summary>
    public static List<Vector2Int> ResolucoesDisponiveis()
    {
        var lista = new List<Vector2Int>();
        foreach (Resolution r in Screen.resolutions)
        {
            var tamanho = new Vector2Int(r.width, r.height);
            if (!lista.Contains(tamanho)) lista.Add(tamanho);
        }
        var atual = new Vector2Int(ResolucaoLargura, ResolucaoAltura);
        if (!lista.Contains(atual)) lista.Add(atual);
        lista.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        return lista;
    }

    // --- aplicação ---

    static void AplicarTudo()
    {
        AplicarTela();
        if (QualitySettings.GetQualityLevel() != Qualidade) QualitySettings.SetQualityLevel(Qualidade, true);
        AplicarTaxaDeQuadros();
        AplicarBrilho();
        AplicarAudio();
        Avisar();
    }

    static void AplicarTela()
    {
        // No Editor a janela do Game não muda de tamanho; só vale no jogo compilado.
        if (Application.isEditor) return;
        Screen.SetResolution(ResolucaoLargura, ResolucaoAltura, ModoDeTela);
    }

    static void AplicarTaxaDeQuadros()
    {
        QualitySettings.vSyncCount = VSync ? 1 : 0;
        // Com VSync ligado o limite é ignorado pela Unity; o monitor dita a taxa.
        Application.targetFrameRate = LimiteDeFps > 0 ? LimiteDeFps : -1;
    }

    static void AplicarAudio()
    {
        AudioListener.volume = focado || SomEmSegundoPlano ? VolumeGeral : 0f;
    }

    static void AoMudarFoco(bool temFoco)
    {
        focado = temFoco;
        AplicarAudio();
    }

    static void AplicarBrilho()
    {
        if (volumeBrilho == null)
        {
            var go = new GameObject("Brilho (Configuracoes)");
            UnityEngine.Object.DontDestroyOnLoad(go);
            volumeBrilho = go.AddComponent<Volume>();
            volumeBrilho.isGlobal = true;
            volumeBrilho.priority = 1000f;
            var perfil = ScriptableObject.CreateInstance<VolumeProfile>();
            perfil.name = "Brilho (runtime)";
            ajusteBrilho = perfil.Add<ColorAdjustments>(true);
            volumeBrilho.sharedProfile = perfil;
        }
        ajusteBrilho.postExposure.overrideState = true;
        ajusteBrilho.postExposure.value = (Brilho - 0.5f) * 2f * ExposicaoMaxima;
    }

    static void Avisar() => Alterado?.Invoke();
}
