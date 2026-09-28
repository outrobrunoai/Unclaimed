using System;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Coloque este script em qualquer objeto-pista (precisa de um Collider).
/// Ao chegar perto e olhar para ele, aparece o aviso "[E] Examinar".
/// Ao apertar a tecla, uma cópia do objeto aparece na frente da câmera
/// e pode ser girada com clique + arraste do mouse (scroll = zoom).
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
    public float distanciaMaxima = 2f;
    [Range(10f, 180f)] public float anguloMaximo = 70f;

    [Header("Visualização 360°")]
    [Tooltip("Opcional: um modelo mais detalhado só para a inspeção. Vazio = usa o próprio objeto.")]
    public GameObject modeloInspecao;
    public float distanciaDaCamera = 0.7f;
    [Tooltip("Tamanho (em metros) da maior dimensão do objeto durante a inspeção.")]
    public float tamanhoNaTela = 0.4f;
    public float sensibilidade = 0.4f;
    public float zoomPorScroll = 0.08f;
    public Vector2 limiteDistancia = new Vector2(0.3f, 1.2f);

    [Header("Durante a inspeção")]
    [Tooltip("Arraste aqui o movimento do jogador e o script de olhar com o mouse para travá-los.")]
    public Behaviour[] desativarDuranteInspecao;

    [Header("Eventos")]
    public UnityEvent aoIniciarInspecao;
    [Tooltip("Dispara só na primeira vez (ideal para registrar a pista no diário).")]
    public UnityEvent aoPrimeiraInspecao;
    public UnityEvent aoFinalizarInspecao;

    [Header("Diário de pistas")]
    [Tooltip("Registra esta pista no diário na primeira inspeção. O campo Nome deve ser único.")]
    public bool registrarNoDiario = true;

    // --- estado compartilhado entre todas as pistas ---
    static ObjetoInspecionavel melhor;      // pista mais próxima neste frame
    static float melhorDistancia;
    static int frameAtual = -1;
    public static bool InspecionandoAlgo => emInspecao != null;
    static ObjetoInspecionavel emInspecao;  // pista sendo inspecionada agora

    // --- estado da instância ---
    Collider col;
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

    void Awake()
    {
        col = GetComponentInChildren<Collider>();
    }

    // Cada pista avalia se está ao alcance; só a mais próxima recebe a interação.
    void Update()
    {
        if (Time.frameCount != frameAtual)
        {
            frameAtual = Time.frameCount;
            melhor = null;
            melhorDistancia = float.MaxValue;
        }

        if (emInspecao != null || DiarioDePistas.Aberto) return;

        Camera c = Camera.main;
        if (c == null) return;

        Vector3 alvo = col != null ? col.bounds.center : transform.position;
        Vector3 ponto = col != null ? col.bounds.ClosestPoint(c.transform.position) : transform.position;

        float dist = Vector3.Distance(c.transform.position, ponto);
        if (dist > distanciaMaxima) return;

        Vector3 dir = alvo - c.transform.position;
        if (Vector3.Angle(c.transform.forward, dir) > anguloMaximo) return;

        if (dist < melhorDistancia)
        {
            melhorDistancia = dist;
            melhor = this;
        }
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
        cam = Camera.main.transform;
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

        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

        // Pivô na frente da câmera; o centro visual do objeto fica exatamente no pivô.
        distanciaAtual = Mathf.Clamp(distanciaDaCamera, limiteDistancia.x, limiteDistancia.y);
        pivot = new GameObject("PivotInspecao").transform;
        pivot.position = cam.position + cam.forward * distanciaAtual;
        pivot.rotation = cam.rotation;
        pivot.SetParent(cam, true);

        clone.transform.position += pivot.position - b.center;
        clone.transform.SetParent(pivot, true);

        float maior = Mathf.Max(b.size.x, b.size.y, b.size.z);
        if (maior > 0.0001f) pivot.localScale = Vector3.one * (tamanhoNaTela / maior);

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
            if (registrarNoDiario) DiarioDePistas.Registrar(nome, descricao);
            aoPrimeiraInspecao?.Invoke();
        }
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
            pivot.localPosition = new Vector3(0f, 0f, distanciaAtual);
        }
    }

    void Fechar()
    {
        if (pivot != null) Destroy(pivot.gameObject);
        pivot = null;

        foreach (var comp in desativados)
            if (comp != null) comp.enabled = true;
        desativados.Clear();

        Cursor.lockState = travaAnterior;
        Cursor.visible = cursorVisivelAnterior;

        emInspecao = null;
        aoFinalizarInspecao?.Invoke();
    }

    void OnDisable()
    {
        if (emInspecao == this) Fechar();
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