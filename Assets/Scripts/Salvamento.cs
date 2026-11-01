using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Componente cujo estado entra no save (porta aberta/trancada, gaveta, cadeado, chave coletada...).
/// Salvar devolve um texto curto (JSON) e Carregar recebe o mesmo texto depois que a cena é carregada,
/// já com Awake/OnEnable rodados e antes do Start.
/// </summary>
public interface ISalvavel
{
    string Salvar();
    void Carregar(string estado);
}

/// <summary>
/// Save único do jogo, em JSON no persistentDataPath: cena, posição e olhar do jogador, chaves do Chaveiro e o
/// estado de todo ISalvavel da cena. Os objetos são identificados pelo caminho na hierarquia + tipo do
/// componente (a cena é gerada, sem IDs próprios); nomes repetidos ganham "#n" na ordem da hierarquia.
/// Renomear ou mover um objeto faz o save antigo simplesmente não achá-lo (ele fica no estado da cena).
///
/// Fluxo: MenuPrincipal chama NovoJogo/Continuar, MenuPausa chama Salvar. Continuar carrega a cena salva e,
/// quando ela termina de carregar (sceneLoaded), aplica o estado.
/// </summary>
public static class Salvamento
{
    const int Versao = 1;

    [Serializable]
    class Registro
    {
        public string id;
        public string estado;
    }

    [Serializable]
    class Dados
    {
        public int versao;
        public string cena;
        public string data;
        public Vector3 posicao;
        public float giro, inclinacao;
        public bool agachado;
        public List<string> chaves = new List<string>();
        public List<Registro> objetos = new List<Registro>();
    }

    static Dados pendente;

    public static string Caminho => Path.Combine(Application.persistentDataPath, "save.json");
    public static bool ExisteSave => File.Exists(Caminho);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar()
    {
        pendente = null;
        SceneManager.sceneLoaded -= AoCarregarCena;
        SceneManager.sceneLoaded += AoCarregarCena;
    }

    /// <summary>Apaga o save, zera o progresso da sessão e carrega a cena do jogo do zero.</summary>
    public static void NovoJogo(string cena)
    {
        ApagarSave();
        pendente = null;
        Chaveiro.Limpar();
        SceneManager.LoadScene(cena);
    }

    /// <summary>Carrega a cena do save e restaura o estado. False se não há save legível.</summary>
    public static bool Continuar()
    {
        Dados dados = Ler();
        if (dados == null || string.IsNullOrEmpty(dados.cena)) return false;
        pendente = dados;
        Chaveiro.Limpar();
        SceneManager.LoadScene(dados.cena);
        return true;
    }

    /// <summary>Grava o estado da cena ativa. False se não há jogador na cena ou a escrita falhou.</summary>
    public static bool Salvar()
    {
        Scene cena = SceneManager.GetActiveScene();
        Movimento jogador = UnityEngine.Object.FindFirstObjectByType<Movimento>();
        if (jogador == null)
        {
            Debug.LogWarning("Salvamento: nenhum Movimento (jogador) na cena; nada foi salvo.");
            return false;
        }

        var dados = new Dados
        {
            versao = Versao,
            cena = cena.name,
            data = DateTime.Now.ToString("o"),
            posicao = jogador.transform.position,
            giro = jogador.transform.eulerAngles.y,
            inclinacao = jogador.Inclinacao,
            agachado = jogador.Agachado,
        };
        dados.chaves.AddRange(Chaveiro.Itens);
        foreach (KeyValuePair<string, ISalvavel> par in Coletar(cena))
        {
            try { dados.objetos.Add(new Registro { id = par.Key, estado = par.Value.Salvar() }); }
            catch (Exception e) { Debug.LogException(e, par.Value as UnityEngine.Object); }
        }

        try
        {
            // Escreve ao lado e troca: uma queda no meio não corrompe o save anterior.
            string temporario = Caminho + ".tmp";
            File.WriteAllText(temporario, JsonUtility.ToJson(dados, true));
            if (File.Exists(Caminho)) File.Replace(temporario, Caminho, null);
            else File.Move(temporario, Caminho);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return false;
        }
    }

    public static void ApagarSave()
    {
        try { if (File.Exists(Caminho)) File.Delete(Caminho); }
        catch (Exception e) { Debug.LogException(e); }
    }

    static Dados Ler()
    {
        try
        {
            if (!File.Exists(Caminho)) return null;
            var dados = JsonUtility.FromJson<Dados>(File.ReadAllText(Caminho));
            return dados != null && dados.versao == Versao ? dados : null;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return null;
        }
    }

    static void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        if (pendente == null || cena.name != pendente.cena) return;
        Dados dados = pendente;
        pendente = null;

        foreach (string chave in dados.chaves) Chaveiro.Adicionar(chave);

        List<KeyValuePair<string, ISalvavel>> salvaveis = Coletar(cena);
        var registros = new Dictionary<string, string>();
        foreach (Registro r in dados.objetos) registros[r.id] = r.estado;
        foreach (KeyValuePair<string, ISalvavel> par in salvaveis)
        {
            if (!registros.TryGetValue(par.Key, out string estado)) continue;
            try { par.Value.Carregar(estado); }
            catch (Exception e) { Debug.LogException(e, par.Value as UnityEngine.Object); }
        }

        Movimento jogador = UnityEngine.Object.FindFirstObjectByType<Movimento>();
        if (jogador != null) jogador.Teleportar(dados.posicao, dados.giro, dados.inclinacao, dados.agachado);
    }

    /// <summary>
    /// Todo ISalvavel da cena (inclusive inativos), na ordem da hierarquia. Cadeados vão por último: ao abrir,
    /// eles destrancam as portas do armário, por cima do estado salvo delas.
    /// </summary>
    static List<KeyValuePair<string, ISalvavel>> Coletar(Scene cena)
    {
        var lista = new List<MonoBehaviour>();
        foreach (GameObject raiz in cena.GetRootGameObjects())
            foreach (MonoBehaviour mb in raiz.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb is ISalvavel) lista.Add(mb);

        // Ids na ordem da hierarquia (o "#n" dos repetidos não depende dos cadeados irem para o fim).
        var ids = new Dictionary<MonoBehaviour, string>();
        var usados = new HashSet<string>();
        foreach (MonoBehaviour mb in lista)
        {
            string id = CaminhoNaHierarquia(mb.transform) + ":" + mb.GetType().Name;
            string unico = id;
            for (int n = 2; !usados.Add(unico); n++) unico = id + "#" + n;
            ids[mb] = unico;
        }

        var resultado = new List<KeyValuePair<string, ISalvavel>>();
        foreach (MonoBehaviour mb in lista)
            if (!(mb is CadeadoCombinacao)) resultado.Add(new KeyValuePair<string, ISalvavel>(ids[mb], (ISalvavel)mb));
        foreach (MonoBehaviour mb in lista)
            if (mb is CadeadoCombinacao) resultado.Add(new KeyValuePair<string, ISalvavel>(ids[mb], (ISalvavel)mb));
        return resultado;
    }

    static string CaminhoNaHierarquia(Transform t)
    {
        string caminho = t.name;
        for (Transform p = t.parent; p != null; p = p.parent) caminho = p.name + "/" + caminho;
        return caminho;
    }
}
