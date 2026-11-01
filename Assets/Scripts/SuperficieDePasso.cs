using UnityEngine;

/// <summary>
/// Marca à mão o tipo de chão para o PassosDoJogador (vale para este objeto e os filhos), ex.: "Metal", "Madeira".
/// Só é preciso onde o nome do material não basta (ex.: um tablado de madeira com material genérico).
/// </summary>
public class SuperficieDePasso : MonoBehaviour
{
    public string superficie = "Concreto";
}
