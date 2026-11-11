using UnityEngine;

// Luz interna do compartimento, acesa com a porta aberta (referência dos fabricantes).
public class LuzDeCompartimento : MonoBehaviour
{
    [SerializeField] PecaDeMovel porta;
    [SerializeField] Light luz;
    [SerializeField] float limiarGraus = 3f;

    void Update()
    {
        if (porta == null || luz == null) return;
        bool acesa = porta.Abertura > limiarGraus;
        if (luz.enabled != acesa) luz.enabled = acesa;
    }
}
