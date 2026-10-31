using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ValidateVehicleLighting
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static object Main()
    {
        Check(Application.isPlaying, "Run this validation in Play Mode.");
        var actual = UnityEngine.Object.FindAnyObjectByType<VehiclePhysics>();
        Check(actual != null && actual.profile.lighting != null, "Missing installed lighting profile.");
        var actualLights = actual.GetComponent<VehicleLighting>();
        Check(actualLights != null && actualLights.lamps.Count(b => b.light != null) == 6, "Missing scene lamp bindings.");
        var originalJson = EditorJsonUtility.ToJson(actual.profile.lighting);
        var profile = UnityEngine.Object.Instantiate(actual.profile.lighting);
        profile.initialMode = VehicleHeadlightMode.Automatic;
        profile.automaticDelaySeconds = 2;
        profile.automaticFogInBadWeather = false;
        var physicsProfile = ScriptableObject.CreateInstance<VehiclePhysicsProfile>();
        physicsProfile.lighting = profile;
        var roots = new List<GameObject>();
        try
        {
            var first = MakeVehicle(roots, physicsProfile, 3);
            var second = MakeVehicle(roots, physicsProfile, 9);
            first.Evaluate(1, false, 0);
            second.Evaluate(0, false, 0);
            Check(!first.HeadlightsOn && second.HeadlightsOn, "Initial day/night automatic state or instance independence failed.");
            first.Evaluate(0, false, 1);
            Check(!first.HeadlightsOn, "Automatic transition ignored configured delay.");
            first.Evaluate(0, false, 1);
            Check(first.HeadlightsOn, "Darkness failed to turn lights on.");
            first.Evaluate(0.48f, false, 5);
            Check(first.HeadlightsOn, "Hysteresis flickered in twilight.");
            first.Evaluate(1, false, 2);
            Check(!first.HeadlightsOn, "Dawn failed to turn lights off.");
            first.Evaluate(0.48f, false, 5);
            Check(!first.HeadlightsOn, "Hysteresis turned headlights back on in twilight.");
            first.SetMode(VehicleHeadlightMode.On);
            first.SetHighBeam(true);
            first.SetFogLamps(true);
            first.Evaluate(1, false, 0);
            Check(first.HighBeamOn && first.FogLampsOn && !first.lamps[0].light.enabled && first.lamps[1].light.enabled && first.lamps[2].light.enabled, "Beam or fog selection failed.");
            Check(second.HeadlightsOn && !second.HighBeamRequested && !second.FogLampsRequested, "State leaked across vehicles sharing a profile.");
            first.SetMode(VehicleHeadlightMode.Off);
            first.Evaluate(0, true, 3);
            Check(!first.HeadlightsOn && !first.FogLampsOn && first.lamps.All(b => !b.light.enabled), "Manual OFF did not override darkness.");
            second.enabled = false;
            Check(second.lamps.All(b => !b.light.enabled), "Disabled vehicle lights remained on.");
            second.enabled = true;
            var vehicleApi = first.GetComponent<VehiclePhysics>();
            Check(vehicleApi.SetLowBeam(true) && vehicleApi.LowBeamOn && !vehicleApi.HighBeamOn, "Shared vehicle API failed to enable low beam immediately.");
            Check(vehicleApi.SetHighBeam(true) && vehicleApi.HighBeamOn && !vehicleApi.LowBeamOn, "Shared vehicle API failed to switch to high beam immediately.");
            Check(vehicleApi.SetHighBeam(false) && vehicleApi.LowBeamOn, "Disabling high beam failed to return to low beam.");
            vehicleApi.SetLowBeam(false);
            Check(!vehicleApi.LowBeamOn && !vehicleApi.HighBeamOn, "Shared vehicle API failed to turn headlights off.");
            Check(vehicleApi.SetFogLamps(true) && vehicleApi.FogLampsOn && vehicleApi.LowBeamOn, "Enabling fog lamps from OFF did not enable supporting low beam.");
            Check(vehicleApi.SetFogLamps(false) && !vehicleApi.FogLampsOn, "Shared vehicle API failed to disable fog lamps.");
            Check(vehicleApi.SetHeadlightMode(VehicleHeadlightMode.Automatic) && first.Mode == VehicleHeadlightMode.Automatic, "Shared vehicle API failed to restore AUTO.");
            Check(!second.HighBeamRequested && !second.FogLampsRequested, "Shared vehicle API leaked state to a second car.");
            Check(EditorJsonUtility.ToJson(actual.profile.lighting) == originalJson, "Shared profile mutated.");
            var result = new { passed = true, checks = new[] { "initial day/night", "twilight delay and hysteresis", "manual override", "low/high exclusivity", "fog selection", "independent vehicles with 3 and 9 lamps", "disable cleanup", "profile immutability", "VehiclePhysics direct low/high/fog commands", "immediate manual activation from OFF", "return to AUTO" } };
            Directory.CreateDirectory("docs/reports");
            File.WriteAllText("docs/reports/vehicle-lighting.json", Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented));
            return result;
        }
        finally
        {
            foreach (var root in roots) { root.SetActive(false); UnityEngine.Object.Destroy(root); }
            UnityEngine.Object.Destroy(profile);
            UnityEngine.Object.Destroy(physicsProfile);
        }
    }
    static VehicleLighting MakeVehicle(List<GameObject> roots, VehiclePhysicsProfile profile, int count)
    {
        var root = new GameObject("Lighting test vehicle " + count);
        roots.Add(root);
        root.transform.position = new Vector3(0, -1000, 0);
        var vehicle = root.AddComponent<VehiclePhysics>();
        root.GetComponent<Rigidbody>().isKinematic = true;
        vehicle.profile = profile;
        var lights = root.AddComponent<VehicleLighting>();
        lights.lamps = new VehicleLampBinding[count];
        for (int i = 0; i < count; i++)
        {
            var lamp = new GameObject("Lamp " + i);
            lamp.transform.SetParent(root.transform, false);
            lights.lamps[i] = new VehicleLampBinding { kind = (VehicleLampKind)(i % 3), light = lamp.AddComponent<Light>() };
        }
        return lights;
    }
}
