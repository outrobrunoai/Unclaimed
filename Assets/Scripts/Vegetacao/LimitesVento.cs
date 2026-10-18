using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways, DisallowMultipleComponent]
public sealed class LimitesVento : MonoBehaviour
{
    [Min(0)] public float margem = 0.6f;
    readonly Dictionary<Renderer, Bounds> originais = new Dictionary<Renderer, Bounds>();

    void OnEnable()
    {
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            Bounds bounds = renderer.localBounds;
            originais[renderer] = bounds;
            // Convert a world-metre margin to each renderer's local axes, including rotated/scaled children.
            Matrix4x4 inverse = renderer.transform.worldToLocalMatrix;
            Vector3 extra = Vector3.zero;
            for (int axis = 0; axis < 3; axis++)
                extra[axis] = Mathf.Abs(inverse[axis, 0]) * Mathf.Max(0, margem)
                    + Mathf.Abs(inverse[axis, 1]) * 0.2f + Mathf.Abs(inverse[axis, 2]) * Mathf.Max(0, margem);
            bounds.Expand(extra * 2);
            renderer.localBounds = bounds;
        }
    }

    void OnDisable()
    {
        foreach (var entry in originais) if (entry.Key) entry.Key.localBounds = entry.Value;
        originais.Clear();
    }
}
