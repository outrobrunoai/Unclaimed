using UnityEngine;

// O trilho intermediário anda a meia velocidade da maca.
public class SeguidorDeCurso : MonoBehaviour
{
    [SerializeField] PecaDeMovel fonte;
    [SerializeField] Vector3 direcao = Vector3.back;
    [SerializeField] float fator = .5f;
    Vector3 fechada;

    void Awake() => fechada = transform.localPosition;

    void LateUpdate()
    {
        if (fonte != null)
            transform.localPosition = fechada + direcao.normalized * fonte.Abertura * fator;
    }
}
