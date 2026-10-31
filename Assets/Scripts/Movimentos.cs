using UnityEngine;
using UnityEngine.InputSystem;

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

        // Calcular as proporções para quando estiver agachado
        float diferencaAltura = alturaNormal - alturaAgachado;
        centroAgachado = new Vector3(centroNormal.x, centroNormal.y - (diferencaAltura / 2f), centroNormal.z);
        posicaoCameraAgachada = new Vector3(posicaoCameraNormal.x, posicaoCameraNormal.y - diferencaAltura, posicaoCameraNormal.z);
    }
    
    void Update()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

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
        float escalaOlhar = sensibilidadeMouse * (analogico ? Time.deltaTime : PassoDeReferencia);
        float rotacaoHorizontal = olhar.x * escalaOlhar;
        transform.Rotate(0, rotacaoHorizontal, 0);

        rotacaoVertical -= olhar.y * escalaOlhar;
        rotacaoVertical = Mathf.Clamp(rotacaoVertical, -80f, 80f);
        camera.localRotation = Quaternion.Euler(rotacaoVertical, 0, 0);

        // 2. Zerar a gravidade acumulada quando toca no chão
        if (characterController.isGrounded && velocidadeVertical.y < 0)
        {
            velocidadeVertical.y = -2f;
        }

        // 3. Movimentação Horizontal
        float velocidadeAtual = estaAgachado ? velocidadeAgachado : velocidadeJogador;

        Vector3 direcaoMovimento = transform.right * entradasJogador.x + transform.forward * entradasJogador.y;
        characterController.Move(direcaoMovimento * velocidadeAtual * Time.deltaTime);

        // 4. Aplicação contínua da Gravidade no eixo Y
        velocidadeVertical.y += gravidade * Time.deltaTime;
        characterController.Move(velocidadeVertical * Time.deltaTime);
    }

    private void LidarComAgachamento()
    {
        float alturaAlvo = estaAgachado ? alturaAgachado : alturaNormal;
        Vector3 centroAlvo = estaAgachado ? centroAgachado : centroNormal;
        Vector3 posCameraAlvo = estaAgachado ? posicaoCameraAgachada : posicaoCameraNormal;

        characterController.height = Mathf.Lerp(characterController.height, alturaAlvo, Time.deltaTime * velocidadeTransicao);
        characterController.center = Vector3.Lerp(characterController.center, centroAlvo, Time.deltaTime * velocidadeTransicao);
        camera.localPosition = Vector3.Lerp(camera.localPosition, posCameraAlvo, Time.deltaTime * velocidadeTransicao);
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