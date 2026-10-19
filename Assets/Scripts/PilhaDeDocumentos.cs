using System.Collections.Generic;
using UnityEngine;

public class PilhaDeDocumentos : MonoBehaviour
{
    // Ordem de cima para baixo, sem alterar os pais dos papéis.
    public ObjetoInspecionavel[] folhas;

    static readonly Dictionary<ObjetoInspecionavel, PilhaDeDocumentos> registro = new Dictionary<ObjetoInspecionavel, PilhaDeDocumentos>();
    readonly List<ObjetoInspecionavel> registradas = new List<ObjetoInspecionavel>();

    public static PilhaDeDocumentos De(ObjetoInspecionavel o)
    {
        return o != null && registro.TryGetValue(o, out var pilha) && pilha != null && pilha.isActiveAndEnabled
            ? pilha : null;
    }

    void OnEnable()
    {
        if (folhas == null) return;
        foreach (var folha in folhas)
        {
            if (folha == null) continue;
            registro[folha] = this;
            registradas.Add(folha);
        }
    }

    void OnDisable()
    {
        foreach (var folha in registradas)
            if (registro.TryGetValue(folha, out var pilha) && pilha == this)
                registro.Remove(folha);
        registradas.Clear();
    }

    public int Total
    {
        get
        {
            int total = 0;
            if (folhas != null)
                foreach (var folha in folhas)
                    if (folha != null && folha.isActiveAndEnabled) total++;
            return total;
        }
    }

    public int NaoLidas
    {
        get
        {
            int total = 0;
            if (folhas != null)
                foreach (var folha in folhas)
                    if (folha != null && folha.isActiveAndEnabled && !folha.JaExaminado) total++;
            return total;
        }
    }

    // Índice a partir de zero entre as folhas disponíveis, como no contador.
    public int Indice(ObjetoInspecionavel o)
    {
        int indice = 0;
        if (folhas != null)
            foreach (var folha in folhas)
            {
                if (folha == null || !folha.isActiveAndEnabled) continue;
                if (folha == o) return indice;
                indice++;
            }
        return -1;
    }

    public ObjetoInspecionavel Vizinha(ObjetoInspecionavel o, int passo)
    {
        int indice = Indice(o);
        int total = Total;
        if (indice < 0 || total == 0) return null;
        int alvo = (indice + passo % total + total) % total;
        foreach (var folha in folhas)
        {
            if (folha == null || !folha.isActiveAndEnabled) continue;
            if (alvo-- == 0) return folha;
        }
        return null;
    }
}
