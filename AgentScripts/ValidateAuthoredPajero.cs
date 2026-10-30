using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
public static class ValidateAuthoredPajero
{
 static void Place(ForestKamazController car,Vector3 p,Quaternion q){var rb=car.GetComponent<Rigidbody>();rb.position=p;rb.rotation=q;car.transform.SetPositionAndRotation(p,q);rb.linearVelocity=Vector3.zero;rb.angularVelocity=Vector3.zero;Physics.SyncTransforms();}
 static void Step(ForestKamazController car){car.SendMessage("Update");car.SendMessage("FixedUpdate");Physics.Simulate(.02f);car.SendMessage("LateUpdate");car.GetComponent<TruckBrakeLights>().SendMessage("LateUpdate");}
 public static string Main(){if(!Application.isPlaying)throw new Exception("Requires Play");var car=UnityEngine.Object.FindAnyObjectByType<ForestKamazController>();if(car.wheels.Length!=4||!car.driveFrontWheels||!car.name.Contains("Pajero"))throw new Exception("Wrong active car");if(UnityEngine.Object.FindAnyObjectByType<LongRouteStream>()!=null)throw new Exception("Procedural route still active");var rb=car.GetComponent<Rigidbody>();var pos=rb.position;var rotation=rb.rotation;var mode=Physics.simulationMode;bool paused=EditorApplication.isPaused;GameObject fixture=null;string report="";
 try{Physics.simulationMode=SimulationMode.Script;EditorApplication.isPaused=true;car.testControl=true;car.reverseGear=false;car.lowRange=false;car.testSteer=0;car.testThrottle=0;car.testBrake=true;
 fixture=GameObject.CreatePrimitive(PrimitiveType.Cube);fixture.name="TEMP Pajero test surface";fixture.transform.position=new Vector3(500,19.5f,500);fixture.transform.localScale=new Vector3(200,1,200);Place(car,new Vector3(500,20.5f,500),Quaternion.identity);for(int i=0;i<200;i++)Step(car);if(car.wheels.Count(w=>w.isGrounded)!=4)throw new Exception("Not all four wheels grounded");
 car.testBrake=false;car.testThrottle=1;for(int i=0;i<250;i++)Step(car);float speed=car.SpeedKmh;if(speed<12||speed>65)throw new Exception("Invalid 5s acceleration "+speed);car.testThrottle=0;for(int i=0;i<25;i++)Step(car);float coast=car.SpeedKmh;if(coast<speed*.6f)throw new Exception("No inertia");car.testBrake=true;for(int i=0;i<200;i++)Step(car);if(car.SpeedKmh>.8f||!car.GetComponent<TruckBrakeLights>().Illuminated)throw new Exception("Brakes/stop lamps failed");report="5s acceleration="+speed.ToString("F1")+"km/h; coast="+coast.ToString("F1")+"km/h; stopped="+car.SpeedKmh.ToString("F2")+"; ";
 UnityEngine.Object.DestroyImmediate(fixture);fixture=null;car.lowRange=true;
 var forest=GameObject.Find("Forest - imported dirt road environment");var road=forest.GetComponentsInChildren<MeshFilter>().First(f=>f.name.EndsWith(".025")).GetComponent<MeshCollider>();
 var centers=new[]{new Vector3(-40.62f,43.15f,44.16f),new Vector3(-72.29f,40.28f,41.5f)};
 foreach(var center in centers){var p=center+Vector3.right*9;if(!road.Raycast(new Ray(p+Vector3.up*15,Vector3.down),out var ground,40))throw new Exception("Entry road missing");p.y=ground.point.y+.55f;Place(car,p,Quaternion.LookRotation(Vector3.left));car.testThrottle=0;car.testBrake=true;for(int i=0;i<100;i++)Step(car);car.testBrake=false;int mud=0;bool done=false;
 for(int i=0;i<3500;i++){var target=center+Vector3.left*12;target.y=rb.position.y;var local=car.transform.InverseTransformPoint(target);car.testSteer=Mathf.Clamp(Mathf.Atan2(local.x,Mathf.Max(1,local.z))*Mathf.Rad2Deg/24,-1,1);car.testThrottle=car.SpeedKmh<14?.7f:0;Step(car);mud=Mathf.Max(mud,car.MudContacts);if(rb.position.y<center.y-3)throw new Exception("Pajero below original road");if(rb.position.x<center.x-7){done=true;break;}}
 if(!done||mud==0){string d="";foreach(var w in car.wheels){if(w.GetGroundHit(out var h))d+=" | "+h.collider.name+" rpm="+w.rpm+" slip="+h.forwardSlip+" torque="+w.motorTorque+" p="+h.point;}throw new Exception("Mud crossing failed at "+center+" car="+rb.position+" speed="+car.SpeedKmh+" mud="+mud+d);}report+="mud "+center.x+" crossed, contacts="+mud+"; ";car.testSteer=0;}
 return "PASS Pajero four-wheel AWD, suspension, inertia, braking/lamps and both supplied mud patches. "+report;
 }finally{if(fixture!=null)UnityEngine.Object.DestroyImmediate(fixture);car.testControl=false;car.testThrottle=0;car.testSteer=0;car.testBrake=false;car.lowRange=false;Place(car,pos,rotation);Physics.simulationMode=mode;EditorApplication.isPaused=paused;}
 }
}


