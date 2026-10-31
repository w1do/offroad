using UnityEngine;
using UnityEngine.Rendering;

// One owner for the environment. Settings remain immutable during a run.
public sealed class ForestDayNightWeather : MonoBehaviour
{
    public ForestWeatherProfile profile;
    public Light sun;
    public Transform observer;
    public Material skyTemplate;
    public Material rainMaterial;
    public LightSnowfall snowfall;
    [SerializeField, Range(0, 24)] private float hour = 9f;
    public float Hour => hour;
    public float Rain => rain;
    public float CloudCover => clouds;
    public float Daylight { get; private set; } = 1f;
    private float rain, clouds, targetRain, targetClouds, weatherSeconds, elapsed;
    private System.Random random;
    private Material sky;
    private ParticleSystem drops;
    private ParticleSystem snow;
    private float originalSnowRate;
    private Material previousSky;
    private Light previousSun;
    private Quaternion previousRotation;
    private Color previousLightColor, previousAmbientSky, previousAmbientEquator, previousAmbientGround, previousFogColor;
    private float previousIntensity, previousFogDensity, previousReflection;
    private bool previousFog;
    private AmbientMode previousAmbientMode;
    private FogMode previousFogMode;

    private void OnEnable()
    {
        if (profile == null || sun == null || skyTemplate == null) return;
        previousSky = RenderSettings.skybox;
        previousSun = RenderSettings.sun;
        previousRotation = sun.transform.rotation;
        previousLightColor = sun.color;
        previousIntensity = sun.intensity;
        previousAmbientMode = RenderSettings.ambientMode;
        previousAmbientSky = RenderSettings.ambientSkyColor;
        previousAmbientEquator = RenderSettings.ambientEquatorColor;
        previousAmbientGround = RenderSettings.ambientGroundColor;
        previousFog = RenderSettings.fog;
        previousFogMode = RenderSettings.fogMode;
        previousFogDensity = RenderSettings.fogDensity;
        previousFogColor = RenderSettings.fogColor;
        previousReflection = RenderSettings.reflectionIntensity;
        sky = new Material(skyTemplate);
        RenderSettings.skybox = sky;
        RenderSettings.sun = sun;
        hour = Mathf.Repeat(profile.startHour, 24f);
        rain = targetRain = profile.initialRain;
        clouds = targetClouds = Mathf.Max(profile.initialCloudCover, rain * 0.95f);
        random = new System.Random(profile.weatherSeed);
        elapsed = weatherSeconds = 0;
        if (snowfall != null)
        {
            snow = snowfall.GetComponent<ParticleSystem>();
            if (snow != null) originalSnowRate = snow.emission.rateOverTimeMultiplier;
        }
        CreateRain();
        ApplyEnvironment();
    }

    private void CreateRain()
    {
        if (rainMaterial == null) return;
        var root = new GameObject("Local rain (visual only)");
        root.transform.SetParent(transform, false);
        drops = root.AddComponent<ParticleSystem>();
        drops.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = drops.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = profile.maximumRainParticles;
        main.startLifetime = (profile.rainHeightMetres + 12f) / Mathf.Max(1f, profile.rainSpeedMetresPerSecond);
        main.startSpeed = 0;
        main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.032f);
        main.startColor = new Color(0.65f, 0.75f, 0.82f, 0.4f);
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        var shape = drops.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(profile.rainRadiusMetres * 2f, 0.1f, profile.rainRadiusMetres * 2f);
        var velocity = drops.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = 1.2f;
        velocity.y = -profile.rainSpeedMetresPerSecond;
        velocity.z = 0.4f;
        var collision = drops.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.quality = ParticleSystemCollisionQuality.Low;
        collision.collidesWith = ~0;
        collision.lifetimeLoss = 1;
        collision.colliderForce = 0;
        collision.sendCollisionMessages = false;
        var renderer = drops.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = rainMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.035f;
        renderer.lengthScale = 3f;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        var emission = drops.emission;
        emission.rateOverTime = 0;
        drops.useAutoRandomSeed = false;
        drops.randomSeed = unchecked((uint)profile.weatherSeed);
        drops.Play();
    }

    private void Update()
    {
        if (sky == null) return;
        float dt = Time.deltaTime;
        elapsed += dt;
        if (profile.advanceClock) SetHour(hour + dt * 24f / Mathf.Max(60f, profile.dayLengthMinutes * 60f));
        weatherSeconds += dt;
        if (profile.automaticWeather && weatherSeconds >= Mathf.Max(10f, profile.weatherPeriodSeconds))
        {
            weatherSeconds = 0;
            targetRain = random.NextDouble() < profile.rainProbability ? Mathf.Lerp(0.3f, 1f, (float)random.NextDouble()) : 0f;
            targetClouds = targetRain > 0 ? Mathf.Lerp(0.82f, 1f, targetRain) : Mathf.Lerp(0.15f, 0.7f, (float)random.NextDouble());
        }
        float step = dt / Mathf.Max(1f, profile.transitionSeconds);
        rain = Mathf.MoveTowards(rain, targetRain, step);
        clouds = Mathf.MoveTowards(clouds, targetClouds, step);
        ApplyEnvironment();
    }

    private void LateUpdate()
    {
        if (drops == null) return;
        if (observer == null && Camera.main != null) observer = Camera.main.transform;
        if (observer != null) drops.transform.position = observer.position + Vector3.up * profile.rainHeightMetres;
    }

    public void SetHour(float value) { hour = Mathf.Repeat(value, 24f); }
    public void SetWeather(float cloudCover, float rainfall)
    {
        targetRain = Mathf.Clamp01(rainfall);
        targetClouds = Mathf.Max(Mathf.Clamp01(cloudCover), targetRain * 0.95f);
        weatherSeconds = 0;
    }
    [ContextMenu("Preview / Noon")] private void Noon() { SetHour(12); ApplyEnvironment(); }
    [ContextMenu("Preview / Midnight")] private void Midnight() { SetHour(0); ApplyEnvironment(); }
    [ContextMenu("Weather / Clear")] private void Clear() { SetWeather(0.25f, 0); }
    [ContextMenu("Weather / Rain")] private void Rainy() { SetWeather(1, 0.85f); }

    public void ApplyEnvironment()
    {
        if (sky == null || profile == null || sun == null) return;
        float phase = (hour - 6f) / 24f * Mathf.PI * 2f;
        float tilt = profile.maximumSunElevationDegrees * Mathf.Deg2Rad;
        Vector3 sunDirection = Quaternion.Euler(0, profile.sunAzimuthDegrees, 0) *
            new Vector3(Mathf.Cos(phase), Mathf.Sin(phase) * Mathf.Sin(tilt), Mathf.Sin(phase) * Mathf.Cos(tilt));
        float elevation = Mathf.Asin(sunDirection.y) * Mathf.Rad2Deg;
        var rotation = Quaternion.LookRotation(-sunDirection, Vector3.up);
        float daylight = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-6, 18, elevation));
        Daylight = daylight;
        float aboveHorizon = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-2, 8, elevation));
        float overcast = Mathf.Lerp(1, 0.36f, clouds * rain);
        bool night = elevation < -6;
        sun.transform.rotation = night ? Quaternion.LookRotation(sunDirection, Vector3.up) : rotation;
        sun.intensity = night ? profile.moonlightIntensity * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-6, -15, elevation)) : profile.daylightIntensity * aboveHorizon * overcast;
        sun.color = night ? new Color(0.55f, 0.68f, 1f) : Color.Lerp(new Color(1f, 0.48f, 0.22f), new Color(1f, 0.95f, 0.85f), daylight);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        Color ambient = Color.Lerp(profile.nightAmbient, profile.daylightAmbient, daylight) * Mathf.Lerp(1, 0.65f, rain);
        RenderSettings.ambientSkyColor = ambient;
        RenderSettings.ambientEquatorColor = ambient * 0.65f;
        RenderSettings.ambientGroundColor = ambient * 0.3f;
        RenderSettings.reflectionIntensity = Mathf.Lerp(0.08f, 0.85f, daylight) * Mathf.Lerp(1, 0.55f, rain);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = Mathf.Lerp(profile.clearFogDensity, profile.rainFogDensity, rain);
        RenderSettings.fogColor = Color.Lerp(new Color(0.025f, 0.04f, 0.075f), new Color(0.57f, 0.65f, 0.72f), daylight) * Mathf.Lerp(1, 0.7f, rain);
        sky.SetVector("_SunDirection", sunDirection);
        sky.SetFloat("_Daylight", daylight);
        sky.SetFloat("_CloudCover", clouds);
        sky.SetFloat("_Rain", rain);
        sky.SetVector("_CloudOffset", profile.cloudWind * elapsed);
        if (drops != null)
        {
            var emission = drops.emission;
            emission.rateOverTime = rain * profile.rainDropsPerSecond;
            var main = drops.main;
            main.startColor = new Color(0.65f, 0.75f, 0.82f, Mathf.Lerp(0.12f, 0.4f, daylight));
        }
        if (snow != null)
        {
            var emission = snow.emission;
            emission.rateOverTimeMultiplier = originalSnowRate * (1 - rain);
        }
    }

    private void OnDisable()
    {
        if (sky == null) return;
        RenderSettings.skybox = previousSky;
        RenderSettings.sun = previousSun;
        RenderSettings.ambientMode = previousAmbientMode;
        RenderSettings.ambientSkyColor = previousAmbientSky;
        RenderSettings.ambientEquatorColor = previousAmbientEquator;
        RenderSettings.ambientGroundColor = previousAmbientGround;
        RenderSettings.fog = previousFog;
        RenderSettings.fogMode = previousFogMode;
        RenderSettings.fogDensity = previousFogDensity;
        RenderSettings.fogColor = previousFogColor;
        RenderSettings.reflectionIntensity = previousReflection;
        if (sun != null) { sun.transform.rotation = previousRotation; sun.color = previousLightColor; sun.intensity = previousIntensity; }
        if (snow != null) { var emission = snow.emission; emission.rateOverTimeMultiplier = originalSnowRate; }
        if (drops != null) { drops.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); Release(drops.gameObject); }
        Release(sky);
        sky = null;
    }

    private static void Release(Object value)
    {
        if (Application.isPlaying) Destroy(value);
        else DestroyImmediate(value);
    }
}
