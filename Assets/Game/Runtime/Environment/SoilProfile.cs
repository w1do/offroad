using UnityEngine;

public enum SoilType { DryDirt, WetDirt, Clay, DeepMud, Sand, Gravel, Grass, WetGrass, Rock, Asphalt, ShallowWater, Swamp }

[CreateAssetMenu(menuName = "Game/World/Soil profile")]
public sealed class SoilProfile : ScriptableObject
{
    [Header("Prototype calibration, SI units; not laboratory soil data")]
    public SoilType soilType = SoilType.DeepMud;
    [Range(0, 1)] public float moisture = .8f, compaction = .15f, waterContent = .65f;
    [Min(1)] public float densityKgPerCubicMetre = 1600;
    [Min(0)] public float cohesionPascal = 2500;
    [Range(0, 60)] public float internalFrictionDegrees = 18;
    [Min(1)] public float bearingStrengthPascal = 45000;
    [Min(1)] public float deformationResistancePascal = 90000;
    [Min(0)] public float rollingResistance = .055f;
    [Min(0)] public float rutDepthLimitMetres = .38f;
    [Min(0)] public float recoveryMetresPerSecond;
    [Range(.02f, 2)] public float longitudinalGrip = .18f, lateralGrip = .38f;

    public float StrengthPascal(float localCompaction)
    {
        float packed = Mathf.Clamp01(compaction + localCompaction);
        return Mathf.Max(1, bearingStrengthPascal * Mathf.Lerp(1, .35f, moisture)
            * Mathf.Lerp(1, 2.5f, packed) + cohesionPascal);
    }

    public float SinkageMetres(float pressurePascal, float localCompaction, float slipSpeed)
    {
        float excess = Mathf.Max(0, pressurePascal - StrengthPascal(localCompaction));
        float shear = Mathf.Clamp01(Mathf.Abs(slipSpeed) / 9) * moisture * waterContent;
        float stiffness = Mathf.Max(1, deformationResistancePascal
            + densityKgPerCubicMetre * Physics.gravity.magnitude * rutDepthLimitMetres
            * Mathf.Tan(internalFrictionDegrees * Mathf.Deg2Rad));
        return Mathf.Clamp(excess / stiffness + shear * rutDepthLimitMetres, 0, rutDepthLimitMetres);
    }
}
