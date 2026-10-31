using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
public static class BuildEmptyGround
{
 public static object Main()
 {
  if(Application.isPlaying)throw new Exception("Stop Play mode first");
  var scene=SceneManager.GetActiveScene();
  var car=UnityEngine.Object.FindAnyObjectByType<VehiclePhysics>();
  if(car==null||scene.path!="Assets/MountainDrive/MountainPass.unity")throw new Exception("Expected main scene and Pajero");
  const string backup="Assets/Game/Scenes/BeforeCustomGround.unity";
  if(!File.Exists(backup)&&!EditorSceneManager.SaveScene(scene,backup,true))throw new Exception("Backup failed");
  var camera=UnityEngine.Object.FindAnyObjectByType<DriveCamera>();
  var sun=scene.GetRootGameObjects().First(g=>g.name=="Sun");
  var weather=UnityEngine.Object.FindAnyObjectByType<ForestDayNightWeather>();
  // Retain the established environment controller and vehicle lighting references.
  foreach(var root in scene.GetRootGameObjects())
   if(root!=car.gameObject&&root!=camera.gameObject&&root!=sun&&(weather==null||root!=weather.gameObject))
    UnityEngine.Object.DestroyImmediate(root);
  const string folder="Assets/Game/Generated/CustomGround";
  Directory.CreateDirectory(folder);AssetDatabase.Refresh();
  var material=AssetDatabase.LoadAssetAtPath<Material>(folder+"/Gravel ground.mat");
  if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,folder+"/Gravel ground.mat");}
  const string maps="Assets/Game/Art/RoadScans/gravel-road-2/";
  material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(maps+"schotter2_diffuse.jpg"));
  material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(maps+"schotter2_normal.jpg"));
  material.EnableKeyword("_NORMALMAP");material.SetFloat("_BumpScale",.7f);material.SetFloat("_Smoothness",.12f);
  material.SetTextureScale("_BaseMap",new Vector2(20,20));material.SetTextureScale("_BumpMap",new Vector2(20,20));EditorUtility.SetDirty(material);
  var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.name="Ground - square 100 x 100 m";ground.transform.localScale=new Vector3(10,1,10);ground.GetComponent<MeshRenderer>().sharedMaterial=material;GameObjectUtility.SetStaticEditorFlags(ground,StaticEditorFlags.BatchingStatic);
  // Keep the ready-made scanned mesh available for the next terrain-building step.
  var source=AssetDatabase.LoadAssetAtPath<GameObject>(maps+"schotter2.obj");
  var sample=UnityEngine.Object.Instantiate(source);sample.name="Selected ground scan - gravel-road-2";
  foreach(var r in sample.GetComponentsInChildren<MeshRenderer>())r.sharedMaterial=material;
  PrefabUtility.SaveAsPrefabAsset(sample,folder+"/Selected gravel ground.prefab");UnityEngine.Object.DestroyImmediate(sample);
  car.transform.SetPositionAndRotation(new Vector3(0,.65f,0),Quaternion.identity);
  car.externalControl=false;
  camera.target=car.transform;camera.hood=false;camera.orbitYaw=20;camera.orbitPitch=20;camera.followDistance=10;
  camera.transform.position=new Vector3(-4,4,-9);camera.transform.LookAt(car.transform.position+Vector3.up);
  if(weather!=null){weather.observer=car.transform;weather.snowfall=null;weather.SetHour(12);weather.ApplyEnvironment();}
  Physics.SyncTransforms();
  var collider=ground.GetComponent<MeshCollider>();int hits=0;
  for(int x=-45;x<=45;x+=15)for(int z=-45;z<=45;z+=15){if(!collider.Raycast(new Ray(new Vector3(x,10,z),Vector3.down),out var hit,20)||Mathf.Abs(hit.point.y)>.001f)throw new Exception("Ground collision mismatch");hits++;}
  Selection.activeGameObject=ground;
  if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.LookAt(new Vector3(0,0,0),Quaternion.Euler(35,25,0),28);
  AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Scene save failed");
  return new {scene=scene.path,backup,ground="100 x 100 m, built-in Plane mesh, matching MeshCollider",selectedScan=maps+"schotter2.obj",rayChecks=hits,roots=scene.GetRootGameObjects().Select(g=>g.name).ToArray()};
 }
}
