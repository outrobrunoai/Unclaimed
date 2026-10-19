using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Conteúdo da estação da recepção (EstacaoRecepcao.json), separado da interface: estação e sessão, pessoas,
/// conversas do Correio (com anexos e estado inicial de leitura), pastas, arquivos e agenda. Os identificadores
/// são estáveis: a interface guarda o estado da sessão (lidas, pasta aberta, arquivo aberto) por eles.
/// </summary>
[Serializable]
public class EstacaoDados
{
    [Serializable]
    public class Estacao
    {
        public string nome, sistema, versao, orgao, usuario, nomeUsuario, setor;
        public string data, diaDaSemana, dataSeguinte, diaSeguinte, horaInicial;
        public string suporte, memoria, processador;
    }

    [Serializable] public class Pessoa { public string id, nome, endereco, cargo; }

    [Serializable]
    public class Mensagem
    {
        public string id, de, data, corpo;
        public string[] para, cc, anexos;
        public bool lida;
    }

    [Serializable] public class Conversa { public string id, assunto; public Mensagem[] mensagens; }
    [Serializable] public class Pasta { public string id, nome, pai; }
    [Serializable] public class Linha { public string[] c; }

    [Serializable]
    public class Bloco
    {
        public string tipo;      // titulo, subtitulo, meta, paragrafo, lista, tabela, nota
        public string texto;
        public string[] itens;   // lista
        public string[] colunas; // tabela
        public float[] larguras; // frações da largura (tabela)
        public Linha[] linhas;   // tabela
    }

    [Serializable]
    public class Arquivo
    {
        public string id, nome, tipo, pasta, modificado, autor;   // tipo: documento, planilha, nota, imagem
        public Bloco[] blocos;   // documento, planilha
        public string texto;     // nota
        public string imagem;    // nome da textura (imagem)
    }

    [Serializable] public class Evento { public string data, titulo, detalhe; }

    public Estacao estacao;
    public Pessoa[] pessoas;
    public Conversa[] conversas;
    public Pasta[] pastas;
    public Arquivo[] arquivos;
    public Evento[] agenda;

    readonly Dictionary<string, Pessoa> porPessoa = new Dictionary<string, Pessoa>();
    readonly Dictionary<string, Arquivo> porArquivo = new Dictionary<string, Arquivo>();
    readonly Dictionary<string, Pasta> porPasta = new Dictionary<string, Pasta>();

    public static EstacaoDados Carregar(string json)
    {
        var d = JsonUtility.FromJson<EstacaoDados>(json);
        foreach (var p in d.pessoas) d.porPessoa[p.id] = p;
        foreach (var a in d.arquivos) d.porArquivo[a.id] = a;
        foreach (var p in d.pastas) d.porPasta[p.id] = p;
        return d;
    }

    public Pessoa PessoaPor(string id) => id != null && porPessoa.TryGetValue(id, out var p) ? p : null;
    public Arquivo ArquivoPor(string id) => id != null && porArquivo.TryGetValue(id, out var a) ? a : null;
    public Pasta PastaPor(string id) => id != null && porPasta.TryGetValue(id, out var p) ? p : null;
    public string NomeDe(string id) => PessoaPor(id)?.nome ?? id;

    public IEnumerable<Pasta> Subpastas(string pai)
    {
        foreach (var p in pastas) if (p.pai == pai) yield return p;
    }

    public IEnumerable<Arquivo> ArquivosEm(string pasta)
    {
        foreach (var a in arquivos) if (a.pasta == pasta) yield return a;
    }

    /// <summary>"17/11/2026 07:48" → valor ordenável.</summary>
    public static long Ordem(string data)
    {
        if (string.IsNullOrEmpty(data) || data.Length < 10) return 0;
        long v = long.Parse(data.Substring(6, 4)) * 100000000L + long.Parse(data.Substring(3, 2)) * 1000000L + long.Parse(data.Substring(0, 2)) * 10000L;
        if (data.Length >= 16) v += long.Parse(data.Substring(11, 2)) * 100 + long.Parse(data.Substring(14, 2));
        return v;
    }
}
