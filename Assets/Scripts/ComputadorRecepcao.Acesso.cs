using System;
using UnityEngine;

/// <summary>
/// Aplicativo "Controle de acesso" da estação da recepção (D07, puzzle P05): o registro automático do leitor de
/// crachá da porta do recebimento. Mostra a entrada do crachá LV-12 (Daniel Vilar) às 23h04 e nenhuma saída.
/// Abrir o aplicativo coleta a evidência D07. Conteúdo em Resources/Historia/registro_sensor.json.
/// </summary>
public partial class ComputadorRecepcao
{
    [Serializable]
    public class RegistroDeAcesso
    {
        public string data, hora, cracha, nome, empresa, evento;
    }

    [Serializable]
    public class DadosDeAcesso
    {
        public string porta, aviso;
        public RegistroDeAcesso[] registros;
    }

    [Header("Controle de acesso (D07)")]
    public string idEvidenciaAcesso = "D07";

    DadosDeAcesso acesso;

    public DadosDeAcesso Acesso
    {
        get
        {
            if (acesso == null)
            {
                TextAsset t = Resources.Load<TextAsset>("Historia/registro_sensor");
                acesso = t != null ? JsonUtility.FromJson<DadosDeAcesso>(t.text) : new DadosDeAcesso();
                if (acesso.registros == null) acesso.registros = new RegistroDeAcesso[0];
            }
            return acesso;
        }
    }

    void AoAbrirAcesso()
    {
        if (!string.IsNullOrEmpty(idEvidenciaAcesso)) Caderno.Coletar(idEvidenciaAcesso);
    }

    void AppAcesso(float w, float h)
    {
        DadosDeAcesso d = Acesso;
        Caixa(new Rect(0, 0, w, h), Color.white);
        Texto(new Rect(24f * s, 18f * s, w - 48f * s, 30f * s), "Registro do leitor de crachá", Estilo(22, CTexto, Peso.Negrito));
        Texto(new Rect(24f * s, 52f * s, w - 48f * s, 22f * s), d.porta, Estilo(14, CSecundario));
        var eAviso = Estilo(13, CSecundario, Peso.Normal, true);
        float hAviso = Altura(d.aviso, eAviso, w - 48f * s);
        Texto(new Rect(24f * s, 78f * s, w - 48f * s, hAviso), d.aviso, eAviso);

        float y0 = 90f * s + hAviso;
        float[] colunas = { 0f, 130f, 210f, 310f, 560f, 720f };
        string[] cabecalho = { "Data", "Hora", "Crachá", "Nome", "Empresa", "Evento" };
        Caixa(new Rect(16f * s, y0, w - 32f * s, 32f * s), CFerramentas);
        for (int c = 0; c < cabecalho.Length; c++)
            Texto(new Rect((24f + colunas[c]) * s, y0 + 7f * s, 200f * s, 22f * s), cabecalho[c], Estilo(14, CSecundario, Peso.Negrito));

        float linha = 34f * s;
        float y = y0 + 36f * s;
        for (int i = 0; i < d.registros.Length; i++)
        {
            RegistroDeAcesso r = d.registros[i];
            if (i % 2 == 1) Caixa(new Rect(16f * s, y, w - 32f * s, linha), CJanela);
            string[] valores = { r.data, r.hora, r.cracha, r.nome, r.empresa, r.evento };
            for (int c = 0; c < valores.Length; c++)
                Texto(new Rect((24f + colunas[c]) * s, y + 8f * s, 240f * s, 22f * s), valores[c], Estilo(15, CTexto, c == 2 ? Peso.Mono : Peso.Normal));
            y += linha;
        }
        Linha(16f * s, y + 4f * s, w - 32f * s, CBorda);
        Texto(new Rect(24f * s, y + 14f * s, w - 48f * s, 22f * s), $"{d.registros.Length} registro(s) em 18/11/2026. Atualizado automaticamente.",
            Estilo(13, CFraco));
    }
}
