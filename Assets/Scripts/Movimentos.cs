using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class Movimento : MonoBehaviour
{
    public Transform camera;
    public Vector2 entradasJogador;
    public Vector2 olhar;
    private CharacterController characterController;
    private float velocidadeJogador = 4f;

    float rotacaoVertical = 0.0f;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    void Update()
    {
        Vector3 movimentacao = new Vector3(entradasJogador.x, 0, entradasJogador.y);
        transform.Translate(movimentacao * velocidadeJogador * Time.deltaTime);
        
        float rotacaoHorizontal = olhar.x * 5.0f * Time.deltaTime;
        transform.Rotate(0, rotacaoHorizontal, 0);

        rotacaoVertical -= olhar.y * 5.0f * Time.deltaTime;
        camera.localRotation = Quaternion.Euler(rotacaoVertical, 0, 0);
    }

    void OnMove(InputValue value)
    {
        entradasJogador = value.Get<Vector2>();
        //Debug.Log("Movimentando");
    }

    void OnLook(InputValue value)
    {
        olhar = value.Get<Vector2>();
    }
}