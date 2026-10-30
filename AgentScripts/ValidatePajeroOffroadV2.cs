using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
public static class ValidatePajeroOffroadV2
{
 public static string Main(){if(!Application.isPlaying)throw new Exception("Requires Play");var car=UnityEngine.Object.FindAnyObjectByType<ForestKamazController>();var rb=car.GetComponent<Rigidbody>();var drive=car.GetComponent<VehiclePhysics>();var position=rb.position;var rotation=rb.rotation;var mode=Physics.simulationMode;bool paused=EditorApplication.isPaused;int passed=0;float maxRoll=0;
 try{Physics.simulationMode=SimulationMode.Script;EditorApplication.isPaused=true;car.testControl=true;car.testThrottle=-1;car.testSteer=0;car.testBrake=false;
 foreach(var point in new[]{new Vector3(-20,0,42),new Vector3(-20,0,34),new Vector3(-30,0,40),new Vector3(41.31f,0,96.23f),new Vector3(-70,0,32),new Vector3(75,0,0),new Vector3(-50,0,-20),new Vector3(12,0,50)}){
 var top=Physics.RaycastAll(new Vector3(point.x,200,point.z),Vector3.down,400,~0,QueryTriggerInteraction.Ignore).Where(h=>h.collider.GetComponent<ForestGroundPatch>()!=null).OrderBy(h=>h.distance).ToArray();if(top.Length==0)throw new Exception("No physical terrain at "+point);var p=top[0].point+Vector3.up*1;rb.position=p;rb.rotation=Quaternion.identity;car.transform.SetPositionAndRotation(p,rb.rotation);rb.linearVelocity=Vector3.zero;rb.angularVelocity=Vector3.zero;drive.ResetState();Physics.SyncTransforms();int contacts=0;
 for(int i=0;i<700;i++){car.SendMessage("Update");car.SendMessage("FixedUpdate");Physics.Simulate(Time.fixedDeltaTime);car.SendMessage("LateUpdate");contacts=Math.Max(contacts,car.wheels.Count(w=>w.isGrounded));maxRoll=Mathf.Max(maxRoll,Vector3.Angle(car.transform.up,Vector3.up));var ground=Physics.RaycastAll(new Vector3(rb.position.x,200,rb.position.z),Vector3.down,400,~0,QueryTriggerInteraction.Ignore).Where(h=>h.collider.GetComponent<ForestGroundPatch>()!=null).OrderBy(h=>h.distance).ToArray();if(ground.Length>0&&rb.position.y<ground[0].point.y-1.1f)throw new Exception("Car beneath visible ground at "+rb.position);}
 if(contacts<2)throw new Exception("No suspension contact at "+point);passed++;
 }return "PASS "+passed+" road/shoulder/forest drop-and-hold locations including reported underground position; no below-ground driving; max body angle="+maxRoll.ToString("F1")+"deg.";
 }finally{car.testControl=false;car.testThrottle=0;car.testBrake=false;car.testSteer=0;rb.position=position;rb.rotation=rotation;car.transform.SetPositionAndRotation(position,rotation);rb.linearVelocity=Vector3.zero;rb.angularVelocity=Vector3.zero;drive.ResetState();Physics.simulationMode=mode;EditorApplication.isPaused=paused;}
 }
}
