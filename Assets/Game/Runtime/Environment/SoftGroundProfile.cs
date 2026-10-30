using UnityEngine;

[CreateAssetMenu(menuName="Game/World/Deformable mud profile")]
public sealed class SoftGroundProfile : ScriptableObject
{
    public SoilProfile soil;
    [Range(0,1)] public float displacedSoilFraction = .35f;
    [Min(0)] public float maximumBermHeightMetres = .08f;
    [Min(1)] public int maximumColliderCommitsPerStep = 2;
    [Min(.001f)] public float wheelContactHeightToleranceMetres = .006f;
    [Header("Prototype soil calibration / SI units")]
    public float maximumDepthMetres=.38f;
    public float diggingMetresPerSecond=.065f;
    public float rollingCompactionMetresPerSecond=.025f;
    public float settlementDepthMetres=.035f;
    public float nominalWheelLoadNewtons=5000;
    public float fullDigSlipMetresPerSecond=9;
    public float minimumDigSlipMetresPerSecond=.6f;
    public float footprintLengthMetres=.60f;
    public float edgeTaperMetres=.6f;
    public float collisionUpdateSeconds=.16f;
}
