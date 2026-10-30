using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public static class ValidateTruckRepair
{
    public static async Task<string> Main()
    {
        if(!Application.isPlaying)throw new Exception("Requires Play.");
        Application.runInBackground=true;
        var car=UnityEngine.Object.FindAnyObjectByType<ForestKamazController>();var body=car.GetComponent<Rigidbody>();
        Vector3 original=body.position;Quaternion rotation=body.rotation;int frames=Time.frameCount;
        var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="TEMPORARY PHYSICS TEST - removed after test";
        ground.transform.position=new Vector3(2000,-0.5f,2000);ground.transform.localScale=new Vector3(300,1,300);
        string result="";
        try
        {
            foreach(var visual in car.visuals)
            {
                foreach(var f in visual.GetComponentsInChildren<MeshFilter>())
                    if(Mathf.Abs(f.sharedMesh.bounds.center.y)>0.025f || Mathf.Abs(f.sharedMesh.bounds.center.z)>0.025f)
                        throw new Exception("Off-center wheel geometry: "+visual.name+" "+f.sharedMesh.bounds);
            }
            car.testControl=true;car.testThrottle=0;car.testBrake=true;car.testSteer=0;
            body.position=new Vector3(2000,0.3f,2000);body.rotation=Quaternion.identity;body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;
            Physics.SyncTransforms();await Wait(1.5f);
            int contacts=car.wheels.Count(w=>w.isGrounded);if(contacts!=6)throw new Exception("Ground contacts="+contacts);
            car.testBrake=false;car.testThrottle=1;await Wait(3);
            float acceleration=car.SpeedKmh;if(acceleration<4 || acceleration>16)throw new Exception("Acceleration out of target 4..16 km/h: "+acceleration+" velocity="+body.linearVelocity+" position="+body.position+" contacts="+car.wheels.Count(w=>w.isGrounded)+" mass="+body.mass);
            car.testThrottle=0;await Wait(1.2f);float coast=car.SpeedKmh;
            if(coast<2 || coast>acceleration)throw new Exception("Coasting outside target: "+coast);
            car.testBrake=true;await Wait(2);float braked=car.SpeedKmh;if(braked>0.7f)throw new Exception("Brake failed="+braked);
            result="PASS geometry centered on all six hubs; contacts="+contacts+" speed after3s="+acceleration.ToString("F2")+" coast after1.2s="+coast.ToString("F2")+" braked="+braked.ToString("F2");
            car.testBrake=false;car.testSteer=0.45f;
            body.position=new Vector3(2000,0.12f,2000);body.rotation=Quaternion.identity;body.angularVelocity=Vector3.zero;body.linearVelocity=Vector3.forward*(50f/3.6f);
            await Wait(1.5f);
            float lateral=Mathf.Abs(car.LateralSpeed);float grip=car.wheels[2].sidewaysFriction.stiffness;
            if(lateral<1f || grip>0.8f)throw new Exception("High-speed slide missing: lateral="+lateral+" grip="+grip);
            result+="; 50km/h corner lateral="+lateral.ToString("F2")+"m/s rearGrip="+grip.ToString("F2");
            car.testSteer=-0.45f;car.testBrake=false;await Wait(0.7f);
            if(car.wheels[0].steerAngle>=0)throw new Exception("Countersteer failed");
            result+="; countersteer responds; frames="+(Time.frameCount-frames);
            car.testThrottle=0;car.testSteer=0;car.testBrake=true;
            body.position=new Vector3(2000,0.3f,2000);body.rotation=Quaternion.identity;body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;
            ground.AddComponent<ForestDrivingSurface>();
            await Wait(1.5f);
            if(car.MudContacts!=6 || car.wheels[2].sidewaysFriction.stiffness>0.5f || car.wheels[2].forwardFriction.stiffness>0.6f)
                throw new Exception("Mud surface contact/grip failed: "+car.MudContacts);
            result+="; mud=6 contacts, reduced lateral/forward grip";
            var camera=UnityEngine.Object.FindAnyObjectByType<DriveCamera>();
            if(camera!=null)
            {
                float yaw=camera.orbitYaw;camera.orbitYaw=yaw+90;await Wait(0.5f);
                float heading=Vector3.Angle(Vector3.ProjectOnPlane(camera.transform.forward,Vector3.up),car.transform.forward);
                camera.orbitYaw=yaw;
                if(heading<60)throw new Exception("Orbit camera failed: "+heading);
                result+="; camera orbit=90deg";
            }
            return result;
        }
        finally
        {
            car.testControl=false;car.testSteer=0;car.testThrottle=0;car.testBrake=false;
            body.position=original;body.rotation=rotation;body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;
            UnityEngine.Object.Destroy(ground);
        }
    }
    static async Task Wait(float seconds){float t=Time.time;while(Time.time-t<seconds){if(!Application.isPlaying)throw new Exception("Play stopped during test");await Task.Delay(10);}}
}
