using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class ConfigureSpruitSunrise
{
    public static object Main()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before saving lighting.");
        const string folder = "Assets/Game/Art/Environment/SpruitSunrise/";
        AssetDatabase.Refresh();
        var importer = (TextureImporter)AssetImporter.GetAtPath(folder + "spruit_sunrise_4k.hdr");
        importer.textureShape = TextureImporterShape.TextureCube;
        importer.generateCubemap = TextureImporterGenerateCubemap.Cylindrical;
        importer.sRGBTexture = false;
        importer.maxTextureSize = 4096;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();
        var cube = AssetDatabase.LoadAssetAtPath<Cubemap>(folder + "spruit_sunrise_4k.hdr");
        if (cube == null) throw new InvalidOperationException("HDRI cubemap import failed.");
        var shader = Shader.Find("Skybox/Cubemap");
        if (shader == null) throw new InvalidOperationException("Skybox/Cubemap shader missing.");
        var material = AssetDatabase.LoadAssetAtPath<Material>(folder + "SpruitSunriseSky.mat");
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, folder + "SpruitSunriseSky.mat");
        }
        material.SetTexture("_Tex", cube);
        material.SetFloat("_Exposure", 1f);
        material.SetFloat("_Rotation", 0f);
        material.SetColor("_Tint", Color.gray);
        RenderSettings.skybox = material;
        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        RenderSettings.reflectionIntensity = 1f;
        foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>())
        {
            if (camera.cameraType != CameraType.Game) continue;
            Undo.RecordObject(camera, "Enable HDRI sky");
            camera.clearFlags = CameraClearFlags.Skybox;
            EditorUtility.SetDirty(camera);
        }
        EditorUtility.SetDirty(material);
        DynamicGI.UpdateEnvironment();
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Scene save failed.");
        return new { scene = scene.path, sky = AssetDatabase.GetAssetPath(RenderSettings.skybox), cubemapSize = cube.width, ambient = RenderSettings.ambientMode.ToString() };
    }
}
