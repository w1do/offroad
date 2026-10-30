using System;
using UnityEngine;
using UnityEditor;
public static class ValidateActualClimb
{
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Requires Play.");
        var stage=UnityEngine.Object.FindAnyObjectByType<MountainClimbStage>();var car=stage.truck;var chains=car.GetComponent<TruckChains>();var rb=car.GetComponent<Rigidbody>();
        var pos=rb.position;var rot=rb.rotation;var oldMode=Physics.simulationMode;bool paused=EditorApplication.isPaused;
        Physics.simulationMode=SimulationMode.Script;EditorApplication.isPaused=true;
        int maximum=120;float maxSide=0;
        try
        {
            car.testControl=true;car.testBrake=true;car.testThrottle=0;car.testSteer=0;car.lowRange=true;car.reverseGear=false;
            var p=stage.centerline[120];var q=Quaternion.LookRotation(stage.centerline[123]-p);Reset(rb,p+Vector3.up*0.3f,q);
            for(int i=0;i<100;i++)Step(car,chains);
            if(!chains.Installed){if(!chains.RequestToggle())throw new Exception("Cannot install chains at safe approach");for(int i=0;i<220;i++)Step(car,chains);}
            for(int step=0;step<8000;step++)
            {
                int nearest=120;float best=float.MaxValue;
                for(int i=110;i<220;i++){float d=(rb.position-stage.centerline[i]).sqrMagnitude;if(d<best){best=d;nearest=i;}}
                maximum=Mathf.Max(maximum,nearest);maxSide=Mathf.Max(maxSide,Mathf.Abs(rb.position.x-stage.centerline[nearest].x));
                if(nearest>=205)break;
                var target=stage.centerline[Mathf.Min(nearest+3,stage.centerline.Length-1)];var local=car.transform.InverseTransformPoint(target);
                car.testSteer=Mathf.Clamp(Mathf.Atan2(local.x,Mathf.Max(1,local.z))*Mathf.Rad2Deg/18f,-1,1);
                car.testBrake=false;car.testThrottle=car.SpeedKmh<9?1f:0;Step(car,chains);
                if(rb.position.y<stage.centerline[nearest].y-2)throw new Exception("Fell below road near "+nearest*2);
            }
            if(maximum<205)throw new Exception("Actual icy climb not completed: reached "+maximum*2+"m; speed="+car.SpeedKmh+" side="+maxSide+" position="+rb.position+" angles="+car.transform.eulerAngles+" steer="+car.testSteer+" chains="+chains.Installed+" grip="+car.wheels[2].sidewaysFriction.stiffness+" wheelSteer="+car.wheels[0].steerAngle);
            return "PASS actual road: 240m approach -> "+maximum*2+"m summit on rear chains / low range; max lateral offset="+maxSide.ToString("F2")+"m; no fall-through";
        }
        finally
        {car.testControl=false;car.testThrottle=0;car.testSteer=0;car.testBrake=false;car.lowRange=false;Reset(rb,pos,rot);Physics.simulationMode=oldMode;EditorApplication.isPaused=paused;}
    }
    static void Step(ForestKamazController car,TruckChains chains){car.SendMessage("Update");chains.Advance(Time.fixedDeltaTime);car.SendMessage("FixedUpdate");Physics.Simulate(Time.fixedDeltaTime);car.SendMessage("LateUpdate");}
    static void Reset(Rigidbody rb,Vector3 p,Quaternion q){rb.transform.SetPositionAndRotation(p,q);rb.position=p;rb.rotation=q;rb.linearVelocity=Vector3.zero;rb.angularVelocity=Vector3.zero;Physics.SyncTransforms();}
}
