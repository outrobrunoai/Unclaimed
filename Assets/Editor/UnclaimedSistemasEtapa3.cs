// Places the stage-3 systems (phone signal zones, building exits) and the arrival intro in the morgue map.
// Idempotent: running it again updates the same objects instead of duplicating them.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class UnclaimedSistemasEtapa3
{
    const string Raiz = "Unclaimed - Necroterio (planta)";
    const string Grupo = "Sistemas - etapa 3";
    const float AlturaDoJogador = 1f;   // the player's transform sits 1 m above the floor (Jogabilidade: y 1 on floor 0)

    [MenuItem("Tools/Unclaimed/Sistemas/Colocar etapa 3 (celular, saidas e introducao)")]
    public static void Colocar()
    {
        GameObject g = GameObject.Find(Raiz);
        if (g == null) { Debug.LogWarning($"Unclaimed: '{Raiz}' not found in the active scene."); return; }
        Transform raiz = g.transform;
        Undo.SetCurrentGroupName("Place stage 3 systems");
        Transform grupo = Filho(raiz, Grupo);

        // Signal only at the reception and in the receiving yard (rain). Triggers on Ignore Raycast, out of the aim's way.
        Zona(grupo, "Sinal - recepcao", 2, new Vector3(-14.75f, 0f, -12.4f), new Vector3(-6.5f, 3.2f, -6.0f));
        Zona(grupo, "Sinal - patio", 3, new Vector3(12.75f, 0f, 5.4f), new Vector3(21f, 4f, 15.5f));

        // Exits: a thin solid panel in each opening, aimed at from inside ("Ir embora"). The front one is the pedestrian
        // gate (left open by Celso), not the glass door: the player can step out into the front yard.
        var recepcao = Saida(grupo, "Saida - entrada principal", new Vector3(-11.4f, 1.05f, -18.84f), new Vector3(2.0f, 2.1f, 0.04f));
        recepcao.fatoQueBloqueia = "corrente";
        recepcao.textoBloqueada = "Uma corrente prende a porta.";
        Saida(grupo, "Saida - portao do patio", new Vector3(18.0f, 1.1f, 5.45f), new Vector3(3.0f, 2.2f, 0.04f));

        Introducao(raiz, grupo);

        EditorSceneManager.MarkSceneDirty(g.scene);
        Debug.Log("Unclaimed: stage 3 systems and intro placed.");
    }

    static void Introducao(Transform raiz, Transform grupo)
    {
        Transform t = Filho(grupo, "Introducao - chegada");
        var intro = Garantir<IntroChegada>(t.gameObject);
        Undo.RecordObject(intro, "Intro");

        // Sidewalk (phone) -> pedestrian gate (east leaf open) -> front path -> glass door -> the player's usual start.
        var caminho = new (string nome, Vector3 pos, float giro)[]
        {
            ("Ponto 0 - calcada", new Vector3(-11.15f, 0f, -24.2f), 8f),
            ("Ponto 1 - antes do portao", new Vector3(-10.9f, 0f, -19.8f), 0f),
            ("Ponto 2 - depois do portao", new Vector3(-10.9f, 0f, -18.2f), -4f),
            ("Ponto 3 - caminho", new Vector3(-11.3f, 0f, -15.2f), 0f),
            ("Ponto 4 - porta", new Vector3(-11.375f, 0f, -13.2f), 0f),
            ("Ponto 5 - recepcao", new Vector3(-11.375f, 0f, -11.5f), 0f),
        };
        var pontos = new List<Transform>();
        foreach (var c in caminho)
        {
            Transform p = Filho(t, c.nome);
            Undo.RecordObject(p, "Intro point");
            p.SetPositionAndRotation(NoChao(c.pos) + Vector3.up * AlturaDoJogador, Quaternion.Euler(0f, c.giro, 0f));
            pontos.Add(p);
        }
        // The end is exactly the old start (floor 0 inside the reception).
        pontos[pontos.Count - 1].position = new Vector3(-11.375f, AlturaDoJogador, -11.5f);

        intro.pontos = pontos.ToArray();
        intro.contato = "celso";
        intro.mensagens = new[] { "intro_01", "intro_02", "intro_03", "intro_04", "intro_05" };
        intro.portasParaAbrir = Object.FindObjectsByType<FolhaDePorta>(FindObjectsInactive.Include)
            .Where(f => f.transform.IsChildOf(raiz) && f.transform.parent != null && f.transform.parent.name == "Porta de vidro - entrada")
            .ToArray();
        intro.segurarParaPular = 3f;
        EditorUtility.SetDirty(intro);
    }

    static Vector3 NoChao(Vector3 p)
    {
        var acertos = Physics.RaycastAll(new Vector3(p.x, 5f, p.z), Vector3.down, 10f, ~(1 << 2), QueryTriggerInteraction.Ignore);
        float y = acertos.Length == 0 ? p.y : acertos.Max(h => h.point.y);
        return new Vector3(p.x, y, p.z);
    }

    static void Zona(Transform grupo, string nome, int barras, Vector3 min, Vector3 max)
    {
        Transform t = Filho(grupo, nome);
        Undo.RecordObject(t, nome);
        t.position = (min + max) * 0.5f; t.rotation = Quaternion.identity; t.localScale = Vector3.one;
        t.gameObject.layer = 2;
        var box = Garantir<BoxCollider>(t.gameObject);
        Undo.RecordObject(box, nome);
        box.isTrigger = true; box.center = Vector3.zero; box.size = max - min;
        var z = Garantir<ZonaDeSinal>(t.gameObject);
        Undo.RecordObject(z, nome);
        z.barras = barras;
    }

    static SaidaDoPredio Saida(Transform grupo, string nome, Vector3 centro, Vector3 tamanho)
    {
        Transform t = Filho(grupo, nome);
        Undo.RecordObject(t, nome);
        t.position = centro; t.rotation = Quaternion.identity; t.localScale = Vector3.one;
        t.gameObject.layer = 0;
        var box = Garantir<BoxCollider>(t.gameObject);
        Undo.RecordObject(box, nome);
        box.isTrigger = false; box.center = Vector3.zero; box.size = tamanho;
        var s = Garantir<SaidaDoPredio>(t.gameObject);
        Undo.RecordObject(s, nome);
        return s;
    }

    static T Garantir<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : Undo.AddComponent<T>(go);
    }

    static Transform Filho(Transform pai, string nome)
    {
        Transform t = pai.Find(nome);
        if (t != null) return t;
        var go = new GameObject(nome);
        Undo.RegisterCreatedObjectUndo(go, nome);
        go.transform.SetParent(pai, false);
        return go.transform;
    }
}
