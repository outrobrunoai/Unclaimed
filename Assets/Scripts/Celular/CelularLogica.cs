using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Regras do celular, sem interface (testável): chegada de mensagens, respostas e fila de envio de provas.
/// Tudo o que importa fica em fatos do EstadoDaNoite, então o save não precisa de nada próprio:
///   "msg:&lt;id&gt;"   mensagem recebida (ou mandada pelo jogador)
///   "env:&lt;id&gt;"   evidência enviada à redação ("enviou_algo" na primeira)
/// O envio só anda com sinal; sem sinal, a fila espera. Fechar o celular não cancela a fila.
/// </summary>
public static class CelularLogica
{
    public const string IdAudio = "D13";
    public const string FatoAudioCopiado = "audio_copiado";

    public class ItemNaFila
    {
        public string id;
        public float total, feito;
        public float Progresso => total <= 0f ? 1f : Mathf.Clamp01(feito / total);
    }

    static readonly List<ItemNaFila> fila = new List<ItemNaFila>();
    public static IReadOnlyList<ItemNaFila> Fila => fila;

    static DadosDoCelular dados;
    public static DadosDoCelular Dados
    {
        get => dados ?? DadosDoCelular.Padrao;
        set => dados = value;
    }

    /// <summary>Uma mensagem nova chegou (id da mensagem).</summary>
    public static event Action<DadosDoCelular.Mensagem> AoReceber;
    /// <summary>Uma evidência terminou de ser enviada.</summary>
    public static event Action<string> AoEnviar;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar()
    {
        fila.Clear();
        dados = null;
        AoReceber = null;
        AoEnviar = null;
    }

    public static void Limpar() => fila.Clear();

    static bool Condicao(string fato) => string.IsNullOrEmpty(fato) || EstadoDaNoite.Tem(fato);

    public static bool Recebida(DadosDoCelular.Mensagem m) => EstadoDaNoite.Tem("msg:" + m.id);

    /// <summary>Mensagens que já estavam no celular ao começar a noite (sem condição) entram sem precisar de sinal.</summary>
    public static void ReceberIniciais()
    {
        foreach (DadosDoCelular.Mensagem m in Dados.mensagens)
            if (m != null && string.IsNullOrEmpty(m.quando)) EstadoDaNoite.Marcar("msg:" + m.id);
    }

    /// <summary>Com sinal, entrega as mensagens cujas condições já valem. Devolve quantas chegaram.</summary>
    public static int ReceberPendentes(bool comSinal)
    {
        if (!comSinal) return 0;
        int n = 0;
        foreach (DadosDoCelular.Mensagem m in Dados.mensagens)
        {
            if (m == null || !Condicao(m.quando) || !EstadoDaNoite.Marcar("msg:" + m.id)) continue;
            n++;
            if (m.de != "eu") AoReceber?.Invoke(m);
        }
        return n;
    }

    /// <summary>Entrega uma mensagem pelo id, sem exigir sinal nem a condição "quando". Falso se não existe ou já foi recebida.</summary>
    public static bool Entregar(string idMensagem)
    {
        if (string.IsNullOrEmpty(idMensagem)) return false;
        foreach (DadosDoCelular.Mensagem m in Dados.mensagens)
        {
            if (m == null || m.id != idMensagem) continue;
            if (!EstadoDaNoite.Marcar("msg:" + m.id)) return false;
            if (m.de != "eu") AoReceber?.Invoke(m);
            return true;
        }
        return false;
    }

    public static IEnumerable<DadosDoCelular.Mensagem> Conversa(string contato)
    {
        foreach (DadosDoCelular.Mensagem m in Dados.mensagens)
            if (m != null && m.contato == contato && Recebida(m)) yield return m;
    }

    public static IEnumerable<DadosDoCelular.Resposta> RespostasDisponiveis(string contato)
    {
        foreach (DadosDoCelular.Resposta r in Dados.respostas)
            if (r != null && r.contato == contato && Condicao(r.quando) &&
                !(!string.IsNullOrEmpty(r.naoSe) && EstadoDaNoite.Tem(r.naoSe)) && !EstadoDaNoite.Tem("msg:" + r.id))
                yield return r;
    }

    /// <summary>O jogador manda uma resposta. Precisa de sinal. Marca o fato da resposta.</summary>
    public static bool Responder(DadosDoCelular.Resposta r, bool comSinal)
    {
        if (r == null || !comSinal || EstadoDaNoite.Tem("msg:" + r.id)) return false;
        EstadoDaNoite.Marcar("msg:" + r.id);
        if (!string.IsNullOrEmpty(r.marca))
        {
            EstadoDaNoite.Marcar(r.marca);
            if (!string.IsNullOrEmpty(r.grupo) && EstadoDaNoite.Marcar("primeiro:" + r.grupo))
                EstadoDaNoite.Marcar(r.marca + ":primeiro");
        }
        return true;
    }

    // ------------------------------------------------------------------ envio de provas

    public static bool Enviada(string id) => EstadoDaNoite.Tem("env:" + id);

    public static bool NaFila(string id)
    {
        foreach (ItemNaFila i in fila) if (i.id == id) return true;
        return false;
    }

    /// <summary>A evidência pode ir para a fila? O áudio só depois de copiado no computador.</summary>
    public static bool PodeEnviar(string id, out string motivo)
    {
        motivo = "";
        if (!Caderno.Coletou(id)) { motivo = "Ainda não encontrada."; return false; }
        if (Enviada(id)) { motivo = "Enviada."; return false; }
        if (NaFila(id)) { motivo = "Na fila."; return false; }
        if (id == IdAudio && !EstadoDaNoite.Tem(FatoAudioCopiado))
        {
            motivo = "Copie o áudio no computador do arquivo primeiro.";
            return false;
        }
        return true;
    }

    public static bool Enfileirar(string id)
    {
        if (!PodeEnviar(id, out _)) return false;
        Evidencia e = Caderno.Catalogo.Buscar(id);
        float segundos = e != null && e.tipo == "audio" ? Dados.envio.segundosPorAudio : Dados.envio.segundosPorDocumento;
        fila.Add(new ItemNaFila { id = id, total = Mathf.Max(0.1f, segundos) });
        return true;
    }

    /// <summary>Anda a fila (um item por vez). Sem sinal, nada anda.</summary>
    public static void Avancar(float dt, bool comSinal)
    {
        if (!comSinal || fila.Count == 0 || dt <= 0f) return;
        ItemNaFila atual = fila[0];
        atual.feito += dt;
        if (atual.feito < atual.total) return;
        fila.RemoveAt(0);
        EstadoDaNoite.Marcar("env:" + atual.id);
        EstadoDaNoite.Marcar("enviou_algo");
        AoEnviar?.Invoke(atual.id);
    }
}
