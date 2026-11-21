using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fotos que o jogador tirou nesta noite. Cada foto de alvo vira o fato "foto:&lt;alvo&gt;:&lt;variante&gt;" no
/// EstadoDaNoite (assim o álbum entra no save sem formato próprio). As imagens ficam só na memória: depois de
/// carregar um save, a foto aparece no álbum pela legenda.
///
/// Duas fotos do mesmo alvo com variantes diferentes marcam "mudou:&lt;alvo&gt;" (ex.: o braço da pessoa na mesa
/// em outra posição).
/// </summary>
public static class AlbumDeFotos
{
    public const int MaximoDeFotosLivres = 24;

    public class Foto
    {
        public string alvo;          // vazio = foto sem alvo
        public int variante;
        public string legenda;
        public Texture2D imagem;     // null depois de carregar um save
        public float hora;
    }

    static readonly List<Foto> fotos = new List<Foto>();
    static readonly Dictionary<string, string> legendas = new Dictionary<string, string>();

    public static IReadOnlyList<Foto> Fotos => fotos;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar() => Limpar();

    public static void Limpar()
    {
        foreach (Foto f in fotos) Destruir(f.imagem);
        fotos.Clear();
        legendas.Clear();
    }

    static void Destruir(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
    }

    public static string Fato(string alvo, int variante) => "foto:" + alvo + ":" + variante;

    /// <summary>
    /// Registra uma foto. Com alvo: marca o fato, coleta a evidência (se houver) e confere se o alvo mudou.
    /// Devolve true se foi a primeira foto desta variante do alvo.
    /// </summary>
    public static bool Registrar(string alvo, int variante, string legenda, string idEvidencia, Texture2D imagem)
    {
        var foto = new Foto { alvo = alvo ?? "", variante = variante, legenda = legenda, imagem = imagem, hora = Time.time };
        bool nova = false;
        if (!string.IsNullOrEmpty(alvo))
        {
            legendas[Fato(alvo, variante)] = legenda;
            nova = EstadoDaNoite.Marcar(Fato(alvo, variante));
            if (!string.IsNullOrEmpty(idEvidencia)) Caderno.Coletar(idEvidencia);
            if (VariantesDe(alvo).Count > 1 && EstadoDaNoite.Marcar("mudou:" + alvo) && alvo == "mesa_autopsia")
                MiraDeInteracao.Avisar("O braço dele não estava assim na primeira foto.", 3.5f);
        }
        else if (ContarLivres() >= MaximoDeFotosLivres)
        {
            // Fotos sem alvo não servem de prova: as mais antigas saem para não encher a memória.
            for (int i = 0; i < fotos.Count; i++)
                if (string.IsNullOrEmpty(fotos[i].alvo))
                {
                    Destruir(fotos[i].imagem);
                    fotos.RemoveAt(i);
                    break;
                }
        }
        fotos.Add(foto);
        return nova;
    }

    static int ContarLivres()
    {
        int n = 0;
        foreach (Foto f in fotos) if (string.IsNullOrEmpty(f.alvo)) n++;
        return n;
    }

    /// <summary>Variantes já fotografadas de um alvo (pelos fatos, então vale depois de carregar um save).</summary>
    public static List<int> VariantesDe(string alvo)
    {
        var resultado = new List<int>();
        string prefixo = "foto:" + alvo + ":";
        foreach (string fato in EstadoDaNoite.Fatos)
            if (fato.StartsWith(prefixo) && int.TryParse(fato.Substring(prefixo.Length), out int v) && !resultado.Contains(v))
                resultado.Add(v);
        resultado.Sort();
        return resultado;
    }

    public static bool Fotografou(string alvo) => VariantesDe(alvo).Count > 0;

    /// <summary>Depois de carregar um save: recria no álbum as fotos de alvo que estão só nos fatos.</summary>
    /// <param name="descartarSemFato">Ao abrir a cena: tira fotos de outra partida (alvo sem fato) e as fotos livres.</param>
    public static void SincronizarComFatos(bool descartarSemFato = false)
    {
        if (descartarSemFato)
            for (int i = fotos.Count - 1; i >= 0; i--)
                if (string.IsNullOrEmpty(fotos[i].alvo) || !EstadoDaNoite.Tem(Fato(fotos[i].alvo, fotos[i].variante)))
                {
                    Destruir(fotos[i].imagem);
                    fotos.RemoveAt(i);
                }
        foreach (string fato in EstadoDaNoite.Fatos)
        {
            if (!fato.StartsWith("foto:")) continue;
            int ultimo = fato.LastIndexOf(':');
            if (ultimo <= 5 || !int.TryParse(fato.Substring(ultimo + 1), out int variante)) continue;
            string alvo = fato.Substring(5, ultimo - 5);
            bool existe = false;
            foreach (Foto f in fotos) if (f.alvo == alvo && f.variante == variante) { existe = true; break; }
            if (existe) continue;
            AlvoFotografico registrado = AlvoFotografico.Buscar(alvo);
            string legenda = legendas.TryGetValue(fato, out string l) ? l : registrado != null ? registrado.legenda : alvo;
            fotos.Add(new Foto { alvo = alvo, variante = variante, legenda = legenda });
        }
    }
}
