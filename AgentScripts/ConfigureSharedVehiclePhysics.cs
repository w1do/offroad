using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class ConfigureSharedVehiclePhysics
{
 public static string Main(){if(Application.isPlaying)throw new Exception("Stop Play first");var current=UnityEngine.Object.FindAnyObjectByType<VehiclePhysics>();var old=current!=null?current.GetComponent<ForestKamazController>():UnityEngine.Object.FindAnyObjectByType<ForestKamazController>();var car=current!=null?current.gameObject:old.gameObject;var physics=car.GetComponent<VehiclePhysics>();if(physics==null)physics=car.AddComponent<VehiclePhysics>();
 string previous="Assets/Game/Generated/AuthoredForestPajero/Pajero driving profile.asset",path="Assets/Game/Generated/AuthoredForestPajero/Pajero Sport 2005 physics.asset";var profile=AssetDatabase.LoadAssetAtPath<VehiclePhysicsProfile>(path);if(profile==null){profile=AssetDatabase.LoadAssetAtPath<VehiclePhysicsProfile>(previous);if(profile!=null)AssetDatabase.MoveAsset(previous,path);else{profile=ScriptableObject.CreateInstance<VehiclePhysicsProfile>();AssetDatabase.CreateAsset(profile,path);}}
 profile.displayName="MITSUBISHI PAJERO SPORT";profile.massKg=2050;profile.centreOfMass=new Vector3(0,.54f,-.03f);profile.maximumSpeedKmh=200;profile.reverseSpeedKmh=25;profile.ratedPowerKw=162;profile.ratedPowerRpm=6250;profile.peakTorqueNm=281;profile.peakTorqueRpm=4000;profile.idleRpm=850;profile.redlineRpm=6800;profile.engineInertia=.42f;
 float ratedTorque=162000/(6250*Mathf.PI/30);profile.torqueNm=new AnimationCurve(new Keyframe(600,90),new Keyframe(850,120),new Keyframe(2000,220),new Keyframe(4000,281),new Keyframe(5200,266),new Keyframe(6250,ratedTorque),new Keyframe(6600,220),new Keyframe(7000,0));
 profile.gears=new[]{4.4f,2.75f,1.8f,1.28f,1.0f};profile.finalDrive=4.65f;profile.reverseRatio=3.8f;profile.lowRatio=1.95f;profile.efficiency=.86f;profile.centreCoupling=150;profile.centreLockTorqueNm=5000;profile.clutchCapacityNm=390;profile.clutchCoupling=1.4f;profile.clutchFullRpm=2100;profile.engineFrictionNm=25;profile.engineViscousFriction=.10f;profile.idleGovernorNmPerRpm=1.2f;profile.aerodynamicDrag=.65f;
 profile.axles=new[]{new VehicleAxleSettings{name="Front axle",steering=true,driveShare=.38f,antiRollN=5000,lateralMultiplier=1,wheelspinLateralMultiplier=.78f},new VehicleAxleSettings{name="Rear axle",handbrake=true,chains=true,driveShare=.62f,antiRollN=4200,lateralMultiplier=.88f,wheelspinLateralMultiplier=.44f}};
 physics.profile=profile;if(old!=null)physics.axles=new[]{new VehicleAxleBinding{left=old.wheels[0],right=old.wheels[1],leftVisual=old.visuals[0],rightVisual=old.visuals[1]},new VehicleAxleBinding{left=old.wheels[2],right=old.wheels[3],leftVisual=old.visuals[2],rightVisual=old.visuals[3]}};physics.automaticSimulation=true;physics.externalControl=false;physics.Initialize();if(old!=null)old.enabled=false;
 var input=car.GetComponent<VehiclePlayerInput>();if(input==null)input=car.AddComponent<VehiclePlayerInput>();input.enabled=true;var audio=car.GetComponent<VehicleAudio>();audio.profile.idleRpm=850;audio.profile.maximumRpm=6800;EditorUtility.SetDirty(audio.profile);
 EditorUtility.SetDirty(profile);EditorUtility.SetDirty(physics);if(old!=null)EditorUtility.SetDirty(old);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
 return "Installed shared VehiclePhysics + independent VehiclePhysicsProfile + separate VehiclePlayerInput. Legacy physics disabled. Pajero: 200 km/h, "+profile.PowerKwAt(6250).ToString("F3")+" kW @6250rpm, "+profile.TorqueAt(4000).ToString("F3")+"Nm @4000rpm; configurable per-axle drive/brakes/steering/suspension. Profile="+path;
 }
}

