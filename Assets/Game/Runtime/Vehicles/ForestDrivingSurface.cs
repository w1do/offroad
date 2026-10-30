using UnityEngine;

public sealed class ForestDrivingSurface : MonoBehaviour
{
    public enum Cover { Mud, Ice, PackedSnow, Gravel }
    public Cover cover;
    [Range(0.05f, 2f)] public float forwardGrip = 0.12f;
    [Range(0.1f, 2f)] public float lateralGrip = 0.42f;
    [Range(0f, 0.2f)] public float rollingResistance = 0.045f;
    public bool chainsImproveGrip;
    public float chainedForwardGrip = 0.95f;
    public float chainedLateralGrip = 0.75f;
}
