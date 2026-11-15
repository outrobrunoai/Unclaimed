using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Evidencia
{
    public string id, tipo, titulo, local, resumo;
    public bool opcional;
}

[Serializable]
public class ParDeEvidencias { public string a, b; }

[Serializable]
public class Comparacao
{
    public string id, tipo, titulo, texto;
    public ParDeEvidencias[] pares;
}

[Serializable]
public class CatalogoDeEvidencias
{
    public Evidencia[] evidencias;
    public Comparacao[] comparacoes;
    static CatalogoDeEvidencias padrao;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar() => padrao = null;

    public static CatalogoDeEvidencias Carregar(string json) => JsonUtility.FromJson<CatalogoDeEvidencias>(json);

    public static CatalogoDeEvidencias Padrao
    {
        get
        {
            if (padrao == null)
            {
                TextAsset arquivo = Resources.Load<TextAsset>("Historia/evidencias");
                if (arquivo == null)
                {
                    Debug.LogWarning("CatalogoDeEvidencias: catálogo de evidências não encontrado.");
                    padrao = new CatalogoDeEvidencias();
                }
                else padrao = Carregar(arquivo.text);
            }
            return padrao;
        }
    }

    public Evidencia Buscar(string id)
    {
        if (string.IsNullOrEmpty(id) || evidencias == null) return null;
        foreach (Evidencia evidencia in evidencias)
            if (evidencia != null && evidencia.id == id) return evidencia;
        return null;
    }

    public Comparacao ComparacaoEntre(string a, string b)
    {
        if (a == b || Buscar(a) == null || Buscar(b) == null || comparacoes == null) return null;
        foreach (Comparacao comparacao in comparacoes)
        {
            if (comparacao == null || comparacao.pares == null) continue;
            foreach (ParDeEvidencias par in comparacao.pares)
                if (par != null && ((par.a == a && par.b == b) || (par.a == b && par.b == a)))
                    return comparacao;
        }
        return null;
    }

    public List<string> Problemas()
    {
        var problemas = new List<string>();
        var ids = new HashSet<string>();
        foreach (Evidencia evidencia in evidencias ?? Array.Empty<Evidencia>())
        {
            if (evidencia == null) { problemas.Add("Evidência nula."); continue; }
            if (string.IsNullOrWhiteSpace(evidencia.id) || !ids.Add(evidencia.id))
                problemas.Add($"Id de evidência vazio ou repetido: {evidencia.id}.");
            if (string.IsNullOrWhiteSpace(evidencia.titulo) || string.IsNullOrWhiteSpace(evidencia.resumo))
                problemas.Add($"Evidência sem título ou resumo: {evidencia.id}.");
        }
        var idsComparacoes = new HashSet<string>();
        foreach (Comparacao comparacao in comparacoes ?? Array.Empty<Comparacao>())
        {
            if (comparacao == null) { problemas.Add("Comparação nula."); continue; }
            if (string.IsNullOrWhiteSpace(comparacao.id) || !idsComparacoes.Add(comparacao.id))
                problemas.Add($"Id de comparação vazio ou repetido: {comparacao.id}.");
            if (string.IsNullOrWhiteSpace(comparacao.titulo) || string.IsNullOrWhiteSpace(comparacao.texto))
                problemas.Add($"Comparação sem título ou texto: {comparacao.id}.");
            foreach (ParDeEvidencias par in comparacao.pares ?? Array.Empty<ParDeEvidencias>())
            {
                if (par == null || Buscar(par.a) == null || Buscar(par.b) == null)
                    problemas.Add($"Par com evidência desconhecida: {comparacao.id}.");
                if (par != null && par.a == par.b)
                    problemas.Add($"Par com a mesma evidência: {comparacao.id}.");
            }
        }
        return problemas;
    }
}
