using UnityEngine;

[CreateAssetMenu(menuName="Game/World/Deformable mud profile")]
public sealed class SoftGroundProfile : ScriptableObject
{
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
