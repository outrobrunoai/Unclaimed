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
    }

    [Serializable]
    public class Textos
    {
        public string titulo = "Fim";
        public string incompleto, provasNenhuma, provasAlgumas, provasTodas;
        public string gravadorComJogador, gravadorEntregue, gravadorAbandonado, copiaEnviada;
        public string samuelNaoLocalizado, samuelIrene, samuelJornal, ajudaPedida;
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
        r.copiaEnviada = tem("env:" + CelularLogica.IdAudio);
        r.ajudaPedida = tem("ajuda_pedida");

        r.samuel = tem("samuel:irene:primeiro") ? CasoSamuel.IrenePrimeiro
            : tem("samuel:jornal:primeiro") ? CasoSamuel.JornalPrimeiro : CasoSamuel.NaoLocalizado;
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
        Add(r.gravador == DestinoDoGravador.ComJogador ? t.gravadorComJogador
            : r.gravador == DestinoDoGravador.Entregue ? t.gravadorEntregue : t.gravadorAbandonado);
        if (r.copiaEnviada && r.gravador != DestinoDoGravador.ComJogador) Add(t.copiaEnviada);
        if (r.ajudaPedida) Add(t.ajudaPedida);
        Add(r.samuel == CasoSamuel.IrenePrimeiro ? t.samuelIrene : r.samuel == CasoSamuel.JornalPrimeiro ? t.samuelJornal : t.samuelNaoLocalizado);
        return p;
    }

    public static Textos CarregarTextos()
    {
        TextAsset a = Resources.Load<TextAsset>("Historia/finais");
        if (a == null) { Debug.LogWarning("Epilogo: Historia/finais.json não encontrado."); return new Textos(); }
        return JsonUtility.FromJson<Textos>(a.text) ?? new Textos();
    }
}
