using System;
using System.Collections.Generic;
using UnityEngine;

public enum FaseDeDaniel { Mesa, Sombra, Obstrucao, Caca }

/// <summary>Fatos descobertos nesta noite, compartilhados pelo caderno e pelo save.</summary>
public static class EstadoDaNoite
{
    [Serializable]
    class Dados
    {
        public int fase;
        public List<string> fatos;
    }

    static readonly HashSet<string> fatos = new HashSet<string>();
    public static event Action<string> AoMarcar;
    public static FaseDeDaniel Fase { get; set; }
    public static IReadOnlyCollection<string> Fatos => fatos;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar()
    {
        Limpar();
        AoMarcar = null;
    }

    public static bool Marcar(string fato)
    {
        if (string.IsNullOrEmpty(fato) || !fatos.Add(fato)) return false;
        AoMarcar?.Invoke(fato);
        return true;
    }

    public static bool Tem(string fato) => !string.IsNullOrEmpty(fato) && fatos.Contains(fato);

    public static void Limpar()
    {
        fatos.Clear();
        Fase = FaseDeDaniel.Mesa;
    }

    public static string Exportar() => JsonUtility.ToJson(new Dados
    {
        fase = (int)Fase,
        fatos = new List<string>(fatos)
    });

    public static void Importar(string json)
    {
        Limpar();
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            Dados dados = JsonUtility.FromJson<Dados>(json);
            if (dados == null || dados.fatos == null || !Enum.IsDefined(typeof(FaseDeDaniel), dados.fase))
                throw new ArgumentException("Estado da noite incompleto ou com fase inválida.");
            Fase = (FaseDeDaniel)dados.fase;
            // Restaurar não anuncia os fatos como novas descobertas.
            foreach (string fato in dados.fatos)
                if (!string.IsNullOrEmpty(fato)) fatos.Add(fato);
        }
        catch (ArgumentException)
        {
            Limpar();
            Debug.LogWarning("EstadoDaNoite: estado salvo inválido; a noite foi reiniciada.");
        }
    }
}
