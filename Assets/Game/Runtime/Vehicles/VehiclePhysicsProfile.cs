using System;

using UnityEngine;

using UnityEngine.Scripting.APIUpdating;



[Serializable]

public sealed class VehicleAxleSettings

{

    public string name="Axle";

    public bool steering,handbrake,chains;
    public bool primaryDrive;

    [Range(-1,1)]public float steeringMultiplier=1;

    [Min(0)]public float driveShare=.5f;

    public float wheelRadius=.39731f,wheelMass=30,suspensionTravel=.22f;
    public float tyreWidthMetres=.285f;
    public TireProfile tire;

    public float wheelDampingRate=.02f;

    public float springNPerMetre=38000,damperNsPerMetre=5800,springTarget=.45f;

    public float forceApplicationHeight=.10f,antiRollN=4500;

    [Range(.1f,2)]public float lateralMultiplier=1;

    [Range(.1f,1)]public float wheelspinLateralMultiplier=.6f;

    public float limitedSlipTorqueNm,limitedSlipCoupling=10;

}



[MovedFrom(true,"","Assembly-CSharp","PajeroDriveProfile")]

[CreateAssetMenu(menuName="Game/Vehicles/Physics profile")]

public sealed class VehiclePhysicsProfile : ScriptableObject

{

    public string displayName="Vehicle";
    public VehicleEquipmentProfile equipment;
    public VehicleWheelEffectsProfile wheelEffects;
    public WheelRutContactSettings rutContact = new WheelRutContactSettings();

    [Header("Chassis / SI units")]

    public float massKg=1500;

    public Vector3 centreOfMass=new Vector3(0,.55f,0);

    public float maximumSpeedKmh=160,reverseSpeedKmh=25,aerodynamicDrag=.65f;

    public float angularDamping=.32f;

    [Header("Engine — net shaft torque")]

    public float ratedPowerKw=100,ratedPowerRpm=4500,peakTorqueNm=240,peakTorqueRpm=2500;

    public float idleRpm=850,redlineRpm=6500,engineInertia=.42f;

    public AnimationCurve torqueNm=new AnimationCurve(new Keyframe(600,100),new Keyframe(2500,240),new Keyframe(4500,212),new Keyframe(6500,0));

    public float engineFrictionNm=25,engineViscousFriction=.10f,idleGovernorNmPerRpm=1.2f;

    public float clutchFullRpm=1900,clutchEngageSpeed=2;

    [Header("Transmission")]

    public float[] gears={3.9f,2.25f,1.45f,1f,.82f};

    public float reverseRatio=3.8f,finalDrive=4.6f,lowRatio=1.95f,efficiency=.86f;

    public float clutchCapacityNm=360,clutchCoupling=1.4f,shiftSeconds=.28f;

    public float centreLockTorqueNm=1800,centreCoupling=25;

    [Header("Driver response")]

    public float pedalRisePerSecond=.62f,pedalReleasePerSecond=3.5f,precisionPedal=.32f;

    public float steeringDegrees=31,highSpeedSteeringDegrees=18,steeringRate=80;

    [Header("Tyres and brakes")]

    public float dirtLongitudinal=.32f,dirtLateral=.58f,grassLongitudinal=.24f,grassLateral=.42f;

    [Header("Base forest soil � prototype calibration")]

    public bool baseSoilChainsImproveGrip=true;

    public float baseSoilChainedLongitudinal=.72f,baseSoilChainedLateral=.65f;

    public float wheelspinLateralMultiplier=.44f,undrivenLateralMultiplier=1;

    public float serviceBrakeNm=2400,handBrakeNm=3200,rollingResistance=.018f;

    public float forwardPeakSlip=.22f,forwardSlidingSlip=.85f,forwardSlidingGrip=.57f,chainsSlidingGrip=.82f;

    public float lateralPeakSlip=.16f,lateralSlidingSlip=.5f,lateralSlidingGrip=.54f,gripResponse=3;

    public float staticBrakeSpeed=.45f;

    [Header("Axles — same order as prefab bindings")]

    public VehicleAxleSettings[] axles={

        new VehicleAxleSettings{name="Front",steering=true,driveShare=.38f,antiRollN=5000},

        new VehicleAxleSettings{name="Rear",handbrake=true,chains=true,primaryDrive=true,driveShare=.62f,antiRollN=4200,lateralMultiplier=.88f}

    };

    public float TorqueAt(float rpm)

    {

        float torque=Mathf.Max(0,torqueNm.Evaluate(rpm));

        return Mathf.Min(torque,peakTorqueNm,ratedPowerKw*1000/Mathf.Max(1,rpm*Mathf.PI/30));

    }

    public float PowerKwAt(float rpm)=>TorqueAt(rpm)*rpm*Mathf.PI/30000;

    public string ValidationError(int bindingCount)

    {

        if(massKg<=0||ratedPowerKw<=0||peakTorqueNm<=0||redlineRpm<=idleRpm)return "Invalid engine/chassis values";

        if(gears==null||gears.Length==0||Array.Exists(gears,g=>g<=0))return "Forward ratios must be positive";

        if(axles==null||axles.Length!=bindingCount)return "Profile axle count does not match prefab bindings";

        float share=0;foreach(var axle in axles){if(axle==null||axle.wheelRadius<=0||axle.suspensionTravel<=0||axle.springNPerMetre<=0)return "Invalid axle settings";share+=axle.driveShare;}

        if(equipment!=null&&equipment.switchableDrive&&!Array.Exists(axles,a=>a.primaryDrive&&a.driveShare>0))return "Select a primary driven axle for 2WD";
        return share>0?null:"At least one axle must be driven";

    }

}

