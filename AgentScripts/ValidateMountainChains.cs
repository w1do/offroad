using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
public static class ValidateMountainChains
{
    static ForestKamazController car;static TruckChains chains;
    static float peakSlip;
    static void Step(float seconds)
    {
        for(int i=0;i<Mathf.CeilToInt(seconds/Time.fixedDeltaTime);i++)
        {car.SendMessage("Update");chains.Advance(Time.fixedDeltaTime);car.SendMessage("FixedUpdate");Physics.Simulate(Time.fixedDeltaTime);car.SendMessage("LateUpdate");peakSlip=Mathf.Max(peakSlip,car.DrivenSlip);}
    }
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Requires Play.");
        car=UnityEngine.Object.FindAnyObjectByType<ForestKamazController>();chains=car.GetComponent<TruckChains>();
        var rb=car.GetComponent<Rigidbody>();var original=rb.position;var rotation=rb.rotation;
        var oldMode=Physics.simulationMode;bool paused=EditorApplication.isPaused;Physics.simulationMode=SimulationMode.Script;EditorApplication.isPaused=true;
        var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="TEMP chain calibration";ground.transform.localScale=new Vector3(160,1,160);
        var surface=ground.AddComponent<ForestDrivingSurface>();surface.cover=ForestDrivingSurface.Cover.Ice;surface.chainsImproveGrip=true;surface.forwardGrip=0.10f;surface.lateralGrip=0.35f;surface.rollingResistance=0.01f;surface.chainedForwardGrip=1.05f;surface.chainedLateralGrip=0.85f;
        Vector3 center=new Vector3(4000,0,4000);float without=0,with=0,slip=0,side=0;
        try
        {
            car.testControl=true;car.testBrake=true;car.testThrottle=0;car.testSteer=0;car.lowRange=true;car.reverseGear=false;
            ground.transform.rotation=Quaternion.identity;ground.transform.position=center-Vector3.up*0.5f;Reset(rb,center+Vector3.up*0.3f,Quaternion.identity);Step(1.5f);
            if(chains.Installed){if(!chains.RequestToggle())throw new Exception("Cannot remove initial chains");Step(chains.installationSeconds+0.1f);}
            var tilt=Quaternion.Euler(-20,0,0);ground.transform.rotation=tilt;ground.transform.position=center-tilt*Vector3.up*0.5f;
            Reset(rb,center+tilt*Vector3.up*0.3f,tilt);Step(0.6f);car.testBrake=false;car.testThrottle=1;
            var start=rb.position;peakSlip=0;Step(4);without=Vector3.Dot(rb.position-start,tilt*Vector3.forward);slip=peakSlip;
            car.testThrottle=0;car.testBrake=true;ground.transform.rotation=Quaternion.identity;ground.transform.position=center-Vector3.up*0.5f;
            Reset(rb,center+Vector3.up*0.3f,Quaternion.identity);Step(1.5f);
            if(chains.Installed){if(!chains.RequestToggle())throw new Exception("Cannot remove initial chains");Step(chains.installationSeconds+0.1f);}
            car.testBrake=false;car.SendMessage("Update");if(chains.RequestToggle())throw new Exception("Installation allowed without parking brake");
            car.testBrake=true;car.SendMessage("Update");if(!chains.RequestToggle())throw new Exception("Cannot fit chains while stopped");Step(2);
            if(chains.Installed || !chains.Working)throw new Exception("Early chain activation");
            car.testBrake=false;car.SendMessage("Update");chains.Advance(0.02f);if(chains.Working || chains.Installed)throw new Exception("Installation cancellation failed");
            car.testBrake=true;Step(0.5f);if(!chains.RequestToggle())throw new Exception("Cannot restart installation");Step(chains.installationSeconds+0.1f);
            if(!chains.Installed || chains.wheelChainVisuals.Any(v=>!v.activeSelf))throw new Exception("Chain state or visuals missing");
            ground.transform.rotation=tilt;ground.transform.position=center-tilt*Vector3.up*0.5f;
            Reset(rb,center+tilt*Vector3.up*0.3f,tilt);Step(0.6f);car.testBrake=false;car.testThrottle=1;start=rb.position;Step(8);with=Vector3.Dot(rb.position-start,tilt*Vector3.forward);
            if(without>0.5f || slip<0.25f || with<1f || with<without+2)throw new Exception("Climb comparison failed: without="+without+" slip="+slip+" chains="+with+" speed="+car.SpeedKmh+" rpm="+car.wheels[2].rpm+" torque="+car.wheels[2].motorTorque+" contacts="+car.IceContacts+" forwardGrip="+car.wheels[2].forwardFriction.stiffness+" low="+car.lowRange);
            // Bare ice side slope, alternating signs; gravity, no steering impulse.
            car.testBrake=true;car.testThrottle=0;ground.transform.rotation=Quaternion.identity;ground.transform.position=center-Vector3.up*0.5f;Reset(rb,center+Vector3.up*0.3f,Quaternion.identity);Step(1.5f);
            if(!chains.RequestToggle())throw new Exception("Cannot remove chains");Step(chains.installationSeconds+0.1f);
            foreach(float bank in new[]{-10f,10f})
            {
                tilt=Quaternion.Euler(0,0,bank);ground.transform.rotation=tilt;ground.transform.position=center-tilt*Vector3.up*0.5f;
                Reset(rb,center+tilt*Vector3.up*0.3f,tilt);car.testBrake=false;car.testThrottle=1;start=rb.position;Step(2.5f);
                float moved=rb.position.x-start.x;if(moved*bank> -0.05f)throw new Exception("Side slope did not slide downhill: bank="+bank+" x="+moved);side=Mathf.Max(side,Mathf.Abs(moved));
            }
            return "PASS 20deg ice hill: bare progress="+without.ToString("F2")+"m slip="+slip.ToString("F2")+"; chains="+with.ToString("F2")+"m; +/-10deg side slopes drift="+side.ToString("F2")+"m; install rules, cancellation, timing, visuals passed";
        }
        finally
        {
            car.testBrake=false;car.testThrottle=0;car.testSteer=0;car.testControl=false;car.lowRange=false;
            Reset(rb,original,rotation);UnityEngine.Object.DestroyImmediate(ground);Physics.simulationMode=oldMode;EditorApplication.isPaused=paused;
        }
    }
    static void Reset(Rigidbody rb,Vector3 p,Quaternion q){rb.transform.SetPositionAndRotation(p,q);rb.position=p;rb.rotation=q;rb.linearVelocity=Vector3.zero;rb.angularVelocity=Vector3.zero;Physics.SyncTransforms();}
}
