using System;
using UnityEngine;

[Serializable]
public struct WaterBodySample
{
    public Vector3 localPosition;
    [Min(0)] public float displacedVolumeCubicMetres, dragAreaSquareMetres;
    [Min(.01f)] public float fullSubmersionMetres;
}

[CreateAssetMenu(menuName = "Game/Vehicles/Water interaction profile")]
public sealed class VehicleWaterProfile : ScriptableObject
{
    public WaterBodySample[] bodySamples = Array.Empty<WaterBodySample>();
    public Vector3 intakeLocalPosition = new Vector3(0, 1.1f, 1.6f);
    [Min(.01f)] public float intakeFloodSeconds = 1.5f;
    [Min(1)] public float maximumForcePerSampleNewtons = 12000;
}
