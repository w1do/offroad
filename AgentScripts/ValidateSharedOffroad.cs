using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
public static class ValidateSharedOffroad
{
 public static string Main(){if(!Application.isPlaying)throw new Exception("Requires Play");var v=UnityEngine.Object.FindAnyObjectByType<VehiclePhysics>();var body=v.GetComponent<Rigidbody>();var p=body.position;var q=body.rotation;var mode=Physics.simulationMode;bool paused=EditorApplication.isPaused,automatic=v.automaticSimulation,external=v.externalControl;int passed=0;
 try{Physics.simulationMode=SimulationMode.Script;EditorApplication.isPaused=true;v.automaticSimulation=false;v.externalControl=true;
 foreach(var point in new[]{new Vector3(-20,0,42),new Vector3(-20,0,34),new Vector3(-30,0,40),new Vector3(41.31f,0,96.23f),new Vector3(-70,0,32),new Vector3(75,0,0),new Vector3(-50,0,-20),new Vector3(12,0,50)}){var hits=Ground(point);if(hits.Length==0)throw new Exception("Visible ground missing at "+point);var pos=hits[0].point+Vector3.up*1;body.position=pos;body.rotation=Quaternion.identity;v.transform.SetPositionAndRotation(pos,body.rotation);body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;v.ResetState();v.SetInput(new VehicleControlInput{brake=1});Physics.SyncTransforms();int contacts=0;for(int i=0;i<700;i++){v.Simulate(.01f);Physics.Simulate(.01f);v.SendMessage("LateUpdate");contacts=Math.Max(contacts,v.Wheels.Count(w=>w.isGrounded));var ground=Ground(body.position);if(ground.Length>0&&body.position.y<ground[0].point.y-1.1f)throw new Exception("Body beneath visible terrain at "+body.position);}if(contacts<2)throw new Exception("No tyre contact at "+point);passed++;}
 return "PASS shared physics: "+passed+" road/shoulder/forest fall-and-brake cases, including reported underground coordinate, with exact ground skins; no below-ground driving.";
 }finally{body.position=p;body.rotation=q;v.transform.SetPositionAndRotation(p,q);body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;v.SetInput(default);v.ResetState();v.automaticSimulation=automatic;v.externalControl=external;Physics.simulationMode=mode;EditorApplication.isPaused=paused;}
 }
 static RaycastHit[] Ground(Vector3 p)=>Physics.RaycastAll(new Vector3(p.x,200,p.z),Vector3.down,500,~0,QueryTriggerInteraction.Ignore).Where(h=>h.collider.GetComponent<ForestGroundPatch>()!=null).OrderBy(h=>h.distance).ToArray();
}
