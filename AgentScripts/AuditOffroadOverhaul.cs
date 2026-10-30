using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

public static class AuditOffroadOverhaul
{
    public static string Main()
    {
        var vehicles = UnityEngine.Object.FindObjectsByType<VehiclePhysics>();
        var mud = UnityEngine.Object.FindObjectsByType<SoftGroundRuts>();
        var renderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>();
        var report = new {
            utc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion,
            scene = SceneManager.GetActiveScene().path, playing = Application.isPlaying,
            renderPipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.name,
            fixedStepSeconds = Time.fixedDeltaTime,
            terrains = UnityEngine.Object.FindObjectsByType<Terrain>().Length,
            rigidbodies = UnityEngine.Object.FindObjectsByType<Rigidbody>().Length,
            wheelColliders = UnityEngine.Object.FindObjectsByType<WheelCollider>().Length,
            meshColliders = UnityEngine.Object.FindObjectsByType<MeshCollider>().Length,
            particles = UnityEngine.Object.FindObjectsByType<ParticleSystem>().Length,
            vehicles = vehicles.Select(v => new { v.name, profile = AssetDatabase.GetAssetPath(v.profile), equipment = AssetDatabase.GetAssetPath(v.profile.equipment), axles = v.axles.Length }).ToArray(),
            soil = mud.Select(m => new { m.name, profile = AssetDatabase.GetAssetPath(m.profile), position = m.transform.position.ToString(), vertices = m.GetComponent<MeshFilter>().sharedMesh.vertexCount }).ToArray(),
            waterMeshes = renderers.Where(r => r.name.IndexOf("water", StringComparison.OrdinalIgnoreCase) >= 0).Select(r => new { r.name, bounds = r.bounds.ToString(), materials = r.sharedMaterials.Select(m => m == null ? "missing" : m.name).ToArray() }).ToArray(),
            materials = renderers.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().Select(m => new { m.name, shader = m.shader.name }).ToArray(),
            hardware = new { SystemInfo.processorType, SystemInfo.graphicsDeviceName, SystemInfo.systemMemorySize }
        };
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented);
        Directory.CreateDirectory("docs/reports"); File.WriteAllText("docs/reports/offroad-audit.json", json);
        return json;
    }
}
