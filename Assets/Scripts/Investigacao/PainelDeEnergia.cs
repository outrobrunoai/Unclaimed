using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Painel de energia da sala técnica (D04, puzzle P02 e apagão S1). Mirar e apertar E abre o painel: três
/// disjuntores com o consumo e a soma ao lado do limite (10). Para acender a luz de exame (4) é preciso
/// desligar a luz geral (3), porque a refrigeração (6) não pode ser desligada.
///
/// Quando Daniel tira o fusível da luz geral, o painel mostra o encaixe vazio; com o fusível reserva
/// ("fusivel_reserva" no Chaveiro, pego no depósito) o jogador o recoloca.
/// Abrir o painel coleta a evidência D04. O estado da rede entra no save por este componente.
/// Prefab de José; Bruno coloca na parede da sala técnica.
/// </summary>
public class PainelDeEnergia : InterfaceDeTela, ISalvavel
{
    public string tecla = "E";
    [Min(0.2f)] public float alcance = 1.6f;
    public string textoUsar = "Abrir painel de energia";
    public string idEvidencia = "D04";
    public string itemFusivel = "fusivel_reserva";

    [Header("Som")]
    public AudioSource fonte;
    public AudioClip somDisjuntor, somRecusado;

    public override float AlcanceDeInteracao => alcance;
    public override string Tecla => tecla;
    public override string Indicacao => textoUsar;
    public override void Interagir() => AbrirInterface();

    string mensagem = "";
    float mensagemAte, abertoEm;

    protected override void AoAbrir()
    {
        abertoEm = Time.unscaledTime;
        if (!string.IsNullOrEmpty(idEvidencia)) Caderno.Coletar(idEvidencia);
        mensagem = "";
    }

    public string Salvar() => RedeEletrica.Exportar();
    public void Carregar(string estado) => RedeEletrica.Importar(estado);

    void Avisar(string texto)
    {
        mensagem = texto;
        mensagemAte = Time.unscaledTime + 3.5f;
    }

    void Tocar(AudioClip c)
    {
        if (c == null) return;
        if (fonte != null) fonte.PlayOneShot(c);
        else AudioSource.PlayClipAtPoint(c, transform.position);
    }

    public void Acionar(string id)
    {
        RedeEletrica.Circuito c = RedeEletrica.Buscar(id);
        if (c == null) return;
        switch (RedeEletrica.Alternar(id))
        {
            case ResultadoDoCircuito.Ok:
                Tocar(somDisjuntor);
                Avisar(c.ligado ? c.nome + ": ligado." : c.nome + ": desligado.");
                break;
            case ResultadoDoCircuito.Sobrecarga:
                Tocar(somRecusado);
                Avisar($"O painel não aguenta: {RedeEletrica.Consumo} + {c.consumo} passa do limite de {RedeEletrica.Limite}.");
                break;
            case ResultadoDoCircuito.Essencial:
                Tocar(somRecusado);
                Avisar("A refrigeração precisa ficar sempre ligada.");
                break;
            case ResultadoDoCircuito.SemFusivel:
                Tocar(somRecusado);
                Avisar("Falta o fusível deste circuito.");
                break;
        }
    }

    public void Recolocar(string id)
    {
        if (!Chaveiro.Tem(itemFusivel)) { Avisar("Você precisa de um fusível reserva."); return; }
        if (RedeEletrica.ColocarFusivel(id))
        {
            Tocar(somDisjuntor);
            Avisar("Fusível no lugar.");
        }
    }

    protected override void Desenhar()
    {
        float s = Escala;
        var fundo = new Rect(0, 0, Screen.width, Screen.height);
        // Entrada curta: o fundo escurece e o quadro sobe um pouco até o lugar.
        float e = DesenhoUi.Suave((Time.unscaledTime - abertoEm) / 0.3f);
        DesenhoUi.Caixa(fundo, new Color(0.03f, 0.03f, 0.035f, 0.92f * e));
        DesenhoUi.Vinheta(e);

        float w = 900 * s, h = 640 * s;
        Rect caixa = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f + (1f - e) * 24f * s, w, h);
        DesenhoUi.Caixa(caixa, new Color(0.55f, 0.57f, 0.55f));
        DesenhoUi.Caixa(new Rect(caixa.x + 12 * s, caixa.y + 12 * s, w - 24 * s, h - 24 * s), new Color(0.42f, 0.44f, 0.43f));

        GUI.Label(new Rect(caixa.x + 40 * s, caixa.y + 30 * s, w - 80 * s, 40 * s), "QUADRO DE DISTRIBUIÇÃO — LIGAÇÃO PROVISÓRIA",
            DesenhoUi.Estilo(22, new Color(0.08f, 0.08f, 0.08f), true));
        int consumo = RedeEletrica.Consumo;
        Color corSoma = consumo >= RedeEletrica.Limite ? new Color(0.65f, 0.12f, 0.08f) : new Color(0.08f, 0.25f, 0.1f);
        GUI.Label(new Rect(caixa.x + 40 * s, caixa.y + 72 * s, w - 80 * s, 30 * s),
            $"Carga: {consumo} / {RedeEletrica.Limite} unidades", DesenhoUi.Estilo(20, corSoma, true));

        IReadOnlyList<RedeEletrica.Circuito> circuitos = RedeEletrica.Circuitos;
        float cw = (w - 80 * s - (circuitos.Count - 1) * 30 * s) / circuitos.Count;
        for (int i = 0; i < circuitos.Count; i++)
        {
            RedeEletrica.Circuito c = circuitos[i];
            Rect r = new Rect(caixa.x + 40 * s + i * (cw + 30 * s), caixa.y + 130 * s, cw, 380 * s);
            DesenhoUi.Caixa(r, new Color(0.2f, 0.21f, 0.22f));
            GUI.Label(new Rect(r.x + 14 * s, r.y + 14 * s, r.width - 28 * s, 60 * s), c.nome.ToUpper(),
                DesenhoUi.Estilo(17, new Color(0.92f, 0.92f, 0.88f), true));
            GUI.Label(new Rect(r.x + 14 * s, r.y + 70 * s, r.width - 28 * s, 28 * s), $"Consumo: {c.consumo}",
                DesenhoUi.Estilo(16, new Color(0.85f, 0.85f, 0.8f)));

            bool aceso = c.ligado && c.fusivel;
            Rect led = new Rect(r.x + r.width / 2f - 14 * s, r.y + 112 * s, 28 * s, 28 * s);
            DesenhoUi.Caixa(led, aceso ? new Color(0.3f, 0.95f, 0.35f) : new Color(0.25f, 0.06f, 0.05f));

            Rect alavanca = new Rect(r.x + r.width / 2f - 50 * s, r.y + 160 * s, 100 * s, 120 * s);
            DesenhoUi.Caixa(alavanca, new Color(0.08f, 0.08f, 0.09f));
            Rect pino = new Rect(alavanca.x + 12 * s, c.ligado ? alavanca.y + 10 * s : alavanca.yMax - 60 * s, alavanca.width - 24 * s, 50 * s);
            DesenhoUi.Caixa(pino, c.essencial ? new Color(0.75f, 0.15f, 0.1f) : new Color(0.85f, 0.85f, 0.82f));
            GUI.Label(new Rect(r.x, alavanca.yMax + 6 * s, r.width, 24 * s), c.ligado ? "LIGADO" : "DESLIGADO",
                DesenhoUi.Estilo(15, Color.white, true, TextAnchor.UpperCenter));
            if (GUI.Button(alavanca, GUIContent.none, GUIStyle.none)) Acionar(c.id);

            if (!c.fusivel)
            {
                Rect bf = new Rect(r.x + 14 * s, r.yMax - 56 * s, r.width - 28 * s, 42 * s);
                GUI.Label(new Rect(r.x + 14 * s, bf.y - 30 * s, r.width - 28 * s, 26 * s), "Encaixe do fusível vazio",
                    DesenhoUi.Estilo(14, new Color(1f, 0.6f, 0.45f), true));
                if (DesenhoUi.Botao(bf, "Colocar fusível", Chaveiro.Tem(itemFusivel), 15)) Recolocar(c.id);
            }
        }

        if (!string.IsNullOrEmpty(mensagem) && Time.unscaledTime < mensagemAte)
            GUI.Label(new Rect(caixa.x + 40 * s, caixa.yMax - 110 * s, w - 80 * s, 50 * s), mensagem,
                DesenhoUi.Estilo(18, new Color(0.08f, 0.08f, 0.08f), true));
        if (DesenhoUi.Botao(new Rect(caixa.xMax - 190 * s, caixa.yMax - 64 * s, 150 * s, 42 * s), "Fechar (Esc)"))
            FecharInterface();
    }
}
