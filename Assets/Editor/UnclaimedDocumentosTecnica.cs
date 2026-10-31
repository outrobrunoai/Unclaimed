// Technical-room papers share the reception's mesh, material and inspection pipeline.
using UnityEditor;

public static class UnclaimedDocumentosTecnica
{
    public const string Dir = "Assets/UnclaimedPrototype/Tecnica/Documentos";

    [MenuItem("Tools/Unclaimed/Tecnica/Build Documentos")]
    public static void Build() => UnclaimedDocumentosRecepcao.BuildDocuments(Dir, "DocumentosTecnica.json", Dir);
}
