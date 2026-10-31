using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ConfigureForestWeather
{
    public static object Main()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before configuring weather.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/MountainDrive/MountainPass.unity") throw new InvalidOperationException("Expected MountainPass scene, got " + scene.path);
        AssetDatabase.Refresh();
        const string folder = "Assets/Game/Configs/Weather";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Game/Configs", "Weather");
        var profile = AssetDatabase.LoadAssetAtPath<ForestWeatherProfile>(folder + "/Forest weather.asset");
        if (profile == null) { profile = ScriptableObject.CreateInstance<ForestWeatherProfile>(); AssetDatabase.CreateAsset(profile, folder + "/Forest weather.asset"); }
        var shader = Shader.Find("Game/Forest Weather Sky");
        if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Weather sky shader failed to import.");
        var sky = AssetDatabase.LoadAssetAtPath<Material>(folder + "/Forest dynamic sky.mat");
        if (sky == null) { sky = new Material(shader); AssetDatabase.CreateAsset(sky, folder + "/Forest dynamic sky.mat"); }
        var rain = AssetDatabase.LoadAssetAtPath<Material>(folder + "/Rain.mat");
        if (rain == null)
        {
            rain = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            rain.SetFloat("_Surface", 1);
            rain.SetFloat("_Blend", 0);
            rain.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            rain.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            rain.SetFloat("_ZWrite", 0);
            rain.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            rain.SetColor("_BaseColor", new Color(0.7f, 0.8f, 0.9f, 0.32f));
            rain.renderQueue = 3000;
            AssetDatabase.CreateAsset(rain, folder + "/Rain.mat");
        }
        var controller = UnityEngine.Object.FindAnyObjectByType<ForestDayNightWeather>();
        if (controller == null) controller = new GameObject("Forest day night and weather").AddComponent<ForestDayNightWeather>();
        Undo.RecordObject(controller, "Configure forest weather");
        controller.profile = profile;
        controller.sun = RenderSettings.sun != null ? RenderSettings.sun : UnityEngine.Object.FindObjectsByType<Light>().First(l => l.type == LightType.Directional);
        controller.skyTemplate = sky;
        controller.rainMaterial = rain;
        controller.observer = Camera.main != null ? Camera.main.transform : null;
        controller.snowfall = UnityEngine.Object.FindAnyObjectByType<LightSnowfall>();
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Scene save failed.");
        return new { scene = scene.path, controller = controller.name, sun = controller.sun.name, profile = AssetDatabase.GetAssetPath(profile), sky = sky.name, rain = rain.name };
    }
}
