using System;
using System.Collections.Generic;
using UnityEngine;

public enum ResultadoDoCircuito { Ok, Sobrecarga, SemFusivel, Essencial, Desconhecido }

/// <summary>
/// Estado da energia do prédio (puzzle P02 e apagão S1 de Daniel). O painel aguenta um limite de unidades;
/// cada circuito consome uma parte. Ligar um circuito que passaria do limite é recusado (o painel mostra a
/// soma). A refrigeração é essencial: não pode ser desligada. Sem fusível, o circuito não liga.
///
/// Valores do D04: limite 10; refrigeração 6, luz geral 3, luz de exame 4.
/// Consumidores (luzes do mapa) escutam AoMudar pelo id do circuito.
/// </summary>
public static class RedeEletrica
{
    public const string Refrigeracao = "refrigeracao", Geral = "geral", Exame = "exame";

    [Serializable]
    public class Circuito
    {
        public string id, nome;
        public int consumo;
        public bool ligado, essencial, fusivel = true;
    }

    [Serializable]
    class Dados { public List<Circuito> circuitos = new List<Circuito>(); }

    public static int Limite { get; private set; } = 10;
    static readonly List<Circuito> circuitos = new List<Circuito>();
    public static IReadOnlyList<Circuito> Circuitos => circuitos;

    /// <summary>Disparado quando algum circuito muda (id do circuito).</summary>
    public static event Action<string> AoMudar;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar()
    {
        AoMudar = null;
        Padrao();
    }

    static RedeEletrica() => Padrao();

    /// <summary>Estado do começo da noite: refrigeração e luz geral ligadas, luz de exame desligada.</summary>
    public static void Padrao()
    {
        Limite = 10;
        circuitos.Clear();
        circuitos.Add(new Circuito { id = Refrigeracao, nome = "Refrigeração (câmara fria)", consumo = 6, ligado = true, essencial = true });
        circuitos.Add(new Circuito { id = Geral, nome = "Luz geral", consumo = 3, ligado = true });
        circuitos.Add(new Circuito { id = Exame, nome = "Luz de exame (autópsia)", consumo = 4, ligado = false });
        Anunciar(null);
    }

    public static Circuito Buscar(string id)
    {
        foreach (Circuito c in circuitos) if (c.id == id) return c;
        return null;
    }

    public static bool Ligado(string id)
    {
        Circuito c = Buscar(id);
        return c != null && c.ligado && c.fusivel;
    }

    public static int Consumo
    {
        get
        {
            int total = 0;
            foreach (Circuito c in circuitos) if (c.ligado && c.fusivel) total += c.consumo;
            return total;
        }
    }

    public static ResultadoDoCircuito Alternar(string id)
    {
        Circuito c = Buscar(id);
        if (c == null) return ResultadoDoCircuito.Desconhecido;
        return Definir(id, !c.ligado);
    }

    public static ResultadoDoCircuito Definir(string id, bool ligar)
    {
        Circuito c = Buscar(id);
        if (c == null) return ResultadoDoCircuito.Desconhecido;
        if (c.ligado == ligar) return ResultadoDoCircuito.Ok;
        if (!ligar)
        {
            if (c.essencial) return ResultadoDoCircuito.Essencial;
            c.ligado = false;
            Anunciar(id);
            return ResultadoDoCircuito.Ok;
        }
        if (!c.fusivel) return ResultadoDoCircuito.SemFusivel;
        if (Consumo + c.consumo > Limite) return ResultadoDoCircuito.Sobrecarga;
        c.ligado = true;
        if (id == Exame) EstadoDaNoite.Marcar("luz_exame");
        Anunciar(id);
        return ResultadoDoCircuito.Ok;
    }

    /// <summary>Daniel tira o fusível (apagão S1). O circuito apaga, mas continua "ligado" na chave.</summary>
    public static bool RemoverFusivel(string id)
    {
        Circuito c = Buscar(id);
        if (c == null || !c.fusivel) return false;
        c.fusivel = false;
        EstadoDaNoite.Marcar("apagao");
        Anunciar(id);
        return true;
    }

    /// <summary>O jogador põe o fusível reserva. Se a soma passar do limite, o circuito volta desligado.</summary>
    public static bool ColocarFusivel(string id)
    {
        Circuito c = Buscar(id);
        if (c == null || c.fusivel) return false;
        c.fusivel = true;
        if (c.ligado && Consumo > Limite) c.ligado = false;
        EstadoDaNoite.Marcar("apagao_resolvido");
        Anunciar(id);
        return true;
    }

    public static string Exportar()
    {
        var d = new Dados();
        foreach (Circuito c in circuitos)
            d.circuitos.Add(new Circuito { id = c.id, nome = c.nome, consumo = c.consumo, ligado = c.ligado, essencial = c.essencial, fusivel = c.fusivel });
        return JsonUtility.ToJson(d);
    }

    public static void Importar(string json)
    {
        Padrao();
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            var d = JsonUtility.FromJson<Dados>(json);
            if (d?.circuitos == null) return;
            foreach (Circuito salvo in d.circuitos)
            {
                Circuito c = Buscar(salvo.id);
                if (c == null) continue;
                c.ligado = salvo.ligado || c.essencial;
                c.fusivel = salvo.fusivel;
            }
        }
        catch (ArgumentException) { Padrao(); }
        Anunciar(null);
    }

    static void Anunciar(string id) => AoMudar?.Invoke(id);
}
