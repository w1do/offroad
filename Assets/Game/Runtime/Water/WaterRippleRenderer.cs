using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(VehiclePhysics))]
public sealed class WaterRippleRenderer : MonoBehaviour
{
    public Material material;
    [Range(8, 256)] public int maximumRipples = 64;
    public float lifetimeSeconds = 1.8f, spacingMetres = .35f;
    VehiclePhysics vehicle;
    Mesh mesh;
    Vector3[] points, previous;
    float[] ages;
    bool[] wet;
    int next;
    MaterialPropertyBlock properties;
    void OnEnable()
    {
        if(properties==null)properties=new MaterialPropertyBlock();
        vehicle = GetComponent<VehiclePhysics>();
        next = 0; previous = null; wet = null;
        points = new Vector3[Mathf.Clamp(maximumRipples, 8, 256)]; ages = new float[points.Length];
        for (int i = 0; i < ages.Length; i++) ages[i] = float.MaxValue;
        mesh = new Mesh { name = "Pooled water ripple quad" };
        mesh.vertices = new[] { new Vector3(-1,0,-1), new Vector3(-1,0,1), new Vector3(1,0,1), new Vector3(1,0,-1) };
        mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
        mesh.triangles = new[] { 0,1,2,0,2,3 }; mesh.RecalculateNormals(); mesh.RecalculateBounds();
    }
    void Update()
    {
        if (!Application.isPlaying || material == null || vehicle == null || !vehicle.Ready) return;
        if (previous == null || previous.Length != vehicle.Wheels.Length) { previous = new Vector3[vehicle.Wheels.Length]; wet = new bool[previous.Length]; }
        for (int i = 0; i < vehicle.Wheels.Length; i++)
        {
            var wheel = vehicle.Wheels[i]; vehicle.GetWheelPose(i, out var centre, out _);
            Vector3 bottom = centre - Vector3.up * wheel.radius;
            if (!WaterVolume.Sample(bottom, out _, out float depth)) { wet[i] = false; continue; }
            Vector3 surface = bottom + Vector3.up * (depth + .015f);
            if (!wet[i] || (surface - previous[i]).sqrMagnitude >= spacingMetres * spacingMetres)
            { points[next] = surface; ages[next] = 0; next = (next + 1) % points.Length; previous[i] = surface; }
            wet[i] = true;
        }
        for (int i = 0; i < points.Length; i++)
        {
            if (ages[i] >= lifetimeSeconds) continue;
            ages[i] += Time.deltaTime; float phase = Mathf.Clamp01(ages[i] / Mathf.Max(.01f, lifetimeSeconds));
            properties.SetFloat("_Opacity", (1 - phase) * .32f);
            Graphics.DrawMesh(mesh, Matrix4x4.TRS(points[i], Quaternion.identity, Vector3.one * Mathf.Lerp(.12f, 1.4f, phase)), material,
                gameObject.layer, null, 0, properties, ShadowCastingMode.Off, false);
        }
    }
    public void ShiftOrigin(Vector3 delta)
    {
        if (points != null) for (int i = 0; i < points.Length; i++) points[i] -= delta;
        if (previous != null) for (int i = 0; i < previous.Length; i++) previous[i] -= delta;
    }
    void OnDisable() { if (mesh != null) Destroy(mesh); }
}
