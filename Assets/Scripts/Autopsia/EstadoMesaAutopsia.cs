using UnityEngine;

// Só alterna o estado da mesa; o puzzle ainda não está implementado.
public class EstadoMesaAutopsia : MonoBehaviour
{
    [SerializeField] GameObject ocupado, p08;

    public bool P08Ativo => p08 != null && p08.activeSelf;

    public void MostrarP08(bool ativo)
    {
        if (ocupado != null) ocupado.SetActive(!ativo);
        if (p08 != null) p08.SetActive(ativo);
    }
}
