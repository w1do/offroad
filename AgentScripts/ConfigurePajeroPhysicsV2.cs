using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class ConfigurePajeroPhysicsV2
{
 public static string Main(){if(Application.isPlaying)throw new Exception("Stop Play first");var car=UnityEngine.Object.FindAnyObjectByType<ForestKamazController>();var profile=AssetDatabase.LoadAssetAtPath<VehiclePhysicsProfile>("Assets/Game/Generated/AuthoredForestPajero/Pajero driving profile.asset");if(profile==null){profile=ScriptableObject.CreateInstance<VehiclePhysicsProfile>();AssetDatabase.CreateAsset(profile,"Assets/Game/Generated/AuthoredForestPajero/Pajero driving profile.asset");}var drive=car.GetComponent<VehiclePhysics>();if(drive==null)drive=car.gameObject.AddComponent<VehiclePhysics>();drive.profile=profile;var rb=car.GetComponent<Rigidbody>();car.centreOfMass=new Vector3(0,.54f,-.03f);rb.centerOfMass=car.centreOfMass;rb.angularDamping=.32f;rb.solverIterations=12;rb.solverVelocityIterations=6;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
 foreach(var wheel in car.wheels){wheel.mass=30;wheel.suspensionDistance=.22f;wheel.forceAppPointDistance=.10f;var spring=wheel.suspensionSpring;spring.spring=38000;spring.damper=5800;spring.targetPosition=.45f;wheel.suspensionSpring=spring;wheel.ConfigureVehicleSubsteps(10,12,12);EditorUtility.SetDirty(wheel);}
 var importer=AssetImporter.GetAtPath("Assets/Game/Art/RoadMud/forest-puddle.fbx") as ModelImporter;if(importer!=null&&!importer.isReadable){importer.isReadable=true;importer.SaveAndReimport();}
 foreach(var surface in UnityEngine.Object.FindObjectsByType<ForestDrivingSurface>())if(surface.cover==ForestDrivingSurface.Cover.Mud&&surface.GetComponent<MeshFilter>()!=null){if(surface.GetComponent<SoftGroundRuts>()==null)surface.gameObject.AddComponent<SoftGroundRuts>();surface.forwardGrip=.24f;surface.lateralGrip=.32f;surface.rollingResistance=.06f;EditorUtility.SetDirty(surface);}
 var audio=car.GetComponent<VehicleAudio>();audio.profile.idleRpm=850;audio.profile.maximumRpm=4500;audio.profile.engineVolume=.34f;EditorUtility.SetDirty(audio.profile);Time.fixedDeltaTime=.01f;
 EditorUtility.SetDirty(drive);EditorUtility.SetDirty(car);EditorUtility.SetDirty(rb);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());return "Pajero engine/flywheel/clutch + 5 manual gears + LOW/HIGH + progressive throttle; physical anti-roll on both axles; bounded local digging on added mud; 100Hz fixed simulation; original forest geometry unchanged.";
 }
}
