using System;using System.IO;using UnityEngine;using UnityEditor;
public static class CaptureExplorationWorld {
 public static object Main(){if(!Application.isPlaying)throw new Exception("Requires Play Mode");var world=UnityEngine.Object.FindAnyObjectByType<ExplorationWorld>();var player=world.vehicle;var body=player.GetComponent<Rigidbody>();bool kinematic=body.isKinematic,paused=EditorApplication.isPaused;var velocity=body.linearVelocity;var angular=body.angularVelocity;var observer=new GameObject("Exploration preview observer");var camera=observer.AddComponent<Camera>();var rt=new RenderTexture(1400,900,24);var texture=new Texture2D(1400,900,TextureFormat.RGB24,false);var previous=RenderTexture.active;
 try{EditorApplication.isPaused=true;body.isKinematic=true;camera.farClipPlane=2000;camera.targetTexture=rt;
 for(int shot=0;shot<2;shot++){
 if(shot==0){observer.transform.position=new Vector3(0,1.8f,9);observer.transform.LookAt(new Vector3(0,1.2f,45));}else{var p=world.Map.LakeCentre+Vector2.right*world.Map.LakeRadii.x*.98f;observer.transform.position=new Vector3(p.x,ExplorationMap.LakeLevel+4,p.y);observer.transform.LookAt(new Vector3(world.Map.LakeCentre.x,ExplorationMap.LakeLevel,world.Map.LakeCentre.y+world.Map.LakeRadii.y*.8f));}
 world.vehicle=observer.transform;world.EnsureWindow(true);world.GetComponentInChildren<ExplorationDistantForest>().SendMessage("Update");foreach(var detail in world.GetComponentsInChildren<ExplorationUnderstory>())detail.SendMessage("Update");Physics.SyncTransforms();camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1400,900),0,0);texture.Apply();File.WriteAllBytes(shot==0?"work/exploration-road-preview.png":"work/exploration-lake-preview.png",texture.EncodeToPNG());}
 return new{success=true};
 }finally{world.vehicle=player;world.EnsureWindow(true);body.isKinematic=kinematic;if(!kinematic){body.linearVelocity=velocity;body.angularVelocity=angular;}EditorApplication.isPaused=paused;RenderTexture.active=previous;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(observer);UnityEngine.Object.DestroyImmediate(texture);rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
 }
}




