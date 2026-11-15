using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Carrega a cena de jogabilidade junto com o mapa, sem mudar a cena ativa.</summary>
public class CarregarJogabilidade : MonoBehaviour
{
    [Tooltip("Nome da cena de jogabilidade que será carregada junto com o mapa.")]
    public string cena = "Jogabilidade";

    static readonly HashSet<string> carregando = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar()
    {
        carregando.Clear();
        SceneManager.sceneLoaded -= AoCarregarCena;
        SceneManager.sceneLoaded += AoCarregarCena;
    }

    void Awake()
    {
        Scene carregada = SceneManager.GetSceneByName(cena);
        if (carregada.IsValid() && carregada.isLoaded) return;
        if (!carregando.Add(cena)) return;

        try
        {
            SceneManager.LoadScene(cena, LoadSceneMode.Additive);
        }
        catch
        {
            carregando.Remove(cena);
            throw;
        }
    }

    static void AoCarregarCena(Scene carregada, LoadSceneMode modo)
    {
        carregando.Remove(carregada.name);
    }
}
