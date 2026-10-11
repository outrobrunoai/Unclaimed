using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Coloque este script em qualquer objeto-pista (precisa de um Collider).
/// Ao apontar a mira (centro da tela) diretamente para ele, a até "distanciaMaxima" metros,
/// aparece o aviso "[E] Examinar". Paredes e outros objetos na frente bloqueiam a mira.
/// Ao apertar a tecla, uma cópia do objeto aparece na frente da câmera
/// e pode ser girada com clique + arraste do mouse (scroll = zoom).
/// A cópia é desenhada por cima de tudo (câmera overlay do URP), então nunca
/// atravessa paredes ou móveis. Isso exige a layer "Inspecao" (veja abaixo).
/// Funciona com o Input System novo e com o Input Manager antigo.
/// </summary>
public class ObjetoInspecionavel : MonoBehaviour
{
    [Header("Informações da pista")]
    public string nome = "Pista";
    [TextArea] public string descricao;

    [Header("Interação")]
    [Tooltip("Nome da tecla (ex.: E, F, Q).")]
    public string teclaInteracao = "E";
    [Tooltip("Até que distância (em metros) a mira alcança o objeto.")]
    public float distanciaMaxima = 2f;
    [Tooltip("Folga da mira (em metros) para facilitar apontar para objetos pequenos. 0 = o centro da tela precisa acertar o objeto exatamente.")]
    [Range(0f, 0.2f)] public float folgaDaMira = 0.04f;

    [Header("Visualização 360°")]
    [Tooltip("Opcional: um modelo mais detalhado só para a inspeção. Vazio = usa o próprio objeto.")]
    public GameObject modeloInspecao;
    public float distanciaDaCamera = 0.7f;
    [Tooltip("Tamanho (em metros) da maior dimensão do objeto durante a inspeção.")]
    public float tamanhoNaTela = 0.4f;
    public float sensibilidade = 0.4f;
    public float zoomPorScroll = 0.08f;
    public Vector2 limiteDistancia = new Vector2(0.3f, 1.2f);

    [Header("Posição ao inspecionar")]
    [Tooltip("Onde o objeto aparece na tela. (0,0) = centro. X: -1 = borda esquerda, +1 = borda direita. " +
             "Y: -1 = borda de baixo, +1 = borda de cima. Ex.: (-0.35, 0) deixa o objeto à esquerda. " +
             "Dica: selecione o objeto na cena para ver uma prévia (esfera verde-azulada) na frente da câmera do jogador.")]
    public Vector2 posicaoNaTela = Vector2.zero;
    [Tooltip("FaceParaOJogador = sempre mostra a face escolhida abaixo virada para o jogador, de qualquer posição. ManterDaCena = mantém a rotação que o objeto tem na cena. RotacaoManual = usa a Rotacao Inicial abaixo.")]
    public OrientacaoNaInspecao orientacao = OrientacaoNaInspecao.FaceParaOJogador;
    [Tooltip("Qual lado do objeto (nos eixos do próprio objeto) deve ficar virado para o jogador. Ex.: um livro deitado mostra a capa com Cima. Selecione o objeto na cena: a seta amarela mostra o lado escolhido.")]
    public EixoLocal faceVoltadaParaJogador = EixoLocal.Cima;
    [Tooltip("Qual lado do objeto deve ficar para cima na tela. Precisa ser um eixo diferente da face escolhida. A seta azul-clara mostra o lado escolhido.")]
    public EixoLocal topoNaTela = EixoLocal.Frente;
    [Tooltip("Gira o objeto no próprio plano da tela (em graus) para acertar a posição. Ex.: deixar o texto da capa reto.")]
    public float giroNaTela = 0f;
    [Tooltip("Só usado em RotacaoManual: rotação (em graus) relativa à câmera.")]
    public Vector3 rotacaoInicial = new Vector3(0f, 180f, 0f);

    [Header("Não atravessar paredes")]
    [Tooltip("Layer usada só durante a inspeção. Precisa existir em Project Settings > Tags and Layers (o script GarantirLayerInspecao cria sozinho). O script aplica ela na cópia automaticamente.")]
    public string nomeDaLayer = "Inspecao";

    [Header("Durante a inspeção")]
    [Tooltip("Arraste aqui o movimento do jogador e o script de olhar com o mouse para travá-los.")]
    public Behaviour[] desativarDuranteInspecao;

    [Header("Eventos")]
    public UnityEvent aoIniciarInspecao;
    [Tooltip("Dispara só na primeira vez (ideal para registrar a pista no diário usando o Inspector).")]
    public UnityEvent aoPrimeiraInspecao;
    public UnityEvent aoFinalizarInspecao;

    // --- estado compartilhado entre todas as pistas ---
    static ObjetoInspecionavel melhor;      // pista mais próxima neste frame
    static float melhorDistancia;
    static int frameAtual = -1;
    public static bool InspecionandoAlgo => emInspecao != null;
    static ObjetoInspecionavel emInspecao;  // pista sendo inspecionada agora

    // --- estado da instância ---
    Transform cam;
    Transform pivot;
    float distanciaAtual;
    bool arrastando;
    Vector2 ultimaPosMouse;
    bool jaExaminado;
    CursorLockMode travaAnterior;
    bool cursorVisivelAnterior;
    readonly System.Collections.Generic.List<Behaviour> desativados = new System.Collections.Generic.List<Behaviour>();
    GUIStyle estilo;

    // câmera sobreposta (desenha o objeto por cima de tudo)
    Camera camPrincipal;
    Camera camSobreposta;
    UniversalAdditionalCameraData dadosCamPrincipal;
    int mascaraOriginal;
    bool mascaraAlterada;
    bool avisouLayer;

    // Cada pista confere se a mira está apontando para ela; só uma por vez recebe a interação.
    void Update()
    {
        if (Time.frameCount != frameAtual)
        {
            frameAtual = Time.frameCount;
            melhor = null;
            melhorDistancia = float.MaxValue;
        }

        if (emInspecao != null) return;

        Camera c = Camera.main;
        if (c == null) return;

        float dist;
        if (!EstaApontando(c, out dist)) return;

        if (dist < melhorDistancia)
        {
            melhorDistancia = dist;
            melhor = this;
        }
    }

    // A "mira" é um raio que sai do centro da câmera. Só vale se o primeiro objeto sólido
    // atingido for este (paredes e outros objetos na frente bloqueiam).
    static readonly RaycastHit[] acertos = new RaycastHit[32];

    bool EstaApontando(Camera c, out float distancia)
    {
        var raio = new Ray(c.transform.position, c.transform.forward);

        // 1) Mira exata (centro da tela).
        int n = Physics.RaycastNonAlloc(raio, acertos, distanciaMaxima,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        if (Avaliar(n, out distancia)) return true;

        // 2) Com folga, para facilitar a mira em objetos pequenos.
        if (folgaDaMira > 0f)
        {
            n = Physics.SphereCastNonAlloc(raio, folgaDaMira, acertos, distanciaMaxima,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            if (Avaliar(n, out distancia)) return true;
        }

        distancia = 0f;
        return false;
    }

    // Entre tudo que o raio atravessou: este objeto precisa estar mais perto do que qualquer
    // coisa sólida. Triggers de outros objetos (zonas de evento etc.) não bloqueiam a mira.
    bool Avaliar(int n, out float distancia)
    {
        float meu = float.MaxValue;
        float bloqueio = float.MaxValue;

        for (int i = 0; i < n; i++)
        {
            RaycastHit h = acertos[i];
            if (h.collider == null) continue;
            if (h.distance <= 0f) continue; // já estava sobreposto na origem (ex.: o próprio jogador)

            if (h.collider.transform.IsChildOf(transform))
                meu = Mathf.Min(meu, h.distance);
            else if (!h.collider.isTrigger)
                bloqueio = Mathf.Min(bloqueio, h.distance);
        }

        distancia = meu;
        return meu < float.MaxValue && meu <= bloqueio;
    }

    // LateUpdate roda depois de todos os Update, então "melhor" já está decidido.
    void LateUpdate()
    {
        if (emInspecao == this)
        {
            Inspecionar();
            if (TeclaApertada(teclaInteracao) || TeclaApertada("Escape"))
                Fechar();
        }
        else if (emInspecao == null && melhor == this && TeclaApertada(teclaInteracao))
        {
            Abrir();
        }
    }

    void Abrir()
    {
        camPrincipal = Camera.main;
        cam = camPrincipal.transform;
        emInspecao = this;

        GameObject fonte = modeloInspecao != null ? modeloInspecao : gameObject;
        GameObject clone = Instantiate(fonte, fonte.transform.position, fonte.transform.rotation);
        clone.name = nome + " (inspeção)";
        clone.transform.localScale = fonte.transform.lossyScale;

        // A cópia não pode ser interativa nem física.
        foreach (var s in clone.GetComponentsInChildren<ObjetoInspecionavel>())
        {
            s.enabled = false;
            Destroy(s);
        }
        foreach (var c in clone.GetComponentsInChildren<Collider>()) c.enabled = false;
        foreach (var r in clone.GetComponentsInChildren<Rigidbody>()) r.isKinematic = true;

        Renderer[] rends = clone.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0)
        {
            Destroy(clone);
            emInspecao = null;
            return;
        }

        // Mede o objeto sem rotação: assim tamanho e centro não dependem de como ele está virado na cena.
        Quaternion rotacaoOriginal = clone.transform.rotation;
        clone.transform.rotation = Quaternion.identity;
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        Vector3 centroLocal = clone.transform.InverseTransformPoint(b.center);
        float maior = Mathf.Max(b.size.x, b.size.y, b.size.z);

        // Pivô na frente da câmera, na posição escolhida.
        distanciaAtual = Mathf.Clamp(distanciaDaCamera, limiteDistancia.x, limiteDistancia.y);
        pivot = new GameObject("PivotInspecao").transform;
        pivot.SetParent(cam, false);
        pivot.localRotation = Quaternion.identity;
        pivot.localPosition = PosicaoLocal(camPrincipal, distanciaAtual);

        // Vira o objeto (de frente para o jogador, por padrão) e põe o centro dele exatamente no pivô.
        clone.transform.rotation = CalcularRotacao(rotacaoOriginal);
        clone.transform.position += pivot.position - clone.transform.TransformPoint(centroLocal);
        clone.transform.SetParent(pivot, true);

        if (maior > 0.0001f) pivot.localScale = Vector3.one * (tamanhoNaTela / maior);

        ConfigurarCameraSobreposta(clone);

        // Trava o jogador e libera o cursor.
        desativados.Clear();
        foreach (var comp in desativarDuranteInspecao)
        {
            if (comp != null && comp.enabled)
            {
                comp.enabled = false;
                desativados.Add(comp);
            }
        }
        travaAnterior = Cursor.lockState;
        cursorVisivelAnterior = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        arrastando = false;

        aoIniciarInspecao?.Invoke();

        if (!jaExaminado)
        {
            jaExaminado = true;
            aoPrimeiraInspecao?.Invoke(); // Ideal para chamar mecânicas de diário ou som de nova pista!
        }
    }

    // Rotação (no mundo) que o objeto terá ao aparecer na inspeção.
    Quaternion CalcularRotacao(Quaternion rotacaoOriginal)
    {
        switch (orientacao)
        {
            case OrientacaoNaInspecao.FaceParaOJogador:
            {
                // Destino: o lado escolhido aponta para a câmera e o "topo" escolhido aponta para cima na tela.
                Quaternion alvo = Quaternion.LookRotation(-cam.forward, cam.up);
                Quaternion local = Quaternion.LookRotation(Eixo(faceVoltadaParaJogador), Eixo(topoNaTela));
                return Quaternion.AngleAxis(giroNaTela, cam.forward) * alvo * Quaternion.Inverse(local);
            }
            case OrientacaoNaInspecao.RotacaoManual:
                return cam.rotation * Quaternion.Euler(rotacaoInicial);
            default:
                return rotacaoOriginal;
        }
    }

    static Vector3 Eixo(EixoLocal eixo)
    {
        switch (eixo)
        {
            case EixoLocal.Cima: return Vector3.up;
            case EixoLocal.Baixo: return Vector3.down;
            case EixoLocal.Frente: return Vector3.forward;
            case EixoLocal.Tras: return Vector3.back;
            case EixoLocal.Direita: return Vector3.right;
            default: return Vector3.left;
        }
    }

    // Converte "posição na tela" (-1..1) + distância em uma posição local da câmera.
    // Assim o objeto fica no mesmo ponto da tela mesmo quando você dá zoom.
    Vector3 PosicaoLocal(Camera c, float distancia)
    {
        float meiaAltura = Mathf.Tan(c.fieldOfView * 0.5f * Mathf.Deg2Rad) * distancia;
        return new Vector3(
            posicaoNaTela.x * meiaAltura * c.aspect,
            posicaoNaTela.y * meiaAltura,
            distancia);
    }

    void Inspecionar()
    {
        if (pivot == null) return;

        Vector2 pos = PosicaoMouse();

        if (BotaoEsquerdoPressionado())
        {
            if (arrastando)
            {
                Vector2 d = pos - ultimaPosMouse;
                // Gira em torno dos eixos da câmera, então o arraste sempre "parece certo".
                pivot.Rotate(cam.up, -d.x * sensibilidade, Space.World);
                pivot.Rotate(cam.right, d.y * sensibilidade, Space.World);
            }
            arrastando = true;
        }
        else
        {
            arrastando = false;
        }
        ultimaPosMouse = pos;

        float scroll = Mathf.Clamp(ScrollVertical(), -1f, 1f);
        if (Mathf.Abs(scroll) > 0.01f)
        {
            distanciaAtual = Mathf.Clamp(distanciaAtual - scroll * zoomPorScroll, limiteDistancia.x, limiteDistancia.y);
            pivot.localPosition = PosicaoLocal(camPrincipal, distanciaAtual);
        }
    }

    void Fechar()
    {
        if (pivot != null) Destroy(pivot.gameObject);
        pivot = null;
        DesfazerCameraSobreposta();

        foreach (var comp in desativados)
            if (comp != null) comp.enabled = true;
        desativados.Clear();

        Cursor.lockState = travaAnterior;
        Cursor.visible = cursorVisivelAnterior;

        emInspecao = null;
        aoFinalizarInspecao?.Invoke();
    }

    // Desenha o objeto por cima de tudo com uma câmera "overlay" (URP camera stacking).
    // Assim ele nunca atravessa paredes, nem no zoom out.
    void ConfigurarCameraSobreposta(GameObject clone)
    {
        int camada = LayerMask.NameToLayer(nomeDaLayer);
        camPrincipal = Camera.main;
        dadosCamPrincipal = camPrincipal != null ? camPrincipal.GetUniversalAdditionalCameraData() : null;

        if (camada < 0 || dadosCamPrincipal == null || dadosCamPrincipal.renderType != CameraRenderType.Base)
        {
            if (!avisouLayer)
            {
                Debug.LogWarning($"ObjetoInspecionavel: a layer '{nomeDaLayer}' não existe ou a câmera principal não é uma Base Camera do URP. " +
                                 "O objeto pode atravessar paredes. Crie a layer em Project Settings > Tags and Layers.");
                avisouLayer = true;
            }
            dadosCamPrincipal = null;
            return;
        }

        // Coloca a cópia (e o pivô) na layer de inspeção.
        foreach (var t in clone.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = camada;
        pivot.gameObject.layer = camada;

        // A câmera principal para de desenhar essa layer durante a inspeção.
        mascaraOriginal = camPrincipal.cullingMask;
        camPrincipal.cullingMask = mascaraOriginal & ~(1 << camada);
        mascaraAlterada = true;

        // Câmera extra: desenha só a layer de inspeção, por cima da imagem final.
        var go = new GameObject("CameraInspecao");
        go.transform.SetParent(camPrincipal.transform, false);
        camSobreposta = go.AddComponent<Camera>();
        camSobreposta.fieldOfView = camPrincipal.fieldOfView;
        camSobreposta.nearClipPlane = 0.01f;
        camSobreposta.farClipPlane = 20f;
        camSobreposta.cullingMask = 1 << camada;

        var dados = camSobreposta.GetUniversalAdditionalCameraData();
        dados.renderType = CameraRenderType.Overlay;
        dados.renderPostProcessing = dadosCamPrincipal.renderPostProcessing;
        dadosCamPrincipal.cameraStack.Add(camSobreposta);
    }

    void DesfazerCameraSobreposta()
    {
        if (camSobreposta != null)
        {
            if (dadosCamPrincipal != null) dadosCamPrincipal.cameraStack.Remove(camSobreposta);
            Destroy(camSobreposta.gameObject);
            camSobreposta = null;
        }

        if (mascaraAlterada && camPrincipal != null)
            camPrincipal.cullingMask = mascaraOriginal;

        mascaraAlterada = false;
        dadosCamPrincipal = null;
    }

    void OnDisable()
    {
        if (emInspecao == this) Fechar();
    }

    // Prévia no Scene: com o objeto selecionado, mostra onde ele vai aparecer e qual lado ficará virado para o jogador.
    void OnDrawGizmosSelected()
    {
        Camera c = Camera.main;
        if (c != null)
        {
            float d = Mathf.Clamp(distanciaDaCamera, limiteDistancia.x, limiteDistancia.y);
            Vector3 centro = c.transform.TransformPoint(PosicaoLocal(c, d));

            Gizmos.color = new Color(0.45f, 0.78f, 0.72f, 0.9f);
            Gizmos.DrawWireSphere(centro, tamanhoNaTela * 0.5f);
            Gizmos.DrawLine(c.transform.position, centro);

            // Alcance da mira
            Gizmos.color = new Color(1f, 1f, 1f, 0.4f);
            Gizmos.DrawRay(c.transform.position, c.transform.forward * distanciaMaxima);
        }

        if (orientacao == OrientacaoNaInspecao.FaceParaOJogador)
        {
            var colisor = GetComponentInChildren<Collider>();
            Vector3 origem = colisor != null ? colisor.bounds.center : transform.position;
            float comp = Mathf.Max(0.15f, tamanhoNaTela * 0.5f);

            Gizmos.color = Color.yellow; // lado que ficará virado para o jogador
            Gizmos.DrawLine(origem, origem + transform.TransformDirection(Eixo(faceVoltadaParaJogador)) * comp);
            Gizmos.color = Color.cyan;   // lado que ficará para cima na tela
            Gizmos.DrawLine(origem, origem + transform.TransformDirection(Eixo(topoNaTela)) * comp * 0.6f);
        }
    }

    // ---------- Interface simples (sem precisar criar Canvas) ----------
    void OnGUI()
    {
        bool mostrandoAviso = emInspecao == null && melhor == this;
        bool inspecionando = emInspecao == this;
        if (!mostrandoAviso && !inspecionando) return;

        if (estilo == null)
        {
            estilo = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            estilo.normal.textColor = Color.white;
        }

        if (mostrandoAviso)
        {
            GUI.Label(new Rect(0, Screen.height - 130, Screen.width, 40),
                $"[{teclaInteracao.ToUpper()}] Examinar: {nome}", estilo);
        }
        else
        {
            GUI.Label(new Rect(Screen.width * 0.2f, 30, Screen.width * 0.6f, 40), nome, estilo);
            if (!string.IsNullOrEmpty(descricao))
                GUI.Label(new Rect(Screen.width * 0.2f, 75, Screen.width * 0.6f, 120), descricao, estilo);
            GUI.Label(new Rect(0, Screen.height - 60, Screen.width, 40),
                $"Clique e arraste: girar  |  Scroll: zoom  |  [{teclaInteracao.ToUpper()}] ou [Esc]: fechar", estilo);
        }
    }

    // ---------- Entrada: funciona nos dois sistemas de input da Unity ----------
    static bool TeclaApertada(string nomeTecla)
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null) return false;
        if (!Enum.TryParse(nomeTecla, true, out Key k)) return false;
        return Keyboard.current[k].wasPressedThisFrame;
#else
        if (!Enum.TryParse(nomeTecla, true, out KeyCode k)) return false;
        return Input.GetKeyDown(k);
#endif
    }

    static bool BotaoEsquerdoPressionado()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
        return Input.GetMouseButton(0);
#endif
    }

    static Vector2 PosicaoMouse()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }

    static float ScrollVertical()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
#else
        return Input.mouseScrollDelta.y;
#endif
    }
}

public enum OrientacaoNaInspecao
{
    FaceParaOJogador,
    ManterDaCena,
    RotacaoManual
}

public enum EixoLocal
{
    Cima,
    Baixo,
    Frente,
    Tras,
    Direita,
    Esquerda
}