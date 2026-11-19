// Rest-room papers share the reception's mesh, material and inspection pipeline.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class UnclaimedDocumentosDescanso
{
    public const string Dir = "Assets/UnclaimedPrototype/Descanso/Documentos";

    [MenuItem("Tools/Unclaimed/Descanso/Build Documentos")]
    public static void Build() => UnclaimedDocumentosRecepcao.BuildDocuments(Dir, "DocumentosDescanso.json", UnclaimedDescansoAssets.Folder);

    // Places only the papers in Mauro's locker; the rest of the room's papers come with the full room build.
    [MenuItem("Tools/Unclaimed/Descanso/Place Documentos do armario de Mauro")]
    public static void PlaceMauro()
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if (t.name == "Descanso e vestiario" && t.parent != null && t.parent.name == "Ambientes")
            {
                UnclaimedMorgueGenerator.PlaceMauroLockerDocuments(t.Find("Mobiliario"));
                EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
                return;
            }
        Debug.LogWarning("Unclaimed: Ambientes/Descanso e vestiario not found.");
    }
}
