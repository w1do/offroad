using System;
using UnityEngine;
using UnityEngine.InputSystem;

public enum VehicleHeadlightMode { Automatic, On, Off }
public enum VehicleLampKind { LowBeam, HighBeam, Fog }

[Serializable]
public sealed class VehicleBeamSettings
{
    [Min(0)] public float intensityCandela = 3000f;
    [Min(0.1f)] public float rangeMetres = 55f;
    [Range(1, 179)] public float outerAngleDegrees = 65f;
    [Range(0, 179)] public float innerAngleDegrees = 38f;
    public Color color = new Color(1f, 0.91f, 0.72f);
    public bool shadows = true;
    public Texture2D cookie;
}

[CreateAssetMenu(menuName = "Game/Vehicles/Lighting profile")]
public sealed class VehicleLightingProfile : ScriptableObject
{
    [Header("Controls")]
    public Key modeKey = Key.F;
    public Key lowBeamKey = Key.K;
    public Key highBeamKey = Key.T;
    public Key fogLampsKey = Key.G;
    public VehicleHeadlightMode initialMode = VehicleHeadlightMode.Automatic;
    [Header("Automatic headlights / prototype calibration")]
    [Range(0, 1)] public float switchOnDaylight = 0.35f;
    [Range(0, 1)] public float switchOffDaylight = 0.6f;
    [Min(0)] public float automaticDelaySeconds = 2f;
    public bool automaticFogInBadWeather;
    [Range(0, 1)] public float fogRainThreshold = 0.6f;
    [Min(0)] public float fogDensityThreshold = 0.008f;
    [Header("Beams / candela, prototype calibration")]
    public VehicleBeamSettings lowBeam = new VehicleBeamSettings();
    public VehicleBeamSettings highBeam = new VehicleBeamSettings { intensityCandela = 18000f, rangeMetres = 110f, outerAngleDegrees = 32f, innerAngleDegrees = 18f };
    public VehicleBeamSettings fog = new VehicleBeamSettings { intensityCandela = 1200f, rangeMetres = 28f, outerAngleDegrees = 100f, innerAngleDegrees = 65f, color = new Color(1f, 0.8f, 0.42f), shadows = false };
    public VehicleBeamSettings Beam(VehicleLampKind kind) => kind == VehicleLampKind.HighBeam ? highBeam : kind == VehicleLampKind.Fog ? fog : lowBeam;
}
