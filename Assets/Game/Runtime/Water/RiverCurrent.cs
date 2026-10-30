using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class RiverCurrent : MonoBehaviour
{
    public float halfLength = 30;
    public float halfWidth = 5.5f;
    public float currentSpeed = 2.7f;
    public float densityKgPerCubicMeter = 1000;
    public float dragCoefficient = 1.15f;
    public float maximumDisplacedVolume = 3.6f;
    public float sideAreaSquareMeters = 5.4f;
    public float maximumForceNewtons = 65000;
    public float surfaceSlope = 0.008f;
    public float profileStart = -30;
    public float profileEnd = 30;
    public float[] surfaceHeights;
    public float[] channelWidths;
    public float MinimumAlong => surfaceHeights != null && surfaceHeights.Length > 1 ? profileStart : -halfLength;
    public float MaximumAlong => surfaceHeights != null && surfaceHeights.Length > 1 ? profileEnd : halfLength;
    public Vector3 LastForce { get; private set; }
    public int WetSamples { get; private set; }
    public float LastSubmersion { get; private set; }
    private readonly HashSet<Rigidbody> bodies = new HashSet<Rigidbody>();
    private readonly List<Rigidbody> remove = new List<Rigidbody>();
    private static readonly Vector3[] Samples =
    {
        new Vector3(-0.9f,0.25f,1.5f),new Vector3(0.9f,0.25f,1.5f),
        new Vector3(-0.9f,0.25f,-0.8f),new Vector3(0.9f,0.25f,-0.8f),
        new Vector3(-0.9f,0.25f,-2.7f),new Vector3(0.9f,0.25f,-2.7f)
    };
    private float Sample(float[] values,float alongStream,float fallback)
    {
        if(values==null || values.Length<2)return fallback;
        float index=Mathf.InverseLerp(profileStart,profileEnd,alongStream)*(values.Length-1);
        int lower=Mathf.Min(Mathf.FloorToInt(index),values.Length-2);
        return Mathf.Lerp(values[lower],values[lower+1],index-lower);
    }
    public float SurfaceHeight(float alongStream) { return Sample(surfaceHeights,alongStream,transform.position.y-alongStream*surfaceSlope); }
    public float HalfWidthAt(float alongStream) { return Sample(channelWidths,alongStream,halfWidth); }
    public float ChannelOffset(float alongStream) { return Mathf.Sin(alongStream*0.07f)*1.6f; }
    public bool Contains(Vector3 world)
    {
        Vector3 p=transform.InverseTransformPoint(world);
        return p.x>MinimumAlong && p.x<MaximumAlong && Mathf.Abs(p.z-ChannelOffset(p.x))<HalfWidthAt(p.x);
    }
    private void OnTriggerEnter(Collider other){Register(other);}
    private void OnTriggerStay(Collider other){Register(other);}
    private void Register(Collider other)
    {var body=other.attachedRigidbody;if(body!=null && !body.isKinematic && (body.GetComponent<VehiclePhysics>()!=null||body.GetComponent<ForestKamazController>()!=null))bodies.Add(body);}
    private void OnDisable(){bodies.Clear();}
    private void FixedUpdate()
    {
        remove.Clear();
        foreach(var body in bodies)
        {if(body==null){remove.Add(body);continue;}ApplyForces(body);if(WetSamples==0&&!Contains(body.worldCenterOfMass))remove.Add(body);}
        foreach(var body in remove)bodies.Remove(body);
    }
    public void ApplyForces(Rigidbody body)
    {
        LastForce=Vector3.zero;WetSamples=0;LastSubmersion=0;
        if(body==null || body.isKinematic)return;
        foreach(var local in Samples)
        {
            Vector3 point=body.transform.TransformPoint(local);if(!Contains(point))continue;
            Vector3 p=transform.InverseTransformPoint(point);
            float depth=SurfaceHeight(p.x)-point.y;if(depth<=0)continue;
            float fraction=Mathf.Clamp01(depth/0.75f);WetSamples++;LastSubmersion+=fraction/Samples.Length;
            Vector3 velocity=body.GetPointVelocity(point);
            Vector3 flow=transform.right*(currentSpeed*(1+Mathf.Abs(p.x)*0.004f));
            Vector3 relative=flow-Vector3.ProjectOnPlane(velocity,Vector3.up);
            Vector3 drag=0.5f*densityKgPerCubicMeter*dragCoefficient*(sideAreaSquareMeters/Samples.Length)*fraction*relative.magnitude*relative;
            float buoyancy=densityKgPerCubicMeter*Physics.gravity.magnitude*(maximumDisplacedVolume/Samples.Length)*fraction;
            float verticalDamping=Mathf.Clamp(-velocity.y*900*fraction,-buoyancy*0.65f,buoyancy*0.65f);
            Vector3 force=Vector3.ClampMagnitude(drag+Vector3.up*(buoyancy+verticalDamping),maximumForceNewtons/Samples.Length);
            body.AddForceAtPosition(force,point,ForceMode.Force);LastForce+=force;
        }
    }
    private void OnGUI()
    {
        if(WetSamples==0 || LastSubmersion<=0)return;
        GUI.Box(new Rect(Screen.width/2-205,Screen.height-95,410,65),"RIVER FORD — CURRENT PUSHES SIDEWAYS");
        GUI.Label(new Rect(Screen.width/2-190,Screen.height-65,380,30),$"Current {currentSpeed:F1} m/s — LOW range, steer upstream");
    }
}
