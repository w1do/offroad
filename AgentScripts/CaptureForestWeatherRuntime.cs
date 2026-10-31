using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class CaptureForestWeatherRuntime
{
    public static object Main()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Requires Play Mode.");
        var w = UnityEngine.Object.FindAnyObjectByType<ForestDayNightWeather>();
        var particles = w.GetComponentInChildren<ParticleSystem>();
        if (w.Rain < 0.5f || particles == null || particles.particleCount < 1) throw new Exception("Wait for the rain transition before capturing.");
        if (Vector3.Distance(particles.transform.position, w.observer.position + Vector3.up * w.profile.rainHeightMetres) > 1) throw new Exception("Rain does not follow observer.");
        var camera = Camera.main;
        var rotation = camera.transform.rotation;
        var target = camera.targetTexture;
        var active = RenderTexture.active;
        var rt = RenderTexture.GetTemporary(1280, 720, 24);
        try
        {
            camera.transform.rotation = Quaternion.Euler(-12, camera.transform.eulerAngles.y, 0);
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            Directory.CreateDirectory("work/weather");
            File.WriteAllBytes("work/weather/runtime-rain.png", image.EncodeToPNG());
            UnityEngine.Object.Destroy(image);
            var result = new { passed = true, hour = w.Hour, rain = w.Rain, clouds = w.CloudCover, particles = particles.particleCount, budget = particles.main.maxParticles, scene = camera.gameObject.scene.path };
            File.WriteAllText("docs/reports/forest-weather.json", Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented));
            return result;
        }
        finally { camera.transform.rotation = rotation; camera.targetTexture = target; RenderTexture.active = active; RenderTexture.ReleaseTemporary(rt); }
    }
}
