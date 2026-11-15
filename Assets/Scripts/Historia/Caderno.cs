using System;
using System.Collections.Generic;
using UnityEngine;

public enum ResultadoDaComparacao { Nova, JaRegistrada, SemRelacao, FaltaEvidencia, Invalida }

public static class Caderno
{
    static CatalogoDeEvidencias catalogo;
    public static CatalogoDeEvidencias Catalogo
    {
        get => catalogo ?? CatalogoDeEvidencias.Padrao;
        set => catalogo = value;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar() => catalogo = null;

    public static bool Coletar(string idEvidencia) =>
        Catalogo.Buscar(idEvidencia) != null && EstadoDaNoite.Marcar("ev:" + idEvidencia);

    public static bool Coletou(string idEvidencia) =>
        Catalogo.Buscar(idEvidencia) != null && EstadoDaNoite.Tem("ev:" + idEvidencia);

    public static ResultadoDaComparacao Comparar(string a, string b, out Comparacao comparacao)
    {
        comparacao = null;
        if (a == b || Catalogo.Buscar(a) == null || Catalogo.Buscar(b) == null)
            return ResultadoDaComparacao.Invalida;
        if (!Coletou(a) || !Coletou(b)) return ResultadoDaComparacao.FaltaEvidencia;
        comparacao = Catalogo.ComparacaoEntre(a, b);
        if (comparacao == null) return ResultadoDaComparacao.SemRelacao;
        return EstadoDaNoite.Marcar("con:" + comparacao.id)
            ? ResultadoDaComparacao.Nova : ResultadoDaComparacao.JaRegistrada;
    }

    public static IEnumerable<Evidencia> Coletadas()
    {
        foreach (Evidencia evidencia in Catalogo.evidencias ?? Array.Empty<Evidencia>())
            if (evidencia != null && Coletou(evidencia.id)) yield return evidencia;
    }

    public static IEnumerable<Comparacao> Conclusoes()
    {
        foreach (Comparacao comparacao in Catalogo.comparacoes ?? Array.Empty<Comparacao>())
            if (comparacao != null && EstadoDaNoite.Tem("con:" + comparacao.id)) yield return comparacao;
    }
}
