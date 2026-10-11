// COLOQUE ESTE ARQUIVO EM Assets/Editor  (crie a pasta "Editor" se não existir)
using UnityEditor;
using UnityEngine;

/// <summary>
/// Cria a layer "Inspecao" automaticamente se ela não existir no projeto.
/// Essa layer é necessária para o objeto inspecionado não atravessar paredes.
/// Roda sozinho quando o Unity abre ou recompila; também dá para rodar em
/// Morgue > Criar layer Inspecao.
/// </summary>
[InitializeOnLoad]
public static class GarantirLayerInspecao
{
    const string NomeDaLayer = "Inspecao";

    static GarantirLayerInspecao()
    {
        EditorApplication.delayCall += Garantir;
    }

    [MenuItem("Morgue/Criar layer Inspecao")]
    static void Manual()
    {
        if (LayerMask.NameToLayer(NomeDaLayer) >= 0)
            Debug.Log("A layer '" + NomeDaLayer + "' já existe.");
        else
            Garantir();
    }

    static void Garantir()
    {
        if (LayerMask.NameToLayer(NomeDaLayer) >= 0) return;

        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0) return;

        var tagManager = new SerializedObject(assets[0]);
        var layers = tagManager.FindProperty("layers");

        // Layers 0 a 7 são reservadas pela Unity; as customizadas começam na 8.
        for (int i = 8; i < layers.arraySize; i++)
        {
            var slot = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(slot.stringValue))
            {
                slot.stringValue = NomeDaLayer;
                tagManager.ApplyModifiedProperties();
                Debug.Log("Layer '" + NomeDaLayer + "' criada automaticamente (User Layer " + i + ").");
                return;
            }
        }

        Debug.LogWarning("Não há User Layer livre para criar '" + NomeDaLayer + "'. Libere uma em Project Settings > Tags and Layers.");
    }
}
