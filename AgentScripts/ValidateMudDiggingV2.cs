using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
public static class ValidateMudDiggingV2
{
 public static string Main(){if(!Application.isPlaying)throw new Exception("Requires Play");var car=UnityEngine.Object.FindAnyObjectByType<ForestKamazController>();var drive=car.GetComponent<VehiclePhysics>();var rb=car.GetComponent<Rigidbody>();var p=rb.position;var q=rb.rotation;var constraints=rb.constraints;var mode=Physics.simulationMode;bool paused=EditorApplication.isPaused;var soil=UnityEngine.Object.FindObjectsByType<SoftGroundRuts>().OrderBy(s=>s.transform.position.x).Last();var material=soil.GetComponent<MeshCollider>();
 try{Physics.simulationMode=SimulationMode.Script;EditorApplication.isPaused=true;car.testControl=true;car.testThrottle=-1;car.testSteer=0;car.testBrake=false;car.lowRange=true;car.reverseGear=false;drive.ResetState();var center=soil.transform.parent.position;if(soil.transform.parent.parent!=null)center=soil.transform.parent.parent.position;center.x=-40.62f;center.z=44.16f;
 if(!material.Raycast(new Ray(new Vector3(center.x,100,center.z),Vector3.down),out var hit,150))throw new Exception("Mud centre not physical");var pos=hit.point+Vector3.up*.55f;rb.position=pos;rb.rotation=Quaternion.LookRotation(Vector3.left);car.transform.SetPositionAndRotation(pos,rb.rotation);rb.linearVelocity=Vector3.zero;rb.angularVelocity=Vector3.zero;
 // A constrained stationary fixture isolates excavation from driver steering/movement.
 rb.constraints=RigidbodyConstraints.FreezePositionX|RigidbodyConstraints.FreezePositionZ|RigidbodyConstraints.FreezeRotation;Physics.SyncTransforms();int mud=0;float spin=0;
 for(int i=0;i<200;i++)Step(car);car.testThrottle=1;
 int particlesBefore=car.GetComponent<VehicleWheelEffects>().EmittedParticles;
 for(int i=0;i<700;i++){Step(car);mud=Math.Max(mud,drive.MudContacts);spin=Mathf.Max(spin,drive.WheelspinSpeed);}soil.Commit();int particles=car.GetComponent<VehicleWheelEffects>().EmittedParticles-particlesBefore;
 if(mud==0||spin<2||soil.DugDepth<.002f||soil.DugDepth>soil.maximumDepth+.001f||particles<1)throw new Exception("Digging failed mud="+mud+" spin="+spin+" depth="+soil.DugDepth+" particles="+particles);
 var audio=car.GetComponent<VehicleAudio>();audio.UpdateAudio(.1f);if(Mathf.Abs(audio.EngineRpm-drive.EngineRpm)>1)throw new Exception("Audio not coupled to engine");
 return "PASS real wheelspin on supplied mud scan: contacts="+mud+", spin="+spin.ToString("F1")+"m/s, local rut depth="+soil.DugDepth.ToString("F3")+"m (cap="+soil.maximumDepth+"), soil particles="+particles+", audio RPM="+audio.EngineRpm.ToString("F0")+" matches flywheel. Fixture constraints removed after test.";
 }finally{rb.constraints=constraints;car.testControl=false;car.testThrottle=0;car.testBrake=false;car.testSteer=0;car.lowRange=false;drive.ResetState();rb.position=p;rb.rotation=q;car.transform.SetPositionAndRotation(p,q);rb.linearVelocity=Vector3.zero;rb.angularVelocity=Vector3.zero;Physics.simulationMode=mode;EditorApplication.isPaused=paused;}
 }
 static void Step(ForestKamazController car){car.SendMessage("Update");car.SendMessage("FixedUpdate");Physics.Simulate(Time.fixedDeltaTime);car.SendMessage("LateUpdate");}
}
