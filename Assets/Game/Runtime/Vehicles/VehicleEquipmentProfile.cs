using UnityEngine;
using UnityEngine.InputSystem;

[CreateAssetMenu(menuName="Game/Vehicles/Equipment profile")]
public sealed class VehicleEquipmentProfile : ScriptableObject
{
    [Header("Controls")]
    public Key driveKey=Key.H,chainsKey=Key.Digit1,winchKey=Key.Digit2,hornKey=Key.B,pressureKey=Key.Digit3;
    public Key differentialLockKey=Key.J;
    public Key diagnosticsKey=Key.F3;
    public bool differentialLockAvailable=true,defaultDifferentialLocked;
    public float maximumDifferentialSwitchSpeedKmh=5;
    public float lockedAxleTorqueNm=1800,lockedAxleCoupling=45;
    [Header("Tyre pressure / bar, prototype calibration")]
    public bool pressureAdjustmentAvailable=true;
    public float roadPressureBar=2.2f,softGroundPressureBar=1.2f;
    public float minimumTyrePressureBar=.8f,maximumTyrePressureBar=3;
    public float maximumPressureAdjustmentSpeedKmh=.5f;
    public float deflationBarPerSecond=.08f,inflationBarPerSecond=.04f;
    public float lowPressureSoftGripMultiplier=1.28f,lowPressureSoilLoadMultiplier=.58f;
    public float lowPressureRollingResistance=.012f;
    [Header("Loaded tyre shape / prototype calibration")]
    [Range(0, .2f)] public float roadTyreCompression = .015f;
    [Range(0, .2f)] public float deflatedTyreCompression = .18f;
    [Range(0, .3f)] public float deflatedSidewallBulge = .24f;
    [Range(.4f, .9f)] public float rigidRimRadiusFraction = .78f;
    [Min(.01f)] public float tyreShapeResponseSeconds = .18f;
    [Header("Selectable drive")]
    public bool switchableDrive=true,defaultFourWheelDrive=false;
    public float maximumDriveSwitchSpeedKmh=35;
    [Header("Chains / prototype calibration")]
    public bool chainsAvailable=true,chainsOnAllWheels=true;
    public Material chainMaterial;
    public float chainWidthMetres=.31f;
    [Range(8,32)]public int chainLinkCount=16;
    public float installationSeconds=4,maximumInstallSpeedKmh=.3f;
    public float chainWearPerSlipMetre=.00045f,chainHeatingCPerSlipMetre=.65f;
    public float ambientTemperatureC=-10,chainCoolingCPerSecond=1.5f;
    public float chainWarningTemperatureC=85,chainBreakTemperatureC=130;
    public float hotChainWearMultiplier=4;
    [Header("Winch / SI units, prototype calibration")]
    public bool winchAvailable=true;
    public Vector3 winchMount=new Vector3(0,.65f,2.1f);
    public float maximumCableMetres=28,minimumAnchorMetres=2,anchorSearchDegrees=75;
    public float maximumWinchAttachSpeedKmh=5;
    public float reelMetresPerSecond=.65f,maximumPullNewtons=28000;
    public float cableSpringNewtonsPerMetre=18000,cableDamperNsPerMetre=2500;
    public float cableWearPerSecondAtFullLoad=.018f,maximumContinuousPullSeconds=40;
    public float winchCooldownPerSecond=.65f;
}
