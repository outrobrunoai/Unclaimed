// Place this file in Assets/Editor/UnclaimedMorgueGenerator.cs.
// Run Tools > Unclaimed > Generate Morgue Prototype in an open Unity scene.
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class UnclaimedMorgueGenerator
{
    private const float WallHeight = 3.2f;
    private const float WallThickness = 0.2f;
    private const float DoorWidth = 1.5f;

    private static Transform root;
    private static Material floorMat, wallMat, corridorMat, furnitureMat, bodyMat, clueMat;

    [MenuItem("Tools/Unclaimed/Generate Morgue Prototype")]
    public static void Generate()
    {
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Generate Unclaimed morgue prototype");

        var container = new GameObject("Unclaimed - Necroterio (prototipo)");
        Undo.RegisterCreatedObjectUndo(container, "Generate morgue");
        root = container.transform;

        floorMat = GetMaterial("Floor", new Color(0.27f, 0.30f, 0.32f));
        wallMat = GetMaterial("Walls", new Color(0.68f, 0.72f, 0.69f));
        corridorMat = GetMaterial("Corridor", new Color(0.20f, 0.25f, 0.27f));
        furnitureMat = GetMaterial("Furniture", new Color(0.28f, 0.36f, 0.40f));
        bodyMat = GetMaterial("Body placeholders", new Color(0.58f, 0.60f, 0.56f));
        clueMat = GetMaterial("Clue placeholders", new Color(0.90f, 0.66f, 0.17f));

        // X runs from the entrance to the cold chamber. Z spans both rows of rooms.
        Box("Corredor central", new Vector3(0, -0.1f, 0), new Vector3(26, 0.2f, 3), corridorMat);
        float[] edges = { -13, -7, -1, 5, 13 };
        string[] north = { "Recepcao", "Pertences pessoais", "Preparo", "Sala tecnica" };
        string[] south = { "Escritorio", "Arquivo", "Autopsia", "Camara fria" };

        for (int i = 0; i < 4; i++)
        {
            float midX = (edges[i] + edges[i + 1]) * 0.5f;
            float width = edges[i + 1] - edges[i];
            Room(north[i], midX, 5.5f, width, true);
            Room(south[i], midX, -5.5f, width, false);
        }

        // Outer walls, partitions, and an entrance gap at the west end of the hall.
        WallX("Parede externa norte", 0, 9.5f, 26);
        WallX("Parede externa sul", 0, -9.5f, 26);
        WallZ("Parede externa leste", 13, 0, 19);
        WallZ("Parede oeste superior", -13, 5.5f, 8);
        WallZ("Parede oeste inferior", -13, -5.5f, 8);
        for (int i = 1; i < edges.Length - 1; i++)
        {
            WallZ("Divisoria norte " + i, edges[i], 5.5f, 8);
            WallZ("Divisoria sul " + i, edges[i], -5.5f, 8);
        }

        Populate();
        Marker("INICIO - entrada do turno", new Vector3(-11.5f, 0.1f, 0));
        Marker("EVENTO - objeto muda de lugar", new Vector3(-4, 0.5f, 5));
        Marker("EVENTO - som na camara fria", new Vector3(9, 0.5f, -5.5f));
        Marker("FINAL - identificacao do culpado", new Vector3(0, 0.1f, 0));

        // Lights are placeholders so the floor plan can be inspected immediately.
        for (float x = -10; x <= 10; x += 5)
            Light("Luz do corredor", new Vector3(x, 2.8f, 0), 6.5f, 2.5f);
        foreach (float x in new[] { -10f, -4f, 2f, 9f })
        {
            Light("Luz da sala norte", new Vector3(x, 2.8f, 5.5f), 7, 2);
            Light("Luz da sala sul", new Vector3(x, 2.8f, -5.5f), 7, 2);
        }

        Selection.activeGameObject = container;
        SceneView.lastActiveSceneView?.FrameSelected();
        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log("Unclaimed: prototype generated. Save the scene and add your player controller at INICIO.");
    }

    private static void Room(string name, float x, float z, float width, bool north)
    {
        Box(name + " - piso", new Vector3(x, -0.1f, z), new Vector3(width, 0.2f, 8), floorMat);
        // Each corridor-facing wall has a physical 1.5 m opening, with no blocking collider.
        float doorZ = north ? 1.5f : -1.5f;
        float segment = (width - DoorWidth) * 0.5f;
        WallX(name + " - parede esquerda", x - (width + DoorWidth) * 0.25f, doorZ, segment);
        WallX(name + " - parede direita", x + (width + DoorWidth) * 0.25f, doorZ, segment);
        // A short header indicates a doorway without filling the walkable opening.
        Box(name + " - verga da porta", new Vector3(x, 2.7f, doorZ),
            new Vector3(DoorWidth, 1, WallThickness), wallMat);
    }

    private static void Populate()
    {
        Box("Balcao de entrada", new Vector3(-10, 0.55f, 6.3f), new Vector3(3, 1.1f, 0.7f), furnitureMat);
        Box("Mesa do escritorio", new Vector3(-10, 0.45f, -6), new Vector3(2.2f, 0.9f, 1), furnitureMat);
        Box("Armario de documentos", new Vector3(-4.2f, 1, -8.7f), new Vector3(3.5f, 2, 0.5f), furnitureMat);
        Box("Prateleira de pertences", new Vector3(-4, 1, 8.7f), new Vector3(4, 2, 0.5f), furnitureMat);
        Box("Bancada de preparo", new Vector3(2, 0.45f, 6), new Vector3(3.1f, 0.9f, 1), furnitureMat);
        Box("Quadro eletrico", new Vector3(9, 1.2f, 8.9f), new Vector3(1.4f, 2, 0.4f), furnitureMat);

        for (int i = 0; i < 2; i++)
        {
            float x = 0.6f + i * 2.8f;
            Box("Mesa de autopsia " + (i + 1), new Vector3(x, 0.65f, -6),
                new Vector3(1.1f, 1.3f, 2.3f), furnitureMat);
            Box("Corpo provisório - autopsia " + (i + 1), new Vector3(x, 1.38f, -6),
                new Vector3(0.65f, 0.15f, 1.8f), bodyMat);
        }

        for (int i = 0; i < 6; i++)
        {
            float x = 6.6f + (i % 3) * 2.3f;
            float z = -4.1f - (i / 3) * 3.1f;
            Box("Gaveta mortuaria " + (i + 1), new Vector3(x, 0.55f, z),
                new Vector3(1, 1.1f, 2), furnitureMat);
            Box("Corpo provisório " + (i + 1), new Vector3(x, 1.17f, z),
                new Vector3(0.65f, 0.14f, 1.65f), bodyMat);
            Marker("EXAMINAR - corpo " + (i + 1), new Vector3(x, 1.4f, z));
        }

        Clue("Pista - registro de entrada", -10, 1.2f, 6.3f);
        Clue("Pista - objeto pessoal", -4, 2.1f, 8.4f);
        Clue("Pista - prontuario", -4.2f, 2.1f, -8.4f);
        Clue("Pista - instrumento deslocado", 2, 1.1f, 6);
        Clue("Pista - laudo da autopsia", 2, 1.45f, -6);
    }

    private static void Clue(string name, float x, float y, float z)
    {
        Box(name, new Vector3(x, y, z), new Vector3(0.25f, 0.15f, 0.25f), clueMat);
    }

    private static void Marker(string name, Vector3 position)
    {
        var marker = new GameObject(name);
        marker.transform.SetParent(root, false);
        marker.transform.position = position;
        Undo.RegisterCreatedObjectUndo(marker, "Create marker");
    }

    private static void WallX(string name, float x, float z, float width)
    {
        Box(name, new Vector3(x, WallHeight / 2, z),
            new Vector3(width, WallHeight, WallThickness), wallMat);
    }

    private static void WallZ(string name, float x, float z, float length)
    {
        Box(name, new Vector3(x, WallHeight / 2, z),
            new Vector3(WallThickness, WallHeight, length), wallMat);
    }

    private static GameObject Box(string name, Vector3 position, Vector3 size, Material material)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(obj, "Create " + name);
        obj.name = name;
        obj.transform.SetParent(root, false);
        obj.transform.position = position;
        obj.transform.localScale = size;
        obj.GetComponent<Renderer>().sharedMaterial = material;
        return obj;
    }

    private static void Light(string name, Vector3 position, float range, float intensity)
    {
        var obj = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(obj, "Create light");
        obj.transform.SetParent(root, false);
        obj.transform.position = position;
        var light = obj.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = range;
        light.intensity = intensity;
    }

    private static Material GetMaterial(string name, Color color)
    {
        const string folder = "Assets/UnclaimedPrototype";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets", "UnclaimedPrototype");
        string path = folder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        string pipeline = GraphicsSettings.currentRenderPipeline == null
            ? "Built-in" : GraphicsSettings.currentRenderPipeline.GetType().Name;
        string shaderName = pipeline.Contains("HDRenderPipeline") ? "HDRP/Lit"
            : pipeline.Contains("Universal") ? "Universal Render Pipeline/Lit" : "Standard";
        var shader = Shader.Find(shaderName) ?? Shader.Find("Standard");
        material = new Material(shader) { name = name };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        else material.color = color;
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
