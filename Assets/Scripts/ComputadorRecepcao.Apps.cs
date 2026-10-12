using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Aplicativos da estação. Cada um desenha no espaço (em pixels) do corpo da sua janela; o estado da sessão
// (conversa aberta, lidas, pasta, histórico, arquivos abertos, mês da agenda) fica nos campos abaixo.
public partial class ComputadorRecepcao
{
    // ================================================================== Correio

    readonly HashSet<string> lidas = new HashSet<string>();
    string conversaSel;
    List<EstacaoDados.Conversa> conversasOrdenadas;

    public string ConversaSelecionada => conversaSel;
    public bool Lida(string mensagem) => lidas.Contains(mensagem);

    List<EstacaoDados.Conversa> Conversas =>
        conversasOrdenadas ??= dados.conversas.OrderByDescending(c => c.mensagens.Max(m => EstacaoDados.Ordem(m.data))).ToList();

    public int NaoLidas()
    {
        if (dados == null) return 0;
        int n = 0;
        foreach (var c in dados.conversas) foreach (var m in c.mensagens) if (!lidas.Contains(m.id)) n++;
        return n;
    }

    bool ConversaNaoLida(EstacaoDados.Conversa c) => c.mensagens.Any(m => !lidas.Contains(m.id));

    public void AbrirConversa(string id)
    {
        var c = dados.conversas.FirstOrDefault(x => x.id == id);
        if (c == null) return;
        conversaSel = id;
        foreach (var m in c.mensagens) lidas.Add(m.id);
        var r = Rol("correio-leitura");
        r.alvo = r.pos = 0f;
    }

    string Participantes(EstacaoDados.Conversa c)
    {
        var nomes = new List<string>();
        foreach (var m in c.mensagens)
        {
            string n = m.de == "nina" ? "eu" : dados.NomeDe(m.de);
            if (!nomes.Contains(n)) nomes.Add(n);
        }
        nomes.Sort((a, b) => a == "eu" ? 1 : b == "eu" ? -1 : 0);
        return string.Join(", ", nomes);
    }

    string DataCurta(string data)
    {
        if (string.IsNullOrEmpty(data) || data.Length < 16) return data;
        return data.StartsWith(dados.estacao.data) ? data.Substring(11, 5) : data.Substring(0, 5);
    }

    static string PrimeiraLinha(string t)
    {
        if (string.IsNullOrEmpty(t)) return "";
        foreach (var l in t.Split('\n')) { string x = l.Trim(); if (x.Length > 0 && !x.EndsWith(",") && x != "Bom dia.") return x; }
        return t.Trim();
    }

    void AppCorreio(float w, float h)
    {
        float bar = 44f * s;
        Caixa(new Rect(0, 0, w, bar), CFerramentas);
        Linha(0, bar - s, w, CBorda);
        Icone(new Rect(14f * s, 11f * s, 22f * s, 22f * s), "correio_24");
        int nl = NaoLidas();
        Texto(new Rect(44f * s, 0, 400f * s, bar), "Caixa de entrada" + (nl > 0 ? $"  ·  {nl} não lida" + (nl > 1 ? "s" : "") : ""),
            Estilo(16, CTexto, Peso.Negrito, false, TextAnchor.MiddleLeft));
        Texto(new Rect(w - 420f * s, 0, 400f * s, bar), $"{dados.estacao.usuario} · Correio interno",
            Estilo(13, CSecundario, Peso.Normal, false, TextAnchor.MiddleRight));

        // Lista de conversas
        float lw = Mathf.Min(430f * s, w * 0.4f), item = 84f * s;
        Rect lista = new Rect(0, bar, lw, h - bar);
        Caixa(lista, CConteudo);
        var cs = Conversas;
        float reserva = ComecarRolagem("correio-lista", lista, cs.Count * item);
        for (int i = 0; i < cs.Count; i++)
        {
            var c = cs[i];
            var ultima = c.mensagens[c.mensagens.Length - 1];
            Rect it = new Rect(0, i * item, lw - reserva, item);
            bool sel = c.id == conversaSel, novo = ConversaNaoLida(c), sobre = Sobre(it);
            Caixa(it, sel ? (Foco == App.Correio ? CSelecao : CSelecaoInativa) : sobre ? CHover : Color.clear);
            if (novo) Caixa(new Rect(0, it.y, 4f * s, it.height), CAcento);
            float x = 18f * s, iw = it.width - 30f * s;
            Texto(new Rect(it.xMax - 92f * s, it.y + 10f * s, 80f * s, 20f * s), DataCurta(ultima.data), Estilo(13, novo ? CAcento : CSecundario, novo ? Peso.Negrito : Peso.Normal, false, TextAnchor.UpperRight));
            string quem = Participantes(c) + (c.mensagens.Length > 1 ? $"  ({c.mensagens.Length})" : "");
            Rotulo(new Rect(x, it.y + 9f * s, iw - 90f * s, 22f * s), quem, Estilo(15, CTexto, novo ? Peso.Negrito : Peso.Normal));
            Rotulo(new Rect(x, it.y + 32f * s, iw, 22f * s), c.assunto, Estilo(15, novo ? CTexto : Hex("#2D3A47"), novo ? Peso.Negrito : Peso.Normal));
            bool anexo = c.mensagens.Any(m => m.anexos != null && m.anexos.Length > 0);
            Rotulo(new Rect(x, it.y + 56f * s, iw - (anexo ? 26f * s : 0), 20f * s), PrimeiraLinha(ultima.corpo), Estilo(13, CSecundario));
            if (anexo) Icone(new Rect(it.xMax - 34f * s, it.y + 55f * s, 18f * s, 18f * s), "anexo_24");
            Linha(0, it.yMax - s, it.width, Hex("#E3E7EC"));
            Marcar("conversa:" + c.id, it);
            if (Clique(it)) AbrirConversa(c.id);
        }
        TerminarRolagem();
        Caixa(new Rect(lw, bar, Mathf.Max(1f, s), h - bar), CBorda);

        // Conversa
        Rect leitura = new Rect(lw + s, bar, w - lw - s, h - bar);
        Caixa(leitura, Hex("#F6F7F9"));
        var conv = cs.FirstOrDefault(x => x.id == conversaSel);
        if (conv == null)
        {
            Texto(leitura, "Selecione uma conversa para ler.", Estilo(15, CFraco, Peso.Normal, false, TextAnchor.MiddleCenter));
            return;
        }
        float pad = 28f * s, cw = leitura.width - 2 * pad - 14f * s;
        float alt = MedirConversa(conv, cw);
        float res = ComecarRolagem("correio-leitura", leitura, alt + pad * 2);
        DesenharConversa(conv, pad, pad, leitura.width - 2 * pad - res);
        TerminarRolagem();
    }

    float MedirConversa(EstacaoDados.Conversa c, float cw) => DesenharConversa(c, 0, 0, cw, false);

    float DesenharConversa(EstacaoDados.Conversa c, float x, float y0, float cw, bool desenhar = true)
    {
        float y = y0;
        var sAssunto = Estilo(22, CTexto, Peso.Negrito, true);
        float ha = Altura(c.assunto, sAssunto, cw);
        if (desenhar) Texto(new Rect(x, y, cw, ha), c.assunto, sAssunto);
        y += ha + 4f * s;
        var sInfo = Estilo(13, CSecundario, Peso.Normal, true);
        string info = $"{c.mensagens.Length} mensage{(c.mensagens.Length > 1 ? "ns" : "m")} · {Participantes(c)}";
        float hi = Altura(info, sInfo, cw);
        if (desenhar) Texto(new Rect(x, y, cw, hi), info, sInfo);
        y += hi + 16f * s;

        var sCorpo = Estilo(16, CTexto, Peso.Normal, true);
        var sDest = Estilo(13, CSecundario, Peso.Normal, true);
        foreach (var m in c.mensagens)
        {
            float pad = 18f * s, iw = cw - 2 * pad;
            string dest = "para " + string.Join(", ", (m.para ?? new string[0]).Select(dados.NomeDe)) +
                          (m.cc != null && m.cc.Length > 0 ? "  ·  cc " + string.Join(", ", m.cc.Select(dados.NomeDe)) : "");
            float hd = Altura(dest, sDest, iw - 56f * s - 140f * s);
            float hc = Altura(m.corpo, sCorpo, iw);
            int na = m.anexos?.Length ?? 0;
            float hcab = Mathf.Max(44f * s, 24f * s + hd);
            float altura = pad + hcab + 14f * s + hc + (na > 0 ? 16f * s + 40f * s : 0) + pad;
            if (desenhar)
            {
                Rect card = new Rect(x, y, cw, altura);
                Caixa(card, CConteudo, 5f * s);
                Contorno(card, Hex("#D5DBE2"), Mathf.Max(1f, s), 5f * s);
                Rect av = new Rect(x + pad, y + pad, 40f * s, 40f * s);
                Caixa(av, CorDe(m.de), 20f * s);
                var p = dados.PessoaPor(m.de);
                Texto(av, Iniciais(p?.nome ?? m.de), Estilo(15, Color.white, Peso.Negrito, false, TextAnchor.MiddleCenter));
                float tx = av.xMax + 12f * s, tw = iw - 52f * s - 150f * s;
                string nome = p?.nome ?? m.de;
                var sNome = Estilo(16, CTexto, Peso.Negrito);
                float nw = Mathf.Min(tw, sNome.CalcSize(new GUIContent(nome)).x);
                Texto(new Rect(tx, y + pad, nw + 4f * s, 22f * s), nome, sNome);
                Rotulo(new Rect(tx + nw + 8f * s, y + pad + 2f * s, Mathf.Max(0, tw - nw - 8f * s), 20f * s), p != null ? $"<{p.endereco}>" : "", Estilo(13, CFraco));
                Texto(new Rect(tx, y + pad + 24f * s, tw + 140f * s - 150f * s, hd), dest, sDest);
                Texto(new Rect(card.xMax - pad - 150f * s, y + pad + 2f * s, 150f * s, 20f * s), m.data, Estilo(13, CSecundario, Peso.Normal, false, TextAnchor.UpperRight));
                float yc = y + pad + hcab + 6f * s;
                Linha(x + pad, yc, iw, Hex("#E6EAEE"));
                Texto(new Rect(x + pad, yc + 8f * s, iw, hc), m.corpo, sCorpo);
                if (na > 0)
                {
                    float ax = x + pad, ay = yc + 8f * s + hc + 16f * s;
                    foreach (string id in m.anexos)
                    {
                        var arq = dados.ArquivoPor(id);
                        if (arq == null) continue;
                        var sAnexo = Estilo(14, CTexto);
                        float aw = Mathf.Min(iw, sAnexo.CalcSize(new GUIContent(arq.nome)).x + 64f * s);
                        Rect chip = new Rect(ax, ay, aw, 40f * s);
                        Marcar("anexo:" + id, chip);
                        if (Botao(chip, Hex("#E2EAF3"), Hex("#F0F3F7"), 4f)) AbrirArquivo(arq, App.Correio);
                        Contorno(chip, Hex("#C9D2DC"), Mathf.Max(1f, s), 4f * s);
                        Icone(new Rect(chip.x + 10f * s, chip.y + 8f * s, 24f * s, 24f * s), IconeDe(arq) + "_24");
                        Texto(new Rect(chip.x + 42f * s, chip.y, chip.width - 46f * s, chip.height), arq.nome, Estilo(14, CAcento, Peso.Normal, false, TextAnchor.MiddleLeft));
                        ax += aw + 8f * s;
                    }
                }
            }
            y += altura + 12f * s;
        }
        return y - y0;
    }

    void TecladoCorreio()
    {
        var cs = Conversas;
        int i = cs.FindIndex(c => c.id == conversaSel);
        int novo = TeclaUi("baixo") ? Mathf.Min(cs.Count - 1, i + 1) : TeclaUi("cima") ? Mathf.Max(0, i < 0 ? 0 : i - 1) : i;
        if (novo != i && novo >= 0)
        {
            AbrirConversa(cs[novo].id);
            float item = 84f * s;
            MostrarFaixa("correio-lista", novo * item, (novo + 1) * item, (JanelaDe(App.Correio).area.height - TituloAltura - 44f) * s);
        }
    }

    // ================================================================== Arquivos

    string pastaAtual = "docs";
    readonly Stack<string> historico = new Stack<string>();
    string itemSel;   // "p:id" ou "a:id"

    public string PastaAtual => pastaAtual;
    public int Historico => historico.Count;

    string CaminhoTexto(string pasta)
    {
        var partes = new List<string>();
        for (var p = dados.PastaPor(pasta); p != null; p = dados.PastaPor(p.pai)) partes.Insert(0, p.nome);
        return string.Join(" › ", partes);
    }

    public void IrPara(string pasta, bool guardar = true)
    {
        if (pasta == pastaAtual || dados.PastaPor(pasta) == null) return;
        if (guardar) historico.Push(pastaAtual);
        pastaAtual = pasta;
        itemSel = null;
        var r = Rol("arquivos-lista");
        r.alvo = r.pos = 0f;
    }

    public void Voltar()
    {
        if (historico.Count > 0) IrPara(historico.Pop(), false);
    }

    public void Acima()
    {
        var p = dados.PastaPor(pastaAtual);
        if (p != null && !string.IsNullOrEmpty(p.pai)) IrPara(p.pai);
    }

    List<(string chave, string nome, string tipo, string modificado, string icone)> Itens(string pasta)
    {
        var l = new List<(string, string, string, string, string)>();
        foreach (var p in dados.Subpastas(pasta).OrderBy(x => x.nome)) l.Add(("p:" + p.id, p.nome, "Pasta", "", "pasta"));
        foreach (var a in dados.ArquivosEm(pasta).OrderBy(x => x.nome)) l.Add(("a:" + a.id, a.nome, TipoDe(a), a.modificado, IconeDe(a)));
        return l;
    }

    static string TipoDe(EstacaoDados.Arquivo a)
    {
        switch (a.tipo)
        {
            case "planilha": return "Planilha";
            case "nota": return "Texto simples";
            case "imagem": return "Imagem JPEG";
            default: return "Documento de texto";
        }
    }

    static string IconeDe(EstacaoDados.Arquivo a)
    {
        switch (a.tipo)
        {
            case "planilha": return "planilha";
            case "nota": return "nota";
            case "imagem": return "imagem";
            default: return "doc";
        }
    }

    public void AbrirItem(string chave)
    {
        if (chave == null) return;
        if (chave.StartsWith("p:")) IrPara(chave.Substring(2));
        else AbrirArquivo(dados.ArquivoPor(chave.Substring(2)), App.Arquivos);
    }

    void AppArquivos(float w, float h)
    {
        float bar = 46f * s;
        Caixa(new Rect(0, 0, w, bar), CFerramentas);
        Linha(0, bar - s, w, CBorda);
        bool podeVoltar = historico.Count > 0, podeSubir = !string.IsNullOrEmpty(dados.PastaPor(pastaAtual)?.pai);
        Rect bV = new Rect(10f * s, 7f * s, 36f * s, 32f * s), bA = new Rect(50f * s, 7f * s, 36f * s, 32f * s);
        Marcar("arq-voltar", bV);
        Marcar("arq-acima", bA);
        if (podeVoltar ? Botao(bV, Hex("#D9DFE6")) : false) Voltar();
        Icone(Centro(bV, 20f * s), "g_back", podeVoltar ? CTexto : Hex("#B0B8C2"));
        if (podeSubir ? Botao(bA, Hex("#D9DFE6")) : false) Acima();
        Icone(Centro(bA, 20f * s), "g_up", podeSubir ? CTexto : Hex("#B0B8C2"));
        // Caminho (cada parte leva àquela pasta)
        Rect cam = new Rect(98f * s, 8f * s, w - 110f * s, 30f * s);
        Caixa(cam, CConteudo, 3f * s);
        Contorno(cam, CBorda, Mathf.Max(1f, s), 3f * s);
        var partes = new List<EstacaoDados.Pasta>();
        for (var p = dados.PastaPor(pastaAtual); p != null; p = dados.PastaPor(p.pai)) partes.Insert(0, p);
        float cx = cam.x + 8f * s;
        for (int i = 0; i < partes.Count; i++)
        {
            var sP = Estilo(14, i == partes.Count - 1 ? CTexto : CAcento, i == partes.Count - 1 ? Peso.Negrito : Peso.Normal, false, TextAnchor.MiddleCenter);
            float pw = sP.CalcSize(new GUIContent(partes[i].nome)).x + 12f * s;
            Rect seg = new Rect(cx, cam.y + 3f * s, pw, cam.height - 6f * s);
            Marcar("caminho:" + partes[i].id, seg);
            if (i < partes.Count - 1 && Botao(seg, CHover)) IrPara(partes[i].id);
            Texto(seg, partes[i].nome, sP);
            cx += pw;
            if (i < partes.Count - 1)
            {
                Icone(new Rect(cx, cam.y + 7f * s, 16f * s, 16f * s), "g_chevron", CFraco);
                cx += 18f * s;
            }
        }

        // Locais
        float sw = 210f * s;
        Rect lado = new Rect(0, bar, sw, h - bar);
        Caixa(lado, Hex("#EDF0F3"));
        Caixa(new Rect(sw, bar, Mathf.Max(1f, s), h - bar), CBorda);
        Texto(new Rect(16f * s, bar + 12f * s, sw, 20f * s), "LOCAIS", Estilo(12, CFraco, Peso.Negrito));
        string raiz = pastaAtual;
        for (var p = dados.PastaPor(pastaAtual); p != null && !string.IsNullOrEmpty(p.pai); p = dados.PastaPor(p.pai)) raiz = p.pai;
        float ly = bar + 38f * s;
        foreach (var p in dados.Subpastas(""))
        {
            Rect it = new Rect(6f * s, ly, sw - 12f * s, 34f * s);
            bool atual = p.id == raiz;
            Marcar("local:" + p.id, it);
            if (Botao(it, CHover, atual ? CSelecao : Color.clear)) { if (p.id != pastaAtual) IrPara(p.id); }
            Icone(new Rect(it.x + 10f * s, it.y + 6f * s, 22f * s, 22f * s), p.id == "imagens" ? "imagem_24" : p.id == "notas" ? "nota_24" : "pasta_24");
            Texto(new Rect(it.x + 40f * s, it.y, it.width - 44f * s, it.height), p.nome, Estilo(15, CTexto, atual ? Peso.Negrito : Peso.Normal, false, TextAnchor.MiddleLeft));
            ly += 36f * s;
        }

        // Lista
        float x0 = sw + s, status = 28f * s, cab = 30f * s, linha = 38f * s;
        float lw = w - x0;
        Caixa(new Rect(x0, bar, lw, h - bar), CConteudo);
        float cNome = lw * 0.56f, cTipo = lw * 0.2f;
        Caixa(new Rect(x0, bar, lw, cab), Hex("#F4F6F8"));
        Linha(x0, bar + cab - s, lw, CBorda);
        var sCab = Estilo(13, CSecundario, Peso.Negrito, false, TextAnchor.MiddleLeft);
        Texto(new Rect(x0 + 44f * s, bar, cNome, cab), "Nome", sCab);
        Texto(new Rect(x0 + cNome, bar, cTipo, cab), "Tipo", sCab);
        Texto(new Rect(x0 + cNome + cTipo, bar, lw - cNome - cTipo, cab), "Modificado", sCab);
        var itens = Itens(pastaAtual);
        Rect vista = new Rect(x0, bar + cab, lw, h - bar - cab - status);
        float res = ComecarRolagem("arquivos-lista", vista, itens.Count * linha);
        if (itens.Count == 0) Texto(new Rect(0, 30f * s, lw, 30f * s), "Esta pasta está vazia.", Estilo(15, CFraco, Peso.Normal, false, TextAnchor.UpperCenter));
        for (int i = 0; i < itens.Count; i++)
        {
            var (chave, nome, tipo, mod, icone) = itens[i];
            Rect it = new Rect(0, i * linha, lw - res, linha);
            bool sel = itemSel == chave, sobre = Sobre(it);
            Marcar("item:" + chave, it);
            Caixa(it, sel ? (Foco == App.Arquivos ? CSelecao : CSelecaoInativa) : sobre ? CHover : Color.clear);
            Icone(new Rect(14f * s, it.y + 7f * s, 24f * s, 24f * s), icone + "_24");
            Rotulo(new Rect(44f * s, it.y, cNome - 50f * s, linha), nome, Estilo(15, CTexto, Peso.Normal, false, TextAnchor.MiddleLeft));
            Rotulo(new Rect(cNome, it.y, cTipo - 8f * s, linha), tipo, Estilo(14, CSecundario, Peso.Normal, false, TextAnchor.MiddleLeft));
            Rotulo(new Rect(cNome + cTipo, it.y, lw - cNome - cTipo - res - 8f * s, linha), mod, Estilo(14, CSecundario, Peso.Normal, false, TextAnchor.MiddleLeft));
            if (Clique(it))
            {
                itemSel = chave;
                if (cliques >= 2) { AbrirItem(chave); break; }
            }
        }
        TerminarRolagem();
        Rect st = new Rect(x0, h - status, lw, status);
        Caixa(st, Hex("#F4F6F8"));
        Linha(x0, st.y, lw, CBorda);
        string selNome = itens.FirstOrDefault(t => t.chave == itemSel).nome;
        Texto(new Rect(st.x + 12f * s, st.y, lw - 24f * s, status), $"{itens.Count} {(itens.Count == 1 ? "item" : "itens")}" +
            (selNome != null ? $"  ·  selecionado: {selNome}" : "  ·  clique duplo para abrir"), Estilo(13, CSecundario, Peso.Normal, false, TextAnchor.MiddleLeft));
    }

    void TecladoArquivos()
    {
        if (TeclaUi("voltar")) { Voltar(); return; }
        var itens = Itens(pastaAtual);
        if (itens.Count == 0) return;
        int i = itens.FindIndex(t => t.chave == itemSel);
        if (TeclaUi("baixo")) i = Mathf.Min(itens.Count - 1, i + 1);
        if (TeclaUi("cima")) i = Mathf.Max(0, i - 1);
        if (i >= 0)
        {
            itemSel = itens[i].chave;
            float linha = 38f * s;
            MostrarFaixa("arquivos-lista", i * linha, (i + 1) * linha, (JanelaDe(App.Arquivos).area.height - TituloAltura - 46f - 30f - 28f) * s);
        }
        if (TeclaUi("enter") && itemSel != null) AbrirItem(itemSel);
    }

    // ================================================================== abrir arquivos

    EstacaoDados.Arquivo leitorArquivo, notaAtual, imagemAtual;
    App origemLeitor = App.Arquivos, origemImagem = App.Arquivos;

    public EstacaoDados.Arquivo LeitorArquivo => leitorArquivo;
    public EstacaoDados.Arquivo NotaAtual => notaAtual;
    public EstacaoDados.Arquivo ImagemAtual => imagemAtual;

    public void AbrirArquivo(EstacaoDados.Arquivo a, App origem)
    {
        if (a == null) return;
        switch (a.tipo)
        {
            case "nota":
                notaAtual = a;
                ZerarRolagem("notas-texto");
                AbrirApp(App.Notas);
                break;
            case "imagem":
                imagemAtual = a;
                origemImagem = origem;
                AbrirApp(App.Imagens);
                break;
            default:
                leitorArquivo = a;
                origemLeitor = origem;
                ZerarRolagem("leitor");
                AbrirApp(App.Leitor);
                break;
        }
    }

    void ZerarRolagem(string id)
    {
        var r = Rol(id);
        r.alvo = r.pos = 0f;
    }

    /// <summary>Barra "voltar" dos visualizadores: leva à janela de onde o arquivo foi aberto.</summary>
    bool BotaoVoltarPara(Rect r, App origem)
    {
        string t = origem == App.Correio ? "Voltar ao Correio" : "Voltar a Arquivos";
        var e = Estilo(14, CTexto, Peso.Normal, false, TextAnchor.MiddleLeft);
        r.width = e.CalcSize(new GUIContent(t)).x + 40f * s;
        Marcar("voltar-para:" + origem, r);
        bool ok = Botao(r, Hex("#D9DFE6"), Hex("#E4E8ED"), 4f);
        Contorno(r, Hex("#B5BEC8"), Mathf.Max(1f, s), 4f * s);
        Icone(new Rect(r.x + 8f * s, r.y + (r.height - 18f * s) / 2, 18f * s, 18f * s), "g_back", CTexto);
        Texto(new Rect(r.x + 30f * s, r.y, r.width - 30f * s, r.height), t, e);
        if (ok) AbrirApp(origem);
        return ok;
    }

    // ================================================================== Leitor de documentos

    void AppLeitor(float w, float h)
    {
        var a = leitorArquivo;
        float bar = 46f * s;
        Caixa(new Rect(0, 0, w, bar), CFerramentas);
        Linha(0, bar - s, w, CBorda);
        if (a == null) return;
        Rect bVoltar = new Rect(10f * s, 8f * s, 0, 30f * s);
        BotaoVoltarPara(bVoltar, origemLeitor);
        var sInfo = Estilo(13, CSecundario, Peso.Normal, false, TextAnchor.MiddleRight);
        Rotulo(new Rect(w * 0.35f, 0, w * 0.65f - 14f * s, bar), $"{TipoDe(a)} · modificado em {a.modificado} por {a.autor}", sInfo);

        Rect area = new Rect(0, bar, w, h - bar);
        Caixa(area, CPapelFundo);
        float folha = Mathf.Min(800f * s, w - 64f * s), margem = 54f * s;
        float altura = Blocos(a, 0, 0, folha - 2 * margem, false) + 2 * margem;
        float res = ComecarRolagem("leitor", area, altura + 56f * s);
        float fx = (w - res - folha) / 2f;
        Rect pagina = new Rect(fx, 28f * s, folha, altura);
        Sombra(pagina, 2f * s);
        Caixa(pagina, Color.white, 2f * s);
        Contorno(pagina, Hex("#C7CDD4"), Mathf.Max(1f, s), 2f * s);
        Blocos(a, fx + margem, 28f * s + margem, folha - 2 * margem, true);
        TerminarRolagem();
    }

    float Blocos(EstacaoDados.Arquivo a, float x, float y0, float lw, bool desenhar)
    {
        float y = y0;
        if (a.blocos == null) return 0;
        bool planilha = a.tipo == "planilha";
        foreach (var b in a.blocos)
        {
            switch (b.tipo)
            {
                case "titulo":
                {
                    var e = Estilo(24, CTexto, Peso.Negrito, true);
                    float hh = Altura(b.texto, e, lw);
                    if (desenhar) Texto(new Rect(x, y, lw, hh), b.texto, e);
                    y += hh + 6f * s;
                    break;
                }
                case "subtitulo":
                {
                    y += 10f * s;
                    var e = Estilo(17, Hex("#24476B"), Peso.Negrito, true);
                    float hh = Altura(b.texto, e, lw);
                    if (desenhar) Texto(new Rect(x, y, lw, hh), b.texto, e);
                    y += hh + 6f * s;
                    break;
                }
                case "meta":
                {
                    var e = Estilo(13, CSecundario, Peso.Normal, true);
                    float hh = Altura(b.texto, e, lw);
                    if (desenhar) Texto(new Rect(x, y, lw, hh), b.texto, e);
                    y += hh + 10f * s;
                    if (desenhar) Linha(x, y, lw, Hex("#D5DBE2"));
                    y += 18f * s;
                    break;
                }
                case "paragrafo":
                {
                    var e = Estilo(16, CTexto, Peso.Normal, true);
                    float hh = Altura(b.texto, e, lw);
                    if (desenhar) Texto(new Rect(x, y, lw, hh), b.texto, e);
                    y += hh + 14f * s;
                    break;
                }
                case "lista":
                {
                    var e = Estilo(16, CTexto, Peso.Normal, true);
                    float ind = 24f * s;
                    for (int i = 0; i < b.itens.Length; i++)
                    {
                        float hh = Altura(b.itens[i], e, lw - ind);
                        if (desenhar)
                        {
                            Texto(new Rect(x, y, ind, 22f * s), $"{i + 1}.", Estilo(16, CSecundario));
                            Texto(new Rect(x + ind, y, lw - ind, hh), b.itens[i], e);
                        }
                        y += hh + 8f * s;
                    }
                    y += 8f * s;
                    break;
                }
                case "nota":
                {
                    var e = Estilo(14, Hex("#3E4A57"), Peso.Normal, true);
                    float pad = 12f * s, hh = Altura(b.texto, e, lw - 2 * pad - 4f * s);
                    if (desenhar)
                    {
                        Caixa(new Rect(x, y, lw, hh + 2 * pad), Hex("#F3F5F8"));
                        Caixa(new Rect(x, y, 3f * s, hh + 2 * pad), Hex("#9AA9BA"));
                        Texto(new Rect(x + pad + 4f * s, y + pad, lw - 2 * pad - 4f * s, hh), b.texto, e);
                    }
                    y += hh + 2 * pad + 14f * s;
                    break;
                }
                case "tabela":
                    y += Tabela(b, x, y, lw, desenhar, planilha) + 18f * s;
                    break;
            }
        }
        return y - y0;
    }

    float Tabela(EstacaoDados.Bloco b, float x, float y0, float lw, bool desenhar, bool planilha)
    {
        int n = b.colunas.Length;
        var larg = new float[n];
        float soma = 0;
        for (int i = 0; i < n; i++) soma += b.larguras != null && b.larguras.Length == n ? b.larguras[i] : 1f;
        for (int i = 0; i < n; i++) larg[i] = lw * (b.larguras != null && b.larguras.Length == n ? b.larguras[i] : 1f) / soma;
        float pad = 9f * s, y = y0;
        var eCab = Estilo(14, planilha ? Color.white : CTexto, Peso.Negrito, true);
        var eCel = Estilo(15, CTexto, Peso.Normal, true);
        Color borda = Hex("#C9D0D8");

        float Linha_(string[] cel, GUIStyle e, Color fundo)
        {
            float hh = 0;
            for (int i = 0; i < n; i++) hh = Mathf.Max(hh, Altura(i < cel.Length ? cel[i] : "", e, larg[i] - 2 * pad));
            hh += 2 * pad;
            if (desenhar)
            {
                Caixa(new Rect(x, y, lw, hh), fundo);
                float cx = x;
                for (int i = 0; i < n; i++)
                {
                    Texto(new Rect(cx + pad, y + pad, larg[i] - 2 * pad, hh - 2 * pad), i < cel.Length ? cel[i] : "", e);
                    if (i > 0) Caixa(new Rect(cx, y, Mathf.Max(1f, s), hh), borda);
                    cx += larg[i];
                }
                Linha(x, y + hh - Mathf.Max(1f, s), lw, borda);
            }
            y += hh;
            return hh;
        }

        Linha_(b.colunas, eCab, planilha ? Hex("#4E7D5B") : Hex("#E9EDF1"));
        for (int r = 0; r < b.linhas.Length; r++)
            Linha_(b.linhas[r].c, eCel, planilha && r % 2 == 1 ? Hex("#F4F8F5") : Color.white);
        if (desenhar) Contorno(new Rect(x, y0, lw, y - y0), borda, Mathf.Max(1f, s));
        return y - y0;
    }

    // ================================================================== Notas

    void AppNotas(float w, float h)
    {
        float lw = 240f * s;
        var lista = dados.ArquivosEm("notas").OrderBy(a => a.nome).ToList();
        if (notaAtual != null && !lista.Contains(notaAtual)) lista.Insert(0, notaAtual);
        if (notaAtual == null && lista.Count > 0) notaAtual = lista[0];
        Caixa(new Rect(0, 0, lw, h), Hex("#EDF0F3"));
        Caixa(new Rect(lw, 0, Mathf.Max(1f, s), h), CBorda);
        Texto(new Rect(16f * s, 12f * s, lw, 20f * s), "NOTAS", Estilo(12, CFraco, Peso.Negrito));
        float y = 38f * s;
        foreach (var a in lista)
        {
            Rect it = new Rect(6f * s, y, lw - 12f * s, 54f * s);
            bool sel = a == notaAtual;
            Marcar("nota:" + a.id, it);
            if (Botao(it, CHover, sel ? (Foco == App.Notas ? CSelecao : CSelecaoInativa) : Color.clear)) { notaAtual = a; ZerarRolagem("notas-texto"); }
            Icone(new Rect(it.x + 8f * s, it.y + 8f * s, 22f * s, 22f * s), "nota_24");
            Rotulo(new Rect(it.x + 38f * s, it.y + 6f * s, it.width - 42f * s, 22f * s), a.nome, Estilo(14, CTexto, sel ? Peso.Negrito : Peso.Normal));
            Rotulo(new Rect(it.x + 38f * s, it.y + 28f * s, it.width - 42f * s, 20f * s), a.modificado, Estilo(12, CSecundario));
            y += 56f * s;
        }
        Rect corpo = new Rect(lw + s, 0, w - lw - s, h);
        Caixa(corpo, Color.white);
        if (notaAtual == null) return;
        float bar = 36f * s;
        Caixa(new Rect(corpo.x, 0, corpo.width, bar), Hex("#F6F7F9"));
        Linha(corpo.x, bar - s, corpo.width, Hex("#E1E5EA"));
        Rotulo(new Rect(corpo.x + 16f * s, 0, corpo.width - 32f * s, bar), $"{notaAtual.nome}   ·   modificado em {notaAtual.modificado}   ·   texto simples",
            Estilo(13, CSecundario, Peso.Normal, false, TextAnchor.MiddleLeft));
        var e = Estilo(16, CTexto, Peso.Mono, true);
        Rect vista = new Rect(corpo.x, bar, corpo.width, h - bar);
        float pad = 22f * s, tw = vista.width - 2 * pad - 14f * s;
        float hh = Altura(notaAtual.texto, e, tw);
        ComecarRolagem("notas-texto", vista, hh + 2 * pad);
        Texto(new Rect(pad, pad, tw, hh), notaAtual.texto, e);
        TerminarRolagem();
    }

    void TecladoNotas()
    {
        var lista = dados.ArquivosEm("notas").OrderBy(a => a.nome).ToList();
        if (notaAtual != null && !lista.Contains(notaAtual)) lista.Insert(0, notaAtual);
        int i = lista.IndexOf(notaAtual);
        if (TeclaUi("baixo") && i < lista.Count - 1) notaAtual = lista[i + 1];
        if (TeclaUi("cima") && i > 0) notaAtual = lista[i - 1];
    }

    // ================================================================== Imagens

    void AppImagens(float w, float h)
    {
        Caixa(new Rect(0, 0, w, h), Hex("#2C3036"));
        if (imagemAtual == null) return;
        float bar = 46f * s;
        Texture2D t = Tex(imagemAtual.imagem);
        Rect area = new Rect(24f * s, 20f * s, w - 48f * s, h - bar - 40f * s);
        if (t != null)
        {
            float k = Mathf.Min(area.width / t.width, area.height / t.height);
            Rect img = new Rect(area.center.x - t.width * k / 2, area.center.y - t.height * k / 2, t.width * k, t.height * k);
            Sombra(img, 0);
            GUI.DrawTexture(img, t, ScaleMode.StretchToFill, false);
        }
        Rect b = new Rect(0, h - bar, w, bar);
        Caixa(b, Hex("#23272C"));
        BotaoVoltarPara(new Rect(10f * s, b.y + 8f * s, 0, 30f * s), origemImagem);
        string dim = t != null ? $"{t.width} × {t.height}" : "";
        Rotulo(new Rect(w * 0.4f, b.y, w * 0.6f - 16f * s, bar), $"{imagemAtual.nome}   ·   {dim}   ·   {imagemAtual.modificado}",
            Estilo(13, Hex("#C9D0D8"), Peso.Normal, false, TextAnchor.MiddleRight));
    }

    // ================================================================== Agenda

    static readonly string[] Meses = { "janeiro", "fevereiro", "março", "abril", "maio", "junho", "julho", "agosto", "setembro", "outubro", "novembro", "dezembro" };
    static readonly string[] Semana = { "dom", "seg", "ter", "qua", "qui", "sex", "sáb" };
    static readonly string[] SemanaLonga = { "domingo", "segunda-feira", "terça-feira", "quarta-feira", "quinta-feira", "sexta-feira", "sábado" };
    DateTime mesAgenda, diaAgenda;
    bool agendaIniciada;

    static DateTime Data(string d) => new DateTime(int.Parse(d.Substring(6, 4)), int.Parse(d.Substring(3, 2)), int.Parse(d.Substring(0, 2)));

    void AppAgenda(float w, float h)
    {
        DateTime hoje = Data(dados.estacao.data);
        if (!agendaIniciada) { mesAgenda = new DateTime(hoje.Year, hoje.Month, 1); diaAgenda = hoje; agendaIniciada = true; }
        float bar = 52f * s;
        Caixa(new Rect(0, 0, w, bar), CFerramentas);
        Linha(0, bar - s, w, CBorda);
        Rect bAnt = new Rect(12f * s, 10f * s, 34f * s, 32f * s), bProx = new Rect(50f * s, 10f * s, 34f * s, 32f * s);
        if (Botao(bAnt, Hex("#D9DFE6"))) mesAgenda = mesAgenda.AddMonths(-1);
        Icone(Centro(bAnt, 20f * s), "g_chevron_l", CTexto);
        if (Botao(bProx, Hex("#D9DFE6"))) mesAgenda = mesAgenda.AddMonths(1);
        Icone(Centro(bProx, 20f * s), "g_chevron", CTexto);
        Texto(new Rect(98f * s, 0, 360f * s, bar), $"{Meses[mesAgenda.Month - 1]} de {mesAgenda.Year}", Estilo(20, CTexto, Peso.Negrito, false, TextAnchor.MiddleLeft));
        if (BotaoTexto(new Rect(w - 96f * s, 11f * s, 82f * s, 30f * s), "Hoje", 14)) { mesAgenda = new DateTime(hoje.Year, hoje.Month, 1); diaAgenda = hoje; }

        float side = 300f * s, gw = w - side;
        Caixa(new Rect(0, bar, gw, h - bar), Color.white);
        float hdr = 30f * s, cw = gw / 7f, ch = (h - bar - hdr) / 6f;
        for (int d = 0; d < 7; d++)
            Texto(new Rect(d * cw, bar, cw, hdr), Semana[d], Estilo(13, CSecundario, Peso.Negrito, false, TextAnchor.MiddleCenter));
        Linha(0, bar + hdr - s, gw, CBorda);
        DateTime inicio = mesAgenda.AddDays(-(int)mesAgenda.DayOfWeek);
        for (int k = 0; k < 42; k++)
        {
            DateTime dia = inicio.AddDays(k);
            Rect c = new Rect((k % 7) * cw, bar + hdr + (k / 7) * ch, cw, ch);
            bool doMes = dia.Month == mesAgenda.Month, sel = dia == diaAgenda;
            if (sel) Caixa(c, CSelecao);
            else if (Sobre(c)) Caixa(c, CHover);
            Contorno(c, Hex("#E3E7EC"), Mathf.Max(1f, s) * 0.5f);
            Rect num = new Rect(c.x + 6f * s, c.y + 5f * s, 28f * s, 24f * s);
            if (dia == hoje) Caixa(num, CAcento, 12f * s);
            Texto(num, dia.Day.ToString(), Estilo(14, dia == hoje ? Color.white : doMes ? CTexto : Hex("#B4BCC6"), dia == hoje ? Peso.Negrito : Peso.Normal, false, TextAnchor.MiddleCenter));
            float ey = c.y + 32f * s;
            foreach (var ev in Eventos(dia))
            {
                if (ey + 20f * s > c.yMax) break;
                Rect er = new Rect(c.x + 4f * s, ey, c.width - 8f * s, 19f * s);
                Caixa(er, doMes ? Hex("#DCE7F3") : Hex("#EEF1F4"), 3f * s);
                Rotulo(new Rect(er.x + 5f * s, er.y, er.width - 8f * s, er.height), ev.titulo, Estilo(12, doMes ? Hex("#24476B") : CFraco, Peso.Normal, false, TextAnchor.MiddleLeft));
                ey += 21f * s;
            }
            if (Clique(c)) { diaAgenda = dia; if (!doMes) mesAgenda = new DateTime(dia.Year, dia.Month, 1); }
        }

        // Dia selecionado
        Rect lado = new Rect(gw, bar, side, h - bar);
        Caixa(lado, Hex("#F4F6F8"));
        Caixa(new Rect(gw, bar, Mathf.Max(1f, s), h - bar), CBorda);
        float x = gw + 20f * s, lw = side - 40f * s, y = bar + 20f * s;
        string titulo = $"{SemanaLonga[(int)diaAgenda.DayOfWeek]}, {diaAgenda.Day} de {Meses[diaAgenda.Month - 1]}";
        var eT = Estilo(17, CTexto, Peso.Negrito, true);
        float th = Altura(titulo, eT, lw);
        Texto(new Rect(x, y, lw, th), titulo, eT);
        y += th + 14f * s;
        var evs = Eventos(diaAgenda).ToList();
        if (evs.Count == 0) Texto(new Rect(x, y, lw, 24f * s), "Sem compromissos.", Estilo(15, CFraco));
        foreach (var ev in evs)
        {
            var e1 = Estilo(15, CTexto, Peso.Negrito, true);
            var e2 = Estilo(14, CSecundario, Peso.Normal, true);
            float h1 = Altura(ev.titulo, e1, lw - 14f * s), h2 = Altura(ev.detalhe, e2, lw - 14f * s);
            Caixa(new Rect(x, y, 3f * s, h1 + h2 + 4f * s), CAcento);
            Texto(new Rect(x + 12f * s, y, lw - 14f * s, h1), ev.titulo, e1);
            Texto(new Rect(x + 12f * s, y + h1 + 2f * s, lw - 14f * s, h2), ev.detalhe, e2);
            y += h1 + h2 + 18f * s;
        }
    }

    IEnumerable<EstacaoDados.Evento> Eventos(DateTime dia)
    {
        string d = $"{dia.Day:00}/{dia.Month:00}/{dia.Year}";
        foreach (var e in dados.agenda) if (e.data == d) yield return e;
    }

    // ================================================================== Sobre esta estação

    void AppSobre(float w, float h)
    {
        Caixa(new Rect(0, 0, w, h), Color.white);
        var est = dados.estacao;
        Icone(new Rect(28f * s, 28f * s, 72f * s, 72f * s), "logo_64");
        Texto(new Rect(118f * s, 30f * s, w - 140f * s, 34f * s), est.sistema, Estilo(26, CTexto, Peso.Negrito));
        Texto(new Rect(118f * s, 66f * s, w - 140f * s, 22f * s), $"versão {est.versao} · {est.orgao}", Estilo(14, CSecundario));
        Linha(28f * s, 116f * s, w - 56f * s, CBorda);
        var linhas = new (string, string)[]
        {
            ("Estação", est.nome), ("Sessão", $"{est.nomeUsuario} ({est.usuario})"), ("Setor", est.setor),
            ("Processador", est.processador), ("Memória", est.memoria), ("Suporte", est.suporte),
        };
        float y = 132f * s;
        foreach (var (k, v) in linhas)
        {
            Texto(new Rect(28f * s, y, 140f * s, 26f * s), k, Estilo(15, CSecundario));
            Rotulo(new Rect(170f * s, y, w - 200f * s, 26f * s), v, Estilo(15, CTexto));
            y += 30f * s;
        }
        Rect bF = new Rect(w - 128f * s, h - 56f * s, 104f * s, 34f * s);
        Marcar("sobre-fechar", bF);
        if (BotaoTexto(bF, "Fechar", 15, true)) FecharApp(App.Sobre);
    }
}
