using System;
using UnityEngine;

/// <summary>A tree already lying on the ground. Trunk mesh supplies the actual road obstruction.</summary>
[DisallowMultipleComponent]
public sealed class FallenTreeObstacle : MonoBehaviour
{
    public int TrunkColliderCount { get; private set; }

    public void Place(Vector3 roadCentre, Vector3 acrossRoad, float targetLength)
    {
        foreach (var wind in GetComponentsInChildren<ForestTreeWind>()) wind.enabled = false;
        foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
        var filters = GetComponentsInChildren<MeshFilter>();
        var trunk = System.Array.Find(filters, f => f.name.StartsWith("Tree_Trunk", StringComparison.Ordinal));
        if (trunk == null) throw new InvalidOperationException("Fallen tree needs a trunk mesh");
        Bounds bounds = trunk.sharedMesh.bounds;
        float scale = targetLength / Mathf.Max(1, bounds.size.y);
        transform.localScale = Vector3.one * scale;
        Quaternion baseRotation = Quaternion.FromToRotation(Vector3.up, acrossRoad.normalized);
        Quaternion bestRotation = baseRotation;
        float bestLowest = float.MinValue;
        var vertices = trunk.sharedMesh.vertices;
        // Choose a resting roll with the trunk close to the ground, rather than balanced on a root.
        for (int step = 0; step < 16; step++)
        {
            Quaternion rotation = Quaternion.AngleAxis(step * 22.5f, acrossRoad.normalized) * baseRotation;
            float lowest = float.MaxValue;
            foreach (var vertex in vertices)
                lowest = Mathf.Min(lowest, (rotation * ((vertex - bounds.center) * scale)).y);
            if (lowest > bestLowest) { bestLowest = lowest; bestRotation = rotation; }
        }
        transform.rotation = bestRotation;
        transform.position = roadCentre - bestRotation * (bounds.center * scale) + Vector3.up * (.015f - bestLowest);
        foreach (var filter in filters)
        {
            if (!filter.name.StartsWith("Tree_Trunk", StringComparison.Ordinal)) continue;
            var collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            TrunkColliderCount++;
        }
        if (TrunkColliderCount == 0) throw new InvalidOperationException("Fallen tree needs an explicit trunk mesh");
    }
}



