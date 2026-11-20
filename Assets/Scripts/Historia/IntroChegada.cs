using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Introdução jogável do Novo jogo, em primeira pessoa e sem cortes: o jogador começa na calçada vendo as
/// mensagens com Celso chegarem no celular e depois caminha pelos pontos até a recepção. Pode ser pulada
/// segurando E ou Espaço.
/// </summary>
public class IntroChegada : MonoBehaviour
{
    public const string Fato = "intro_vista";
    public static bool Ativa { get; private set; }

    internal static bool SilenciarAvisos { get; private set; }

    public Transform[] pontos;            // caminho; pose = position (jogador) + rotação Y do ponto. O último é o fim.
    public float velocidade = 1.2f;       // m/s na caminhada
    public string contato = "celso";
    public string[] mensagens;            // ids em celular.json, na ordem
    public float segundosAntes = 2f, segundosPorMensagem = 3f, segundosDepois = 2.5f;
    public FolhaDePorta[] portasParaAbrir; // abre cada uma quando a câmera chega a 2.5 m dela
    public float segurarParaPular = 3f;
    public float olharMaximo = 30f;       // graus de olhar livre com o mouse durante a caminhada
    public bool tocarNoEditor = true;     // false = não toca ao dar Play direto no Editor

    const float EsperaMaximaCena = 5f;
    const float DistanciaAbrirPorta = 2.5f;
    const float AmplitudeBalanco = 0.03f;
    const float PassosPorSegundo = 1.8f;
    const float EscalaMouseBase = 0.04f;  // equivale a sensibilidadeMouse (5) * PassoDeReferencia (0.008) do Movimento
    const float DuracaoDica = 4f;

    Movimento movimento;
    CharacterController controlador;
    Transform camJogador;
    Celular celular;
    Vector3 camPosBase;
    Coroutine rotina;
    bool executando, concluida;
    float tempoSegurado, mostrarDicaAte, faseBalanco;
    float puladaEm = -100f;               // quando foi pulada: a tela volta do preto a partir daí
    const float DuracaoVoltaDoPreto = 0.9f;
    Vector2 desvioOlhar;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar()
    {
        Ativa = false;
        SilenciarAvisos = false;
    }

#if UNITY_EDITOR
    public static void DefinirAtivaParaTeste(bool ativa) => Ativa = ativa;
#endif

    public static bool DeveTocar() => !EstadoDaNoite.Tem(Fato) && !Salvamento.VaiCarregar;

    void Start() => rotina = StartCoroutine(Executar());

    IEnumerator Executar()
    {
        if (!DeveTocar()) yield break;

        float espera = 0f;
        while ((movimento == null || celular == null) && espera < EsperaMaximaCena)
        {
            if (!DeveTocar()) yield break;
            if (movimento == null) movimento = FindFirstObjectByType<Movimento>();
            if (celular == null) celular = FindFirstObjectByType<Celular>();
            if (movimento != null && celular != null) break;
            espera += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!DeveTocar()) yield break;

        if (Application.isEditor && !tocarNoEditor)
        {
            EntregarMensagensRestantes();
            EstadoDaNoite.Marcar(Fato);
            yield break;
        }

        if (movimento == null || celular == null) yield break;

        var pontosValidos = ColetarPontosValidos();
        if (pontosValidos.Count == 0) yield break;

        Configuracoes.Carregar();
        Ativa = true;
        executando = true;
        concluida = false;
        tempoSegurado = 0f;
        desvioOlhar = Vector2.zero;
        faseBalanco = 0f;
        mostrarDicaAte = Time.unscaledTime + DuracaoDica;

        controlador = movimento.GetComponent<CharacterController>();
        camJogador = movimento.camera;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Transform inicio = pontosValidos[0];
        movimento.Teleportar(inicio.position, inicio.eulerAngles.y, 0f, false);
        if (camJogador != null) camPosBase = camJogador.localPosition;
        BloquearJogador(true);

        // Fase 1: celular na mão mostrando a conversa com Celso.
        celular.MostrarConversa(contato);
        yield return Esperar(segundosAntes);

        if (mensagens != null)
        {
            for (int i = 0; i < mensagens.Length && Ativa && executando; i++)
            {
                string id = mensagens[i];
                if (!string.IsNullOrEmpty(id))
                {
                    CelularLogica.Entregar(id);
                    if (celular != null) celular.lidas.Add(id);
                }
                if (i < mensagens.Length - 1) yield return Esperar(segundosPorMensagem);
            }
        }

        yield return Esperar(segundosDepois);
        if (!Ativa || !executando) yield break;

        if (celular != null && celular.EstaAberta)
        {
            celular.Guardar();
            float esperaGuardar = 0f;
            while (Ativa && executando && celular != null && celular.EstaAberta && esperaGuardar < 1.5f)
            {
                esperaGuardar += Time.unscaledDeltaTime;
                yield return null;
            }
        }
        if (!Ativa || !executando) yield break;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Fase 2: caminhada automática até a recepção, com olhar livre limitado pelo mouse.
        Vector3 pos = inicio.position;
        float yawBase = inicio.eulerAngles.y;

        for (int i = 1; i < pontosValidos.Count && Ativa && executando; i++)
        {
            Transform destino = pontosValidos[i];
            bool ultimo = i == pontosValidos.Count - 1;

            while (Ativa && executando)
            {
                if (destino == null) break;
                float dt = Time.deltaTime;
                if (dt <= 0f) { yield return null; continue; }

                Vector3 alvo = destino.position;
                Vector3 falta = alvo - pos;
                float dist = falta.magnitude;
                float passo = Mathf.Max(0.05f, velocidade) * dt;
                bool chegou = dist <= passo + 0.001f;
                pos = chegou ? alvo : pos + falta * (passo / dist);

                Vector3 dirH = new Vector3(falta.x, 0f, falta.z);
                float yawAlvo = dirH.sqrMagnitude > 0.0004f
                    ? Mathf.Atan2(dirH.x, dirH.z) * Mathf.Rad2Deg
                    : destino.eulerAngles.y;
                if (ultimo && dist < 1f)
                    yawAlvo = Mathf.LerpAngle(destino.eulerAngles.y, yawAlvo, dist);
                yawBase = Mathf.LerpAngle(yawBase, yawAlvo, 1f - Mathf.Exp(-4f * dt));

                AtualizarOlharLivre(dt);
                AtualizarBalanco(dt);

                movimento.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yawBase + desvioOlhar.x, 0f));
                if (camJogador != null) camJogador.localRotation = Quaternion.Euler(desvioOlhar.y, 0f, 0f);

                VerificarPortas(camJogador != null ? camJogador.position : pos);

                if (chegou) break;
                yield return null;
            }
        }

        if (Ativa && executando) Concluir();
    }

    IEnumerator Esperar(float segundos)
    {
        float decorrido = 0f;
        while (decorrido < segundos && Ativa && executando)
        {
            decorrido += Time.deltaTime;
            yield return null;
        }
    }

    void Update()
    {
        if (!Ativa || !executando) return;

        bool segurando = SegurandoPular(out bool algumaTecla);
        if (algumaTecla) mostrarDicaAte = Time.unscaledTime + DuracaoDica;

        if (segurando)
        {
            tempoSegurado += Time.unscaledDeltaTime;
            mostrarDicaAte = Time.unscaledTime + DuracaoDica;
            if (tempoSegurado >= Mathf.Max(0.01f, segurarParaPular)) { puladaEm = Time.unscaledTime; Concluir(); }
        }
        else
        {
            // Soltou antes do fim: o anel e o escurecimento voltam aos poucos, sem sumir de uma vez.
            tempoSegurado = Mathf.Max(0f, tempoSegurado - Time.unscaledDeltaTime * 2.5f);
        }
    }

    /// <summary>Encerra a introdução imediatamente, entregando o que faltar e devolvendo o controle ao jogador.</summary>
    public void Concluir()
    {
        if (concluida) return;
        concluida = true;
        executando = false;

        if (rotina != null)
        {
            StopCoroutine(rotina);
            rotina = null;
        }

        if (celular != null && celular.EstaAberta) celular.Guardar();

        EntregarMensagensRestantes();
        EstadoDaNoite.Marcar(Fato);

        Transform fim = UltimoPontoValido();
        if (movimento != null)
        {
            if (fim != null)
                movimento.Teleportar(fim.position, fim.eulerAngles.y, 0f, false);
            else if (camJogador != null)
            {
                camJogador.localPosition = camPosBase;
                camJogador.localRotation = Quaternion.identity;
            }
            BloquearJogador(false);
        }

        if (portasParaAbrir != null)
        {
            for (int i = 0; i < portasParaAbrir.Length; i++)
                AbrirPortaSeFechada(portasParaAbrir[i]);
        }

        tempoSegurado = 0f;
        Ativa = false;
    }

    void OnDisable() => LiberarSeAtiva();

    void OnDestroy() => LiberarSeAtiva();

    void LiberarSeAtiva()
    {
        if (rotina != null)
        {
            StopCoroutine(rotina);
            rotina = null;
        }
        if (!executando && !Ativa) return;
        executando = false;
        if (movimento != null)
        {
            if (camJogador != null)
            {
                camJogador.localPosition = camPosBase;
                camJogador.localRotation = Quaternion.identity;
            }
            BloquearJogador(false);
        }
        Ativa = false;
    }

    void BloquearJogador(bool bloquear)
    {
        if (movimento != null)
        {
            if (!bloquear) movimento.olhar = Vector2.zero;
            movimento.enabled = !bloquear;
        }
        if (controlador != null) controlador.enabled = !bloquear;
    }

    void EntregarMensagensRestantes()
    {
        if (mensagens == null) return;
        SilenciarAvisos = true;
        try
        {
            for (int i = 0; i < mensagens.Length; i++)
            {
                string id = mensagens[i];
                if (string.IsNullOrEmpty(id)) continue;
                CelularLogica.Entregar(id);
                if (celular != null) celular.lidas.Add(id);
            }
        }
        finally
        {
            SilenciarAvisos = false;
        }
    }

    void AtualizarOlharLivre(float dt)
    {
        Vector2 delta = DeltaMouse();
        float limite = Mathf.Max(0f, olharMaximo);
        if (delta.sqrMagnitude > 0.0001f && limite > 0f)
        {
            float sens = Configuracoes.Sensibilidade > 0f ? Configuracoes.Sensibilidade : Configuracoes.SensibilidadePadrao;
            float escala = EscalaMouseBase * sens;
            desvioOlhar.x = Mathf.Clamp(desvioOlhar.x + delta.x * escala, -limite, limite);
            float sinalY = Configuracoes.InverterY ? 1f : -1f;
            desvioOlhar.y = Mathf.Clamp(desvioOlhar.y + delta.y * escala * sinalY, -limite, limite);
        }
        else
        {
            desvioOlhar = Vector2.Lerp(desvioOlhar, Vector2.zero, 1f - Mathf.Exp(-2.2f * dt));
        }
    }

    void AtualizarBalanco(float dt)
    {
        if (camJogador == null) return;
        faseBalanco += dt * PassosPorSegundo * Mathf.PI;
        float intensidade = Configuracoes.IntensidadeBalanco;
        float dy = (Mathf.Abs(Mathf.Sin(faseBalanco)) - 0.5f) * AmplitudeBalanco * intensidade;
        float dx = Mathf.Sin(faseBalanco) * (AmplitudeBalanco * 0.45f) * intensidade;
        camJogador.localPosition = camPosBase + new Vector3(dx, dy, 0f);
    }

    void VerificarPortas(Vector3 refPos)
    {
        if (portasParaAbrir == null) return;
        const float limiteSqr = DistanciaAbrirPorta * DistanciaAbrirPorta;
        for (int i = 0; i < portasParaAbrir.Length; i++)
        {
            FolhaDePorta porta = portasParaAbrir[i];
            if (porta == null || porta.trancada || porta.DestinoAberta) continue;
            Vector3 d = porta.transform.position - refPos;
            d.y = 0f;
            if (d.sqrMagnitude <= limiteSqr) porta.Abrir();
        }
    }

    static void AbrirPortaSeFechada(FolhaDePorta porta)
    {
        if (porta == null || porta.trancada || porta.DestinoAberta) return;
        porta.Abrir();
    }

    List<Transform> ColetarPontosValidos()
    {
        var lista = new List<Transform>();
        if (pontos == null) return lista;
        for (int i = 0; i < pontos.Length; i++)
            if (pontos[i] != null) lista.Add(pontos[i]);
        return lista;
    }

    Transform UltimoPontoValido()
    {
        if (pontos == null) return null;
        for (int i = pontos.Length - 1; i >= 0; i--)
            if (pontos[i] != null) return pontos[i];
        return null;
    }

    void OnGUI()
    {
        useGUILayout = false;
        if (Event.current.type != EventType.Repaint) return;
        float s = DesenhoUi.S;
        GUI.depth = -900;

        // Depois de pular: volta do preto já na recepção.
        float desdePulo = Time.unscaledTime - puladaEm;
        if (!Ativa && desdePulo < DuracaoVoltaDoPreto)
        {
            DesenhoUi.Caixa(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 1f - Suave(desdePulo / DuracaoVoltaDoPreto)));
            return;
        }
        if (!Ativa || !executando) return;

        float progresso = Mathf.Clamp01(tempoSegurado / Mathf.Max(0.01f, segurarParaPular));
        float restante = mostrarDicaAte - Time.unscaledTime;
        if (restante <= 0f && progresso <= 0f) return;

        // Segurando: a tela escurece junto com o anel (até quase preto no fim).
        if (progresso > 0f)
            DesenhoUi.Caixa(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.92f * progresso * progresso));

        float alfa = progresso > 0f ? 1f : Mathf.Clamp01(restante / 0.6f) * 0.8f;

        // Discreto, no estilo das escolhas da saída: texto em máquina de escrever e um traço fino que enche.
        string texto = progresso > 0f ? "Pulando a chegada…" : "Segure E para pular";
        GUIStyle estilo = DesenhoUi.EstiloMaquina(17f, new Color(0.92f, 0.92f, 0.9f, alfa), TextAnchor.MiddleRight);
        GUIStyle sombra = DesenhoUi.EstiloMaquina(17f, new Color(0f, 0f, 0f, 0.6f * alfa), TextAnchor.MiddleRight);
        float largura = 260f * s, margem = 56f * s;
        var r = new Rect(Screen.width - margem - largura, Screen.height - margem - 30f * s, largura, 26f * s);
        GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), texto, sombra);
        GUI.Label(r, texto, estilo);

        float espessura = Mathf.Max(1f, 1.5f * s);
        var trilho = new Rect(r.x, r.yMax + 6f * s, largura, espessura);
        DesenhoUi.Caixa(trilho, new Color(1f, 1f, 1f, 0.18f * alfa));
        if (progresso > 0f)   // enche da direita para a esquerda, alinhado ao texto
            DesenhoUi.Caixa(new Rect(trilho.xMax - largura * progresso, trilho.y, largura * progresso, espessura), new Color(1f, 1f, 1f, 0.9f * alfa));
    }

    static float Suave(float x)
    {
        float c = Mathf.Clamp01(x);
        return 1f - (1f - c) * (1f - c) * (1f - c);
    }

    static bool SegurandoPular(out bool algumaTeclaApertada)
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard teclado = Keyboard.current;
        algumaTeclaApertada = teclado != null && teclado.anyKey.wasPressedThisFrame;
        return teclado != null && (teclado.eKey.isPressed || teclado.spaceKey.isPressed);
#else
        algumaTeclaApertada = Input.anyKeyDown;
        return Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Space);
#endif
    }

    static Vector2 DeltaMouse()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
#else
        return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 15f;
#endif
    }
}
