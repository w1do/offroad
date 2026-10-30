using UnityEngine;

[RequireComponent(typeof(VehiclePhysics))]
public sealed class VehicleWinch : MonoBehaviour
{
    public bool Pulling {get;private set;}
    public bool Broken {get;private set;}
    public float CableIntegrity {get;private set;}=1;
    public float ContinuousPullSeconds {get;private set;}
    public float TensionNewtons {get;private set;}
    public string Message {get;private set;}="2: зацепить лебёдку за дерево впереди";
    public Transform AnchorTransform=>anchor==null?null:anchor.transform;
    VehiclePhysics vehicle;Rigidbody body;Collider anchor;Vector3 localAnchor;float restLength;
    LineRenderer rope;Material ropeMaterial;
    VehicleEquipmentProfile Config=>vehicle!=null&&vehicle.profile!=null?vehicle.profile.equipment:null;
    public float Heat=>Config==null?0:Mathf.Clamp01(ContinuousPullSeconds/Mathf.Max(1,Config.maximumContinuousPullSeconds));
    public Vector3 Mount=>transform.TransformPoint(Config==null?Vector3.zero:Config.winchMount);
    void Awake(){vehicle=GetComponent<VehiclePhysics>();body=GetComponent<Rigidbody>();}
    public bool Toggle()
    {
        if(Pulling){Detach("Лебёдка отключена — трос остывает");return true;}
        var c=Config;if(c==null||!c.winchAvailable){Message="На машине нет лебёдки";return false;}
        if(Broken){Message="Трос оборван. Повторное включение его не восстанавливает";return false;}
        if(vehicle.SpeedKmh>c.maximumWinchAttachSpeedKmh){Message=$"Для зацепления лебёдки снизьте скорость до {c.maximumWinchAttachSpeedKmh:F0} км/ч";return false;}
        RaycastHit best=default;bool found=false;float score=float.MaxValue;
        for(int height=0;height<3;height++)for(float angle=-c.anchorSearchDegrees;angle<=c.anchorSearchDegrees;angle+=3){Vector3 start=Mount+Vector3.up*(height*.45f);Vector3 direction=Quaternion.AngleAxis(angle,Vector3.up)*Vector3.ProjectOnPlane(transform.forward,Vector3.up).normalized;
            var hits=Physics.RaycastAll(start,direction,c.maximumCableMetres,~0,QueryTriggerInteraction.Ignore);
            foreach(var h in hits){if(h.collider.transform.IsChildOf(transform)||!h.collider.TryGetComponent<WinchAnchorSurface>(out _))continue;float value=h.distance+Mathf.Abs(angle)*.12f;if(value<score&&ClearLine(h.collider,h.point)){best=h;score=value;found=true;}}
        }
        if(!found){Message="Нет доступного дерева впереди в пределах длины троса";return false;}
        return Attach(best.collider,best.point);
    }
    public bool Attach(Collider target,Vector3 point)
    {
        var c=Config;if(c==null||!c.winchAvailable||Broken||target==null||!target.TryGetComponent<WinchAnchorSurface>(out _)||target.attachedRigidbody!=null){Message="Недопустимая опора лебёдки";return false;}
        float distance=Vector3.Distance(Mount,point);if(!target.Raycast(new Ray(Mount,(point-Mount).normalized),out var targetHit,distance+.2f)||Mathf.Abs(targetHit.distance-distance)>.35f||distance<c.minimumAnchorMetres||distance>c.maximumCableMetres||!ClearLine(target,point)){Message="Точка зацепления недоступна или трос перекрыт";return false;}
        anchor=target;localAnchor=target.transform.InverseTransformPoint(point);restLength=distance;Pulling=true;Message="Лебёдка тянет. 2: отпустить; следите за нагрузкой троса";return true;
    }
    bool ClearLine(Collider target,Vector3 point)
    {
        Vector3 delta=point-Mount;float distance=delta.magnitude;
        foreach(var h in Physics.RaycastAll(Mount,delta.normalized,Mathf.Max(0,distance-.15f),~0,QueryTriggerInteraction.Ignore))
            if(!h.collider.transform.IsChildOf(transform)&&(h.collider!=target||h.distance<distance-.35f))return false;
        return true;
    }
    public void Simulate(float dt)
    {
        if(dt<=0||Config==null)return;var c=Config;TensionNewtons=0;
        if(!Pulling){ContinuousPullSeconds=Mathf.Max(0,ContinuousPullSeconds-c.winchCooldownPerSecond*dt);return;}
        if(anchor==null){Detach("Опора недоступна — лебёдка отключена");return;}
        Vector3 target=anchor.transform.TransformPoint(localAnchor),delta=target-Mount;float distance=delta.magnitude;
        if(distance>c.maximumCableMetres*1.05f){BreakCable("Трос оборван: превышена длина");return;}
        if(distance<c.minimumAnchorMetres){Detach("Машина подтянута к опоре");return;}
        if(!ClearLine(anchor,target)){Detach("Трос упёрся в препятствие — тяга остановлена");return;}
        restLength=Mathf.Max(c.minimumAnchorMetres,restLength-c.reelMetresPerSecond*dt);
        float awaySpeed=Vector3.Dot(body.GetPointVelocity(Mount),-delta.normalized);
        TensionNewtons=Mathf.Clamp((distance-restLength)*c.cableSpringNewtonsPerMetre+awaySpeed*c.cableDamperNsPerMetre,0,c.maximumPullNewtons);
        float load=TensionNewtons/Mathf.Max(1,c.maximumPullNewtons);AdvanceWear(dt,load);
        if(Broken)return;
        body.AddForceAtPosition(delta.normalized*TensionNewtons,Mount,ForceMode.Force);
    }
    public void AdvanceWear(float dt,float load)
    {
        if(!Pulling||Broken||Config==null||dt<=0)return;load=Mathf.Clamp01(load);
        ContinuousPullSeconds=load>.15f?ContinuousPullSeconds+dt:Mathf.Max(0,ContinuousPullSeconds-Config.winchCooldownPerSecond*dt);
        CableIntegrity=Mathf.Clamp01(CableIntegrity-Config.cableWearPerSecondAtFullLoad*load*load*dt);
        if(CableIntegrity<=0||ContinuousPullSeconds>=Config.maximumContinuousPullSeconds)BreakCable("Трос оборван от длительной нагрузки");
    }
    void BreakCable(string reason){Broken=true;CableIntegrity=0;Detach(reason);}
    public void Detach(string reason){Pulling=false;anchor=null;TensionNewtons=0;Message=reason;if(rope!=null)rope.enabled=false;}
    void OnDisable(){Detach("Лебёдка отключена");}
    void LateUpdate()
    {
        if(!Pulling||anchor==null){if(rope!=null)rope.enabled=false;return;}
        if(rope==null){var root=new GameObject("Winch cable");root.transform.SetParent(transform,false);rope=root.AddComponent<LineRenderer>();ropeMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));ropeMaterial.SetColor("_BaseColor",new Color(.18f,.16f,.12f));rope.sharedMaterial=ropeMaterial;rope.useWorldSpace=true;rope.positionCount=2;rope.startWidth=.018f;rope.endWidth=.018f;}
        rope.enabled=true;rope.SetPosition(0,Mount);rope.SetPosition(1,anchor.transform.TransformPoint(localAnchor));
    }
    void OnDestroy(){if(ropeMaterial!=null)Destroy(ropeMaterial);}
}
