using System;
using UnityEngine;

[Serializable]
public sealed class VehicleLampBinding
{
    public VehicleLampKind kind;
    public Light light;
    [Tooltip("Optional dedicated lens renderer; never assign a combined body mesh.")]
    public Renderer lens;
}

// Shared by every vehicle. Geometry and lamp count belong to prefab bindings.
[DisallowMultipleComponent]
[RequireComponent(typeof(VehiclePhysics))]
public sealed class VehicleLighting : MonoBehaviour
{
    public VehicleLampBinding[] lamps = Array.Empty<VehicleLampBinding>();
    public ForestDayNightWeather environment;
    public VehicleHeadlightMode Mode { get; private set; }
    public bool HeadlightsOn { get; private set; }
    public bool LowBeamOn => HeadlightsOn && !HighBeamOn;
    public bool HighBeamOn { get; private set; }
    public bool FogLampsOn { get; private set; }
    public bool HighBeamRequested { get; private set; }
    public bool FogLampsRequested { get; private set; }
    public VehicleLightingProfile Profile => vehicle != null && vehicle.profile != null ? vehicle.profile.lighting : null;
    private VehiclePhysics vehicle;
    private bool automaticOn, initialized;
    private float thresholdSeconds;
    private float environmentSearchSeconds;
    private MaterialPropertyBlock lensProperties;
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    private void OnEnable()
    {
        vehicle = GetComponent<VehiclePhysics>();
        initialized = false;
        thresholdSeconds = 0;
        environment = environment != null ? environment : FindAnyObjectByType<ForestDayNightWeather>();
        HighBeamRequested = FogLampsRequested = false;
        lensProperties ??= new MaterialPropertyBlock();
    }

    private void LateUpdate()
    {
        Refresh(Time.deltaTime);
    }

    public void Refresh(float deltaSeconds = 0)
    {
        environmentSearchSeconds -= Mathf.Max(0, deltaSeconds);
        if (environment == null && environmentSearchSeconds <= 0) { environment = FindAnyObjectByType<ForestDayNightWeather>(); environmentSearchSeconds = 2f; }
        float daylight = environment != null && environment.isActiveAndEnabled ? environment.Daylight : FallbackDaylight();
        bool poorVisibility = Profile != null &&
            (environment != null && environment.isActiveAndEnabled && environment.Rain >= Profile.fogRainThreshold ||
             RenderSettings.fog && RenderSettings.fogDensity >= Profile.fogDensityThreshold);
        Evaluate(daylight, poorVisibility, deltaSeconds);
    }

    private static float FallbackDaylight()
    {
        var sun = RenderSettings.sun;
        if (sun != null) return Mathf.Clamp01(sun.intensity * Mathf.Max(0, -sun.transform.forward.y));
        return RenderSettings.ambientSkyColor.grayscale;
    }

    // Public environment input also supports other skies and headless validation.
    public void Evaluate(float daylight, bool poorVisibility, float deltaSeconds)
    {
        var profile = Profile;
        if (profile == null || vehicle == null || !vehicle.enabled) { AllOff(); return; }
        float onThreshold = Mathf.Clamp01(profile.switchOnDaylight);
        float offThreshold = Mathf.Clamp(profile.switchOffDaylight, onThreshold + 0.001f, 1f);
        if (!initialized)
        {
            Mode = profile.initialMode;
            automaticOn = daylight <= onThreshold;
            initialized = true;
        }
        bool desired = automaticOn ? daylight < offThreshold : daylight <= onThreshold;
        if (desired == automaticOn) thresholdSeconds = 0;
        else
        {
            thresholdSeconds += Mathf.Max(0, deltaSeconds);
            if (thresholdSeconds >= profile.automaticDelaySeconds) { automaticOn = desired; thresholdSeconds = 0; }
        }
        HeadlightsOn = Mode == VehicleHeadlightMode.On || Mode == VehicleHeadlightMode.Automatic && automaticOn;
        HighBeamOn = HeadlightsOn && HighBeamRequested;
        FogLampsOn = HeadlightsOn && (FogLampsRequested || profile.automaticFogInBadWeather && poorVisibility);
        ApplyBindings();
    }

    public void SetMode(VehicleHeadlightMode mode)
    {
        if (!initialized) Evaluate(1, false, 0);
        Mode = mode;
    }
    public void CycleMode() => SetMode((VehicleHeadlightMode)(((int)Mode + 1) % 3));
    public void SetHighBeam(bool value) { HighBeamRequested = value; }
    public void SetFogLamps(bool value) { FogLampsRequested = value; }

    private void ApplyBindings()
    {
        if (lamps == null) return;
        // Low and high beam may share the original model's headlamp glass.
        foreach (var binding in lamps) if (binding != null) SetLens(binding.lens, false, Color.white);
        foreach (var binding in lamps)
        {
            if (binding == null) continue;
            bool on = binding.kind == VehicleLampKind.LowBeam ? HeadlightsOn && !HighBeamOn : binding.kind == VehicleLampKind.HighBeam ? HighBeamOn : FogLampsOn;
            var beam = Profile.Beam(binding.kind);
            if (beam == null) { if (binding.light != null) binding.light.enabled = false; SetLens(binding.lens, false, Color.white); continue; }
            if (binding.light != null)
            {
                var light = binding.light;
                light.type = LightType.Spot;
                light.color = beam.color;
                light.lightUnit = UnityEngine.Rendering.LightUnit.Candela;
                light.intensity = Mathf.Max(0, beam.intensityCandela);
                light.range = Mathf.Max(0.1f, beam.rangeMetres);
                light.spotAngle = Mathf.Clamp(beam.outerAngleDegrees, 1, 179);
                light.innerSpotAngle = Mathf.Clamp(beam.innerAngleDegrees, 0, light.spotAngle);
                light.cookie = beam.cookie;
                light.shadows = beam.shadows ? LightShadows.Soft : LightShadows.None;
                light.enabled = on;
            }
            if (on) SetLens(binding.lens, true, beam.color);
        }
    }

    private void SetLens(Renderer lens, bool on, Color color)
    {
        if (lens == null) return;
        lens.enabled = on;
        lensProperties ??= new MaterialPropertyBlock();
        lens.GetPropertyBlock(lensProperties);
        lensProperties.SetColor(BaseColor, color * 2f);
        lens.SetPropertyBlock(lensProperties);
    }

    private void AllOff()
    {
        HeadlightsOn = HighBeamOn = FogLampsOn = false;
        if (lamps == null) return;
        foreach (var binding in lamps)
        {
            if (binding == null) continue;
            if (binding.light != null) binding.light.enabled = false;
            SetLens(binding.lens, false, Color.white);
        }
    }
    private void OnDisable() { AllOff(); }
}
