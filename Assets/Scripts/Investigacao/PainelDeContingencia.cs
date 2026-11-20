using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Painel físico de contingência da sala técnica: controla as alavancas, os sinaleiros (LEDs) e o visor
/// de carga total conforme o estado da RedeEletrica, além de gravar e restaurar a rede no save.
/// </summary>
public class PainelDeContingencia : MonoBehaviour, ISalvavel
{
    [System.Serializable]
    public class Coluna
    {
        public string circuito;
        public Transform seletor;
        public Renderer lente;
    }

    [System.Serializable]
    public class Leitura
    {
        public int total;
        public Material material;
    }

    public Coluna[] colunas = new Coluna[0];
    public Material lenteAcesa, lenteApagada;
    [Tooltip("Ângulo Z local da alavanca (graus).")]
    public float anguloLigado = 315f, anguloDesligado = 45f;
    [Min(0.01f)] public float duracaoGiro = 0.18f;
    public Renderer visor;
    public Leitura[] leituras = new Leitura[0];
    [Min(0.1f)] public float tempoDoAviso = 1.6f;
    public string idEvidencia = "D04";
    public AudioSource fonte;
    public AudioClip somChave, somRecusado;

    readonly Dictionary<Coluna, Coroutine> animacoes = new Dictionary<Coluna, Coroutine>();
    Coroutine piscarVisor;
    string circuitoAcionado;

    void OnEnable()
    {
        RedeEletrica.AoMudar -= AoMudar;
        RedeEletrica.AoMudar += AoMudar;
        Atualizar();
    }

    void OnDisable()
    {
        RedeEletrica.AoMudar -= AoMudar;
        animacoes.Clear();
        piscarVisor = null;
    }

    void AoMudar(string id)
    {
        PararPiscarVisor();
        AplicarColunas(ignorarAnimadas: true);
        AplicarVisor(RedeEletrica.Consumo);
    }

    public string Indicacao(string circuito)
    {
        RedeEletrica.Circuito c = RedeEletrica.Buscar(circuito);
        if (c == null) return "";
        if (c.essencial) return "Chave da refrigeração (travada)";
        return c.ligado ? "Desligar: " + c.nome : "Ligar: " + c.nome;
    }

    public void Acionar(string circuito)
    {
        if (!string.IsNullOrEmpty(idEvidencia)) Caderno.Coletar(idEvidencia);

        RedeEletrica.Circuito c = RedeEletrica.Buscar(circuito);
        if (c == null)
        {
            Tocar(somRecusado);
            return;
        }
        if (c.essencial)
        {
            Tocar(somRecusado);
            MiraDeInteracao.Avisar("A refrigeração não pode ser desligada. A chave está travada.");
            return;
        }

        int atual = RedeEletrica.Consumo;
        ResultadoDoCircuito resultado;
        circuitoAcionado = circuito;
        try
        {
            resultado = RedeEletrica.Alternar(circuito);
        }
        finally
        {
            circuitoAcionado = null;
        }

        switch (resultado)
        {
            case ResultadoDoCircuito.Ok:
                Tocar(somChave);
                PararPiscarVisor();
                AplicarResultadoOk(circuito, c);
                AplicarVisor(RedeEletrica.Consumo);
                break;

            case ResultadoDoCircuito.Sobrecarga:
                Tocar(somRecusado);
                MiraDeInteracao.Avisar(
                    $"A chave voltou sozinha: {atual} + {c.consumo} = {atual + c.consumo}, acima do limite de {RedeEletrica.Limite}.");
                AplicarSobrecarga(circuito, c, atual + c.consumo);
                break;

            case ResultadoDoCircuito.SemFusivel:
                Tocar(somRecusado);
                string nome = string.IsNullOrEmpty(c.nome) ? circuito : c.nome.ToLower();
                MiraDeInteracao.Avisar("A chave não liga a " + nome + ": falta o fusível no quadro geral.");
                break;

            case ResultadoDoCircuito.Essencial:
            case ResultadoDoCircuito.Desconhecido:
                Tocar(somRecusado);
                break;
        }
    }

    public void Atualizar()
    {
        PararTodasAnimacoes();
        AplicarColunas(ignorarAnimadas: false);
        AplicarVisor(RedeEletrica.Consumo);
    }

    public string Salvar() => RedeEletrica.Exportar();

    public void Carregar(string estado)
    {
        RedeEletrica.Importar(estado);
        Atualizar();
    }

    void AplicarResultadoOk(string circuito, RedeEletrica.Circuito c)
    {
        if (colunas == null) return;
        float destino = Angulo(c.ligado);
        for (int i = 0; i < colunas.Length; i++)
        {
            Coluna col = colunas[i];
            if (col == null || col.circuito != circuito) continue;
            AplicarLente(col, c);
            if (PodeAnimar())
                IniciarAnimacao(col, Girar(col, destino));
            else
                DefinirAnguloZ(col.seletor, destino);
        }
    }

    void AplicarSobrecarga(string circuito, RedeEletrica.Circuito c, int totalRecusado)
    {
        float repouso = Angulo(c.ligado);
        float tentado = Angulo(!c.ligado);

        if (PodeAnimar())
        {
            if (colunas != null)
            {
                for (int i = 0; i < colunas.Length; i++)
                {
                    Coluna col = colunas[i];
                    if (col == null || col.circuito != circuito) continue;
                    IniciarAnimacao(col, GirarEVoltar(col, repouso, tentado));
                }
            }
            PararPiscarVisor();
            piscarVisor = StartCoroutine(PiscarVisor(totalRecusado));
        }
        else
        {
            if (colunas != null)
            {
                for (int i = 0; i < colunas.Length; i++)
                {
                    Coluna col = colunas[i];
                    if (col == null || col.circuito != circuito) continue;
                    DefinirAnguloZ(col.seletor, repouso);
                }
            }
            AplicarVisor(RedeEletrica.Consumo);
        }
    }

    void AplicarColunas(bool ignorarAnimadas)
    {
        if (colunas == null) return;
        for (int i = 0; i < colunas.Length; i++)
        {
            Coluna col = colunas[i];
            if (col == null) continue;
            RedeEletrica.Circuito c = RedeEletrica.Buscar(col.circuito);
            if (c == null) continue;

            AplicarLente(col, c);

            bool animando = ignorarAnimadas && (animacoes.ContainsKey(col) || col.circuito == circuitoAcionado);
            if (!animando)
                DefinirAnguloZ(col.seletor, Angulo(c.ligado));
        }
    }

    void AplicarLente(Coluna col, RedeEletrica.Circuito c)
    {
        if (col.lente == null) return;
        Material mat = (c.ligado && c.fusivel) ? lenteAcesa : lenteApagada;
        if (mat != null) col.lente.sharedMaterial = mat;
    }

    void AplicarVisor(int total)
    {
        if (visor == null) return;
        Material mat = MaterialDaLeitura(total);
        if (mat != null) visor.sharedMaterial = mat;
    }

    Material MaterialDaLeitura(int total)
    {
        if (leituras == null) return null;
        for (int i = 0; i < leituras.Length; i++)
        {
            Leitura l = leituras[i];
            if (l != null && l.total == total) return l.material;
        }
        return null;
    }

    float Angulo(bool ligado) => ligado ? anguloLigado : anguloDesligado;

    bool PodeAnimar() => Application.isPlaying && isActiveAndEnabled;

    void IniciarAnimacao(Coluna col, IEnumerator rotina)
    {
        PararAnimacao(col);
        if (col.seletor == null) return;
        animacoes[col] = StartCoroutine(rotina);
    }

    void PararAnimacao(Coluna col)
    {
        if (col != null && animacoes.TryGetValue(col, out Coroutine cor))
        {
            if (cor != null) StopCoroutine(cor);
            animacoes.Remove(col);
        }
    }

    void PararPiscarVisor()
    {
        if (piscarVisor != null)
        {
            StopCoroutine(piscarVisor);
            piscarVisor = null;
        }
    }

    void PararTodasAnimacoes()
    {
        foreach (Coroutine cor in animacoes.Values)
            if (cor != null) StopCoroutine(cor);
        animacoes.Clear();
        PararPiscarVisor();
    }

    IEnumerator Girar(Coluna col, float destino)
    {
        float origem = col.seletor.localEulerAngles.z;
        float duracao = Mathf.Max(0.01f, duracaoGiro);
        float decorrido = 0f;
        while (decorrido < duracao)
        {
            if (col.seletor == null) { animacoes.Remove(col); yield break; }
            decorrido += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(decorrido / duracao));
            DefinirAnguloZ(col.seletor, Mathf.LerpAngle(origem, destino, t));
            yield return null;
        }
        if (col.seletor != null) DefinirAnguloZ(col.seletor, destino);
        animacoes.Remove(col);
    }

    IEnumerator GirarEVoltar(Coluna col, float repouso, float tentado)
    {
        float origem = col.seletor.localEulerAngles.z;
        float meio = Mathf.LerpAngle(repouso, tentado, 0.5f);
        float duracao = Mathf.Max(0.01f, duracaoGiro);

        float decorrido = 0f;
        while (decorrido < duracao)
        {
            if (col.seletor == null) { animacoes.Remove(col); yield break; }
            decorrido += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(decorrido / duracao));
            DefinirAnguloZ(col.seletor, Mathf.LerpAngle(origem, meio, t));
            yield return null;
        }

        decorrido = 0f;
        while (decorrido < duracao)
        {
            if (col.seletor == null) { animacoes.Remove(col); yield break; }
            decorrido += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(decorrido / duracao));
            DefinirAnguloZ(col.seletor, Mathf.LerpAngle(meio, repouso, t));
            yield return null;
        }

        if (col.seletor != null) DefinirAnguloZ(col.seletor, repouso);
        animacoes.Remove(col);
    }

    IEnumerator PiscarVisor(int totalRecusado)
    {
        Material matRecusado = MaterialDaLeitura(totalRecusado);
        if (visor == null || matRecusado == null)
        {
            piscarVisor = null;
            AplicarVisor(RedeEletrica.Consumo);
            yield break;
        }

        float duracao = Mathf.Max(0.1f, tempoDoAviso);
        float decorrido = 0f;
        while (decorrido < duracao)
        {
            if (visor == null) { piscarVisor = null; yield break; }
            bool mostrarRecusado = ((int)(decorrido / 0.2f) % 2) == 0;
            if (mostrarRecusado)
                visor.sharedMaterial = matRecusado;
            else
                AplicarVisor(RedeEletrica.Consumo);
            yield return null;
            decorrido += Time.deltaTime;
        }

        piscarVisor = null;
        AplicarVisor(RedeEletrica.Consumo);
    }

    static void DefinirAnguloZ(Transform seletor, float anguloZ)
    {
        if (seletor == null) return;
        Vector3 e = seletor.localEulerAngles;
        e.z = anguloZ;
        seletor.localEulerAngles = e;
    }

    void Tocar(AudioClip clip)
    {
        if (clip == null) return;
        if (fonte != null) fonte.PlayOneShot(clip);
        else AudioSource.PlayClipAtPoint(clip, transform.position);
    }
}
