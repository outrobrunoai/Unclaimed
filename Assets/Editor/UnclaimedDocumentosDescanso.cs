// Rest-room papers share the reception's mesh, material and inspection pipeline.
using UnityEditor;

public static class UnclaimedDocumentosDescanso
{
    public const string Dir = "Assets/UnclaimedPrototype/Descanso/Documentos";

    [MenuItem("Tools/Unclaimed/Descanso/Build Documentos")]
    public static void Build() => UnclaimedDocumentosRecepcao.BuildDocuments(Dir, "DocumentosDescanso.json", UnclaimedDescansoAssets.Folder);
}
