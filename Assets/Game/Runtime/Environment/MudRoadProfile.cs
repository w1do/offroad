using UnityEngine;

[CreateAssetMenu(menuName="Game/World/Mud road section")]
public sealed class MudRoadProfile : ScriptableObject
{
    [Header("Prototype calibration, metres")]
    public float edgeBlendMetres=1.05f;
    public float trackHalfSpacingMetres=.8f,trackHalfWidthMetres=.42f;
    public float leftRutDepthMetres=.075f,rightRutDepthMetres=.155f;
    public float laneWanderMetres=.17f,scanReliefMetres=.11f;
    public float firmGripMultiplier=1.6f,softGripMultiplier=.78f;
    public float firmDigMultiplier=.25f,softDigMultiplier=1.7f;
    public float buriedRollingResistance=.17f;
    public float puddleDepthMetres=.045f;
}
