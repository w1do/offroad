using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ValidateSplitGripDifferential
{
    static object Trial(VehiclePhysics source,bool locked,out float travel)
    {
        var scene=SceneManager.CreateScene("Split-grip differential "+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        var physics=scene.GetPhysicsScene();VehiclePhysicsProfile config=null;
        try
        {
            for(int side=0;side<2;side++)
            {
                var lane=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(lane,scene);
                lane.name=side==0?"Firm left tyres":"Slippery right tyres";lane.transform.position=new Vector3(side==0?-2.5f:2.5f,-.5f,10);lane.transform.localScale=new Vector3(5,1,50);
                var surface=lane.AddComponent<ForestDrivingSurface>();surface.cover=ForestDrivingSurface.Cover.Gravel;
                surface.forwardGrip=side==0?.7f:.015f;surface.lateralGrip=.8f;surface.rollingResistance=.018f;
            }
            var clone=UnityEngine.Object.Instantiate(source.gameObject,new Vector3(0,.65f,0),Quaternion.identity);SceneManager.MoveGameObjectToScene(clone,scene);
            var vehicle=clone.GetComponent<VehiclePhysics>();var body=clone.GetComponent<Rigidbody>();body.isKinematic=false;
            config=UnityEngine.Object.Instantiate(source.profile);foreach(var axle in config.axles)axle.limitedSlipTorqueNm=0;
            vehicle.profile=config;vehicle.automaticSimulation=false;vehicle.externalControl=true;clone.GetComponent<VehiclePlayerInput>().enabled=false;
            vehicle.Initialize();vehicle.SetFourWheelDrive(true);vehicle.SetTransmission(false,true);vehicle.SetInput(new VehicleControlInput{brake=1});Physics.SyncTransforms();
            for(int i=0;i<200;i++){vehicle.Simulate(.01f);physics.Simulate(.01f);}
            if(!vehicle.SetDifferentialLock(locked))throw new Exception("Cannot select differential mode on stopped fixture");
            Vector3 start=body.position;float peakSpeed=0,peakSplit=0,maximumHeight=start.y;int grounded=0;
            for(int i=0;i<500;i++)
            {
                float yaw=Mathf.DeltaAngle(0,body.rotation.eulerAngles.y);
                vehicle.SetInput(new VehicleControlInput{throttle=1,steering=Mathf.Clamp(-yaw*.04f-body.angularVelocity.y*.1f,-.5f,.5f)});
                vehicle.Simulate(.01f);physics.Simulate(.01f);peakSpeed=Mathf.Max(peakSpeed,body.linearVelocity.magnitude);maximumHeight=Mathf.Max(maximumHeight,body.position.y);
                grounded=Mathf.Max(grounded,vehicle.Wheels.Count(w=>w.isGrounded));
                for(int axle=0;axle<vehicle.axles.Length;axle++)peakSplit=Mathf.Max(peakSplit,Mathf.Abs(vehicle.Wheels[axle*2].rpm-vehicle.Wheels[axle*2+1].rpm));
            }
            travel=body.position.z-start.z;
            if(grounded<4||peakSpeed>20||maximumHeight>start.y+.5f||Mathf.Abs(body.position.x)>2)throw new Exception("Unstable split-grip fixture");
            return new{locked,seconds=5,travelMetres=travel,peakSpeedMps=peakSpeed,peakAxleSplitRpm=peakSplit,finalEngineRpm=vehicle.EngineRpm,finalPosition=body.position.ToString("F3")};
        }
        finally{foreach(var root in scene.GetRootGameObjects())UnityEngine.Object.DestroyImmediate(root);SceneManager.UnloadSceneAsync(scene);if(config!=null)UnityEngine.Object.DestroyImmediate(config);}
    }
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Requires Play mode");var source=UnityEngine.Object.FindAnyObjectByType<VehiclePhysics>();
        var open=Trial(source,false,out float openTravel);var locked=Trial(source,true,out float lockedTravel);
        if(lockedTravel<openTravel+1)throw new Exception("Lock must help split-grip departure: "+lockedTravel+" vs "+openTravel);
        string report=Newtonsoft.Json.JsonConvert.SerializeObject(new{passed=true,open,locked},Newtonsoft.Json.Formatting.Indented);
        File.WriteAllText("docs/reports/split-grip-differential.json",report);return report;
    }
}
