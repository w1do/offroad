using UnityEngine;

[CreateAssetMenu(menuName = "Game/Forest weather")]
public sealed class ForestWeatherProfile : ScriptableObject
{
    [Header("Clock (prototype calibration)")]
    [Range(0, 24)] public float startHour = 9f;
    [Min(1)] public float dayLengthMinutes = 48f;
    public bool advanceClock = true;
    public float sunAzimuthDegrees = -38f;
    [Range(10, 80)] public float maximumSunElevationDegrees = 48f;
    [Header("Lighting")]
    [Min(0)] public float daylightIntensity = 2.1f;
    [Min(0)] public float moonlightIntensity = 0.22f;
    public Color daylightAmbient = new Color(0.60f, 0.70f, 0.79f);
    public Color nightAmbient = new Color(0.12f, 0.16f, 0.22f);
    [Min(0)] public float clearFogDensity = 0.0018f;
    [Min(0)] public float rainFogDensity = 0.009f;
    [Header("Clouds and rain (visual only)")]
    public int weatherSeed = 451;
    public bool automaticWeather = true;
    [Min(10)] public float weatherPeriodSeconds = 240f;
    [Min(1)] public float transitionSeconds = 30f;
    [Range(0, 1)] public float initialCloudCover = 0.4f;
    [Range(0, 1)] public float initialRain = 0f;
    [Range(0, 1)] public float rainProbability = 0.35f;
    public Vector2 cloudWind = new Vector2(0.004f, 0.001f);
    [Min(0)] public float rainDropsPerSecond = 1800f;
    [Min(100)] public int maximumRainParticles = 4000;
    [Min(1)] public float rainRadiusMetres = 22f;
    [Min(1)] public float rainHeightMetres = 15f;
    [Min(1)] public float rainSpeedMetresPerSecond = 22f;
}
