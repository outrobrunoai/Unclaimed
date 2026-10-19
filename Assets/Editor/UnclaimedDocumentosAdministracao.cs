// Narrative office papers share the reception's mesh, material and inspection pipeline.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class UnclaimedDocumentosAdministracao
{
    public const string Dir = "Assets/UnclaimedPrototype/Administracao/Documentos";
    public static string PrefabPath(string id) => UnclaimedAdministracaoAssets.PrefabPath("Documento - " + id);

    [MenuItem("Tools/Unclaimed/Build Documentos Administracao")]
    public static void Build() => UnclaimedDocumentosRecepcao.BuildDocuments(Dir, "DocumentosAdministracao.json", UnclaimedAdministracaoAssets.Folder);

    [MenuItem("Tools/Unclaimed/Place Documentos Administracao")]
    public static void Place()
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if (t.name == "Arquivo e escritorio" && t.parent != null && t.parent.name == "Ambientes")
            {
                UnclaimedMorgueGenerator.PlaceArquivoDocuments(t);
                EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
                return;
            }
        Debug.LogWarning("Unclaimed: Ambientes/Arquivo e escritorio not found.");
    }
}
