using UnityEngine;

// Samples tyre width and lower arc, then places ONE native suspension ray on
// a real surface point. The body and native wheel shape remain unchanged.
[DisallowMultipleComponent]
[RequireComponent(typeof(WheelCollider))]
public sealed class WheelContactEnvelope : MonoBehaviour
{
    [SerializeField, HideInInspector] WheelCollider wheel;
    [SerializeField, HideInInspector] Vector3 baseCentre;
    [SerializeField, HideInInspector] bool captured;
    [SerializeField, HideInInspector] Vector3 basePosition;
    [SerializeField, HideInInspector] bool positionCaptured;
    float fraction, angle;
    Vector3 offset;
    Rigidbody body;
    readonly RaycastHit[] rayHits = new RaycastHit[32];
    public bool ArcSupported => Mathf.Abs(angle) > .1f;
    public float ArcDegrees => angle;
    public float HeightGainMetres { get; private set; }
    public bool ShoulderSupported => Mathf.Abs(fraction) > .01f;
    public Vector3 WorldOffset => !positionCaptured ? Vector3.zero : transform.parent==null
        ? transform.localPosition-basePosition : transform.parent.TransformVector(transform.localPosition-basePosition);

    public void Bind(WheelCollider target)
    {
        wheel = target;
        body = wheel.attachedRigidbody;
        if (!captured) { baseCentre = wheel.center; captured = true; }
        if (!positionCaptured) { basePosition = transform.localPosition; positionCaptured = true; }
        Restore();
    }

    public void Restore()
    {
        if (wheel != null && captured) wheel.center = baseCentre;
        if (positionCaptured) transform.localPosition = basePosition;
        offset = Vector3.zero; fraction = angle = 0; HeightGainMetres = 0;
    }

    public void GetWorldPose(out Vector3 position, out Quaternion rotation)
    {
        wheel.GetWorldPose(out position, out rotation);
        position -= WorldOffset;
    }

    bool Sample(Vector3 hub, Vector3 across, Vector3 forward, Vector3 up, float width, float f, float degrees,
        WheelRutContactSettings settings, out float height, out SoftGroundSurfaceSample surface)
    {
        float crown = settings.shoulderCrownDropMetres * f * f;
        float rad=degrees*Mathf.Deg2Rad;
        Vector3 probe = hub - up * wheel.radius + across * (f * width * .5f) + up * (crown+wheel.radius*(1-Mathf.Cos(rad)))+forward*(wheel.radius*Mathf.Sin(rad));
        height = 0;
        bool found=SoftGroundRuts.TrySampleSurface(probe, gameObject.scene, out surface);
        if(settings.longitudinalSupport)
        {
            float span=wheel.radius*2+wheel.suspensionDistance;
            int count=gameObject.scene.GetPhysicsScene().Raycast(probe+up*span,-up,rayHits,span*2,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            if(count<rayHits.Length)for(int i=0;i<count;i++)
            {
                var h=rayHits[i];var c=h.collider;
                if(c.attachedRigidbody==body||c.attachedRigidbody!=null&&!c.attachedRigidbody.isKinematic||c.GetComponent<SoftGroundRuts>()!=null
                    ||Physics.GetIgnoreLayerCollision(gameObject.layer,c.gameObject.layer)||Physics.GetIgnoreCollision(wheel,c)||Vector3.Dot(h.normal,up)<.15f)continue;
                if(Vector3.Dot(h.point-hub,up)>settings.contactSkinMetres)continue;
                if(!found||Vector3.Dot(h.point-surface.point,up)>0){surface=new SoftGroundSurfaceSample{point=h.point,normal=h.normal};found=true;}
            }
        }
        if(!found||degrees!=0&&Vector3.Dot(surface.point-hub,up)>settings.contactSkinMetres)return false;
        float alignment = Vector3.Dot(up, surface.normal);
        if (alignment < .15f) return false;
        height = Vector3.Dot(surface.point - probe, surface.normal) / alignment;
        return true;
    }

    // A planar slope does not need extra casts. Refine only where neighbouring
    // surface points fail to lie on each other's tangent planes (e.g. a kerb).
    static float PlaneGap(SoftGroundSurfaceSample a,SoftGroundSurfaceSample b,Vector3 up)
    {
        if(a.normal.sqrMagnitude<.5f||b.normal.sqrMagnitude<.5f)return 0;
        Vector3 delta=b.point-a.point;
        float first=Mathf.Abs(Vector3.Dot(delta,a.normal))/Mathf.Max(.15f,Vector3.Dot(up,a.normal));
        float second=Mathf.Abs(Vector3.Dot(delta,b.normal))/Mathf.Max(.15f,Vector3.Dot(up,b.normal));
        return Mathf.Max(first,second);
    }

    void Refine(Vector3 hub,Vector3 across,Vector3 forward,Vector3 up,float width,float f,
        float lowAngle,float highAngle,SoftGroundSurfaceSample low,SoftGroundSurfaceSample high,
        WheelRutContactSettings settings,ref float bestHeight,ref float bestFraction,
        ref float bestAngle,ref SoftGroundSurfaceSample bestSurface)
    {
        for(int step=0;step<Mathf.Clamp(settings.arcRefinementSteps,0,8);step++)
        {
            float middle=(lowAngle+highAngle)*.5f;
            bool usable=Sample(hub,across,forward,up,width,f,middle,settings,out float height,out var surface);
            if(usable&&height>bestHeight)
            {bestHeight=height;bestFraction=f;bestAngle=middle;bestSurface=surface;}
            if(surface.normal.sqrMagnitude<.5f)break;
            float left=PlaneGap(low,surface,up),right=PlaneGap(surface,high,up);
            if(Mathf.Max(left,right)<settings.arcRefinementHeightMetres)break;
            if(left>=right){highAngle=middle;high=surface;}
            else{lowAngle=middle;low=surface;}
        }
    }

    public Vector3 Prepare(WheelRutContactSettings settings, float width)
    {
        GetWorldPose(out var hub, out _);
        if (!isActiveAndEnabled || settings == null || !settings.enabled || !settings.shoulderSupport&&!settings.longitudinalSupport)
        { Restore(); return hub; }
        Vector3 up = transform.up;
        Vector3 across = Quaternion.AngleAxis(wheel.steerAngle, up) * transform.right;
        Vector3 forward=Quaternion.AngleAxis(wheel.steerAngle,up)*transform.forward;
        bool central = Sample(hub, across, forward, up, width, 0, 0, settings, out float centreHeight, out _);
        float bestHeight = central ? centreHeight : float.NegativeInfinity;
        float bestFraction = 0,bestAngle=0;
        SoftGroundSurfaceSample bestSurface = default;
        int count = settings.shoulderSupport?Mathf.Clamp(settings.widthSamples | 1, 3, 9):1;
        int arcs=settings.longitudinalSupport?Mathf.Clamp(settings.longitudinalSamples|1,3,33):1;
        for (int i = 0; i < count; i++)
        {
            float f = i==0?0:((i + 1) / 2) * 2f / (count - 1) * (i % 2 == 1 ? -1 : 1);
            float previousAngle=0;
            SoftGroundSurfaceSample previousSurface=default;
            for(int a=0;a<arcs;a++)
            {
                float degrees=arcs==1?0:(a-(arcs-1)*.5f)*2/(arcs-1)*settings.maximumLongitudinalArcDegrees;
                bool usable=Sample(hub,across,forward,up,width,f,degrees,settings,out float height,out var surface);
                if(usable&&height>bestHeight)
                {bestHeight=height;bestFraction=f;bestAngle=degrees;bestSurface=surface;}
                if(a>0&&settings.adaptiveLongitudinalSampling&&PlaneGap(previousSurface,surface,up)>settings.arcRefinementHeightMetres)
                    Refine(hub,across,forward,up,width,f,previousAngle,degrees,previousSurface,surface,settings,
                        ref bestHeight,ref bestFraction,ref bestAngle,ref bestSurface);
                previousAngle=degrees;previousSurface=surface;
            }
        }
        if ((ShoulderSupported||ArcSupported) && (!ShoulderSupported||settings.shoulderSupport) && (!ArcSupported||settings.longitudinalSupport)
            && Sample(hub, across, forward, up, width, fraction, angle, settings, out float previousHeight, out var previous)
            && previousHeight >= bestHeight - settings.supportSwitchHysteresisMetres)
        { bestHeight = previousHeight; bestFraction = fraction; bestAngle=angle; bestSurface = previous; }
        float reference = central ? centreHeight : 0;
        if (!central && wheel.GetGroundHit(out var hit))
            reference = Vector3.Dot(hit.point - (hub - up * wheel.radius), up);
        float gain = bestHeight - reference;
        if (Mathf.Abs(bestFraction) < .01f && Mathf.Abs(bestAngle)<.1f || gain < settings.minimumSupportGainMetres)
        { Restore(); return hub; }
        fraction = bestFraction;angle=bestAngle; HeightGainMetres = Mathf.Max(0, gain);
        float radians=angle*Mathf.Deg2Rad;
        offset = across * (fraction * width * .5f) + up * (settings.shoulderCrownDropMetres * fraction * fraction+wheel.radius*(1-Mathf.Cos(radians)))+forward*(wheel.radius*Mathf.Sin(radians));
        transform.localPosition=basePosition+(transform.parent==null?offset:transform.parent.InverseTransformVector(offset));
        if(bestSurface.ground!=null)bestSurface.ground.RequestWheelContactUpdate();
        return hub;
    }

    public Vector3 LongitudinalNormalReaction(float load,WheelRutContactSettings settings)
    {
        Vector3 forward=Quaternion.AngleAxis(wheel.steerAngle,transform.up)*transform.forward;
        return -forward*Mathf.Sign(angle)*Mathf.Min(settings.maximumLongitudinalReactionNewtons,Mathf.Max(0,load)*Mathf.Abs(Mathf.Tan(angle*Mathf.Deg2Rad)));
    }

    void OnDisable() { Restore(); }
}
