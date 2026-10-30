using UnityEngine;

public enum DriveSurface { Asphalt, Ice, Broken, Gravel }

public sealed class RoadSurface : MonoBehaviour
{
    public DriveSurface kind;
}
