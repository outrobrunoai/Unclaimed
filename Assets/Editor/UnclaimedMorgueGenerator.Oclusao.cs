// Occlusion culling: walls, ceilings and the outer shell hide what is behind them, so inside the building the
// camera no longer draws the yard, the street and the other rooms through the walls (measured 2026-10-19:
// without it the reception view still drew ~5 M triangles, most of them outdoors). Door leaves, glass and
// furniture are never occluders (they move or can be seen through); everything else is culled dynamically.
// The same data drives SombrasPorVisibilidade (shadow maps of hidden spots are skipped), placed here too.
// The data must be re-baked whenever the structure is regenerated: Bake Prototype Lighting does it first.
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static partial class UnclaimedMorgueGenerator
{
    private static readonly string[] OccluderGroups = { "Paredes", "Tetos", "Fechamento externo" };

    // Umbra cell sizes in metres: walls are 0.15 m thick and the narrowest wall pieces beside doors ~0.3 m,
    // the smallest real opening is a 0.85 m doorway.
    private const float SmallestOccluder = 0.5f, SmallestHole = 0.1f;

    [MenuItem("Tools/Unclaimed/Bake Occlusion Culling")]
    public static void BakeOcclusionCulling()
    {
        GameObject building = GameObject.Find(GroupPrefix + " (planta)");
        if (building == null) { Debug.LogWarning("Unclaimed: '" + GroupPrefix + " (planta)' not found; occlusion not baked."); return; }
        int occluders = MarkOccluders(building.transform);
        StaticOcclusionCulling.smallestOccluder = SmallestOccluder;
        StaticOcclusionCulling.smallestHole = SmallestHole;
        StaticOcclusionCulling.backfaceThreshold = 100f;
        var timer = System.Diagnostics.Stopwatch.StartNew();
        // Reuse an existing manager or named object, including inactive children, in this scene.
        GameObject shadowManager = null;
        foreach (GameObject root in building.scene.GetRootGameObjects())
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.GetComponent<SombrasPorVisibilidade>() != null)
                {
                    shadowManager = candidate.gameObject;
                    break;
                }
                if (shadowManager == null && candidate.name == "Sombras por visibilidade")
                    shadowManager = candidate.gameObject;
            }
        if (shadowManager == null)
        {
            shadowManager = new GameObject("Sombras por visibilidade");
            Undo.RegisterCreatedObjectUndo(shadowManager, "Create shadow visibility manager");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(shadowManager, building.scene);
        }
        Undo.RecordObject(shadowManager, "Configure shadow visibility manager");
        shadowManager.name = "Sombras por visibilidade";
        shadowManager.isStatic = false;
        if (shadowManager.transform.parent != null)
            Undo.SetTransformParent(shadowManager.transform, null, "Move shadow visibility manager to scene root");
        if (shadowManager.GetComponent<SombrasPorVisibilidade>() == null)
            Undo.AddComponent<SombrasPorVisibilidade>(shadowManager);
        PinPunctualShadowResolution(building.scene);
        bool ok = StaticOcclusionCulling.Compute();
        Debug.Log("Unclaimed: occlusion culling " + (ok ? "baked" : "FAILED") + ": " + occluders + " occluders, " +
            StaticOcclusionCulling.umbraDataSize + " bytes, " + timer.Elapsed.TotalSeconds.ToString("F1") + " s.");
    }

    // The six shadowed spots asked for the High tier (1024) but only fit the 2048 shadow atlas at 512, so URP
    // shrank them all. Once SombrasPorVisibilidade drops hidden ones, the rest would jump to 1024 (visible
    // sharpening while walking). Pinning Medium keeps exactly today's 512 and fits up to 16 spots.
    private static void PinPunctualShadowResolution(UnityEngine.SceneManagement.Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Light light in root.GetComponentsInChildren<Light>(true))
            {
                if (light.type == LightType.Directional || light.shadows == LightShadows.None) continue;
                if (!light.TryGetComponent(out UniversalAdditionalLightData data))
                    data = Undo.AddComponent<UniversalAdditionalLightData>(light.gameObject);
                // The property throws outside Play Mode; the serialized field is what the scene keeps.
                var so = new SerializedObject(data);
                SerializedProperty tier = so.FindProperty("m_AdditionalLightsShadowResolutionTier");
                if (tier.intValue == UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium) continue;
                tier.intValue = UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium;
                so.ApplyModifiedProperties();
            }
    }

    // Occluder + occludee on opaque structural renderers; the flag is removed from anything that moves or is clear.
    private static int MarkOccluders(Transform building)
    {
        int count = 0;
        foreach (string groupName in OccluderGroups)
        {
            Transform group = building.Find(groupName);
            if (group == null) continue;
            foreach (MeshRenderer r in group.GetComponentsInChildren<MeshRenderer>(true))
            {
                var flags = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
                bool solid = r.gameObject.isStatic && r.shadowCastingMode != ShadowCastingMode.ShadowsOnly;
                foreach (Material m in r.sharedMaterials)
                    if (m == null || m.renderQueue >= (int)RenderQueue.AlphaTest) solid = false;
                var wanted = solid ? flags | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic
                                   : flags & ~StaticEditorFlags.OccluderStatic;
                if (wanted != flags) GameObjectUtility.SetStaticEditorFlags(r.gameObject, wanted);
                if (solid) count++;
            }
        }
        return count;
    }
}
