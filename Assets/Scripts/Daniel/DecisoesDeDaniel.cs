using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Regras de Daniel sem cena (testáveis): quando a fase muda, quando ele sai da mesa e qual ponto ele escolhe.
/// As fases avançam por fatos, nunca por tempo, e nunca voltam:
///   Mesa → Sombra quando ele sai da mesa ("daniel_saiu");
///   Sombra → Obstrução quando o jogador tem duas provas da troca ("con:troca");
///   → Caça quando o jogador fica com o gravador no confronto ("caca").
/// </summary>
public static class DecisoesDeDaniel
{
    public const float IntervaloSombra = 90f, IntervaloObstrucao = 60f;

    /// <summary>Verdadeiro enquanto Daniel está caçando o jogador (fase Caça sem ter entregue o gravador).</summary>
    public static bool CacaEmAndamento => EstadoDaNoite.Fase == FaseDeDaniel.Caca && !EstadoDaNoite.Tem("gravador_entregue");

    public static FaseDeDaniel ProximaFase(FaseDeDaniel atual, Func<string, bool> tem)
    {
        FaseDeDaniel alvo = FaseDeDaniel.Mesa;
        if (tem("daniel_saiu")) alvo = FaseDeDaniel.Sombra;
        if (tem("con:troca")) alvo = FaseDeDaniel.Obstrucao;
        if (tem("caca")) alvo = FaseDeDaniel.Caca;
        return alvo > atual ? alvo : atual;
    }

    /// <summary>Daniel só larga a mesa (P08) depois que o jogador fotografou a mesa (F01) e achou Léo em C-02 (F02),
    /// garantindo que a conclusão "troca" (F01 × F02) sempre possa ser formada.</summary>
    public static bool PodeSairDaMesa(Func<string, bool> tem) => tem("ev:F01") && tem("ev:F02");

    public static float Intervalo(FaseDeDaniel fase) => fase == FaseDeDaniel.Obstrucao ? IntervaloObstrucao : IntervaloSombra;

    public struct Candidato
    {
        public string id, sala, grupo;
        public TipoDeAcao tipo;
        public Vector3 posicao;
        public bool disponivel, valeNaFase;
    }

    /// <summary>
    /// Escolhe a próxima ação comum (presença e porta trancada) entre os candidatos: fora das salas protegidas,
    /// disponível, valendo na fase. Prefere pontos a uma distância média do jogador (perto o bastante para ser
    /// ouvido, longe o bastante para não ser visto). Apagão, armadilha, corrente e aparição têm regras próprias e
    /// não entram aqui. Devolve -1 se nenhum serve.
    /// </summary>
    public static int Escolher(IList<Candidato> candidatos, ICollection<string> protegidas, Vector3 jogador,
                               ICollection<string> gruposTrancados, Func<float> sorteio)
    {
        float soma = 0f;
        var pesos = new float[candidatos.Count];
        for (int i = 0; i < candidatos.Count; i++)
        {
            Candidato c = candidatos[i];
            if (!c.disponivel || !c.valeNaFase || protegidas.Contains(c.sala)) continue;
            if (c.tipo != TipoDeAcao.Presenca && c.tipo != TipoDeAcao.TrancarPorta) continue;
            if (c.tipo == TipoDeAcao.TrancarPorta && !string.IsNullOrEmpty(c.grupo) && gruposTrancados.Contains(c.grupo)) continue;
            float d = Vector3.Distance(c.posicao, jogador);
            float peso = d < 4f ? 0.1f : d < 25f ? 1f : 0.35f;
            if (c.tipo == TipoDeAcao.TrancarPorta) peso *= 0.6f;
            pesos[i] = peso;
            soma += peso;
        }
        if (soma <= 0f) return -1;
        float r = Mathf.Clamp01(sorteio()) * soma;
        for (int i = 0; i < pesos.Length; i++)
        {
            if (pesos[i] <= 0f) continue;
            if (r <= pesos[i]) return i;
            r -= pesos[i];
        }
        for (int i = pesos.Length - 1; i >= 0; i--) if (pesos[i] > 0f) return i;
        return -1;
    }
}
