using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public sealed class MountainSUV : MonoBehaviour
{
    public WheelCollider[] wheels;
    public Transform[] wheelVisuals;
    public MountainRoute route;
    public bool testControl;
    public float testThrottle;
    public float testSteer;
    public bool testBrake;
    public DriveSurface surface;
    public int groundedWheels;
    public float SpeedKmh => body == null ? 0 : body.linearVelocity.magnitude * 3.6f;
    public float Progress => route == null ? 0 : route.Nearest(transform.position) / (float)(route.points.Length - 1);
    private Rigidbody body;
    private Vector3 spawn;
    private Quaternion spawnRotation;
    private float throttle;
    private float steering;
    private bool handbrake;
    private Quaternion[] visualRotation;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.centerOfMass = new Vector3(0, 0.58f, 0);
        spawn = transform.position;
        spawnRotation = transform.rotation;
        visualRotation = new Quaternion[wheelVisuals.Length];
        for (int i = 0; i < wheels.Length; i++)
        {
            wheels[i].ConfigureVehicleSubsteps(8, 8, 10);
            visualRotation[i] = wheelVisuals[i].localRotation;
        }
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (testControl) { throttle = testThrottle; steering = testSteer; handbrake = testBrake; }
        else if (keyboard != null)
        {
            throttle = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0)
                - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
            steering = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0)
                - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
            handbrake = keyboard.spaceKey.isPressed;
            if (keyboard.rKey.wasPressedThisFrame) ResetToRoad();
        }
        for (int i = 0; i < wheels.Length; i++)
        {
            wheels[i].GetWorldPose(out Vector3 position, out Quaternion rotation);
            wheelVisuals[i].SetPositionAndRotation(position, rotation * visualRotation[i]);
        }
        if (transform.position.y < -30) ResetToRoad();
    }

    private void FixedUpdate()
    {
        float forwardSpeed = Vector3.Dot(body.linearVelocity, transform.forward);
        float turn = steering * Mathf.Lerp(31, 12, Mathf.Clamp01(SpeedKmh / 85));
        surface = DriveSurface.Gravel;
        groundedWheels = 0;
        for (int i = 0; i < wheels.Length; i++)
        {
            WheelCollider wheel = wheels[i];
            DriveSurface contact = DriveSurface.Gravel;
            if (wheel.GetGroundHit(out WheelHit hit))
            {
                groundedWheels++;
                var road = hit.collider.GetComponent<RoadSurface>();
                if (road != null) contact = road.kind;
                surface = contact;
            }
            float grip = contact == DriveSurface.Ice ? 0.16f : contact == DriveSurface.Broken ? 0.95f : contact == DriveSurface.Asphalt ? 1.55f : 0.65f;
            var sideways = wheel.sidewaysFriction;
            sideways.stiffness = grip * (handbrake && i >= 2 ? 0.25f : 1);
            wheel.sidewaysFriction = sideways;
            var forward = wheel.forwardFriction;
            forward.stiffness = contact == DriveSurface.Ice ? 0.24f : grip;
            wheel.forwardFriction = forward;
            wheel.steerAngle = i < 2 ? turn : 0;
            bool reversingDirection = throttle * forwardSpeed < -0.7f;
            float motor = reversingDirection || handbrake ? 0 : throttle * (throttle < 0 ? 620 : 1050);
            if ((SpeedKmh > 100 && throttle > 0) || (forwardSpeed < -7 && throttle < 0)) motor = 0;
            wheel.motorTorque = motor;
            wheel.brakeTorque = handbrake ? 3800 : reversingDirection ? 2400 : Mathf.Abs(throttle) < 0.01f ? 100 : 0;
        }
        if (groundedWheels > 0)
        {
            body.AddForce(-transform.up * body.linearVelocity.sqrMagnitude * 2.1f);
            ApplyAntiRoll(0, 1);
            ApplyAntiRoll(2, 3);
        }
    }

    private void ApplyAntiRoll(int left, int right)
    {
        bool l = wheels[left].GetGroundHit(out WheelHit lh);
        bool r = wheels[right].GetGroundHit(out WheelHit rh);
        float lt = l ? (-wheels[left].transform.InverseTransformPoint(lh.point).y - wheels[left].radius) / wheels[left].suspensionDistance : 1;
        float rt = r ? (-wheels[right].transform.InverseTransformPoint(rh.point).y - wheels[right].radius) / wheels[right].suspensionDistance : 1;
        float force = (lt - rt) * 4200;
        if (l) body.AddForceAtPosition(wheels[left].transform.up * -force, wheels[left].transform.position);
        if (r) body.AddForceAtPosition(wheels[right].transform.up * force, wheels[right].transform.position);
    }

    public void ResetToRoad()
    {
        if (route != null)
        {
            int i = Mathf.Clamp(route.Nearest(transform.position), 0, route.points.Length - 3);
            spawn = route.points[i] + Vector3.up * 0.5f;
            spawnRotation = Quaternion.LookRotation(route.points[i + 2] - route.points[i]);
        }
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.position = spawn;
        body.rotation = spawnRotation;
        transform.SetPositionAndRotation(spawn, spawnRotation);
    }
}
