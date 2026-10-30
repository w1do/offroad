using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class ConfigureOffroadOverhaul
{
    const string Folder = "Assets/Game/Configs/Offroad";
    static void Backup(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        string copy = "work/offroad-before/" + path;
        Directory.CreateDirectory(Path.GetDirectoryName(copy));
        if (!File.Exists(copy)) File.Copy(path, copy);
        if (File.Exists(path + ".meta") && !File.Exists(copy + ".meta")) File.Copy(path + ".meta", copy + ".meta");
    }
    static T Asset<T>(string name) where T : ScriptableObject
    {
        string path = Folder + "/" + name + ".asset";
        var result = AssetDatabase.LoadAssetAtPath<T>(path);
        if (result == null) { result = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(result, path); }
        return result;
    }
    static int AddWater(GameObject root)
    {
        int count = 0;
        foreach(var soil in root.GetComponentsInChildren<SoftGroundRuts>(true))
        {
            var filter=soil.GetComponent<MeshFilter>();
            if(soil.profile==null||filter.sharedMesh==null||filter.sharedMesh.vertexCount==0)continue;
            var tracks=soil.GetComponent<TireTrackRenderer>();if(tracks==null)tracks=soil.gameObject.AddComponent<TireTrackRenderer>();tracks.persistentId=soil.persistentId;
        }
        foreach (var mesh in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mesh.name != "Water in low ruts" || mesh.sharedMesh == null || mesh.sharedMesh.vertexCount == 0) continue;
            var renderer = mesh.GetComponent<Renderer>(); if (renderer == null) continue;
            var child = mesh.transform.Find("Physical puddle volume");
            GameObject go;
            if (child == null) { go = new GameObject("Physical puddle volume"); go.transform.SetParent(mesh.transform, false); }
            else go = child.gameObject;
            // The generated puddle meshes use horizontal local X/Z, with height in Y.
            var b = mesh.sharedMesh.bounds; var box = go.GetComponent<BoxCollider>(); if (box == null) box = go.AddComponent<BoxCollider>();
            box.isTrigger = true; box.center = new Vector3(b.center.x, b.max.y - .75f, b.center.z);
            box.size = new Vector3(b.size.x, 1.5f, b.size.z);
            var water = go.GetComponent<WaterVolume>(); if (water == null) water = go.AddComponent<WaterVolume>();
            water.surfaceMesh = mesh; water.localSurfaceHeight = b.max.y; water.BuildSurface(); count++;
        }
        return count;
    }
    public static string Main()
    {
        if (Application.isPlaying) throw new Exception("Configure outside Play mode");
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        var vehicle = UnityEngine.Object.FindAnyObjectByType<VehiclePhysics>();
        if (vehicle == null) throw new Exception("Open MountainPass with Pajero");
        Backup(vehicle.gameObject.scene.path); Backup(AssetDatabase.GetAssetPath(vehicle.profile));
        Backup(AssetDatabase.GetAssetPath(vehicle.profile.equipment));
        var mud = Asset<SoilProfile>("Wet forest clay");
        var gravel = Asset<SoilProfile>("Firm gravel");
        gravel.soilType = SoilType.Gravel; gravel.moisture = .18f; gravel.waterContent = .1f;
        gravel.bearingStrengthPascal = 350000; gravel.deformationResistancePascal = 700000;
        gravel.cohesionPascal = 5000; gravel.internalFrictionDegrees = 35; gravel.rollingResistance = .025f;
        gravel.rutDepthLimitMetres = .06f; gravel.longitudinalGrip = .28f; gravel.lateralGrip = .5f;
        foreach (string guid in AssetDatabase.FindAssets("t:SoftGroundProfile"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid); Backup(path);
            var soft = AssetDatabase.LoadAssetAtPath<SoftGroundProfile>(path);
            soft.soil = path.Contains("Firm gravel") ? gravel : mud; soft.maximumColliderCommitsPerStep = 2; EditorUtility.SetDirty(soft);
        }
        var tires = new[] { Asset<TireProfile>("Road tires"), Asset<TireProfile>("All terrain tires"), Asset<TireProfile>("Mud terrain tires") };
        for (int i = 0; i < tires.Length; i++)
        {
            var t = tires[i]; t.tireType = (TireType)i; t.radiusMetres = vehicle.profile.axles[0].wheelRadius;
            t.widthMetres = vehicle.profile.axles[0].tyreWidthMetres; t.massKg = vehicle.profile.axles[0].wheelMass;
            t.inertiaKgSquareMetres = .5f * t.massKg * t.radiusMetres * t.radiusMetres;
            t.softGroundMultiplier = i == 0 ? .78f : i == 2 ? 1.14f : 1;
            EditorUtility.SetDirty(t);
        }
        foreach (var axle in vehicle.profile.axles) axle.tire = tires[1];
        var e = vehicle.profile.equipment; e.differentialLockAvailable = true; e.differentialLockKey = UnityEngine.InputSystem.Key.J;
        var waterProfile = Asset<VehicleWaterProfile>("Pajero water interaction");
        waterProfile.bodySamples = new WaterBodySample[6];
        for (int i = 0; i < 6; i++) waterProfile.bodySamples[i] = new WaterBodySample {
            localPosition = new Vector3(i % 2 == 0 ? -.7f : .7f, .22f, 1.3f - i / 2 * 1.3f),
            displacedVolumeCubicMetres = .24f, dragAreaSquareMetres = .42f, fullSubmersionMetres = .65f };
        var interaction = vehicle.GetComponent<VehicleWaterInteraction>(); if (interaction == null) interaction = vehicle.gameObject.AddComponent<VehicleWaterInteraction>();
        interaction.profile = waterProfile;
        if(vehicle.GetComponent<VehicleDiagnostics>()==null)vehicle.gameObject.AddComponent<VehicleDiagnostics>();
        string materialPath = Folder + "/Water ripples.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null) { material = new Material(Shader.Find("Game/Water Ripple")); AssetDatabase.CreateAsset(material, materialPath); }
        var ripples = vehicle.GetComponent<WaterRippleRenderer>(); if (ripples == null) ripples = vehicle.gameObject.AddComponent<WaterRippleRenderer>(); ripples.material = material;
        var route = UnityEngine.Object.FindAnyObjectByType<AuthoredForestRoute>(); int volumes = 0;
        foreach (var module in route.arms.SelectMany(a => a.modules).Distinct())
        {
            string path = AssetDatabase.GetAssetPath(module); Backup(path); var contents = PrefabUtility.LoadPrefabContents(path);
            try { volumes += AddWater(contents); PrefabUtility.SaveAsPrefabAsset(contents, path); }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
        foreach (var root in vehicle.gameObject.scene.GetRootGameObjects()) volumes += AddWater(root);
        foreach (var asset in new UnityEngine.Object[] { mud, gravel, vehicle.profile, e, waterProfile }) EditorUtility.SetDirty(asset);
        EditorSceneManager.MarkSceneDirty(vehicle.gameObject.scene); EditorSceneManager.SaveScene(vehicle.gameObject.scene); AssetDatabase.SaveAssets();
        return "Configured Pajero, 3 tire profiles, clay/gravel profiles, water samples and " + volumes + " water volumes. Backups: work/offroad-before.";
    }
}
