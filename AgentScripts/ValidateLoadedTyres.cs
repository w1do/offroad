using System;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class ValidateLoadedTyres {
public static string Main(){
 var source=UnityEngine.Object.FindAnyObjectByType<VehiclePhysics>();
 var scene=SceneManager.CreateScene("Loaded tyres "+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
 try{
 var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(floor,scene);floor.transform.position=new Vector3(0,-.5f,0);floor.transform.localScale=new Vector3(30,1,30);
 var clone=UnityEngine.Object.Instantiate(source.gameObject,new Vector3(0,1,0),Quaternion.identity);SceneManager.MoveGameObjectToScene(clone,scene);
 var v=clone.GetComponent<VehiclePhysics>();v.automaticSimulation=false;v.externalControl=true;clone.GetComponent<VehiclePlayerInput>().enabled=false;v.Initialize();v.SetNeutral(true);v.SetInput(new VehicleControlInput{handbrake=true,brake=1});
 var p=clone.GetComponent<VehicleTyrePressure>();var physics=scene.GetPhysicsScene();
 Action settle=()=>{for(int i=0;i<400;i++){v.Simulate(.01f);physics.Simulate(.01f);}};settle();
 float road=v.Wheels[0].radius;float height=clone.transform.position.y;
 if(!p.RequestPressure(v.profile.equipment.minimumTyrePressureBar))throw new Exception(p.Message);p.Advance(100);settle();
 float low=v.Wheels[0].radius;float lowHeight=clone.transform.position.y;
 var root=v.axles[0].leftVisual;
 var filters=root.GetComponentsInChildren<MeshFilter>();var before=new Vector3[filters.Length][];for(int i=0;i<filters.Length;i++)before[i]=filters[i].sharedMesh.vertices;
v.GetWheelPose(0,out var hub,out var rotation);root.SetPositionAndRotation(hub,rotation);
 typeof(VehicleTyrePressure).GetMethod("LateUpdate",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(p,null);
 foreach(var f in root.GetComponentsInChildren<MeshFilter>()){var a=f.sharedMesh.vertices;foreach(var x in a)if(float.IsNaN(x.x))throw new Exception("NaN mesh");}
 float biggestChange=0;int changed=0;for(int j=0;j<filters.Length;j++){var after=filters[j].sharedMesh.vertices;for(int k=0;k<after.Length;k++){float change=(after[k]-before[j][k]).magnitude;biggestChange=Mathf.Max(biggestChange,change);if(change>.00001f)changed++;
 float radial=Vector3.ProjectOnPlane(filters[j].transform.TransformPoint(before[j][k])-hub,rotation*Vector3.right).magnitude;
 if(radial<v.profile.axles[0].tire.radiusMetres*v.profile.equipment.rigidRimRadiusFraction-.001f&&change>.00001f)throw new Exception("Rigid rim deformed");}}
 if(changed==0)throw new Exception("No visible tyre deformation");
 var cameraObject=new GameObject("Tyre inspection camera");SceneManager.MoveGameObjectToScene(cameraObject,scene);var camera=cameraObject.AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.gray;
 camera.transform.position=hub+v.transform.right*(v.transform.InverseTransformPoint(hub).x<0?-1.8f:1.8f)+v.transform.forward*.65f+Vector3.up*.12f;camera.transform.LookAt(hub);camera.fieldOfView=35;
 var rt=new RenderTexture(800,600,24);camera.targetTexture=rt;camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(800,600,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,800,600),0,0);image.Apply();System.IO.File.WriteAllBytes("work/deflated-tyre.png",image.EncodeToPNG());RenderTexture.active=old;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);
 if(low>=road-.005f||lowHeight>=height-.003f||p.DeformedMeshCount==0)throw new Exception("No loaded radius/body response "+road+" / "+low+" height "+height+" / "+lowHeight);
 if(!p.RequestPressure(v.profile.equipment.roadPressureBar))throw new Exception(p.Message);p.Advance(100);settle();float restored=v.Wheels[0].radius;
 if(Mathf.Abs(restored-road)>.006f)throw new Exception("Radius failed to recover");
 string result=$"PASS road radius={road:F4}, deflated={low:F4}, inflated={restored:F4} m; body drop={height-lowHeight:F4} m; meshes={p.DeformedMeshCount}; changed vertices={changed}; max local change={biggestChange:F4}; grip={p.SoftGripMultiplier:F2}";
 System.IO.File.WriteAllText("docs/reports/loaded-tyres.txt",result);return result;
 }finally{foreach(var root in scene.GetRootGameObjects())UnityEngine.Object.DestroyImmediate(root);SceneManager.UnloadSceneAsync(scene);}
}}
