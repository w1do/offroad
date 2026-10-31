using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Captures on separate rendered frames: URP caches light data within a frame.
public static class CaptureVehicleLighting
{
    static VehicleLighting lights;
    static ForestDayNightWeather weather;
    static Camera camera;
    static RenderTexture target;
    static float oldHour;
    static VehicleHeadlightMode oldMode;
    static bool oldHigh, oldFog;
    static int stage, frame;
    static readonly string[] Names = { "off", "low", "high", "fog", "day-front", "night-front" };
    static readonly List<float> Brightness = new List<float>();
    static readonly List<object> Samples = new List<object>();
    public static object Main()
    {
        if (!Application.isPlaying) throw new Exception("Requires Play Mode.");
        lights = UnityEngine.Object.FindAnyObjectByType<VehiclePhysics>().GetComponent<VehicleLighting>();
        weather = lights.environment;
        if (weather == null) throw new Exception("Missing environment.");
        oldHour = weather.Hour; oldMode = lights.Mode; oldHigh = lights.HighBeamRequested; oldFog = lights.FogLampsRequested;
        camera = new GameObject("Lamp verification camera").AddComponent<Camera>();
        camera.CopyFrom(Camera.main);
        camera.transform.position = lights.transform.TransformPoint(new Vector3(0, 2.8f, -5.5f));
        camera.transform.LookAt(lights.transform.TransformPoint(new Vector3(0, 0.2f, 15)));
        target = new RenderTexture(1280, 720, 24);
        target.Create();
        camera.targetTexture = target;
        camera.enabled = true;
        Directory.CreateDirectory("work/vehicle-lighting");
        stage = 0; Brightness.Clear(); Samples.Clear();
        SetStage();
        EditorApplication.update += Tick;
        return new { scheduled = true, report = "docs/reports/vehicle-lighting-render.json" };
    }
    static void SetStage()
    {
        weather.SetHour(stage == 4 ? 12 : 0); weather.ApplyEnvironment();
        camera.transform.position = lights.transform.TransformPoint(stage >= 4 ? new Vector3(2.7f, 1.35f, 5.7f) : new Vector3(0, 2.8f, -5.5f));
        camera.transform.LookAt(lights.transform.TransformPoint(stage >= 4 ? new Vector3(0, 0.9f, 1.4f) : new Vector3(0, 0.2f, 15)));
        lights.SetMode(stage == 0 || stage == 4 ? VehicleHeadlightMode.Off : VehicleHeadlightMode.On);
        lights.SetHighBeam(stage == 2);
        lights.SetFogLamps(stage == 3);
        lights.Evaluate(0, false, 3);
        frame = Time.frameCount;
    }
    static void Tick()
    {
        try
        {
            if (!Application.isPlaying || lights == null || camera == null) throw new Exception("Play Mode interrupted during capture.");
            if (Time.frameCount < frame + 4) return;
            var active = RenderTexture.active;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                var pixels = image.GetPixels();
                float roadBrightness = 0; int n = 0;
                for (int y = 100; y < 430; y++) for (int x = 250; x < 1030; x++) { roadBrightness += pixels[y * 1280 + x].grayscale; n++; }
                roadBrightness /= n;
                Brightness.Add(roadBrightness);
                Samples.Add(new { mode = Names[stage], enabledLights = lights.lamps.Count(b => b.light.enabled), roadBrightness });
                File.WriteAllBytes("work/vehicle-lighting/" + Names[stage] + ".png", image.EncodeToPNG());
            }
            finally { RenderTexture.active = active; UnityEngine.Object.Destroy(image); }
            stage++;
            if (stage < Names.Length) { SetStage(); return; }
            bool passed = Brightness.Skip(1).Take(3).All(value => value > Brightness[0] * 1.1f + 0.002f);
            File.WriteAllText("docs/reports/vehicle-lighting-render.json", Newtonsoft.Json.JsonConvert.SerializeObject(new { passed, samples = Samples, check = "Every beam mode brightens the road versus OFF on separately rendered frames" }, Newtonsoft.Json.Formatting.Indented));
            Cleanup();
        }
        catch (Exception error)
        {
            File.WriteAllText("docs/reports/vehicle-lighting-render.json", Newtonsoft.Json.JsonConvert.SerializeObject(new { passed = false, error = error.ToString() }, Newtonsoft.Json.Formatting.Indented));
            Cleanup();
        }
    }
    static void Cleanup()
    {
        EditorApplication.update -= Tick;
        if (weather != null) { weather.SetHour(oldHour); weather.ApplyEnvironment(); }
        if (lights != null) { lights.SetMode(oldMode); lights.SetHighBeam(oldHigh); lights.SetFogLamps(oldFog); lights.Evaluate(weather == null ? 1 : weather.Daylight, false, 3); }
        if (camera != null) { camera.enabled = false; camera.targetTexture = null; UnityEngine.Object.Destroy(camera.gameObject); }
        if (target != null) { target.Release(); UnityEngine.Object.Destroy(target); }
    }
}
