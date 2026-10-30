using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class WaterVolume : MonoBehaviour
{
    public float densityKgPerCubicMetre = 1000, dragCoefficient = 1.1f;
    public Vector3 localFlowMetresPerSecond;
    public float localSurfaceHeight;
    public MeshFilter surfaceMesh;
    Vector3[] vertices;
    int[] triangles;
    readonly Dictionary<Vector2Int, List<int>> surfaceGrid = new Dictionary<Vector2Int, List<int>>();
    static readonly List<WaterVolume> active = new List<WaterVolume>();
    BoxCollider bounds;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void NewSession() { active.Clear(); }
    void OnEnable() { bounds = GetComponent<BoxCollider>(); bounds.isTrigger = true; BuildSurface(); if (!active.Contains(this)) active.Add(this); }
    public void BuildSurface()
    {
        bounds = GetComponent<BoxCollider>();
        surfaceGrid.Clear(); vertices = null; triangles = null;
        if (surfaceMesh == null || surfaceMesh.sharedMesh == null) return;
        vertices = surfaceMesh.sharedMesh.vertices; triangles = surfaceMesh.sharedMesh.triangles;
        for (int i = 0; i < vertices.Length; i++) vertices[i] = transform.InverseTransformPoint(surfaceMesh.transform.TransformPoint(vertices[i]));
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 lo = Vector3.Min(vertices[triangles[i]], Vector3.Min(vertices[triangles[i + 1]], vertices[triangles[i + 2]]));
            Vector3 hi = Vector3.Max(vertices[triangles[i]], Vector3.Max(vertices[triangles[i + 1]], vertices[triangles[i + 2]]));
            for (int z = Mathf.FloorToInt(lo.z); z <= Mathf.FloorToInt(hi.z); z++)
                for (int x = Mathf.FloorToInt(lo.x); x <= Mathf.FloorToInt(hi.x); x++)
                { var key = new Vector2Int(x, z); if (!surfaceGrid.TryGetValue(key, out var list)) surfaceGrid[key] = list = new List<int>(); list.Add(i); }
        }
    }
    void OnDisable() { active.Remove(this); }
    public float Depth(Vector3 world)
    {
        if (bounds == null || !bounds.enabled) return 0;
        Vector3 p = transform.InverseTransformPoint(world), d = p - bounds.center;
        if (Mathf.Abs(d.x) > bounds.size.x * .5f || Mathf.Abs(d.z) > bounds.size.z * .5f
            || d.y < -bounds.size.y * .5f || p.y > localSurfaceHeight) return 0;
        float surface = localSurfaceHeight;
        if (vertices != null)
        {
            surface = float.NegativeInfinity;
            if (!surfaceGrid.TryGetValue(new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z)), out var candidates)) return 0;
            foreach (int i in candidates)
            {
                Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                float determinant = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(determinant) < .000001f) continue;
                float u = ((b.z - c.z) * (p.x - c.x) + (c.x - b.x) * (p.z - c.z)) / determinant;
                float v = ((c.z - a.z) * (p.x - c.x) + (a.x - c.x) * (p.z - c.z)) / determinant;
                if (u >= -.0001f && v >= -.0001f && u + v <= 1.0001f) surface = Mathf.Max(surface, u * a.y + v * b.y + (1 - u - v) * c.y);
            }
            if (float.IsNegativeInfinity(surface)) return 0;
        }
        return Mathf.Max(0, transform.TransformPoint(new Vector3(p.x, surface, p.z)).y - world.y);
    }
    public static bool Sample(Vector3 world, out WaterVolume volume, out float depth)
    {
        volume = null; depth = 0;
        foreach (var water in active) { float candidate = water.Depth(world); if (candidate > depth) { depth = candidate; volume = water; } }
        return volume != null;
    }
    public Vector3 Flow => transform.TransformDirection(localFlowMetresPerSecond);
}
