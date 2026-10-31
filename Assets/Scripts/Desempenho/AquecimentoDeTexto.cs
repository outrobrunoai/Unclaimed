using System.Collections.Generic;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

// Medido em 2026-10-19, Release/Vulkan: primeiro texto do quadro ~550 ms
// (TextSettings.OnEnable, FontEngine.LoadFontFace, ReadObject, GetOpenTypeFontFeatures)
// e do PC ~190 ms (UpdateGlyphAdjustmentRecords/GetOpenTypeFontFeatures).
// Antecipa esse custo do TextCore para o carregamento da cena.
public class AquecimentoDeTexto : MonoBehaviour
{
    const string Caracteres = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~"
        + "áàâãäéèêëíìîïóòôõöúùûüçñÁÀÂÃÄÉÈÊËÍÌÎÏÓÒÔÕÖÚÙÛÜÇÑºª°§–—“”‘’…•·€";

    List<Font> fontes;
    Stopwatch cronometro;
    bool concluido;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Registrar()
    {
        Criar();
        // Cenas carregadas depois (menu → jogo) trazem fontes novas.
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, _) => Criar();
    }

    static void Criar()
    {
        if (FindObjectsByType<AquecimentoDeTexto>(FindObjectsInactive.Include).Length > 0) return;
        new GameObject("AquecimentoDeTexto").AddComponent<AquecimentoDeTexto>();
    }

    void OnGUI()
    {
        useGUILayout = false;
        if (concluido) return;

        if (fontes == null)
        {
            cronometro = Stopwatch.StartNew();
            ColetarFontes();
            cronometro.Stop();
        }
        if (Event.current.type != EventType.Repaint) return;

        concluido = true;
        cronometro.Start();
        Color corAnterior = GUI.color;
        try
        {
            GUI.color = new Color(corAnterior.r, corAnterior.g, corAnterior.b, 0f);
            foreach (Font fonte in fontes)
            {
                var estilo = new GUIStyle(GUI.skin.label)
                {
                    font = fonte,
                    wordWrap = false,
                    richText = false
                };
                int tamanhoPadrao = estilo.fontSize;
                DesenharTamanhos(estilo, tamanhoPadrao);
                if (fonte == null || fonte == GUI.skin.font || fonte == GUI.skin.label.font)
                {
                    estilo.fontStyle = FontStyle.Bold;
                    DesenharTamanhos(estilo, tamanhoPadrao);
                }
            }
        }
        finally
        {
            GUI.color = corAnterior;
        }
        cronometro.Stop();
        Debug.Log($"AquecimentoDeTexto: {fontes.Count} fontes, {cronometro.Elapsed.TotalMilliseconds:F1} ms.");
        Destroy(gameObject);
    }

    void ColetarFontes()
    {
        fontes = new List<Font>();
        // Null no skin representa o estilo padrão; referências nulas dos componentes são ignoradas.
        fontes.Add(GUI.skin.font);
        if (!fontes.Contains(GUI.skin.label.font)) fontes.Add(GUI.skin.label.font);
        foreach (ComputadorRecepcao computador in FindObjectsByType<ComputadorRecepcao>(FindObjectsInactive.Include))
        {
            AdicionarFonte(computador.fonte);
            AdicionarFonte(computador.fonteNegrito);
            AdicionarFonte(computador.fonteMono);
        }
        foreach (QuadroDeAvisos quadro in FindObjectsByType<QuadroDeAvisos>(FindObjectsInactive.Include))
        {
            AdicionarFonte(quadro.fonte);
            AdicionarFonte(quadro.fonteNegrito);
        }
    }

    void AdicionarFonte(Font fonte)
    {
        if (fonte != null && !fontes.Contains(fonte)) fontes.Add(fonte);
    }

    static void DesenharTamanhos(GUIStyle estilo, int tamanhoPadrao)
    {
        var area = new Rect(0f, 0f, 4000f, 40f);
        estilo.fontSize = tamanhoPadrao;
        GUI.Label(area, Caracteres, estilo);
        estilo.fontSize = 32;
        GUI.Label(area, Caracteres, estilo);
    }
}
