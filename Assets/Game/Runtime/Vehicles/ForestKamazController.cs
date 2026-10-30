using UnityEngine;
using UnityEngine.InputSystem;

// Initial six-wheel driving rig for the imported forest test scene.
[RequireComponent(typeof(Rigidbody))]
public sealed class ForestKamazController : MonoBehaviour
{
    public WheelCollider[] wheels;
    public Transform[] visuals;
    public string vehicleName = "KAMAZ";
    public bool driveFrontWheels;
    public Vector3 centreOfMass = new Vector3(0, 0.95f, -0.4f);
    public float aerodynamicDrag = 3.8f;
    public float motorTorqueNm = 2400f;
    public float brakeTorqueNm = 8500f;
    public float dirtForwardGrip = 0.65f;
    public bool reverseGear;
    public bool lowRange;
    public float lowRangeRatio = 3f;
    public bool BrakeApplied { get; private set; }
    public float DrivenSlip { get; private set; }
    public float wheelPowerWatts = 150000f;
    public float throttleResponseSeconds = 0.9f;
    public float rollingResistance = 0.015f;
    public float engineBrakingNewtons = 1500f;
    public float maximumSpeedKmh = 65f;
    public float reverseSpeedKmh = 10f;
    public float steeringRateDegrees = 45f;
    public float driftStartKmh = 30f;
    public float lateralAccelerationLimit = 2.8f;
    public float frontGrip = 1.05f;
    public float rearGrip = 0.95f;
    public float slidingRearGrip = 0.48f;
    public float gripTransitionRate = 1.5f;
    public float LateralSpeed => body == null ? 0 : Vector3.Dot(body.linearVelocity, transform.right);
    public bool Sliding { get; private set; }
    public int MudContacts { get; private set; }
    public int IceContacts { get; private set; }
    public bool ParkingBrakeApplied => brake;
    public float ThrottleInput => drivetrain!=null&&drivetrain.enabled ? drivetrain.Pedal : Mathf.Max(0, throttle);
    public VehiclePhysics Drivetrain => drivetrain;
    private VehiclePhysics drivetrain;
    private TruckChains chains;
    public bool testControl;
    public float testThrottle;
    public float testSteer;
    public bool testBrake;
    public float SpeedKmh => body == null ? 0 : body.linearVelocity.magnitude * 3.6f;
    private Rigidbody body;
    [SerializeField, HideInInspector] private Vector3 spawn;
    [SerializeField, HideInInspector] private Quaternion spawnRotation;
    private float throttle, steer;
    private bool brake;
    private float appliedThrottle;
    private WheelHit[] contacts;
    private bool[] hasContact;
    private bool wasStaticBraking;

    public void ShiftDebugOrigin(Vector3 delta) { spawn -= delta; }
    public void SetDebugRecovery(Vector3 position, Quaternion rotation) { spawn = position; spawnRotation = rotation; }

    private void Awake()
    {
        InitializeRuntime();
        body.centerOfMass = centreOfMass;
        spawn = body.position;
        spawnRotation = body.rotation;
        foreach (var wheel in wheels) wheel.ConfigureVehicleSubsteps(8, 8, 10);
    }
    private void OnEnable() { InitializeRuntime(); }
    private void InitializeRuntime()
    {
        body = GetComponent<Rigidbody>();
        chains = GetComponent<TruckChains>();
        drivetrain = GetComponent<VehiclePhysics>();
        if (wheels == null) return;
        contacts = new WheelHit[wheels.Length];
        hasContact = new bool[wheels.Length];
    }

    private void Update()
    {
        var k = Keyboard.current;
        throttle = testControl ? testThrottle : k == null ? 0 :
            (k.wKey.isPressed || k.upArrowKey.isPressed ? 1 : 0) - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1 : 0);
        steer = testControl ? testSteer : k == null ? 0 :
            (k.dKey.isPressed || k.rightArrowKey.isPressed ? 1 : 0) - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1 : 0);
        brake = testControl ? testBrake : k != null && k.spaceKey.isPressed;
        if (!testControl && k != null && k.vKey.wasPressedThisFrame && SpeedKmh < 1.5f)
            reverseGear = !reverseGear;
        if (!testControl && k != null && k.lKey.wasPressedThisFrame && SpeedKmh < 1.5f)
            lowRange = !lowRange;
        if (!testControl && k != null && k.rKey.wasPressedThisFrame)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = spawn;
            body.rotation = spawnRotation;
            if(drivetrain!=null)drivetrain.ResetState();
        }
    }

    private void LateUpdate()
    {
        for (int i = 0; i < wheels.Length; i++)
        {
            wheels[i].GetWorldPose(out var position, out var rotation);
            visuals[i].SetPositionAndRotation(position, rotation);
        }
    }

    private void FixedUpdate()
    {
        if(drivetrain!=null&&drivetrain.enabled&&drivetrain.profile!=null)
        {
            drivetrain.Step(Mathf.Max(0,throttle),steer,throttle<-.1f,brake);
            BrakeApplied=drivetrain.BrakeApplied;DrivenSlip=drivetrain.DrivenSlip;
            Sliding=drivetrain.Sliding;MudContacts=drivetrain.MudContacts;IceContacts=drivetrain.IceContacts;
            return;
        }
        float speed = Vector3.Dot(body.linearVelocity, transform.forward);
        float driveRequest = Mathf.Max(0, throttle) * (reverseGear ? -1 : 1);
        BrakeApplied = brake || throttle < -0.1f;
        int groundedCount = 0;
        foreach (var wheel in wheels) if (wheel.isGrounded) groundedCount++;
        // At walking speed, hand longitudinal contact to a bounded static-friction
        // solver. WheelCollider's sliding model otherwise creeps on a held slope.
        bool staticBraking = BrakeApplied && Mathf.Abs(speed) < 0.5f && groundedCount >= 4;
        float holdCapacity = 0;
        float totalLoad = 0;
        Vector3 contactNormal = Vector3.zero;
        appliedThrottle = Mathf.MoveTowards(appliedThrottle, BrakeApplied ? 0 : driveRequest,
            Time.fixedDeltaTime / (Mathf.Abs(throttle) < 0.01f ? 0.15f : Mathf.Max(0.1f, throttleResponseSeconds)));
        float speedLimit = appliedThrottle < 0 ? reverseSpeedKmh : lowRange ? maximumSpeedKmh / lowRangeRatio : maximumSpeedKmh;
        float drivenSpeedKmh = Mathf.Max(0, speed * Mathf.Sign(appliedThrottle)) * 3.6f;
        float limiter = Mathf.Clamp01((speedLimit - drivenSpeedKmh) / 5f);
        float omega = 0;
        int drivenCount = driveFrontWheels ? wheels.Length : wheels.Length - 2;
        for (int i = 0; i < wheels.Length; i++) if (i >= 2 || driveFrontWheels) omega += Mathf.Abs(wheels[i].rpm) * (2f * Mathf.PI / 60f) / Mathf.Max(1,drivenCount);
        float torque = Mathf.Min(motorTorqueNm * (lowRange ? lowRangeRatio : 1f), wheelPowerWatts / (Mathf.Max(1,drivenCount) * Mathf.Max(omega, 1f)));
        float rearPosition = 0;
        for (int i = 2; i < wheels.Length; i++) rearPosition += wheels[i].transform.localPosition.z / Mathf.Max(1,wheels.Length-2);
        float wheelbase = Mathf.Abs(wheels[0].transform.localPosition.z - rearPosition);
        float turnDemand = speed * speed * Mathf.Abs(Mathf.Tan(wheels[0].steerAngle * Mathf.Deg2Rad)) / Mathf.Max(1f, wheelbase);
        float drift = Mathf.Clamp01((SpeedKmh - driftStartKmh) / 15f) *
            Mathf.Clamp01((turnDemand - lateralAccelerationLimit) / lateralAccelerationLimit);
        Sliding = Mathf.Abs(LateralSpeed) > 1f;
        bool grounded = false;
        MudContacts = 0;
        IceContacts = 0;
        DrivenSlip = 0;
        float resistanceCoefficient = rollingResistance;
        for (int i = 0; i < wheels.Length; i++)
        {
            var wheel = wheels[i];
            wheel.steerAngle = Mathf.MoveTowards(wheel.steerAngle,
                i < 2 ? steer * Mathf.Lerp(30, 10, Mathf.Clamp01(SpeedKmh / 55)) : 0,
                steeringRateDegrees * Time.fixedDeltaTime);
            // AWD prototype shares a bounded power budget per wheel, so one unloaded
            // spinning wheel cannot suppress torque at every other driven wheel.
            float wheelTorque = driveFrontWheels ? Mathf.Min(motorTorqueNm * (lowRange ? lowRangeRatio : 1f),
                wheelPowerWatts / (Mathf.Max(1,drivenCount) * Mathf.Max(1,Mathf.Abs(wheel.rpm)*2f*Mathf.PI/60f))) : torque;
            wheel.motorTorque = (i < 2 && !driveFrontWheels) || BrakeApplied ? 0 : appliedThrottle * wheelTorque * limiter;
            wheel.brakeTorque = BrakeApplied ? brakeTorqueNm : 0;
            var lateral = wheel.sidewaysFriction;
            float targetGrip = i < 2 ? frontGrip : Mathf.Lerp(rearGrip, slidingRearGrip, drift);
            var forward = wheel.forwardFriction;
            float targetForwardGrip = dirtForwardGrip;
            hasContact[i] = wheel.GetGroundHit(out var hit);
            if (hasContact[i])
            {
                contacts[i] = hit;
                if (i >= 2 || driveFrontWheels) DrivenSlip = Mathf.Max(DrivenSlip, Mathf.Abs(hit.forwardSlip));
                if (hit.collider.TryGetComponent<ForestDrivingSurface>(out var surface))
                {
                    bool chained = i >= 2 && chains != null && chains.Installed && surface.chainsImproveGrip;
                    float combinedSlipGrip = i >= 2 && !chained ? Mathf.Lerp(1f, 0.25f, Mathf.Clamp01(Mathf.Abs(hit.forwardSlip) / 0.5f)) : 1f;
                    targetGrip = Mathf.Min(targetGrip, (chained ? surface.chainedLateralGrip : surface.lateralGrip) * combinedSlipGrip);
                    targetForwardGrip = chained ? surface.chainedForwardGrip : surface.forwardGrip;
                    resistanceCoefficient += surface.rollingResistance / wheels.Length;
                    if (surface.cover == ForestDrivingSurface.Cover.Mud) MudContacts++;
                    else IceContacts++;
                }
                holdCapacity += hit.force * targetForwardGrip;
                totalLoad += hit.force;
                contactNormal += hit.normal * hit.force;
            }
            lateral.stiffness = Mathf.MoveTowards(lateral.stiffness, targetGrip, gripTransitionRate * Time.fixedDeltaTime);
            wheel.sidewaysFriction = lateral;
            // No simultaneous WheelCollider longitudinal force in the static regime.
            forward.stiffness = staticBraking ? 0 : wasStaticBraking ? targetForwardGrip : Mathf.MoveTowards(forward.stiffness, targetForwardGrip, gripTransitionRate * Time.fixedDeltaTime);
            wheel.forwardFriction = forward;
            grounded |= wheel.isGrounded;
        }
        if (staticBraking && totalLoad > 0)
        {
            Vector3 tangent = Vector3.ProjectOnPlane(transform.forward, contactNormal.normalized).normalized;
            float requested = -Vector3.Dot(Physics.gravity * body.mass, tangent)
                - Vector3.Dot(body.linearVelocity, tangent) * body.mass / Time.fixedDeltaTime;
            float force = Mathf.Clamp(requested, -holdCapacity, holdCapacity);
            for (int i = 0; i < wheels.Length; i++)
                if (hasContact[i]) body.AddForceAtPosition(tangent * force * (contacts[i].force / totalLoad), contacts[i].point);
        }
        if (grounded && !staticBraking)
        {
            Vector3 planar = Vector3.ProjectOnPlane(body.linearVelocity, transform.up);
            float resistance = body.mass * Physics.gravity.magnitude * resistanceCoefficient;
            if (Mathf.Abs(throttle) < 0.01f) resistance += engineBrakingNewtons;
            resistance = Mathf.Min(resistance, body.mass * planar.magnitude / Time.fixedDeltaTime);
            body.AddForce(-planar.normalized * resistance);
            body.AddForce(-body.linearVelocity * body.linearVelocity.magnitude * aerodynamicDrag);
        }
        wasStaticBraking = staticBraking;
    }

    private void OnGUI()
    {
        if(drivetrain!=null&&drivetrain.enabled&&drivetrain.profile!=null)
        {
            GUI.Box(new Rect(18,18,680,148),vehicleName);
            GUI.Label(new Rect(30,43,655,112),$"{SpeedKmh:F0} км/ч   {drivetrain.EngineRpm:F0} об/мин   Передача: {(drivetrain.Neutral?"N":reverseGear?"R":drivetrain.Gear.ToString())}  {(lowRange?"ПОНИЖЕННАЯ":"ПОВЫШЕННАЯ")}   Газ: {drivetrain.Pedal*100:F0}%\n{(DrivenSlip>.35f?"ПРОБУКСОВКА":"")}  {(Sliding?"ЗАНОС":"")}   Угол скольжения: {drivetrain.SlipAngle:F0}°\nW: плавный газ / Shift: малый газ   S: тормоз   Space: ручник\nQ / E: передачи   N: нейтраль   L: диапазон   V: задний ход\nМышь: обзор   C: камера   T + удерживать Space: цепи   R: вернуть машину");
            return;
        }
        GUI.Box(new Rect(18, 18, 640, 126), vehicleName + " / FOREST DRIVE");
        GUI.Label(new Rect(30, 45, 620, 90), $"{SpeedKmh:F0} km/h  {(reverseGear ? "REVERSE" : "FORWARD")} {(lowRange ? "LOW" : "HIGH")}  {(IceContacts > 0 ? "ICE / SNOW" : MudContacts > 0 ? "MUD" : "DIRT")}  {(DrivenSlip > 0.25f ? "WHEELSPIN" : "")}\nW: gas  S / Space: brake  V: reverse  L: low/high (stopped)\nRMB: look  Wheel: zoom  C: camera  R: reset  H: horn\n{(chains == null ? "" : chains.Message)} {(chains != null && chains.Working ? (chains.Progress * 100).ToString("F0") + "%" : "")}");
    }
}
