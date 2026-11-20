using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Conteúdo do celular (Resources/Historia/celular.json): contatos, mensagens e as respostas que o jogador pode
/// mandar. Os textos são do Bruno; a lógica (CelularLogica) só lê os campos.
///
/// Mensagem: chega quando o fato "quando" existe (vazio = já estava no celular ao começar) e o jogador tem
/// sinal. "de" = "eu" para as que o jogador mandou.
/// Resposta: botão que aparece na conversa quando "quando" existe e "naoSe" não existe; ao mandar, marca
/// "marca" (e as mensagens que dependem dele chegam depois).
/// </summary>
[Serializable]
public class DadosDoCelular
{
    [Serializable]
    public class Contato { public string id, nome; }

    [Serializable]
    public class Mensagem
    {
        public string id, contato, de, texto, quando;
    }

    [Serializable]
    public class Resposta
    {
        public string id, contato, rotulo, texto, quando, naoSe, marca;
        [Tooltip("Respostas do mesmo grupo disputam quem foi primeiro: a primeira também marca \"<marca>:primeiro\".")]
        public string grupo;
    }

    [Serializable]
    public class Envio
    {
        [Tooltip("Evidências que formam o conjunto da 784 (decide o final 'todas as provas').")]
        public string[] provasDa784;
        public float segundosPorDocumento = 5f, segundosPorAudio = 25f;
    }

    public Contato[] contatos = new Contato[0];
    public Mensagem[] mensagens = new Mensagem[0];
    public Resposta[] respostas = new Resposta[0];
    public Envio envio = new Envio();

    static DadosDoCelular padrao;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar() => padrao = null;

    public static DadosDoCelular Carregar(string json)
    {
        var d = JsonUtility.FromJson<DadosDoCelular>(json) ?? new DadosDoCelular();
        d.contatos = d.contatos ?? new Contato[0];
        d.mensagens = d.mensagens ?? new Mensagem[0];
        d.respostas = d.respostas ?? new Resposta[0];
        d.envio = d.envio ?? new Envio();
        d.envio.provasDa784 = d.envio.provasDa784 ?? new string[0];
        return d;
    }

    public static DadosDoCelular Padrao
    {
        get
        {
            if (padrao != null) return padrao;
            TextAsset t = Resources.Load<TextAsset>("Historia/celular");
            if (t == null) Debug.LogWarning("DadosDoCelular: Historia/celular.json não encontrado.");
            padrao = t != null ? Carregar(t.text) : new DadosDoCelular();
            return padrao;
        }
    }

    public Contato BuscarContato(string id)
    {
        foreach (Contato c in contatos) if (c.id == id) return c;
        return null;
    }

    public List<string> Problemas()
    {
        var problemas = new List<string>();
        var ids = new HashSet<string>();
        foreach (Mensagem m in mensagens)
        {
            if (m == null || string.IsNullOrEmpty(m.id) || !ids.Add(m.id)) problemas.Add("Mensagem sem id ou repetida: " + m?.id);
            else if (BuscarContato(m.contato) == null) problemas.Add("Mensagem com contato desconhecido: " + m.id);
        }
        foreach (Resposta r in respostas)
        {
            if (r == null || string.IsNullOrEmpty(r.id) || !ids.Add(r.id)) problemas.Add("Resposta sem id ou repetida: " + r?.id);
            else if (BuscarContato(r.contato) == null) problemas.Add("Resposta com contato desconhecido: " + r.id);
        }
        return problemas;
    }
}
