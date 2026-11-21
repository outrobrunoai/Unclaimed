using System;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public sealed class ChuvaLeve : MonoBehaviour
{
    [Serializable]
    public struct Poca
    {
        public Vector2 centro;
        public Vector2 raios;
        public float rotacaoGraus;
    }

    [Range(0, 1), Tooltip("Quantidade de chuva.")] public float intensidade = 0.4f;
    [Min(0), Tooltip("Gotas por segundo com intensidade máxima.")] public float taxaMaxima = 2200;
    [Tooltip("Velocidade do vento nos eixos X e Z, em m/s.")] public Vector2 vento = new Vector2(0.25f, 0.08f);
    [Range(0, 1), Tooltip("Oscilação lenta do vento, em m/s.")] public float variacaoVento = 0.12f;
    [Range(0.001f, 0.03f), Tooltip("Largura da gota em metros.")] public float larguraGota = 0.007f;
    [Range(0.01f, 1), Tooltip("Comprimento do rastro em metros.")] public float comprimentoRastro = 0.28f;
    [Range(1, 15), Tooltip("Velocidade média de queda, em m/s.")] public float velocidadeQueda = 7;
    [Range(0, 3), Tooltip("Variação da velocidade de queda, em m/s.")] public float variacaoQueda = 0.8f;
    [Range(0, 1), Tooltip("Multiplicador dos respingos.")] public float respingos = 1;
    [Range(0, 1), Tooltip("Multiplicador das ondulações nas poças.")] public float ondulacoes = 1;
    [Range(0, 1), Tooltip("Umidade das superfícies.")] public float umidade = 0.65f;
    [Min(0), Tooltip("Impactos por m² por segundo com intensidade máxima.")] public float densidadeRespingos = 5;
    [Range(1, 15), Tooltip("Raio ao redor do jogador onde há respingos, em metros.")] public float raioRespingos = 7;
    [Min(0), Tooltip("Anéis por m² de poça por segundo com intensidade máxima.")] public float densidadeOndulacoes = 14;
    [Range(1, 40), Tooltip("Meia largura do volume de chuva, em metros.")] public float raioArea = 18;
    [Range(1, 30), Tooltip("Altura do volume de chuva, em metros.")] public float alturaArea = 11;
    [Tooltip("Base do volume de chuva, em metros.")] public float baseArea = -0.6f;
    [Min(0), Tooltip("Início do desaparecimento à distância, em metros.")] public float inicioFade = 12;
    [Min(0.1f), Tooltip("Fim do desaparecimento à distância, em metros.")] public float fimFade = 16;
    [Tooltip("Até oito elipses em coordenadas mundiais XZ.")] public List<Poca> pocas = new List<Poca>();
    [Tooltip("Sistema de gotas.")] public ParticleSystem gotas;
    // Separate names avoid the C# name collision with the public intensity multipliers.
    [Tooltip("Sistema de gotículas de impacto.")] public ParticleSystem particulasRespingos;
    [Tooltip("Sistema de anéis nas poças.")] public ParticleSystem particulasOndulacoes;
    [Tooltip("Mapa pré-calculado no editor.")] public MapaAbrigoChuva mapa;
    public AudioSource audioChuva;
    public AudioLowPassFilter filtroAudio;
    [Tooltip("Segue a câmera principal quando vazio.")] public Transform alvo;
    [Range(0, 1), Tooltip("Volume sob abrigo.")] public float volumeAbrigado = 0.35f;
    [Range(0, 1), Tooltip("Volume no exterior.")] public float volumeExterno = 0.8f;
    [Range(0.2f, 4), Tooltip("Faixa, a partir do plano da porta para dentro, em que o som passa de externo a abafado (m).")]
    public float faixaInterna = 1.6f;
    public AudioSource audioInterno;
    public AudioLowPassFilter filtroInterno;
    public float corteInterno = 1100f;
    [Range(0, 1)] public float volumeInterno = 0.4f;
    public List<PortalDeChuva> portais = new List<PortalDeChuva>();
    [Min(0.001f)] public float suavizacaoAudio = 0.08f;

    static readonly int ChuvaCentro = Shader.PropertyToID("_ChuvaCentro");
    readonly Vector4[] dadosPocas = new Vector4[8];
    readonly Vector4[] rotacoesPocas = new Vector4[8];
    float acumulador, acumuladorOndulacoes, proximoAudio, exposicaoAudio, alvoAudio, velocidadeAudio, velocidadeExterno, velocidadeInterno;
    readonly HashSet<AudioSource> fontesIniciadas = new HashSet<AudioSource>();
    bool preparada;
    Transform cameraAlvo;
    ParticleSystemRenderer rendererGotas;

    Transform ObterAlvo()
    {
        if (alvo) return alvo;
        if (!cameraAlvo && Camera.main) cameraAlvo = Camera.main.transform;
        return cameraAlvo;
    }

    void OnEnable()
    {
        preparada = false;
        rendererGotas = gotas ? gotas.GetComponent<ParticleSystemRenderer>() : null;
        Aplicar();
    }
    void OnDisable()
    {
        if (Application.isPlaying)
        {
            if (gotas) gotas.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (particulasRespingos) particulasRespingos.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (particulasOndulacoes) particulasOndulacoes.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (audioChuva) audioChuva.Stop();
            if (audioInterno) audioInterno.Stop();
            if (portais != null)
                foreach (var portal in portais) if (portal && portal.fonte) portal.fonte.Stop();
        }
        Shader.SetGlobalFloat("_ChuvaUmidade", 0);
        Shader.SetGlobalInt("_ChuvaPocasQtd", 0);
    }
    void OnValidate() { Aplicar(); }
    void Update()
    {
        AtualizarQuadro();
        if (!Application.isPlaying) return;
        Transform t = ObterAlvo();
        if (!t) return;
        EmitirImpactos(t.position);
        AtualizarAudio(t.position);
    }

    void LateUpdate()
    {
        Transform t = ObterAlvo();
        if (!t || !gotas) return;
        gotas.transform.position = new Vector3(t.position.x, baseArea + alturaArea * 0.5f, t.position.z);
        if (Application.isPlaying && !preparada)
        {
            // Simulate only in play mode, after placing the world-space emitter at the target.
            gotas.Simulate(alturaArea / Mathf.Max(1, velocidadeQueda), false, true, true);
            gotas.Play(false);
            preparada = true;
        }
    }

    void Aplicar()
    {
        Shader.SetGlobalTexture("_ChuvaAbrigoTex", mapa && mapa.textura ? mapa.textura : Texture2D.blackTexture);
        Shader.SetGlobalVector("_ChuvaAbrigoUV", mapa && mapa.textura ? mapa.ParametrosUV : Vector4.zero);
        Shader.SetGlobalVector("_ChuvaFade", new Vector4(inicioFade, Mathf.Max(inicioFade + 0.01f, fimFade), 0, 0));
        Shader.SetGlobalFloat("_ChuvaUmidade", umidade);
        int n = pocas == null ? 0 : Mathf.Min(8, pocas.Count);
        for (int i = 0; i < 8; i++)
        {
            if (i >= n) { dadosPocas[i] = rotacoesPocas[i] = Vector4.zero; continue; }
            Poca p = pocas[i];
            float a = p.rotacaoGraus * Mathf.Deg2Rad;
            dadosPocas[i] = new Vector4(p.centro.x, p.centro.y, Mathf.Max(0.01f, p.raios.x), Mathf.Max(0.01f, p.raios.y));
            rotacoesPocas[i] = new Vector4(Mathf.Cos(a), Mathf.Sin(a), 0, 0);
        }
        Shader.SetGlobalInt("_ChuvaPocasQtd", n);
        Shader.SetGlobalVectorArray("_ChuvaPocas", dadosPocas);
        Shader.SetGlobalVectorArray("_ChuvaPocasRot", rotacoesPocas);
        if (gotas)
        {
            var main = gotas.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0;
            main.startLifetime = Mathf.Max(0.1f, alturaArea / Mathf.Max(1, velocidadeQueda));
            main.startSize = new ParticleSystem.MinMaxCurve(larguraGota * 0.85f, larguraGota * 1.15f);
            var shape = gotas.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(raioArea * 2, alturaArea, raioArea * 2);
            var emission = gotas.emission;
            emission.enabled = true;
            emission.rateOverTime = Mathf.Max(0, taxaMaxima) * intensidade;
            var velocity = gotas.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            float variacao = Mathf.Min(variacaoQueda, velocidadeQueda * 0.9f);
            velocity.y = new ParticleSystem.MinMaxCurve(-velocidadeQueda - variacao, -velocidadeQueda + variacao);
            rendererGotas = gotas.GetComponent<ParticleSystemRenderer>();
            if (rendererGotas)
            {
                rendererGotas.renderMode = ParticleSystemRenderMode.Stretch;
                rendererGotas.lengthScale = 1;
                rendererGotas.cameraVelocityScale = 0;
            }
        }
        else
        {
            rendererGotas = null;
        }
        AtualizarQuadro();
    }

    // Só o que varia por quadro: posição do alvo e oscilação do vento nas gotas.
    void AtualizarQuadro()
    {
        Transform t = ObterAlvo();
        Shader.SetGlobalVector(ChuvaCentro, t ? t.position : transform.position);
        if (!gotas) return;
        float tempo = Application.isPlaying ? Time.time : 0;
        Vector2 v = vento + new Vector2(Mathf.PerlinNoise(tempo * 0.065f, 3.2f) * 2 - 1,
            Mathf.PerlinNoise(7.1f, tempo * 0.065f) * 2 - 1) * variacaoVento;
        var velocity = gotas.velocityOverLifetime;
        velocity.x = new ParticleSystem.MinMaxCurve(v.x - 0.12f, v.x + 0.12f);
        velocity.z = new ParticleSystem.MinMaxCurve(v.y - 0.12f, v.y + 0.12f);
        if (rendererGotas)
        {
            // Unity stretches by size * lengthScale + speed * velocityScale (world units, not sizes).
            rendererGotas.velocityScale = Mathf.Max(0, comprimentoRastro - larguraGota)
                / Mathf.Max(1, new Vector3(v.x, velocidadeQueda, v.y).magnitude);
        }
    }

    bool DentroPoca(Vector3 pos)
    {
        for (int i = 0; pocas != null && i < Mathf.Min(8, pocas.Count); i++)
        {
            Poca p = pocas[i];
            Vector2 d = new Vector2(pos.x, pos.z) - p.centro;
            float a = p.rotacaoGraus * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            Vector2 q = new Vector2(c * d.x + s * d.y, -s * d.x + c * d.y);
            q.x /= Mathf.Max(0.01f, p.raios.x); q.y /= Mathf.Max(0.01f, p.raios.y);
            if (q.sqrMagnitude < 0.72f * 0.72f) return true;
        }
        return false;
    }

    void EmitirImpactos(Vector3 centro)
    {
        if (!mapa) { acumulador = acumuladorOndulacoes = 0; return; }
        float raio = Mathf.Min(raioRespingos, raioArea);
        acumulador += Mathf.Max(0, densidadeRespingos) * Mathf.PI * raio * raio * intensidade * respingos * Time.deltaTime;
        int tentativas = Mathf.Min(256, Mathf.FloorToInt(acumulador));
        acumulador = Mathf.Min(acumulador - tentativas, 1);
        for (int i = 0; i < tentativas; i++)
        {
            Vector2 d = UnityEngine.Random.insideUnitCircle * raio;
            Vector3 p = new Vector3(centro.x + d.x, 0, centro.z + d.y);
            if (!mapa.Amostrar(p, out float y, out TipoSuperficie tipo) || tipo == TipoSuperficie.Outro
                || mapa.ExposicaoSuavizada(p) < 0.5f || DentroPoca(p)) continue;
            p.y = y + 0.01f;
            if (tipo == TipoSuperficie.Vegetacao && UnityEngine.Random.value > 0.2f) continue;
            int qtd = tipo == TipoSuperficie.Duro ? UnityEngine.Random.Range(2, 4) : 1;
            for (int j = 0; j < qtd; j++) EmitirGoticula(p,
                tipo == TipoSuperficie.Duro ? UnityEngine.Random.Range(0.012f, 0.022f) : 0.008f,
                tipo == TipoSuperficie.Terra ? new Color(0.4f, 0.35f, 0.3f, 0.8f) : Color.white);
        }
        EmitirOndulacoes(centro);
    }

    // Ripples are emitted per puddle area, so small puddles still read as hit by rain.
    void EmitirOndulacoes(Vector3 centro)
    {
        if (!particulasOndulacoes || pocas == null) return;
        int n = Mathf.Min(8, pocas.Count);
        for (int i = 0; i < n; i++)
        {
            Poca poca = pocas[i];
            if ((new Vector2(centro.x, centro.z) - poca.centro).sqrMagnitude > fimFade * fimFade) continue;
            float area = Mathf.PI * poca.raios.x * poca.raios.y;
            acumuladorOndulacoes += Mathf.Max(0, densidadeOndulacoes) * area * intensidade * ondulacoes * Time.deltaTime;
        }
        int total = Mathf.Min(64, Mathf.FloorToInt(acumuladorOndulacoes));
        acumuladorOndulacoes = Mathf.Min(acumuladorOndulacoes - total, 1);
        for (int k = 0; k < total; k++)
        {
            Poca poca = pocas[UnityEngine.Random.Range(0, n)];
            if ((new Vector2(centro.x, centro.z) - poca.centro).sqrMagnitude > fimFade * fimFade) continue;
            Vector2 q = UnityEngine.Random.insideUnitCircle * 0.7f;
            q.x *= poca.raios.x; q.y *= poca.raios.y;
            float a = poca.rotacaoGraus * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            Vector3 p = new Vector3(poca.centro.x + c * q.x - s * q.y, 0, poca.centro.y + s * q.x + c * q.y);
            if (!mapa.Amostrar(p, out float y, out _)) continue;
            p.y = y + 0.006f;
            if (!particulasOndulacoes.isPlaying) particulasOndulacoes.Play(false);
            var e = new ParticleSystem.EmitParams { position = p, velocity = Vector3.zero,
                startSize = UnityEngine.Random.Range(0.12f, 0.26f), startLifetime = UnityEngine.Random.Range(0.45f, 1.05f),
                startColor = Color.white, applyShapeToPosition = false };
            particulasOndulacoes.Emit(e, 1);
            if (UnityEngine.Random.value < 0.2f * respingos) EmitirGoticula(p, 0.01f, Color.white);
        }
    }

    void EmitirGoticula(Vector3 p, float tamanho, Color cor)
    {
        if (!particulasRespingos) return;
        if (!particulasRespingos.isPlaying) particulasRespingos.Play(false);
        Vector2 d = UnityEngine.Random.insideUnitCircle * 0.45f;
        var e = new ParticleSystem.EmitParams { position = p,
            velocity = new Vector3(d.x, UnityEngine.Random.Range(0.5f, 1.1f), d.y),
            startSize = tamanho, startLifetime = UnityEngine.Random.Range(0.18f, 0.3f),
            startColor = cor, applyShapeToPosition = false };
        particulasRespingos.Emit(e, 1);
    }

    void AtualizarAudio(Vector3 cabeca)
    {
        if (!audioChuva || !audioChuva.clip)
        {
            if (audioChuva) audioChuva.Stop();
            if (audioInterno) audioInterno.Stop();
            if (portais != null)
                foreach (var portal in portais) if (portal && portal.fonte) portal.fonte.Stop();
            return;
        }
        if (Time.time >= proximoAudio)
        {
            proximoAudio = Time.time + 0.2f;
            float soma = 0, pesos = 0;
            for (int i = 0; i < 17; i++)
            {
                float r = i == 0 ? 0 : i <= 8 ? 3 : 8;
                float a = (i - 1) * Mathf.PI * 0.25f;
                float peso = 1f / Mathf.Max(1, r);
                Vector3 p = cabeca + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                soma += (mapa ? mapa.Exposicao(p) : 0) * peso; pesos += peso;
            }
            alvoAudio = soma / pesos;
        }
        exposicaoAudio = Mathf.SmoothDamp(exposicaoAudio, alvoAudio, ref velocidadeAudio, 0.8f);
        float profundidade = mapa ? mapa.ProfundidadeInterna(cabeca) : 0;
        float dentro01 = Mathf.SmoothStep(0, 1, (profundidade - 0.15f) / Mathf.Max(0.01f, faixaInterna));
        // Near an entrance the side comes from its real plane, and the doorway only lets the outdoor
        // bed in as far as its leaves are open: behind shut glass the camera is fully indoors.
        if (portais != null)
            foreach (var portal in portais)
                if (portal && portal.isActiveAndEnabled && portal.Perto(cabeca, out bool ladoInterno))
                {
                    dentro01 = ladoInterno ? Mathf.Lerp(1, dentro01, portal.Abertura01) : 0;
                    break;
                }
        float volumeBase = volumeExterno * Mathf.Pow(Mathf.Clamp01(intensidade), 0.7f);
        audioChuva.loop = true; audioChuva.spatialBlend = 0; audioChuva.playOnAwake = false;
        if (filtroAudio) filtroAudio.cutoffFrequency = 22000f;
        IniciarAudio(audioChuva, 0);
        audioChuva.volume = Mathf.SmoothDamp(audioChuva.volume, volumeBase * Mathf.Sqrt(1 - dentro01),
            ref velocidadeExterno, suavizacaoAudio);
        if (audioInterno)
        {
            if (!audioInterno.clip) audioInterno.clip = audioChuva.clip;
            audioInterno.loop = true; audioInterno.spatialBlend = 0; audioInterno.playOnAwake = false;
            if (filtroInterno) filtroInterno.cutoffFrequency = corteInterno;
            IniciarAudio(audioInterno, 1f / 3f);
            float volume = volumeInterno * Mathf.Pow(Mathf.Clamp01(intensidade), 0.7f) * dentro01;
            volume *= Mathf.Lerp(0.85f, 1.15f, exposicaoAudio);
            audioInterno.volume = Mathf.SmoothDamp(audioInterno.volume, volume, ref velocidadeInterno, suavizacaoAudio);
        }
        if (portais == null) return;
        for (int i = 0; i < portais.Count; i++)
        {
            var portal = portais[i];
            if (!portal || !portal.isActiveAndEnabled || !portal.fonte) continue;
            if (!portal.fonte.clip) portal.fonte.clip = audioChuva.clip;
            IniciarAudio(portal.fonte, 2f / 3f + 0.11f * i);
            portal.Atualizar(cabeca, dentro01, volumeBase, suavizacaoAudio);
        }
    }

    void IniciarAudio(AudioSource fonte, float deslocamento)
    {
        if (!fontesIniciadas.Contains(fonte))
        {
            fonte.Stop();
            fonte.volume = 0;
            fontesIniciadas.Add(fonte);
        }
        else if (fonte.isPlaying) return;
        fonte.timeSamples = Mathf.Clamp((int)(Mathf.Repeat(deslocamento, 1f) * fonte.clip.samples), 0,
            Mathf.Max(0, fonte.clip.samples - 1));
        fonte.Play();
    }
}
