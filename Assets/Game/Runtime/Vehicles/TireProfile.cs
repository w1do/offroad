using UnityEngine;

public enum TireType { Road, AllTerrain, MudTerrain }

[CreateAssetMenu(menuName = "Game/Vehicles/Tire profile")]
public sealed class TireProfile : ScriptableObject
{
    public TireType tireType = TireType.AllTerrain;
    [Header("SI units, prototype calibration")]
    [Min(.01f)] public float radiusMetres = .39731f, widthMetres = .285f;
    [Min(1)] public float massKg = 30;
    [Min(.01f)] public float inertiaKgSquareMetres = 2.37f;
    [Min(.01f)] public float footprintLengthMetres = .22f;
    [Range(.1f, 2)] public float longitudinalMultiplier = 1, lateralMultiplier = 1, softGroundMultiplier = 1;
    [Min(0)] public float rollingResistance = .018f;
    public Texture2D treadNormal, treadHeight;
    [Min(.01f)] public float treadRepeatMetres = .16f;

    public float ContactAreaSquareMetres(float pressureLoadMultiplier)
    {
        return Mathf.Max(.001f, widthMetres * footprintLengthMetres
            / Mathf.Clamp(pressureLoadMultiplier, .2f, 2));
    }
}
