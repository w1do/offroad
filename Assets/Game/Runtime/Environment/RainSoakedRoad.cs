using UnityEngine;

// Owns only a runtime copy of the road's soil settings. VehiclePhysics still owns every tyre force.
[RequireComponent(typeof(SoftGroundRuts))]
public sealed class RainSoakedRoad : MonoBehaviour
{
    public ForestDayNightWeather weather;
    public bool useLocalRain;
    [Range(0, 1)] public float localRain;
    public float RainIntensity => useLocalRain ? localRain : weather == null ? 0 : weather.Rain;
    [Range(0, 1)] public float initialSaturation = .15f;
    [Min(1)] public float soakingSeconds = 70;
    [Min(1)] public float dryingSeconds = 420;
    public float Saturation { get; private set; }
    public SoilProfile RuntimeSoil => soil;
    private SoftGroundRuts ground;
    private SoftGroundProfile original, runtime;
    private SoilProfile soil;
    private MaterialPropertyBlock block;
    private Renderer surface;

    private void OnEnable()
    {
        ground = GetComponent<SoftGroundRuts>();
        original = ground.profile;
        if (original == null) return;
        runtime = Instantiate(original);
        soil = ScriptableObject.CreateInstance<SoilProfile>();
        soil.name = "Session rain soaked road soil";
        runtime.soil = soil;
        runtime.maximumDepthMetres = .14f;
        runtime.maximumBermHeightMetres = .025f;
        runtime.rollingCompactionMetresPerSecond = .045f;
        runtime.diggingMetresPerSecond = .075f;
        ground.profile = runtime;
        surface = GetComponent<Renderer>();
        block = new MaterialPropertyBlock();
        Saturation = initialSaturation;
        Apply();
    }
    private void FixedUpdate() { Advance(Time.fixedDeltaTime); }
    public void Advance(float seconds)
    {
        float rain = RainIntensity;
        float downpour = Mathf.InverseLerp(.55f, 1, rain);
        float absorption = Mathf.Lerp(rain * .18f, 1, downpour);
        float rate = absorption / Mathf.Max(1, soakingSeconds) - Mathf.Pow(1 - rain, 3) / Mathf.Max(1, dryingSeconds);
        Saturation = Mathf.Clamp01(Saturation + rate * Mathf.Max(0, seconds));
        Apply();
    }
    public void RestoreSaturation(float value) { Saturation = Mathf.Clamp01(value); Apply(); }
    private void Apply()
    {
        if (soil == null) return;
        float wet = Saturation;
        soil.soilType = SoilType.WetDirt;
        soil.moisture = Mathf.Lerp(.15f, .95f, wet);
        soil.waterContent = wet;
        soil.compaction = .12f;
        soil.bearingStrengthPascal = Mathf.Lerp(200000, 35000, wet);
        soil.cohesionPascal = Mathf.Lerp(8000, 2500, wet);
        soil.deformationResistancePascal = Mathf.Lerp(2500000, 1600000, wet);
        soil.rutDepthLimitMetres = Mathf.Lerp(.025f, .14f, wet);
        soil.longitudinalGrip = Mathf.Lerp(.18f, .115f, wet);
        soil.lateralGrip = Mathf.Lerp(.38f, .13f, wet);
        soil.rollingResistance = Mathf.Lerp(.025f, .22f, wet);
        soil.viscousResistanceSecondsPerMetre = Mathf.Lerp(0, .025f, wet);
        surface.GetPropertyBlock(block);
        block.SetFloat("_RainWetness", wet);
        surface.SetPropertyBlock(block);
    }
    private void OnDisable()
    {
        if (ground != null && ground.profile == runtime) ground.profile = original;
        if (surface != null) { surface.GetPropertyBlock(block); block.SetFloat("_RainWetness", 0); surface.SetPropertyBlock(block); }
        if (runtime != null) Destroy(runtime);
        if (soil != null) Destroy(soil);
        runtime = null; soil = null;
    }
}




