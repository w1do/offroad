using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[Serializable]
public sealed class VehicleAxleBinding
{
    public WheelCollider left,right;
    public Transform leftVisual,rightVisual;
}
[Serializable]
public struct VehicleControlInput
{
    [Range(0,1)]public float throttle,brake;
    [Range(-1,1)]public float steering;
    public bool handbrake,precision;
}

// No brand/model geometry, fixed wheel count, keyboard input or forced vehicle motion lives here.
[MovedFrom(true,"","Assembly-CSharp","PajeroDrivetrain")]
[RequireComponent(typeof(Rigidbody))]
public sealed class VehiclePhysics : MonoBehaviour
{
    public VehiclePhysicsProfile profile;
    public VehicleAxleBinding[] axles;
    public bool automaticSimulation=true;
    public bool externalControl;
    public bool FourWheelDrive {get;private set;}
    public string DriveMessage {get;private set;}="H: полный привод";
    public float DriveShare(int axle)=>profile.equipment==null||!profile.equipment.switchableDrive||FourWheelDrive||profile.axles[axle].primaryDrive?profile.axles[axle].driveShare:0;
    public bool SetFourWheelDrive(bool value)
    {
        if(profile.equipment==null||!profile.equipment.switchableDrive){DriveMessage="Переключение привода недоступно";return false;}
        if(SpeedKmh>profile.equipment.maximumDriveSwitchSpeedKmh){DriveMessage="Снизьте скорость для переключения привода";return false;}
        FourWheelDrive=value;clutchLocked=false;DriveMessage=value?"Полный привод включён":"Полный привод выключен — задний привод";return true;
    }
    public bool DifferentialLocked {get;private set;}
    public string DifferentialMessage {get;private set;}="J: блокировка дифференциалов";
    public bool SetDifferentialLock(bool value)
    {
        var e=profile==null?null:profile.equipment;
        if(e==null||!e.differentialLockAvailable){DifferentialMessage="Блокировка недоступна";return false;}
        if(SpeedKmh>e.maximumDifferentialSwitchSpeedKmh){DifferentialMessage="Снизьте скорость для блокировки";return false;}
        DifferentialLocked=value;DifferentialMessage=value?"Дифференциалы заблокированы":"Блокировка снята";return true;
    }
    public bool ReverseGear { get; private set; }
    public bool LowRange { get; private set; }
    public int Gear { get; private set; }=1;
    public bool Neutral { get; private set; }
    public float Pedal { get; private set; }
    public float EngineRpm { get; private set; }
    public float EngineLoad { get; private set; }
    public float SlipAngle { get; private set; }
    public float DrivenSlip { get; private set; }
    public float WheelspinSpeed { get; private set; }
    public int MudContacts { get; private set; }
    public int IceContacts { get; private set; }
    public int RutWallContacts {get;private set;}
    public float RutWallForceNewtons {get;private set;}
    public float MaximumSoilContactErrorMetres {get;private set;}
    bool[] rutWallContact;
    WheelContactEnvelope[] contactEnvelopes;
    public int ShoulderSupports {get;private set;}
    public int ArcSupports {get;private set;}
    public float ArcNormalReactionNewtons {get;private set;}
    public float MaximumShoulderHeightGainMetres {get;private set;}
    public bool WheelAgainstRutWall(int wheel)=>rutWallContact!=null&&wheel>=0&&wheel<rutWallContact.Length&&rutWallContact[wheel];
    public bool BrakeApplied { get; private set; }
    public bool ServiceBrakeApplied=>Input.brake>.01f;
    public bool ParkingBrakeApplied=>Input.handbrake;
    public bool Sliding=>Mathf.Abs(SlipAngle)>7&&body!=null&&body.linearVelocity.magnitude>2;
    public float SpeedKmh=>body==null?0:body.linearVelocity.magnitude*3.6f;
    public float LateralSpeed=>body==null?0:Vector3.Dot(body.linearVelocity,transform.right);
    public VehicleControlInput Input { get; private set; }
    public WheelCollider[] Wheels { get; private set; }=Array.Empty<WheelCollider>();
    public bool Ready { get; private set; }
    public bool precisionGas;
    VehicleWaterInteraction water;
    Rigidbody body;TruckChains chains;VehicleWheelEffects effects;VehicleWinch winch;VehicleTyrePressure tyres;
    WheelHit[] hits;bool[] grounded,chainedWheel;float[] longGrip,lateralGrip;
    float engineOmega,shiftRemaining,totalDriveShare;
    float[] axleOmega,axleTransfer,treadTravel;
    bool clutchLocked;
    Vector3 recoveryPosition;Quaternion recoveryRotation;
    public void SetInput(VehicleControlInput value){value.throttle=Mathf.Clamp01(value.throttle);value.brake=Mathf.Clamp01(value.brake);value.steering=Mathf.Clamp(value.steering,-1,1);Input=value;}
    public void SetTransmission(bool reverse,bool low){ReverseGear=reverse;LowRange=low;}
    public void Shift(int direction){if(!Ready)return;Gear=Mathf.Clamp(Gear+direction,1,profile.gears.Length);Neutral=false;clutchLocked=false;shiftRemaining=profile.shiftSeconds;}
    public void SetNeutral(bool value){Neutral=value;clutchLocked=false;if(profile!=null)shiftRemaining=profile.shiftSeconds;}
    void OnEnable(){Initialize();}
    public void Initialize()
    {
        Ready=false;water=GetComponent<VehicleWaterInteraction>();body=GetComponent<Rigidbody>();chains=GetComponent<TruckChains>();effects=GetComponent<VehicleWheelEffects>();winch=GetComponent<VehicleWinch>();tyres=GetComponent<VehicleTyrePressure>();
        if(profile==null||axles==null)return;string error=profile.ValidationError(axles.Length);if(error!=null){Debug.LogError(name+": "+error,this);return;}
        DifferentialLocked=profile.equipment!=null&&profile.equipment.defaultDifferentialLocked;FourWheelDrive=profile.equipment!=null&&profile.equipment.defaultFourWheelDrive;DriveMessage=FourWheelDrive?"Полный привод включён":"Полный привод выключен — основной привод";
        Wheels=new WheelCollider[axles.Length*2];totalDriveShare=0;
        contactEnvelopes=new WheelContactEnvelope[Wheels.Length];axleOmega=new float[axles.Length];axleTransfer=new float[axles.Length];
        for(int i=0;i<axles.Length;i++)
        {
            if(axles[i]==null||axles[i].left==null||axles[i].right==null){Debug.LogError(name+": missing wheel collider on axle "+i,this);return;}
            Wheels[i*2]=axles[i].left;Wheels[i*2+1]=axles[i].right;var setting=profile.axles[i];totalDriveShare+=setting.driveShare;
            for(int side=0;side<2;side++){var wheel=Wheels[i*2+side];if(Application.isPlaying){var envelope=wheel.GetComponent<WheelContactEnvelope>();if(envelope==null)envelope=wheel.gameObject.AddComponent<WheelContactEnvelope>();envelope.Bind(wheel);contactEnvelopes[i*2+side]=envelope;}wheel.radius=setting.tire==null?setting.wheelRadius:setting.tire.radiusMetres;wheel.mass=setting.tire==null?setting.wheelMass:setting.tire.massKg;wheel.wheelDampingRate=setting.wheelDampingRate;wheel.suspensionDistance=setting.suspensionTravel;wheel.forceAppPointDistance=setting.forceApplicationHeight;var spring=wheel.suspensionSpring;spring.spring=setting.springNPerMetre;spring.damper=setting.damperNsPerMetre;spring.targetPosition=setting.springTarget;wheel.suspensionSpring=spring;wheel.ConfigureVehicleSubsteps(10,12,12);}
        }
        body.mass=profile.massKg;body.centerOfMass=profile.centreOfMass;body.angularDamping=profile.angularDamping;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;body.solverIterations=12;body.solverVelocityIterations=6;
        rutWallContact=new bool[Wheels.Length];treadTravel=new float[Wheels.Length];hits=new WheelHit[Wheels.Length];grounded=new bool[Wheels.Length];chainedWheel=new bool[Wheels.Length];longGrip=new float[Wheels.Length];lateralGrip=new float[Wheels.Length];
        recoveryPosition=body.position;recoveryRotation=body.rotation;Ready=true;ResetState();
    }
    public void ResetState(){if(profile==null)return;Pedal=0;EngineLoad=0;EngineRpm=profile.idleRpm;engineOmega=EngineRpm*Mathf.PI/30;Gear=1;Neutral=false;shiftRemaining=0;clutchLocked=false;}
    public void SetRecoveryPoint(Vector3 p,Quaternion q){recoveryPosition=p;recoveryRotation=q;}
    public void ShiftOrigin(Vector3 delta){recoveryPosition-=delta;}
    public void DebugRecover(){if(body==null)return;if(winch!=null)winch.Detach("Отладочное восстановление");if(water!=null)water.ResetAfterRecovery();body.position=recoveryPosition;body.rotation=recoveryRotation;body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;ResetState();}
    void FixedUpdate(){if(automaticSimulation)Simulate(Time.fixedDeltaTime);}
    void OnDisable(){if(contactEnvelopes!=null)foreach(var envelope in contactEnvelopes)if(envelope!=null)envelope.Restore();foreach(var wheel in Wheels)if(wheel!=null){wheel.motorTorque=0;wheel.brakeTorque=0;}}
    void LateUpdate(){if(!Ready)return;for(int i=0;i<axles.Length;i++){Pose(i*2,axles[i].leftVisual);Pose(i*2+1,axles[i].rightVisual);}}
    void Pose(int wheel,Transform visual){if(visual==null)return;GetWheelPose(wheel,out var p,out var q);visual.SetPositionAndRotation(p,q);}
    public void GetWheelPose(int wheel,out Vector3 position,out Quaternion rotation)
    {
        if(contactEnvelopes!=null&&wheel<contactEnvelopes.Length&&contactEnvelopes[wheel]!=null)contactEnvelopes[wheel].GetWorldPose(out position,out rotation);
        else Wheels[wheel].GetWorldPose(out position,out rotation);
    }
    public bool WheelShoulderSupported(int wheel)=>contactEnvelopes!=null&&wheel>=0&&wheel<contactEnvelopes.Length&&contactEnvelopes[wheel]!=null&&contactEnvelopes[wheel].ShoulderSupported;
    // Compatibility for tools/older input adapters; new cars only require SetInput + normal FixedUpdate.
    public void Step(float throttle,float steering,bool brake,bool handbrake){SetInput(new VehicleControlInput{throttle=throttle,steering=steering,brake=brake?1:0,handbrake=handbrake,precision=precisionGas});Simulate(Time.fixedDeltaTime);}
    public void Simulate(float dt)
    {
        if(!Ready||dt<=0)return;if(water!=null)water.Simulate(dt);if(tyres!=null)tyres.Advance(dt);var input=Input;if(water!=null&&water.IntakeFlooded)input.throttle=0;float target=Mathf.Clamp01(input.throttle)*(input.precision?profile.precisionPedal:1);
        Pedal=Mathf.MoveTowards(Pedal,target,(target>Pedal?profile.pedalRisePerSecond:profile.pedalReleasePerSecond)*dt);BrakeApplied=input.brake>.01f||input.handbrake;shiftRemaining=Mathf.Max(0,shiftRemaining-dt);
        float speed=Vector3.Dot(body.linearVelocity,transform.forward);SlipAngle=Mathf.Atan2(LateralSpeed,Mathf.Max(1,Mathf.Abs(speed)))*Mathf.Rad2Deg;
        float ratio=(ReverseGear?profile.reverseRatio:profile.gears[Mathf.Clamp(Gear-1,0,profile.gears.Length-1)])*profile.finalDrive*(LowRange?profile.lowRatio:1);
        totalDriveShare=0;for(int a=0;a<axles.Length;a++)totalDriveShare+=DriveShare(a);totalDriveShare=Mathf.Max(.001f,totalDriveShare);
        float chainSlipSum=0;int chainContacts=0;
        ShoulderSupports=0;ArcSupports=0;ArcNormalReactionNewtons=0;MaximumShoulderHeightGainMetres=0;RutWallContacts=0;RutWallForceNewtons=0;MaximumSoilContactErrorMetres=0;
        float omega=0;int contacts=0;DrivenSlip=0;WheelspinSpeed=0;MudContacts=0;IceContacts=0;float resistance=profile.rollingResistance;
        for(int i=0;i<Wheels.Length;i++)
        {
            var wheel=Wheels[i];var axle=profile.axles[i/2];GetWheelPose(i,out var hub,out _);if(contactEnvelopes!=null&&contactEnvelopes[i]!=null){hub=contactEnvelopes[i].Prepare(profile.rutContact,axle.tire==null?axle.tyreWidthMetres:axle.tire.widthMetres);if(contactEnvelopes[i].ShoulderSupported){ShoulderSupports++;MaximumShoulderHeightGainMetres=Mathf.Max(MaximumShoulderHeightGainMetres,contactEnvelopes[i].HeightGainMetres);}}rutWallContact[i]=ApplyRutWallContacts(wheel,axle,hub);grounded[i]=wheel.GetGroundHit(out hits[i]);chainedWheel[i]=false;float driveShare=DriveShare(i/2);float share=driveShare/totalDriveShare*.5f;omega+=wheel.rpm*Mathf.PI/30*share;longGrip[i]=profile.dirtLongitudinal;lateralGrip[i]=profile.dirtLateral;
            treadTravel[i]+=wheel.rpm*Mathf.PI/30*wheel.radius*dt;if(!grounded[i])continue;if(contactEnvelopes[i]!=null&&contactEnvelopes[i].ArcSupported){var reaction=contactEnvelopes[i].LongitudinalNormalReaction(hits[i].force,profile.rutContact);body.AddForceAtPosition(reaction,hits[i].point+wheel.transform.up*wheel.forceAppPointDistance);ArcSupports++;ArcNormalReactionNewtons+=reaction.magnitude;}contacts++;Vector3 contactForward=hits[i].forwardDir;float contactSpeed=Vector3.Dot(body.GetPointVelocity(hits[i].point),contactForward);float signedSpin=wheel.rpm*Mathf.PI/30*wheel.radius-contactSpeed;float spin=Mathf.Abs(signedSpin),slip=spin/Mathf.Max(2,Mathf.Abs(contactSpeed));
            if(driveShare>0){DrivenSlip=Mathf.Max(DrivenSlip,slip);WheelspinSpeed=Mathf.Max(WheelspinSpeed,spin);}
            if(hits[i].collider.TryGetComponent<ForestGroundPatch>(out var patch)&&patch.CoverAt(hits[i].point)==1){longGrip[i]=profile.grassLongitudinal;lateralGrip[i]=profile.grassLateral;resistance+=.024f/Wheels.Length;}
            float bareLongitudinal=longGrip[i],bareLateral=lateralGrip[i];
            bool throwsSoil=true,softSoil=false;
            if((axle.chains||profile.equipment!=null&&profile.equipment.chainsOnAllWheels)&&chains!=null&&chains.Installed&&!chains.Broken&&profile.baseSoilChainsImproveGrip){chainedWheel[i]=true;longGrip[i]=profile.baseSoilChainedLongitudinal;lateralGrip[i]=profile.baseSoilChainedLateral;}
            if(hits[i].collider.TryGetComponent<ForestDrivingSurface>(out var surface))
            {
                bareLongitudinal=surface.forwardGrip;bareLateral=surface.lateralGrip;
                softSoil=surface.cover==ForestDrivingSurface.Cover.Mud;throwsSoil=softSoil||surface.cover==ForestDrivingSurface.Cover.Gravel;
                bool chained=(axle.chains||profile.equipment!=null&&profile.equipment.chainsOnAllWheels)&&chains!=null&&chains.Installed&&!chains.Broken&&surface.chainsImproveGrip;chainedWheel[i]=chained;longGrip[i]=chained?surface.chainedForwardGrip:surface.forwardGrip;lateralGrip[i]=chained?surface.chainedLateralGrip:surface.lateralGrip;resistance+=surface.rollingResistance/Wheels.Length;
                if(surface.cover==ForestDrivingSurface.Cover.Mud){MudContacts++;
                    if(hits[i].collider.TryGetComponent<MudRoadSection>(out var mudSection)&&mudSection.profile!=null){float grip=mudSection.GripAt(hits[i].point);longGrip[i]*=grip;lateralGrip[i]*=Mathf.Lerp(1,grip,.65f);var soil=hits[i].collider.GetComponent<SoftGroundRuts>();if(soil==null||soil.profile==null||soil.profile.soil==null)ApplySoilResistance(hits[i],mudSection.ResistanceAt(hits[i].point,soil==null?0:soil.DepthAt(hits[i].point)),dt);}
                    if(tyres!=null){longGrip[i]*=tyres.SoftGripMultiplier;lateralGrip[i]*=Mathf.Lerp(1,.94f,tyres.Deflation);}
                }else if(surface.cover==ForestDrivingSurface.Cover.Ice)IceContacts++;
            }
            if(chainedWheel[i]){longGrip[i]=Mathf.Lerp(bareLongitudinal,longGrip[i],chains.GripFactor);lateralGrip[i]=Mathf.Lerp(bareLateral,lateralGrip[i],chains.GripFactor);}
            if(chains!=null&&chains.Installed&&!chains.Broken&&(axle.chains||profile.equipment!=null&&profile.equipment.chainsOnAllWheels)){chainSlipSum+=spin;chainContacts++;}
            if(hits[i].collider.GetComponent<SoftGroundRuts>()!=null)SoftGroundRuts.Contact(hits[i].point,contactForward,axle.tire==null?axle.tyreWidthMetres:axle.tire.widthMetres,hits[i].force,spin,tyres==null?1:tyres.SoilLoadMultiplier,dt,axle.tire==null?0:axle.tire.ContactAreaSquareMetres(tyres==null?1:tyres.SoilLoadMultiplier),gameObject.scene);
            if(axle.tire!=null){longGrip[i]*=axle.tire.longitudinalMultiplier*(softSoil?axle.tire.softGroundMultiplier:1);lateralGrip[i]*=axle.tire.lateralMultiplier;resistance+=(axle.tire.rollingResistance-profile.rollingResistance)/Wheels.Length;}
            var deformable=hits[i].collider.GetComponent<SoftGroundRuts>();var soilModel=deformable==null||deformable.profile==null?null:deformable.profile.soil;
            if(deformable!=null&&Mathf.Max(Mathf.Abs(contactSpeed),spin)>.05f)SoftGroundRuts.RecordTread(hits[i].point,contactForward,axle.tire==null?axle.tyreWidthMetres:axle.tire.widthMetres,.22f,treadTravel[i],axle.tire==null?.16f:axle.tire.treadRepeatMetres,soilModel==null?.5f:soilModel.moisture,gameObject.scene);
            if(soilModel!=null)
            {
                longGrip[i]*=soilModel.longitudinalGrip/.18f;lateralGrip[i]*=soilModel.lateralGrip/.38f;
                float depth=deformable.DepthAt(hits[i].point);
                float soilResistance=soilModel.rollingResistance*Mathf.Clamp01(depth/Mathf.Max(.01f,soilModel.rutDepthLimitMetres));
                // A pre-existing deep bed displaces soil even before fresh wheel ruts form.
                // Select one resistance envelope, rather than stacking two soil forces.
                if(hits[i].collider.TryGetComponent<MudRoadSection>(out var fullWidth)&&fullWidth.profile!=null&&fullWidth.profile.fullWidthFurrows)
                    soilResistance=Mathf.Max(soilResistance,fullWidth.ResistanceAt(hits[i].point,depth));
                ApplySoilResistance(hits[i],soilResistance,dt);
            }
            float combined=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.15f,1.4f,slip))*Pedal;
            lateralGrip[i]*=axle.lateralMultiplier*Mathf.Lerp(1,driveShare>0?axle.wheelspinLateralMultiplier:profile.undrivenLateralMultiplier,combined);
            if(effects!=null&&driveShare>0&&throwsSoil)effects.Spray(i,hits[i].point,hits[i].normal,contactForward,signedSpin,Pedal,dt,softSoil);
        }
        float sign=ReverseGear?-1:1,connected=omega*ratio*sign;
        float launch=Mathf.SmoothStep(0,1,Mathf.InverseLerp(profile.idleRpm+80,profile.clutchFullRpm,EngineRpm)),engagement=Mathf.Abs(speed)>profile.clutchEngageSpeed?1:launch;
        if(Neutral||shiftRemaining>0||input.brake>.1f&&Mathf.Abs(speed)<1)engagement=0;
        float clutch=Mathf.Clamp((engineOmega-connected)*profile.clutchCoupling,-profile.clutchCapacityNm*engagement,profile.clutchCapacityNm*engagement);
        float friction=profile.engineFrictionNm+engineOmega*profile.engineViscousFriction;
        // Torque curve is NET engine output; compensate internal loss only while firing.
        float combustion=(profile.TorqueAt(EngineRpm)+friction)*Pedal;if(EngineRpm>profile.redlineRpm)combustion=0;
        if(water!=null&&water.IntakeFlooded)combustion=0;
        float governor=Mathf.Clamp((profile.idleRpm-EngineRpm)*profile.idleGovernorNmPerRpm,0,100);
        // Once static clutch friction can hold the load, RPM follows the connected
        // driveline. A permanently viscous clutch would lose rated power at speed.
        if(Neutral||shiftRemaining>0||connected<profile.idleRpm*.85f*Mathf.PI/30||connected>profile.redlineRpm*1.1f*Mathf.PI/30)clutchLocked=false;
        else if(engagement>.99f&&connected>profile.idleRpm*1.1f*Mathf.PI/30&&profile.clutchCapacityNm>=profile.TorqueAt(EngineRpm))clutchLocked=true;
        if(clutchLocked){engineOmega=connected;EngineRpm=engineOmega*30/Mathf.PI;friction=profile.engineFrictionNm+engineOmega*profile.engineViscousFriction;float net=(water!=null&&water.IntakeFlooded?0:Pedal)*(profile.TorqueAt(EngineRpm)+friction)-friction;if(EngineRpm>profile.redlineRpm)net=-friction;clutch=Mathf.Clamp(net,-profile.clutchCapacityNm,profile.clutchCapacityNm);}
        else engineOmega=Mathf.Clamp(engineOmega+(combustion+governor-friction-clutch)/profile.engineInertia*dt,profile.idleRpm*.65f*Mathf.PI/30,profile.redlineRpm*1.10f*Mathf.PI/30);
        EngineRpm=engineOmega*30/Mathf.PI;
        EngineLoad=Mathf.Clamp01(Mathf.Max(0,clutch)/Mathf.Max(1,profile.TorqueAt(EngineRpm)));
        float totalTorque=Neutral||shiftRemaining>0?0:clutch*ratio*profile.efficiency*sign;
        if(Mathf.Abs(speed)*3.6f>(ReverseGear?profile.reverseSpeedKmh:profile.maximumSpeedKmh)&&Mathf.Sign(totalTorque)==Mathf.Sign(speed))totalTorque=0;
        float drivenMean=0;int drivenAxles=0;
        for(int a=0;a<axles.Length;a++){axleOmega[a]=(Wheels[a*2].rpm+Wheels[a*2+1].rpm)*Mathf.PI/60;axleTransfer[a]=0;if(DriveShare(a)>0){drivenMean+=axleOmega[a];drivenAxles++;}}
        if(drivenAxles>1&&profile.centreLockTorqueNm>0){drivenMean/=drivenAxles;float max=0;for(int a=0;a<axles.Length;a++)if(DriveShare(a)>0){axleTransfer[a]=(drivenMean-axleOmega[a])*profile.centreCoupling;max=Mathf.Max(max,Mathf.Abs(axleTransfer[a]));}float scale=Mathf.Min(1,profile.centreLockTorqueNm/Mathf.Max(1,max));for(int a=0;a<axles.Length;a++)axleTransfer[a]*=scale;}
        bool staticHold=BrakeApplied&&Mathf.Abs(speed)<profile.staticBrakeSpeed&&contacts>=2;
        float holdCapacity=0,holdLoad=0;Vector3 holdNormal=Vector3.zero;
        if(staticHold)for(int i=0;i<Wheels.Length;i++)if(grounded[i]&&(input.brake>.01f||input.handbrake&&profile.axles[i/2].handbrake)){float availableBrake=input.brake*profile.serviceBrakeNm+(input.handbrake&&profile.axles[i/2].handbrake?profile.handBrakeNm:0);float load=Mathf.Max(0,hits[i].force);holdCapacity+=Mathf.Min(load*longGrip[i],availableBrake/Wheels[i].radius);holdLoad+=load;holdNormal+=hits[i].normal*load;}
        for(int i=0;i<Wheels.Length;i++)
        {
            var wheel=Wheels[i];var axle=profile.axles[i/2];float differential=0;float lockTorque=DifferentialLocked?Mathf.Max(axle.limitedSlipTorqueNm,profile.equipment.lockedAxleTorqueNm):axle.limitedSlipTorqueNm;float coupling=DifferentialLocked?profile.equipment.lockedAxleCoupling:axle.limitedSlipCoupling;if(DriveShare(i/2)>0&&lockTorque>0){float difference=(Wheels[i/2*2].rpm-Wheels[i/2*2+1].rpm)*Mathf.PI/30;differential=Mathf.Clamp(difference*coupling,-lockTorque,lockTorque)*(i%2==0?-1:1);}
            wheel.motorTorque=input.brake>.01f?0:totalTorque*DriveShare(i/2)/totalDriveShare*.5f+axleTransfer[i/2]*.5f+differential;
            wheel.brakeTorque=Mathf.Clamp01(input.brake)*profile.serviceBrakeNm+(input.handbrake&&axle.handbrake?profile.handBrakeNm:0);
            float steeringLimit=Mathf.Lerp(profile.steeringDegrees,profile.highSpeedSteeringDegrees,Mathf.InverseLerp(15,85,SpeedKmh));wheel.steerAngle=Mathf.MoveTowards(wheel.steerAngle,axle.steering?input.steering*steeringLimit*axle.steeringMultiplier:0,profile.steeringRate*dt);
            var forward=wheel.forwardFriction;forward.extremumSlip=profile.forwardPeakSlip;forward.extremumValue=1;forward.asymptoteSlip=profile.forwardSlidingSlip;forward.asymptoteValue=chainedWheel[i]?profile.chainsSlidingGrip:profile.forwardSlidingGrip;forward.stiffness=staticHold&&(input.brake>.01f||input.handbrake&&axle.handbrake)?0:longGrip[i];wheel.forwardFriction=forward;
            var side=wheel.sidewaysFriction;side.extremumSlip=profile.lateralPeakSlip;side.extremumValue=1;side.asymptoteSlip=profile.lateralSlidingSlip;side.asymptoteValue=profile.lateralSlidingGrip;side.stiffness=rutWallContact[i]?0:Mathf.MoveTowards(side.stiffness,lateralGrip[i]*(input.handbrake&&axle.handbrake&&SpeedKmh>5?.38f:1),profile.gripResponse*dt);wheel.sidewaysFriction=side;
        }
        if(!automaticSimulation)SoftGroundRuts.AdvanceAll(dt);
        if(tyres!=null)resistance+=tyres.RollingResistance;
        if(chains!=null)chains.AdvanceCondition(dt,chainContacts>0?chainSlipSum/chainContacts:0);
        if(winch!=null)winch.Simulate(dt);
        for(int i=0;i<axles.Length;i++)AntiRoll(i*2,i*2+1,profile.axles[i].antiRollN);
        if(staticHold&&holdLoad>0){Vector3 tangent=Vector3.ProjectOnPlane(transform.forward,holdNormal.normalized).normalized;float required=-Vector3.Dot(Physics.gravity*body.mass,tangent)-Vector3.Dot(body.linearVelocity,tangent)*body.mass/dt;float force=Mathf.Clamp(required,-holdCapacity,holdCapacity);for(int i=0;i<Wheels.Length;i++)if(grounded[i]&&(input.brake>.01f||input.handbrake&&profile.axles[i/2].handbrake))body.AddForceAtPosition(tangent*force*Mathf.Max(0,hits[i].force)/holdLoad,hits[i].point);}
        if(contacts>0){Vector3 planar=Vector3.ProjectOnPlane(body.linearVelocity,transform.up);float force=Mathf.Min(body.mass*Physics.gravity.magnitude*resistance,body.mass*planar.magnitude/dt);body.AddForce(-planar.normalized*force);body.AddForce(-body.linearVelocity*body.linearVelocity.magnitude*profile.aerodynamicDrag);}
    }
    bool ApplyRutWallContacts(WheelCollider wheel,VehicleAxleSettings axle,Vector3 centre)
    {
        if(SoftGroundRuts.TrySampleSurface(centre,gameObject.scene,out var sample)){MaximumSoilContactErrorMetres=Mathf.Max(MaximumSoilContactErrorMetres,Mathf.Abs(sample.ColliderHeightErrorMetres));sample.ground.RequestWheelContactUpdate();}
        var settings=profile.rutContact;if(settings==null||!settings.enabled)return false;
        Quaternion steering=Quaternion.AngleAxis(wheel.steerAngle,wheel.transform.up);
        Vector3 across=steering*wheel.transform.right,forward=steering*wheel.transform.forward;
        float width=axle.tire==null?axle.tyreWidthMetres:axle.tire.widthMetres;
        float stiffness=tyres==null?1:Mathf.Lerp(1,settings.deflatedStiffnessMultiplier,tyres.Deflation);
        var left=WheelRutContact.Evaluate(centre,across,forward,wheel.transform.up,wheel.radius,width,-1,gameObject.scene,body,settings,stiffness);
        var right=WheelRutContact.Evaluate(centre,across,forward,wheel.transform.up,wheel.radius,width,1,gameObject.scene,body,settings,stiffness);
        float sum=left.force.magnitude+right.force.magnitude;
        float scale=Mathf.Min(1,settings.maximumForcePerWheelNewtons/Mathf.Max(.001f,sum));
        if(left.Active){body.AddForceAtPosition(left.force*scale,left.point);RutWallContacts++;}
        if(right.Active){body.AddForceAtPosition(right.force*scale,right.point);RutWallContacts++;}
        RutWallForceNewtons+=sum*scale;return left.Active||right.Active;
    }
    // Soil displacement resistance belongs to each contact, not the chassis centre.
    // Unequal sinkage therefore creates a physical yaw moment without scripted steering.
    void ApplySoilResistance(WheelHit hit,float coefficient,float dt)
    {
        Vector3 velocity=Vector3.ProjectOnPlane(body.GetPointVelocity(hit.point),hit.normal);
        body.AddForceAtPosition(SoilResistanceForce(velocity,hit.force,coefficient,body.mass,Wheels.Length,Physics.gravity.magnitude,dt),hit.point);
    }
    public static Vector3 SoilResistanceForce(Vector3 planarVelocity,float normalLoadNewtons,float coefficient,float massKg,int wheelCount,float gravity,float dt)
    {
        float speed=planarVelocity.magnitude;if(speed<.001f||coefficient<=0||dt<=0)return Vector3.zero;
        float normalLoad=Mathf.Max(0,normalLoadNewtons);
        float supportedMass=Mathf.Min(Mathf.Max(0,massKg)/Mathf.Max(1,wheelCount),normalLoad/Mathf.Max(.1f,gravity));
        float force=Mathf.Min(normalLoad*coefficient,supportedMass*speed/dt);
        return -planarVelocity/speed*force;
    }
    void AntiRoll(int left,int right,float stiffness)
    {
        float a=1,b=1;var wl=Wheels[left];var wr=Wheels[right];
        if(grounded[left]){wl.GetWorldPose(out var hub,out _);a=Mathf.Clamp01(Vector3.Dot(wl.transform.TransformPoint(wl.center)-hub,wl.transform.up)/wl.suspensionDistance);}
        if(grounded[right]){wr.GetWorldPose(out var hub,out _);b=Mathf.Clamp01(Vector3.Dot(wr.transform.TransformPoint(wr.center)-hub,wr.transform.up)/wr.suspensionDistance);}
        float force=(a-b)*stiffness;
        // An anti-roll bar redistributes load. Its chassis reactions must remain
        // a zero-sum couple even when one wheel has no ground contact.
        body.AddForceAtPosition(transform.up*-force,wl.transform.position);
        body.AddForceAtPosition(transform.up*force,wr.transform.position);
    }
}

