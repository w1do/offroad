using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.InputSystem;

[MovedFrom(true,"","Assembly-CSharp","TruckAudio")]
[RequireComponent(typeof(Rigidbody))]
public sealed partial class VehicleAudio : MonoBehaviour
{
    public VehicleAudioProfile profile;
    public AudioSource idle, load, road, slip, chains, effects, horn;
    public float EngineRpm { get; private set; }
    public int AirReleaseCount { get; private set; }
    public int GearSoundCount { get; private set; }
    public int ChainFittingCount { get; private set; }
    public bool testHorn;
    private ForestKamazController truck;
    private VehiclePhysics vehicle;
    private bool Common => vehicle!=null&&vehicle.enabled;
    private WheelCollider[] ActiveWheels => Common?vehicle.Wheels:truck.wheels;
    private bool IsBraking => Common?vehicle.ServiceBrakeApplied:truck.BrakeApplied;
    private bool IsLow => Common?vehicle.LowRange:truck.lowRange;
    private bool IsReverse => Common?vehicle.ReverseGear:truck.reverseGear;
    private bool Testing => Common?vehicle.externalControl:truck.testControl;
    private float Speed => Common?vehicle.SpeedKmh:truck.SpeedKmh;
    private float Pedal => Common?vehicle.Pedal:truck.ThrottleInput;
    private float Slip => Common?vehicle.DrivenSlip:truck.DrivenSlip;
    private float Lateral => Common?vehicle.LateralSpeed:truck.LateralSpeed;
    private int Mud => Common?vehicle.MudContacts:truck.MudContacts;
    private int Ice => Common?vehicle.IceContacts:truck.IceContacts;
    private TruckChains chainSystem;
    private bool previousBrake, previousLow, previousReverse, previousChainWork;
    private int previousGear;
    private float airCooldown;
    private bool ready;

    private void OnEnable()
    {
        ready=false;
        truck = GetComponent<ForestKamazController>();
        vehicle=GetComponent<VehiclePhysics>();
        chainSystem = GetComponent<TruckChains>();
        if(profile==null||vehicle==null&&truck==null)return;
        EnsureSource(ref idle,"Engine idle fallback",profile.idle,true);
        EnsureSource(ref load,"Engine load fallback",profile.load,true);
        EnsureSource(ref road,"Tyre rolling",profile.road,true);
        EnsureSource(ref slip,"Tyre slipping",profile.slip,true);
        EnsureSource(ref chains,"Chain rattle",profile.chains,true);
        EnsureSource(ref effects,"Mechanical effects",null,false);
        EnsureSource(ref horn,"Horn",profile.horn,true);
        ready = profile != null && idle != null && load != null && road != null && slip != null && chains != null && effects != null && horn != null;
        if (!ready) return;
        EngineRpm = profile.idleRpm;
        InitializeEngineLayers();
        previousBrake = IsBraking;
        previousGear=Common?vehicle.Gear:0;
        previousLow = IsLow; previousReverse = IsReverse;
        previousChainWork = chainSystem != null && chainSystem.Working;
        foreach (var source in new[] { idle, load, road, slip, chains, horn })
        {
            source.volume = 0;
            if (Application.isPlaying && source.clip != null && !(RecordedEngine && (source==idle||source==load))) source.Play();
        }
    }

    private void OnDisable()
    {
        StopEngineLayers();
        foreach (var source in new[] { idle, load, road, slip, chains, effects, horn })
            if (source != null) source.Stop();
    }

    private void Update() { UpdateAudio(Time.deltaTime); }

    public void UpdateAudio(float deltaTime)
    {
        if (!ready) return;
        float wheelRpm = 0;
        int grounded = 0;
        for (int i = 0; i < ActiveWheels.Length; i++)
        {
            wheelRpm += Mathf.Abs(ActiveWheels[i].rpm) / Mathf.Max(1,ActiveWheels.Length);
            if (ActiveWheels[i].isGrounded) grounded++;
        }
        float throttle = Pedal;
        float ratio = profile.wheelToEngineRatio * (IsLow ? (Common?vehicle.profile.lowRatio:truck.lowRangeRatio) : 1);
        float rpmTarget = Mathf.Clamp(Mathf.Max(profile.idleRpm + wheelRpm * ratio,
            profile.idleRpm + throttle * (IsBraking ? 350 : 900)), profile.idleRpm, profile.maximumRpm);
        EngineRpm = Common ? vehicle.EngineRpm : Mathf.Lerp(EngineRpm, rpmTarget, 1 - Mathf.Exp(-profile.engineResponse * deltaTime));
        float rpmBlend = Mathf.InverseLerp(profile.idleRpm, profile.maximumRpm, EngineRpm);
        float engineMix = Mathf.Clamp01(throttle * 0.7f + rpmBlend * 0.5f);
        idle.pitch = Mathf.Clamp(EngineRpm / Mathf.Max(100,profile.idleReferenceRpm), .75f, profile.maximumEnginePitch);
        load.pitch = Mathf.Clamp(EngineRpm / Mathf.Max(100,profile.loadReferenceRpm), .5f, profile.maximumEnginePitch);
        float master = profile.masterVolume;
        Smooth(idle, profile.engineVolume * master * Mathf.Lerp(0.65f, 0.18f, engineMix), deltaTime);
        Smooth(load, profile.engineVolume * master * Mathf.Lerp(0.08f, 0.85f, engineMix), deltaTime);
        if(RecordedEngine){idle.volume=0;load.volume=0;MixEngineLayers(deltaTime);}
        float speed = Mathf.Clamp01(Speed / 45f);
        float groundedMix = grounded > 0 ? 1 : 0;
        Smooth(road, profile.roadVolume * master * Mathf.Sqrt(speed) * groundedMix, deltaTime);
        road.pitch = Mathf.Lerp(0.6f, 1.5f, speed);
        float skid = Mathf.Clamp01(Mathf.Max(Slip * throttle * 0.8f, Mathf.Abs(Lateral) / 4f));
        Smooth(slip, profile.slipVolume * master * skid * groundedMix, deltaTime);
        slip.pitch = Mud > 0 ? 0.7f : Ice > 0 ? 1.1f : 0.9f;
        float chainMotion = Mathf.Clamp01(Mathf.Max(Speed / 25f, wheelRpm / 120f));
        Smooth(chains, chainSystem != null && chainSystem.Installed ? profile.chainVolume * master * chainMotion * groundedMix : 0, deltaTime);
        chains.pitch = Mathf.Lerp(0.55f, 1.8f, chainMotion);
        bool honking = testHorn || !Testing && Keyboard.current != null && Keyboard.current[Common&&vehicle.profile.equipment!=null?vehicle.profile.equipment.hornKey:UnityEngine.InputSystem.Key.B].isPressed;
        Smooth(horn, honking ? profile.hornVolume * master : 0, deltaTime, 18f);
        airCooldown = Mathf.Max(0, airCooldown - deltaTime);
        if (previousBrake && !IsBraking && airCooldown <= 0)
        { if(profile.brakeAir!=null){PlayEffect(profile.brakeAir);AirReleaseCount++;} airCooldown = 0.7f; }
        if (previousLow != IsLow || previousReverse != IsReverse || Common && previousGear!=vehicle.Gear)
        { PlayEffect(profile.gear); GearSoundCount++; }
        bool chainWork = chainSystem != null && chainSystem.Working;
        if (chainWork && !previousChainWork) { PlayEffect(profile.chainFitting); ChainFittingCount++; }
        previousBrake = IsBraking;
        previousGear=Common?vehicle.Gear:0;
        previousLow = IsLow; previousReverse = IsReverse; previousChainWork = chainWork;
    }

    private void EnsureSource(ref AudioSource source,string label,AudioClip clip,bool loop)
    {
        if(source!=null)return;
        var root=new GameObject(label);root.transform.SetParent(transform,false);
        source=root.AddComponent<AudioSource>();source.clip=clip;source.loop=loop;source.playOnAwake=false;
        source.spatialBlend=profile.engineSpatialBlend;source.dopplerLevel=0;source.minDistance=profile.minDistance;source.maxDistance=profile.maxDistance;source.volume=loop?0:1;
    }
    private void PlayEffect(AudioClip clip)
    { if (clip != null) effects.PlayOneShot(clip, profile.effectVolume * profile.masterVolume); }
    private static void Smooth(AudioSource source, float target, float dt, float response = 8f)
    { source.volume = Mathf.Lerp(source.volume, target, 1 - Mathf.Exp(-response * dt)); }
}

