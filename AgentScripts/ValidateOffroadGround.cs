using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
public static class ValidateOffroadGround
{
 static float Ground(Vector3 p,Transform root){var hits=Physics.RaycastAll(new Vector3(p.x,150,p.z),Vector3.down,300,~0,QueryTriggerInteraction.Ignore).Where(h=>h.collider.transform.IsChildOf(root)).OrderBy(h=>h.distance).ToArray();if(hits.Length==0)throw new Exception("Missing ground "+p);return hits[0].point.y;}
 public static string Main(){if(!Application.isPlaying)throw new Exception("Requires Play");var car=UnityEngine.Object.FindAnyObjectByType<ForestKamazController>();var rb=car.GetComponent<Rigidbody>();var root=GameObject.Find("Continuous forest ground").transform;var pos=rb.position;var rot=rb.rotation;var velocity=rb.linearVelocity;var angular=rb.angularVelocity;var mode=Physics.simulationMode;bool paused=EditorApplication.isPaused;int grounded=0;
 try{Physics.simulationMode=SimulationMode.Script;EditorApplication.isPaused=true;car.testControl=true;car.testThrottle=0;car.testSteer=0;car.testBrake=true;
 foreach(var offset in new[]{new Vector3(0,0,0),new Vector3(12,0,12),new Vector3(-12,0,-12),new Vector3(25,0,-25),new Vector3(-25,0,25),new Vector3(45,0,0),new Vector3(0,0,-45),new Vector3(-45,0,-30)}){var p=pos+offset;p.y=Ground(p,root)+3;rb.position=p;rb.rotation=Quaternion.Euler(0,rot.eulerAngles.y,0);rb.linearVelocity=Vector3.zero;rb.angularVelocity=Vector3.zero;car.transform.SetPositionAndRotation(p,rb.rotation);Physics.SyncTransforms();bool contact=false;
 for(int i=0;i<350;i++){car.SendMessage("Update");car.SendMessage("FixedUpdate");Physics.Simulate(.02f);float y=Ground(rb.position,root);if(rb.position.y<y-1.5f)throw new Exception("Truck below terrain at "+rb.position+" ground="+y);contact|=car.wheels.Any(w=>w.isGrounded);}
 if(!contact)throw new Exception("No wheel contact for "+offset);grounded++;
 }return "PASS actual truck drop/settle at "+grounded+" road and offroad positions, 7s each: wheel contacts confirmed, no underground entry.";
 }finally{car.testControl=false;car.testThrottle=0;car.testBrake=false;car.testSteer=0;rb.position=pos;rb.rotation=rot;car.transform.SetPositionAndRotation(pos,rot);rb.linearVelocity=velocity;rb.angularVelocity=angular;Physics.simulationMode=mode;EditorApplication.isPaused=paused;}
 }
}
