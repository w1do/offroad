using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public static class ValidateTractionBrakes
{
    static ForestKamazController activeCar;
    static TruckBrakeLights activeLamps;
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Requires Play.");
        Application.runInBackground=true;
        var car=UnityEngine.Object.FindAnyObjectByType<ForestKamazController>();var rb=car.GetComponent<Rigidbody>();
        var lamps=car.GetComponentInChildren<TruckBrakeLights>();if(lamps==null)throw new Exception("Missing stop lamps.");
        activeCar=car;activeLamps=lamps;
        var previousMode=Physics.simulationMode;bool paused=UnityEditor.EditorApplication.isPaused;
        UnityEditor.EditorApplication.isPaused=true;Physics.simulationMode=SimulationMode.Script;
        var original=rb.position;var orientation=rb.rotation;
        foreach(var wheel in car.wheels){var f=wheel.forwardFriction;f.extremumSlip=0.08f;f.asymptoteSlip=0.4f;wheel.forwardFriction=f;}
        var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="Temporary traction test";
        ground.transform.position=new Vector3(3000,-0.5f,3000);ground.transform.localScale=new Vector3(150,1,150);
        try
        {
            car.testControl=true;car.testBrake=true;car.testThrottle=0;car.testSteer=0;car.lowRange=false;car.reverseGear=false;
            Reset(rb,new Vector3(3000,0.3f,3000),Quaternion.identity);Wait(1.5f);
            if(!lamps.Illuminated || lamps.lights.Any(l=>!l.enabled))throw new Exception("Stop lamps did not illuminate.");
            car.testBrake=false;car.testThrottle=1;Wait(2.5f);float dry=car.SpeedKmh;
            if(lamps.Illuminated)throw new Exception("Stop lamps stuck on under acceleration: brake="+car.BrakeApplied+" speed="+car.SpeedKmh+" velocity="+rb.linearVelocity+" reverse="+car.reverseGear+" test="+car.testControl+" throttle="+car.testThrottle);
            car.testThrottle=-1;Wait(2);if(car.SpeedKmh>0.8f || !car.BrakeApplied || !lamps.Illuminated)throw new Exception("S braking failed: "+car.SpeedKmh);
            var surface=ground.AddComponent<ForestDrivingSurface>();surface.forwardGrip=0.12f;
            car.testBrake=true;car.testThrottle=0;Reset(rb,new Vector3(3000,0.3f,3000),Quaternion.identity);Wait(1.5f);
            car.testBrake=false;car.testThrottle=1;car.lowRange=true;Wait(2.5f);
            float mud=car.SpeedKmh,slip=car.DrivenSlip;float rpm=car.wheels.Skip(2).Average(w=>Mathf.Abs(w.rpm));
            if(car.MudContacts<4 || slip<0.3f || rpm<25 || mud>=dry)throw new Exception("Mud spin failed: dry="+dry+" mud="+mud+" slip="+slip+"rpm="+rpm);
            surface.forwardGrip=0.65f;
            var tilt=Quaternion.Euler(-15,0,0);ground.transform.rotation=tilt;var center=new Vector3(3000,0,3000);ground.transform.position=center-tilt*Vector3.up*0.5f;
            car.testBrake=true;car.testThrottle=0;Reset(rb,center+tilt*Vector3.up*0.3f,tilt);Wait(1.5f);
            rb.linearVelocity=-car.transform.forward*5;Wait(3);
            if(car.SpeedKmh>0.8f)throw new Exception("Downhill brake failed="+car.SpeedKmh);
            float holdStart=Vector3.Dot(rb.position,tilt*Vector3.forward);Wait(1.5f);float hold=Mathf.Abs(Vector3.Dot(rb.position,tilt*Vector3.forward)-holdStart);
            if(hold>0.2f)throw new Exception("Hill hold drift="+hold);
            car.testBrake=false;car.testThrottle=1;car.lowRange=true;surface.forwardGrip=0.18f;Wait(2);
            float hillSlip=car.DrivenSlip;if(hillSlip<0.3f)throw new Exception("Slippery uphill spin missing="+hillSlip);
            return "PASS dry2.5s="+dry.ToString("F2")+"km/h mud="+mud.ToString("F2")+"km/h slip="+slip.ToString("F2")+" rearRPM="+rpm.ToString("F0")+"; S brake + brake lamps on/off; 15deg downhill stopped and held drift="+hold.ToString("F3")+"m; uphill slip="+hillSlip.ToString("F2");
        }
        finally
        {car.testControl=false;car.testBrake=false;car.testThrottle=0;car.testSteer=0;car.lowRange=false;Reset(rb,original,orientation);UnityEngine.Object.DestroyImmediate(ground);Physics.simulationMode=previousMode;UnityEditor.EditorApplication.isPaused=paused;}
    }
    static void Reset(Rigidbody rb,Vector3 pos,Quaternion rot){rb.position=pos;rb.rotation=rot;rb.linearVelocity=Vector3.zero;rb.angularVelocity=Vector3.zero;Physics.SyncTransforms();}
    static void Wait(float seconds)
    {
        for(int i=0;i<Mathf.CeilToInt(seconds/Time.fixedDeltaTime);i++)
        {
            activeCar.SendMessage("Update");activeCar.SendMessage("FixedUpdate");
            Physics.Simulate(Time.fixedDeltaTime);activeCar.SendMessage("LateUpdate");activeLamps.SendMessage("LateUpdate");
        }
    }
}

