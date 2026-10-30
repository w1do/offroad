using UnityEngine;

[CreateAssetMenu(menuName = "Game/Water/Puddle effects")]
public sealed class PuddleEffectsProfile : ScriptableObject
{
    public Material dropletMaterial;
    [Min(32)] public int maximumParticlesPerWheel = 300;
    [Min(0)] public float minimumSpeedMetresPerSecond = .35f;
    [Min(.01f)] public float fullSprayDepthMetres = .12f;
    [Min(0)] public float particlesPerMetre = 24;
    [Min(0)] public float maximumParticlesPerSecond = 180;
    [Min(0)] public float maximumThrowSpeedMetresPerSecond = 9;
    public Vector2 dropletSizeMetres = new Vector2(.018f, .075f);
    public Color waterColour = new Color(.47f, .43f, .32f, .65f);
    [Header("Water filling existing road depressions (metres)")]
    public float smallFillDepthMetres = .035f;
    public float largeFillDepthMetres = .17f;
    [Header("Local road basins: semi-axes and excavation depth in metres")]
    public Vector2 largeBasinRadiusMetres = new Vector2(1.55f,3.3f);
    public Vector2 smallBasinRadiusMetres = new Vector2(.65f,.9f);
    public float basinDepthMetres = .18f;
    [Header("Nearby environment reflections")]
    public int reflectionResolution = 128;
    public float reflectionRefreshSeconds = 3;
    public float reflectionDistanceMetres = 70;
}
