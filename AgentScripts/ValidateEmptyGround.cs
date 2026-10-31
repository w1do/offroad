using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
public static class ValidateEmptyGround {
 public static object Main(){
 var source=UnityEngine.Object.FindAnyObjectByType<VehiclePhysics>();
 var scene=SceneManager.CreateScene("Ground check",new CreateSceneParameters(LocalPhysicsMode.Physics3D));
 try{
 var floor=UnityEngine.Object.Instantiate(GameObject.Find("Ground - square 100 x 100 m"));SceneManager.MoveGameObjectToScene(floor,scene);
 var clone=UnityEngine.Object.Instantiate(source.gameObject,source.transform.position,source.transform.rotation);SceneManager.MoveGameObjectToScene(clone,scene);
 var v=clone.GetComponent<VehiclePhysics>();v.automaticSimulation=false;v.externalControl=true;clone.GetComponent<VehiclePlayerInput>().enabled=false;v.Initialize();v.SetNeutral(true);v.SetInput(new VehicleControlInput{handbrake=true,brake=1});
 var physics=scene.GetPhysicsScene();for(int i=0;i<300;i++){v.Simulate(.01f);physics.Simulate(.01f);}
 int grounded=v.Wheels.Count(w=>w.isGrounded);float y=clone.transform.position.y;if(grounded!=v.Wheels.Length||y<-.1f||float.IsNaN(y))throw new Exception("Vehicle failed to settle: "+grounded+" y="+y);
 return new {passed=true,seconds=3,grounded,total=v.Wheels.Length,height=y,speed=v.SpeedKmh};
 }finally{SceneManager.UnloadSceneAsync(scene);}
 }
}
