using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(CharacterController))]
public class Movimento : MonoBehaviour
{
    public Transform camera;
    public Vector2 entradasJogador;
    public Vector2 olhar;

    [Header("Configurações de Movimento")]
    [SerializeField] private float velocidadeJogador = 4f;
    [SerializeField] private float sensibilidadeMouse = 5f;

    [Header("Configurações de Agachar")]
    [SerializeField] private float velocidadeAgachado = 2f;
    [SerializeField] private float alturaAgachado = 1f;
    [SerializeField] private float velocidadeTransicao = 10f;

    [Header("Física")]
    [SerializeField] private float gravidade = -9.81f;

    [Header("Balanço da câmera (view bobbing)")]
    [Tooltip("Metros andados a cada passo: define o ritmo do balanço e dos sons de passo.")]
    [SerializeField] private float comprimentoDoPasso = 1.3f;
    [SerializeField] private float comprimentoDoPassoAgachado = 0.75f;
    [Tooltip("Sobe e desce (m). O ponto mais baixo é o pé tocando o chão.")]
    [SerializeField] private float balancoVertical = 0.035f;
    [Tooltip("Vai e vem lateral (m), um ciclo a cada dois passos.")]
    [SerializeField] private float balancoLateral = 0.02f;
    [Tooltip("Inclinação lateral da cabeça (graus), junto do vai e vem.")]
    [SerializeField] private float balancoInclinacao = 0.4f;
    [SerializeField] private float multiplicadorAgachado = 0.6f;
    [Tooltip("Quão rápido o balanço entra ao começar a andar e some ao parar (por segundo).")]
    [SerializeField] private float suavidadeBalanco = 6f;

    /// <summary>
    /// Disparado quando o pé toca o chão (no ponto mais baixo do balanço); o parâmetro diz se está agachado.
    /// PassosDoJogador escuta isto para tocar o som do passo.
    /// </summary>
    public event Action<bool> AoDarPasso;

    // Mantém a sensibilidade do mouse igual à que se tinha a ~125 FPS, quando ela ainda dependia do FPS.
    private const float PassoDeReferencia = 0.008f;

    private CharacterController characterController;
    private InputAction acaoOlhar;
    private Vector3 velocidadeVertical;
    private float rotacaoVertical = 0.0f;

    // --- Variáveis de controle de agachamento ---
    private bool estaAgachado = false;
    private float alturaNormal;
    private Vector3 centroNormal;
    private Vector3 centroAgachado;
    private Vector3 posicaoCameraNormal;
    private Vector3 posicaoCameraAgachada;

    // --- Balanço: a câmera fica em posicaoBaseCamera (em pé/agachado) + o deslocamento do passo ---
    private Vector3 posicaoBaseCamera;
    private float passos;           // passos dados (fração = meio do passo); o balanço é função disto
    private float pesoBalanco;      // 0 parado, 1 andando
    private float inclinacaoBalanco;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        if (TryGetComponent(out PlayerInput playerInput) && playerInput.actions != null)
            acaoOlhar = playerInput.actions.FindAction("Look");

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Salvar as proporções originais do jogador em pé
        alturaNormal = characterController.height;
        centroNormal = characterController.center;
        posicaoCameraNormal = camera.localPosition;
        posicaoBaseCamera = posicaoCameraNormal;

        // Calcular as proporções para quando estiver agachado
        float diferencaAltura = alturaNormal - alturaAgachado;
        centroAgachado = new Vector3(centroNormal.x, centroNormal.y - (diferencaAltura / 2f), centroNormal.z);
        posicaoCameraAgachada = new Vector3(posicaoCameraNormal.x, posicaoCameraNormal.y - diferencaAltura, posicaoCameraNormal.z);
    }

    public float Inclinacao => rotacaoVertical;
    public bool Agachado => estaAgachado;

    /// <summary>Coloca o jogador numa pose salva, sem animação (Salvamento ao continuar o jogo).</summary>
    public void Teleportar(Vector3 posicao, float giro, float inclinacao, bool agachado)
    {
        characterController.enabled = false;
        transform.SetPositionAndRotation(posicao, Quaternion.Euler(0f, giro, 0f));
        estaAgachado = agachado;
        characterController.height = agachado ? alturaAgachado : alturaNormal;
        characterController.center = agachado ? centroAgachado : centroNormal;
        posicaoBaseCamera = agachado ? posicaoCameraAgachada : posicaoCameraNormal;
        camera.localPosition = posicaoBaseCamera;
        characterController.enabled = true;
        Physics.SyncTransforms();

        passos = pesoBalanco = inclinacaoBalanco = 0f;
        rotacaoVertical = Mathf.Clamp(inclinacao, -80f, 80f);
        camera.localRotation = Quaternion.Euler(rotacaoVertical, 0, 0);
        velocidadeVertical = Vector3.zero;
        olhar = Vector2.zero;
    }

    // Campo de visão e brilho vêm das Configuracoes (tela de opções) e são reaplicados quando mudam.
    private void OnEnable()
    {
        Configuracoes.Alterado += AplicarConfiguracoes;
        AplicarConfiguracoes();
    }

    private void OnDisable() => Configuracoes.Alterado -= AplicarConfiguracoes;

    private void AplicarConfiguracoes()
    {
        Configuracoes.Carregar();
        Camera cam = camera != null ? camera.GetComponentInChildren<Camera>() : null;
        if (cam == null) return;
        cam.fieldOfView = Configuracoes.CampoDeVisao;
        // O brilho é um Volume global: só aparece com Post Processing ligado na câmera.
        if (cam.TryGetComponent(out UniversalAdditionalCameraData dados))
            dados.renderPostProcessing = true;
    }

    void Update()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // Esc é do MenuPausa: ele solta o cursor e desliga este componente enquanto o jogo está pausado.

        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        // --- SISTEMA TOGGLE DIRETO NO UPDATE ---
        if (Keyboard.current != null)
        {
            // Se apertou C ou Ctrl neste frame, inverte o estado do agachamento
            if (Keyboard.current.cKey.wasPressedThisFrame || Keyboard.current.leftCtrlKey.wasPressedThisFrame)
            {
                estaAgachado = !estaAgachado;
            }
        }

        LidarComAgachamento();

        // 1. Rotação da Câmera e do Personagem (Olhar)
        // O mouse já entrega o deslocamento do quadro: multiplicar por deltaTime fazia um quadro lento girar a
        // câmera pelo quadrado da duração (o "teleporte" num engasgo). Analógico é velocidade: esse usa deltaTime.
        InputControl controleOlhar = acaoOlhar?.activeControl;
        bool analogico = controleOlhar != null && !(controleOlhar.device is Pointer);
        float escalaOlhar = sensibilidadeMouse * Configuracoes.Sensibilidade * (analogico ? Time.deltaTime : PassoDeReferencia);
        float rotacaoHorizontal = olhar.x * escalaOlhar;
        transform.Rotate(0, rotacaoHorizontal, 0);

        rotacaoVertical -= olhar.y * escalaOlhar * (Configuracoes.InverterY ? -1f : 1f);
        rotacaoVertical = Mathf.Clamp(rotacaoVertical, -80f, 80f);
        camera.localRotation = Quaternion.Euler(rotacaoVertical, 0, inclinacaoBalanco);

        // 2. Zerar a gravidade acumulada quando toca no chão
        if (characterController.isGrounded && velocidadeVertical.y < 0)
        {
            velocidadeVertical.y = -2f;
        }

        // 3. Movimentação Horizontal
        float velocidadeAtual = estaAgachado ? velocidadeAgachado : velocidadeJogador;

        Vector3 posicaoAntes = transform.position;
        Vector3 direcaoMovimento = transform.right * entradasJogador.x + transform.forward * entradasJogador.y;
        characterController.Move(direcaoMovimento * velocidadeAtual * Time.deltaTime);

        // 4. Aplicação contínua da Gravidade no eixo Y
        velocidadeVertical.y += gravidade * Time.deltaTime;
        characterController.Move(velocidadeVertical * Time.deltaTime);

        // 5. Balanço da câmera e passos, pelo quanto o jogador realmente andou (parede na frente = parado)
        Vector3 andado = transform.position - posicaoAntes;
        andado.y = 0f;
        AtualizarBalanco(andado.magnitude);
    }

    private void LidarComAgachamento()
    {
        float alturaAlvo = estaAgachado ? alturaAgachado : alturaNormal;
        Vector3 centroAlvo = estaAgachado ? centroAgachado : centroNormal;
        Vector3 posCameraAlvo = estaAgachado ? posicaoCameraAgachada : posicaoCameraNormal;

        characterController.height = Mathf.Lerp(characterController.height, alturaAlvo, Time.deltaTime * velocidadeTransicao);
        characterController.center = Vector3.Lerp(characterController.center, centroAlvo, Time.deltaTime * velocidadeTransicao);
        posicaoBaseCamera = Vector3.Lerp(posicaoBaseCamera, posCameraAlvo, Time.deltaTime * velocidadeTransicao);
    }

    // Um passo = meia volta do seno: o vertical (|sen|) bate no ponto mais baixo a cada passo, quando o pé toca
    // o chão; o lateral e a inclinação (sen) fazem um ciclo a cada dois passos, um para cada pé.
    private void AtualizarBalanco(float distancia)
    {
        float dt = Time.deltaTime;
        bool andando = characterController.isGrounded && dt > 0f && distancia / dt > 0.3f;

        if (andando)
        {
            // Saindo do repouso: o primeiro passo vem depois de meio passo, não de um inteiro.
            if (pesoBalanco <= 0f) passos = Mathf.Floor(passos) + 0.5f;
            float comprimento = estaAgachado ? comprimentoDoPassoAgachado : comprimentoDoPasso;
            int antes = Mathf.FloorToInt(passos);
            passos += distancia / Mathf.Max(0.1f, comprimento);
            if (Mathf.FloorToInt(passos) != antes) AoDarPasso?.Invoke(estaAgachado);
        }
        pesoBalanco = Mathf.MoveTowards(pesoBalanco, andando ? 1f : 0f, dt * suavidadeBalanco);

        float intensidade = Configuracoes.IntensidadeBalanco * Mathf.SmoothStep(0f, 1f, pesoBalanco) *
                            (estaAgachado ? multiplicadorAgachado : 1f);
        float fase = passos * Mathf.PI;
        var deslocamento = new Vector3(Mathf.Sin(fase) * balancoLateral,
                                       (Mathf.Abs(Mathf.Sin(fase)) - 0.5f) * balancoVertical, 0f);
        inclinacaoBalanco = Mathf.Sin(fase) * balancoInclinacao * intensidade;

        camera.SetLocalPositionAndRotation(posicaoBaseCamera + deslocamento * intensidade,
                                           Quaternion.Euler(rotacaoVertical, 0, inclinacaoBalanco));
    }

    void OnMove(InputValue value)
    {
        entradasJogador = value.Get<Vector2>();
    }

    void OnLook(InputValue value)
    {
        olhar = value.Get<Vector2>();
    }

    // Removi o 'void OnCrouch' daqui para evitar que ele cancele o comando do Update!
}