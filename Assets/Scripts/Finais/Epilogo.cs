using System;
using System.Collections.Generic;
using UnityEngine;

public enum ProvasEnviadas { Nenhuma, Algumas, Todas }
public enum DestinoDoGravador { ComJogador, Entregue, Abandonado }
public enum CasoSamuel { NaoLocalizado, IrenePrimeiro, JornalPrimeiro }

/// <summary>
/// Monta o final pelo que saiu do prédio (seção 8 da história):
///   provas: nenhuma / algumas / todas as da 784 enviadas;
///   gravador: com o jogador / entregue a Daniel / deixado para trás (a cópia enviada vale mesmo sem ele);
///   Samuel: não localizado / Irene primeiro / jornal primeiro.
/// Sair antes da parte 3 (Daniel ainda não está na fase Obstrução) dá o final incompleto.
/// Textos em Resources/Historia/finais.json (do Bruno).
/// </summary>
public static class Epilogo
{
    public struct Resultado
    {
        public bool incompleto;
        public ProvasEnviadas provas;
        public DestinoDoGravador gravador;
        public bool copiaEnviada, ajudaPedida;
        public CasoSamuel samuel;
        public bool conheceGravador, conhece041;
    }

    [Serializable]
    public class Textos
    {
        public string titulo = "Fim";
        public string subtitulo = "Necrotério Municipal, 18 de novembro de 2026";
        public string incompleto, provasNenhuma, provasAlgumas, provasTodas;
        public string gravadorComJogador, gravadorEntregue, gravadorAbandonado, copiaEnviada;
        public string samuelNaoLocalizado, samuelIrene, samuelJornal, ajudaPedida;
        public string capturaTitulo = "Ele me alcançou.";
        public string capturaTexto = "Daniel não queria uma quarta morte. Eu não deixei outra saída para ele.";
        public string saidaNada = "Vim por causa do 083 e ainda nem vi o corpo.";
        public string saidaAutopsia = "O Léo não está na gaveta que a ficha diz. Alguém tirou ele de lá.";
        public string saidaMesa = "Tem alguém naquela mesa com a etiqueta do Léo. Ainda não sei o que isso quer dizer.";
        public string saidaTroca = "O corpo na mesa não é o Léo. Se eu sair agora, ninguém vai saber disso.";
        public string saidaProvas = "Já tenho o bastante para a redação. Ficar mais é arriscar o que eu tenho.";
        public string saidaVoltar = "Voltar lá dentro";
        public string saidaIr = "Ir embora";
    }

    public static Resultado Calcular(Func<string, bool> tem, FaseDeDaniel fase, IEnumerable<string> provasDa784)
    {
        var r = new Resultado { incompleto = fase < FaseDeDaniel.Obstrucao };

        int enviadas = 0, total = 0;
        foreach (string id in provasDa784)
        {
            total++;
            if (tem("env:" + id)) enviadas++;
        }
        r.provas = total > 0 && enviadas == total ? ProvasEnviadas.Todas : tem("enviou_algo") ? ProvasEnviadas.Algumas : ProvasEnviadas.Nenhuma;

        r.gravador = !tem("gravador_pego") ? DestinoDoGravador.Abandonado
            : tem("gravador_entregue") ? DestinoDoGravador.Entregue : DestinoDoGravador.ComJogador;
        r.conheceGravador = tem("gravador_pego") || tem("ev:D09") || tem("ev:D10") || tem("ev:D13");
        r.copiaEnviada = tem("env:" + CelularLogica.IdAudio);
        r.ajudaPedida = tem("ajuda_pedida");

        r.samuel = tem("samuel:irene:primeiro") ? CasoSamuel.IrenePrimeiro
            : tem("samuel:jornal:primeiro") ? CasoSamuel.JornalPrimeiro : CasoSamuel.NaoLocalizado;
        for (int i = 1; i <= 8; i++)
            if (tem("ev:H0" + i)) { r.conhece041 = true; break; }
        return r;
    }

    public static Resultado DaNoiteAtual() =>
        Calcular(EstadoDaNoite.Tem, EstadoDaNoite.Fase, DadosDoCelular.Padrao.envio.provasDa784);

    public static List<string> Paragrafos(Resultado r, Textos t)
    {
        var p = new List<string>();
        void Add(string s) { if (!string.IsNullOrWhiteSpace(s)) p.Add(s); }
        if (r.incompleto) Add(t.incompleto);
        Add(r.provas == ProvasEnviadas.Todas ? t.provasTodas : r.provas == ProvasEnviadas.Algumas ? t.provasAlgumas : t.provasNenhuma);
        if (r.gravador == DestinoDoGravador.ComJogador) Add(t.gravadorComJogador);
        else if (r.gravador == DestinoDoGravador.Entregue) Add(t.gravadorEntregue);
        else if (r.conheceGravador) Add(t.gravadorAbandonado);
        if (r.copiaEnviada && r.gravador != DestinoDoGravador.ComJogador) Add(t.copiaEnviada);
        if (r.ajudaPedida) Add(t.ajudaPedida);
        if (r.samuel == CasoSamuel.IrenePrimeiro) Add(t.samuelIrene);
        else if (r.samuel == CasoSamuel.JornalPrimeiro) Add(t.samuelJornal);
        else if (r.conhece041) Add(t.samuelNaoLocalizado);
        return p;
    }

    public static string FraseDeSaida(Func<string, bool> tem, Textos t)
    {
        if (t == null) t = new Textos();
        if (tem == null) return t.saidaNada;
        if (tem("gravador_pego") || tem("enviou_algo")) return t.saidaProvas;
        if (tem("con:troca")) return t.saidaTroca;
        if (tem("ev:F01")) return t.saidaMesa;
        if (tem("ev:D02")) return t.saidaAutopsia;
        return t.saidaNada;
    }

    public static Textos CarregarTextos()
    {
        TextAsset a = Resources.Load<TextAsset>("Historia/finais");
        if (a == null) { Debug.LogWarning("Epilogo: Historia/finais.json não encontrado."); return new Textos(); }
        return JsonUtility.FromJson<Textos>(a.text) ?? new Textos();
    }
}
