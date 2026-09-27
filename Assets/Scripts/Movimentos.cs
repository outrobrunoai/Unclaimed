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

    [Header("Física e Pulo")]
    [SerializeField] private float alturaPulo = 1.5f;
    [SerializeField] private float gravidade = -9.81f;

    private CharacterController characterController;
    private Vector3 velocidadeVertical;
    private float rotacaoVertical = 0.0f;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
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

        // 1. Rotação da Câmera e do Personagem (Olhar)
        float rotacaoHorizontal = olhar.x * sensibilidadeMouse * Time.deltaTime;
        transform.Rotate(0, rotacaoHorizontal, 0);

        rotacaoVertical -= olhar.y * sensibilidadeMouse * Time.deltaTime;
        rotacaoVertical = Mathf.Clamp(rotacaoVertical, -80f, 80f);
        camera.localRotation = Quaternion.Euler(rotacaoVertical, 0, 0);

        // 2. Zerar a gravidade acumulada quando toca no chão
        if (characterController.isGrounded && velocidadeVertical.y < 0)
        {
            velocidadeVertical.y = -2f; // Mantém o boneco firme contra o chão
        }

        // 3. Movimentação Horizontal usando CharacterController.Move (Respeita colisões)
        Vector3 direcaoMovimento = transform.right * entradasJogador.x + transform.forward * entradasJogador.y;
        characterController.Move(direcaoMovimento * velocidadeJogador * Time.deltaTime);

        // 4. Aplicação contínua da Gravidade no eixo Y
        velocidadeVertical.y += gravidade * Time.deltaTime;
        characterController.Move(velocidadeVertical * Time.deltaTime);
    }

    // Chamado pelo Player Input quando o botão de Mover é acionado
    void OnMove(InputValue value)
    {
        entradasJogador = value.Get<Vector2>();
    }

    // Chamado pelo Player Input quando o botão de Olhar é acionado
    void OnLook(InputValue value)
    {
        olhar = value.Get<Vector2>();
    }

    // Chamado pelo Player Input quando a ação "Jump" (Espaço por padrão) é pressionada
    void OnJump(InputValue value)
    {
        if (value.isPressed && characterController.isGrounded)
        {
            // Fórmulas da física para calcular a velocidade inicial do pulo baseada na altura desejada
            velocidadeVertical.y = Mathf.Sqrt(alturaPulo * -2f * gravidade);
        }
    }
}