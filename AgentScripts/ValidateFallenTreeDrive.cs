using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
public sealed class FallenTreeContactProbe : MonoBehaviour {
 public int contacts;
 private void OnCollisionEnter(Collision collision){if(collision.collider.GetComponentInParent<FallenTreeObstacle>()!=null)contacts++;}
 private void OnCollisionStay(Collision collision){if(collision.collider.GetComponentInParent<FallenTreeObstacle>()!=null)contacts++;}
}
public static class ValidateFallenTreeDrive {
 public static object Main(){
 if(!Application.isPlaying)throw new Exception("Requires Play Mode");var controller=UnityEngine.Object.FindAnyObjectByType<ForestRoadBlockages>();int session=controller.SessionSeed;var original=UnityEngine.Object.FindAnyObjectByType<VehiclePhysics>();var originalBody=original.GetComponent<Rigidbody>();bool kinematic=originalBody.isKinematic,paused=EditorApplication.isPaused;var velocity=originalBody.linearVelocity;var angular=originalBody.angularVelocity;var mode=Physics.simulationMode;GameObject clone=null;
 try{EditorApplication.isPaused=true;Physics.simulationMode=SimulationMode.Script;originalBody.isKinematic=true;controller.Generate(7);var obstacle=controller.GetComponentsInChildren<FallenTreeObstacle>().First();int index=int.Parse(obstacle.name.Substring("Fallen tree at site ".Length));var site=controller.sites[index];
 clone=UnityEngine.Object.Instantiate(original.gameObject,site.centre-site.forward*7+Vector3.up*.8f,Quaternion.LookRotation(site.forward));var car=clone.GetComponent<VehiclePhysics>();car.automaticSimulation=false;car.externalControl=true;clone.GetComponent<VehiclePlayerInput>().enabled=false;var sound=clone.GetComponent<VehicleAudio>();if(sound!=null)sound.enabled=false;var body=clone.GetComponent<Rigidbody>();body.isKinematic=false;var probe=clone.AddComponent<FallenTreeContactProbe>();car.Initialize();car.SetNeutral(false);car.SetFourWheelDrive(true);car.SetTransmission(false,true);Physics.SyncTransforms();car.SetInput(new VehicleControlInput{brake=1});for(int i=0;i<150;i++){car.Simulate(.01f);Physics.Simulate(.01f);}var start=body.position;float peak=0;car.SetInput(new VehicleControlInput{throttle=.6f});for(int i=0;i<800;i++){car.Simulate(.01f);Physics.Simulate(.01f);peak=Mathf.Max(peak,car.SpeedKmh);}
 if(probe.contacts==0)throw new Exception("No physical tree contact: travel="+Vector3.Distance(start,body.position));return new{success=true,seconds=8,treeContacts=probe.contacts,travel=Vector3.Distance(start,body.position),peakKmh=peak};
 }finally{if(clone!=null)UnityEngine.Object.DestroyImmediate(clone);originalBody.isKinematic=kinematic;if(!kinematic){originalBody.linearVelocity=velocity;originalBody.angularVelocity=angular;}Physics.simulationMode=mode;EditorApplication.isPaused=paused;controller.Generate(session);}
 }
}
