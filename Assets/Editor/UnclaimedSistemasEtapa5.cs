// Places the stage-5 systems (the chase) in the morgue map: the audio copy on the archive computer, hiding places in
// the empty break-room lockers and the NavMesh Daniel chases on. Idempotent: running it again rebuilds the same objects.
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class UnclaimedSistemasEtapa5
{
    const string Raiz = "Unclaimed - Necroterio (planta)";
    const string Grupo = "Sistemas - etapa 5";
    const string Prefabs = "Assets/Prefabs/Sistemas/";
    const string NavMeshAsset = "Assets/Scenes/PrototipoNecroterio/NavMesh.asset";

    // Monitor of Nina's desk kit in the desk frame: KIT monitor_x 0.15 + MESA kx -0.25, monitor_y -0.22 + MESA dy 0.08, screen_z 1.0.
    static readonly Vector3 MonitorNaMesa = new Vector3(-0.10f, 1.0f, -0.13f);
    // Break-room lockers whose left compartment (door "Porta 1") is empty: the player opens it and hides inside.
    static readonly string[] Armarios = { "Armarios - parede 1", "Armarios - parede 2", "Armarios - parede 3" };

    [MenuItem("Tools/Unclaimed/Sistemas/Colocar etapa 5 (perseguição)")]
    public static void Colocar()
    {
        GameObject g = GameObject.Find(Raiz);
        if (g == null) { Debug.LogWarning($"Unclaimed: '{Raiz}' not found in the active scene."); return; }
        Transform raiz = g.transform;
        Undo.SetCurrentGroupName("Place stage 5 systems");
        Transform grupo = Filho(raiz, Grupo);

        CopiaDoAudio(raiz, grupo);
        Esconderijos(raiz);
        AssarNavMesh(grupo);

        EditorSceneManager.MarkSceneDirty(g.scene);
        Debug.Log("Unclaimed: stage 5 systems placed.");
    }

    // ------------------------------------------------------------------ audio copy

    static void CopiaDoAudio(Transform raiz, Transform grupo)
    {
        Transform mesa = Achar(raiz, "Mesa do servidor");
        if (mesa == null) { Debug.LogWarning("Unclaimed: archive desk not found."); return; }
        Remover(grupo, "Copia do audio");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Copia do audio.prefab"), grupo);
        go.name = "Copia do audio";
        Undo.RegisterCreatedObjectUndo(go, go.name);
        // An invisible target over the monitor's screen (the prefab's box is only a placeholder).
        go.transform.SetPositionAndRotation(mesa.TransformPoint(MonitorNaMesa), mesa.rotation);
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>()) r.enabled = false;
    }

    // ------------------------------------------------------------------ hiding places

    static void Esconderijos(Transform raiz)
    {
        foreach (string nome in Armarios)
        {
            Transform armario = Achar(raiz, nome);
            if (armario == null) { Debug.LogWarning("Unclaimed: locker not found: " + nome); continue; }
            Remover(armario, "Esconderijo");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Esconderijo.prefab"), armario);
            go.name = "Esconderijo";
            Undo.RegisterCreatedObjectUndo(go, go.name);
            // Left compartment: x -0.327..0, depth -0.23..0.22, floor at 0.112; its door faces local -Z.
            go.transform.SetLocalPositionAndRotation(new Vector3(-0.165f, 0f, -0.01f), Quaternion.identity);
            var box = go.GetComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.0f, 0f);
            box.size = new Vector3(0.30f, 1.75f, 0.40f);
            var e = go.GetComponent<Esconderijo>();
            Transform porta = armario.Find("Porta 1");
            e.porta = porta != null ? porta.GetComponent<PecaDeMovel>() : null;
            e.agachado = false;     // tall locker: standing, looking straight through the crack
            e.inclinacao = 0f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(e);
            e.dentro.SetLocalPositionAndRotation(new Vector3(0f, 1.112f, 0.06f), Quaternion.Euler(0f, 180f, 0f));   // player root = capsule centre, 1 m above the floor
            e.saida.SetLocalPositionAndRotation(new Vector3(0f, 1.0f, -0.75f), Quaternion.Euler(0f, 180f, 0f));
            PrefabUtility.RecordPrefabInstancePropertyModifications(box);
            PrefabUtility.RecordPrefabInstancePropertyModifications(e.dentro);
            PrefabUtility.RecordPrefabInstancePropertyModifications(e.saida);
        }
    }

    // ------------------------------------------------------------------ NavMesh

    // Baked from the physics colliders with every door leaf out of the way (Daniel opens them, DanielAbrePortas) and
    // without trigger volumes (rooms, interaction targets). Agent: 0.3 m radius so he fits the 0.9 m doors.
    static void AssarNavMesh(Transform grupo)
    {
        AjustarAgente(0.3f, 1.8f, 0.3f);
        var surface = grupo.GetComponent<NavMeshSurface>();
        if (surface == null) surface = Undo.AddComponent<NavMeshSurface>(grupo.gameObject);
        surface.agentTypeID = 0;
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = ~(1 << 2);   // Ignore Raycast stays out

        var desligados = new List<Collider>();
        foreach (Collider c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!c.enabled) continue;
            if (c.isTrigger || c.GetComponentInParent<FolhaDePorta>() != null) { c.enabled = false; desligados.Add(c); }
        }
        try { surface.BuildNavMesh(); }
        finally { foreach (Collider c in desligados) c.enabled = true; }

        if (surface.navMeshData != null)
        {
            if (AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshAsset) != null) AssetDatabase.DeleteAsset(NavMeshAsset);
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshAsset);
            AssetDatabase.SaveAssets();
        }
        EditorUtility.SetDirty(surface);
    }

    static void AjustarAgente(float raio, float altura, float degrau)
    {
        var obj = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset");
        if (obj == null || obj.Length == 0) return;
        var so = new SerializedObject(obj[0]);
        SerializedProperty lista = so.FindProperty("m_Settings");
        if (lista == null || lista.arraySize == 0) return;
        SerializedProperty s = lista.GetArrayElementAtIndex(0);
        s.FindPropertyRelative("agentRadius").floatValue = raio;
        s.FindPropertyRelative("agentHeight").floatValue = altura;
        s.FindPropertyRelative("agentClimb").floatValue = degrau;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ helpers

    static Transform Achar(Transform raiz, string nome)
    {
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
            if (t.name == nome) return t;
        return null;
    }

    static void Remover(Transform pai, string nome)
    {
        Transform t = pai.Find(nome);
        if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
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
