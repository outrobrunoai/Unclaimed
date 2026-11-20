using System;
using UnityEngine;

/// <summary>
/// O gravador de Léo dentro do compartimento G-B2 (P06). Pegar marca "gravador_pego", grava o checkpoint CP04
/// e, alguns segundos depois, Daniel aparece na porta do arquivo (Confronto).
/// Fica no envelope de guarda, no compartimento G-B2, atrás do cadeado 4817. Celso manda a combinação quando o jogador conclui a venda.
/// </summary>
public class Gravador : AlvoDeInteracao, ISalvavel
{
    public const string Item = "gravador";
    public string tecla = "E";
    [Min(0.2f)] public float alcance = 1.6f;
    public string textoPegar = "Pegar o gravador";
    public AudioClip somPegar;

    public override float AlcanceDeInteracao => alcance;
    public override string Tecla => tecla;
    public override string Indicacao => EstadoDaNoite.Tem("gravador_pego") ? "" : textoPegar;

    public override void Interagir()
    {
        if (!EstadoDaNoite.Marcar("gravador_pego")) return;
        Chaveiro.Adicionar(Item);
        if (somPegar != null) AudioSource.PlayClipAtPoint(somPegar, transform.position);
        MiraDeInteracao.Avisar($"Você pegou o gravador de Léo. ({OuvirGravador.TeclaPadrao}: ouvir)");
        gameObject.SetActive(false);
    }

    public string Salvar() => gameObject.activeSelf ? "0" : "1";
    public void Carregar(string estado) { if (estado == "1") gameObject.SetActive(false); }

    /// <summary>O jogador está com o gravador agora (pegou e não entregou).</summary>
    public static bool ComOJogador => EstadoDaNoite.Tem("gravador_pego") && !EstadoDaNoite.Tem("gravador_entregue");
}

/// <summary>Legendas da gravação (Resources/Historia/gravacao.json, texto do Bruno).</summary>
[Serializable]
public class Gravacao
{
    [Serializable]
    public class Fala { public float inicio, fim; public string voz, texto; }

    public string titulo;
    public Fala[] falas = new Fala[0];

    public float Duracao
    {
        get
        {
            float d = 0f;
            foreach (Fala f in falas) d = Mathf.Max(d, f.fim);
            return d;
        }
    }

    public Fala Em(float t)
    {
        foreach (Fala f in falas) if (t >= f.inicio && t < f.fim) return f;
        return null;
    }

    public static Gravacao Carregar()
    {
        TextAsset a = Resources.Load<TextAsset>("Historia/gravacao");
        Gravacao g = a != null ? JsonUtility.FromJson<Gravacao>(a.text) : null;
        if (g == null) g = new Gravacao();
        if (g.falas == null) g.falas = new Fala[0];
        return g;
    }
}
