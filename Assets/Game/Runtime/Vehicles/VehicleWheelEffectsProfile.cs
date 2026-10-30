using UnityEngine;

[CreateAssetMenu(menuName = "Game/Vehicles/Wheel effects profile")]
public sealed class VehicleWheelEffectsProfile : ScriptableObject
{
    public Material clodMaterial;
    public Material[] splashMaterials;
    public Mesh[] clodMeshes;
    public Color wetColour = new Color(.19f, .12f, .065f, 1);
    public Color dryColour = new Color(.31f, .25f, .17f, 1);
    [Header("Visual calibration, SI units")]
    [Min(0)] public float minimumSlipSpeed = .65f;
    [Min(0)] public float clodsPerSlipMetre = 8;
    [Min(0)] public float dropletsPerSlipMetre = 24;
    [Min(0)] public float maximumClodsPerSecond = 90;
    [Min(0)] public float maximumDropletsPerSecond = 210;
    [Range(32, 1024)] public int maximumParticlesPerLayer = 384;
    [Range(8, 256)] public int maximumImpactMarks = 96;
    [Min(.1f)] public float markLifetimeSeconds = 75;
    [Min(0)] public float maximumMarksPerSecond = 18;
    public Vector2 clodSizeMetres = new Vector2(.025f, .095f);
    public Vector2 dropletSizeMetres = new Vector2(.012f, .048f);
    [Min(0)] public float maximumThrowSpeed = 8;
}
