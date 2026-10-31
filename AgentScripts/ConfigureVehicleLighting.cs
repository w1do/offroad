using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ConfigureVehicleLighting
{
    const string Folder = "Assets/Game/Configs/VehicleLighting";
    public static object Main()
    {
        if (Application.isPlaying) throw new Exception("Stop Play Mode before configuring lamps.");
        var vehicle = UnityEngine.Object.FindAnyObjectByType<VehiclePhysics>();
        if (vehicle == null || vehicle.profile == null) throw new Exception("Open MountainPass with the configured vehicle.");
        if (vehicle.gameObject.scene.path != "Assets/MountainDrive/MountainPass.unity") throw new Exception("Expected MountainPass.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Game/Configs", "VehicleLighting");
        var profile = AssetDatabase.LoadAssetAtPath<VehicleLightingProfile>(Folder + "/Pajero lighting.asset");
        if (profile == null) { profile = ScriptableObject.CreateInstance<VehicleLightingProfile>(); AssetDatabase.CreateAsset(profile, Folder + "/Pajero lighting.asset"); }
        profile.lowBeam.cookie = Cookie("Low beam cutoff", VehicleLampKind.LowBeam);
        profile.highBeam.cookie = Cookie("High beam focus", VehicleLampKind.HighBeam);
        profile.fog.cookie = Cookie("Fog wide strip", VehicleLampKind.Fog);
        var lensMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Lamp lens.mat");
        if (lensMaterial == null) { lensMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(lensMaterial, Folder + "/Lamp lens.mat"); }
        lensMaterial.SetFloat("_Surface", 1);
        lensMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        lensMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        lensMaterial.SetFloat("_ZWrite", 0);
        lensMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        lensMaterial.renderQueue = 3000;
        vehicle.profile.lighting = profile;
        var lighting = vehicle.GetComponent<VehicleLighting>();
        if (lighting == null) lighting = Undo.AddComponent<VehicleLighting>(vehicle.gameObject);
        lighting.environment = UnityEngine.Object.FindAnyObjectByType<ForestDayNightWeather>();
        if (lighting.lamps == null || lighting.lamps.Length == 0)
        {
            var bindings = new List<VehicleLampBinding>();
            foreach (VehicleLampKind kind in Enum.GetValues(typeof(VehicleLampKind)))
                foreach (int side in new[] { -1, 1 })
                {
                    var node = new GameObject(kind + (side < 0 ? " left" : " right"));
                    Undo.RegisterCreatedObjectUndo(node, "Add headlight");
                    node.transform.SetParent(vehicle.transform, false);
                    float x = kind == VehicleLampKind.HighBeam ? 0.48f : kind == VehicleLampKind.Fog ? 0.62f : 0.66f;
                    node.transform.localPosition = new Vector3(side * x, kind == VehicleLampKind.Fog ? 0.77f : 0.95f, kind == VehicleLampKind.Fog ? 2.23f : 2.22f);
                    node.transform.localRotation = Quaternion.Euler(kind == VehicleLampKind.HighBeam ? 0.7f : kind == VehicleLampKind.Fog ? 7f : 3f, 0, 0);
                    var light = node.AddComponent<Light>();
                    light.type = LightType.Spot;
                    light.enabled = false;
                    light.shadowBias = 0.02f;
                    light.shadowNormalBias = 0.04f;
                    light.shadowNearPlane = 0.06f;
                    bindings.Add(new VehicleLampBinding { kind = kind, light = light });
                }
            lighting.lamps = bindings.ToArray();
        }
        foreach (var binding in lighting.lamps)
        {
            if (binding.lens != null && binding.lens.name == "Dedicated lamp lens") UnityEngine.Object.DestroyImmediate(binding.lens.gameObject);
            var mount = binding.light.transform;
            var local = mount.localPosition;
            local.y = binding.kind == VehicleLampKind.Fog ? 0.77f : 0.95f;
            local.z = binding.kind == VehicleLampKind.Fog ? 2.23f : 2.22f;
            mount.localPosition = local;
        }
        foreach (int side in new[] { -1, 1 })
        {
            var headlamp = OriginalGlass(vehicle, "Object_33 - clearglass", side, false, lensMaterial);
            var foglamp = OriginalGlass(vehicle, "Object_32 - clearglass", side, true, lensMaterial);
            foreach (var binding in lighting.lamps)
                if (Mathf.Sign(binding.light.transform.localPosition.x) == side) binding.lens = binding.kind == VehicleLampKind.Fog ? foglamp : headlamp;
        }
        lighting.Evaluate(1, false, 0);
        EditorUtility.SetDirty(profile);
        EditorUtility.SetDirty(lensMaterial);
        EditorUtility.SetDirty(vehicle.profile);
        EditorUtility.SetDirty(lighting);
        AssetDatabase.SaveAssets();
        var scene = vehicle.gameObject.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Scene save failed.");
        return new { vehicle = vehicle.name, profile = AssetDatabase.GetAssetPath(profile), lamps = lighting.lamps.Length, mode = profile.initialMode.ToString() };
    }
    static Renderer OriginalGlass(VehiclePhysics vehicle, string sourceName, int side, bool fog, Material material)
    {
        var source = vehicle.GetComponentsInChildren<MeshFilter>().First(f => f.name == sourceName);
        var mesh = source.sharedMesh;
        var transform = vehicle.transform.worldToLocalMatrix * source.transform.localToWorldMatrix;
        var normalMatrix = transform.inverse.transpose;
        var vertices = mesh.vertices;
        var normals = mesh.normals;
        var uv = mesh.uv;
        var triangles = mesh.triangles;
        var points = new List<Vector3>(); var ns = new List<Vector3>(); var uvs = new List<Vector2>(); var indices = new List<int>();
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 center = Vector3.zero;
            for (int j = 0; j < 3; j++) center += transform.MultiplyPoint3x4(vertices[triangles[i + j]]) / 3f;
            if (center.z < 1.85f || center.x * side <= 0 || (fog ? center.y > 0.86f : center.y < 0.86f)) continue;
            int first = points.Count;
            for (int j = 0; j < 3; j++)
            {
                int index = triangles[i + j];
                var normal = normalMatrix.MultiplyVector(normals[index]).normalized;
                points.Add(transform.MultiplyPoint3x4(vertices[index]) + normal * 0.0015f);
                ns.Add(normal); uvs.Add(uv.Length == vertices.Length ? uv[index] : Vector2.zero);
            }
            indices.Add(first); indices.Add(first + (transform.determinant < 0 ? 2 : 1)); indices.Add(first + (transform.determinant < 0 ? 1 : 2));
        }
        if (indices.Count == 0) throw new Exception("No original front lamp glass found: " + sourceName);
        string name = (fog ? "Fog" : "Headlamp") + (side < 0 ? " left" : " right") + " original glass glow";
        var derived = new Mesh { name = name };
        derived.SetVertices(points); derived.SetNormals(ns); derived.SetUVs(0, uvs); derived.SetTriangles(indices, 0); derived.RecalculateBounds();
        string path = Folder + "/" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null) { AssetDatabase.CreateAsset(derived, path); existing = derived; }
        else { EditorUtility.CopySerialized(derived, existing); UnityEngine.Object.DestroyImmediate(derived); EditorUtility.SetDirty(existing); }
        var child = vehicle.transform.Find(name);
        if (child == null) { child = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)).transform; child.SetParent(vehicle.transform, false); }
        child.GetComponent<MeshFilter>().sharedMesh = existing;
        var renderer = child.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.enabled = false;
        return renderer;
    }
    static Texture2D Cookie(string name, VehicleLampKind kind)
    {
        string path = Folder + "/" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;
        var texture = new Texture2D(128, 128, TextureFormat.RGBA32, false, true);
        texture.name = name;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        var pixels = new Color[128 * 128];
        for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
        {
            float u = (x + 0.5f) / 64f - 1f, v = (y + 0.5f) / 64f - 1f;
            float radius = u * u + v * v;
            float value = 1 - Mathf.SmoothStep(0.45f, 1, radius);
            if (kind == VehicleLampKind.LowBeam) value *= 1 - Mathf.SmoothStep(0.08f, 0.16f, v);
            if (kind == VehicleLampKind.Fog) value *= 1 - Mathf.SmoothStep(0.12f, 0.34f, Mathf.Abs(v));
            pixels[y * 128 + x] = new Color(value, value, value, value);
        }
        texture.SetPixels(pixels);
        texture.Apply();
        AssetDatabase.CreateAsset(texture, path);
        return texture;
    }
}
