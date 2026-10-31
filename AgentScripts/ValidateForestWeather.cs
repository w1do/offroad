using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ValidateForestWeather
{
    public static object Main()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Validation requires Edit Mode.");
        var controller = UnityEngine.Object.FindAnyObjectByType<ForestDayNightWeather>();
        if (controller == null || controller.profile == null || controller.sun == null || controller.skyTemplate == null || controller.rainMaterial == null || controller.observer == null) throw new Exception("Missing weather references.");
        var shader = controller.skyTemplate.shader;
        if (ShaderUtil.ShaderHasError(shader)) throw new Exception("Sky shader has errors.");
        var oldSky = RenderSettings.skybox;
        var originalProfile = EditorJsonUtility.ToJson(controller.profile);
        var position = Camera.main.transform.position;
        var rotation = Camera.main.transform.rotation;
        var testRoot = new GameObject("Weather validation temporary");
        testRoot.SetActive(false);
        var test = testRoot.AddComponent<ForestDayNightWeather>();
        test.profile = controller.profile;
        test.sun = controller.sun;
        test.observer = controller.observer;
        test.skyTemplate = controller.skyTemplate;
        test.rainMaterial = controller.rainMaterial;
        var camera = Camera.main;
        var oldTarget = camera.targetTexture;
        var oldActive = RenderTexture.active;
        var rt = RenderTexture.GetTemporary(1280, 720, 24);
        Directory.CreateDirectory("work/weather");
        try
        {
            testRoot.SetActive(true);
            test.SendMessage("OnEnable");
            float noon = 0, midnight = 0;
            foreach (int hour in new[] { 6, 12, 18, 0 })
            {
                test.SetHour(hour);
                test.ApplyEnvironment();
                if (hour == 12) noon = test.sun.intensity;
                if (hour == 0) midnight = test.sun.intensity;
                camera.targetTexture = rt;
                camera.Render();
                RenderTexture.active = rt;
                var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                File.WriteAllBytes("work/weather/hour-" + hour + ".png", image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
            }
            if (noon <= midnight * 5) throw new Exception("No meaningful lighting change.");
            test.SetHour(25);
            if (Mathf.Abs(test.Hour - 1) > 0.001f) throw new Exception("Clock wrap failed.");
            var particles = test.GetComponentInChildren<ParticleSystem>();
            if (particles == null || particles.main.maxParticles != controller.profile.maximumRainParticles || particles.collision.colliderForce != 0) throw new Exception("Rain budget or visual-only configuration failed.");
            if (EditorJsonUtility.ToJson(controller.profile) != originalProfile) throw new Exception("Shared weather profile mutated.");
            return new { passed = true, noonIntensity = noon, midnightIntensity = midnight, rainParticleBudget = particles.main.maxParticles, previews = "work/weather/hour-*.png", shaderErrors = false };
        }
        finally
        {
            test.SendMessage("OnDisable");
            testRoot.SetActive(false);
            // OnDisable schedules runtime cleanup; do not leave temporary edit-mode objects.
            foreach (Transform child in testRoot.transform) UnityEngine.Object.DestroyImmediate(child.gameObject);
            UnityEngine.Object.DestroyImmediate(testRoot);
            camera.targetTexture = oldTarget;
            camera.transform.SetPositionAndRotation(position, rotation);
            RenderTexture.active = oldActive;
            RenderTexture.ReleaseTemporary(rt);
            if (RenderSettings.skybox != oldSky) throw new Exception("Environment restoration failed.");
        }
    }
}
