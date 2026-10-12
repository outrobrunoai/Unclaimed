using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chaves e itens de acesso que o jogador carrega, por identificador ("chave_circulacao"). Estado só desta
/// sessão de jogo: o projeto ainda não tem salvamento, então reinicia ao sair do Play Mode. Não é um inventário:
/// só responde "tem / não tem".
/// </summary>
public static class Chaveiro
{
    static readonly HashSet<string> itens = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar() => itens.Clear();

    public static bool Tem(string id) => !string.IsNullOrEmpty(id) && itens.Contains(id);

    /// <summary>Registra a posse; devolve false se já estava com o jogador.</summary>
    public static bool Adicionar(string id) => !string.IsNullOrEmpty(id) && itens.Add(id);

    public static IReadOnlyCollection<string> Itens => itens;
}
