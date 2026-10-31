using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
public static class TuneDriveableForestSoil{
 static void AfterStop(PlayModeStateChange state){if(state!=PlayModeStateChange.EnteredEditMode)return;EditorApplication.playModeStateChanged-=AfterStop;try{Debug.Log("Driveable forest: "+Main());}catch(Exception e){Debug.LogException(e);}}
 public static object Main(){if(Application.isPlaying){EditorApplication.playModeStateChanged+=AfterStop;EditorApplication.isPaused=false;EditorApplication.isPlaying=false;return new{queued=true};}
 var scene=SceneManager.GetActiveScene();var root=GameObject.Find("Soft forest floor - wheel ruts");if(scene.path!="Assets/MountainDrive/MountainPass.unity"||root==null)throw new Exception("Expected forest scene");
 const string backup="Assets/Game/Scenes/BeforeDriveableForestSoil.unity";if(!File.Exists(backup)&&!EditorSceneManager.SaveScene(scene,backup,true))throw new Exception("Backup failed");
 var model=AssetDatabase.LoadAssetAtPath<SoilProfile>("Assets/Game/Generated/MuddyForestFloor/Wet forest soil.asset");var profile=AssetDatabase.LoadAssetAtPath<SoftGroundProfile>("Assets/Game/Generated/SoftForestFloor/Forest floor soil.asset");
 model.soilType=SoilType.WetDirt;model.moisture=.65f;model.waterContent=.45f;model.compaction=.2f;model.bearingStrengthPascal=60000;model.cohesionPascal=3500;model.deformationResistancePascal=1600000;model.rutDepthLimitMetres=.075f;model.rollingResistance=.09f;model.longitudinalGrip=.18f;model.lateralGrip=.32f;
 profile.maximumDepthMetres=.075f;profile.settlementDepthMetres=.035f;profile.rollingCompactionMetresPerSecond=.035f;profile.diggingMetresPerSecond=.055f;profile.maximumBermHeightMetres=.02f;
 foreach(var soil in root.GetComponentsInChildren<SoftGroundRuts>()){soil.maximumDepth=.075f;var surface=soil.GetComponent<ForestDrivingSurface>();surface.forwardGrip=.80f;surface.lateralGrip=.75f;surface.rollingResistance=.035f;}
 EditorUtility.SetDirty(model);EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Save failed");return new{success=true,maximumRut=.075f,resistance=.09f,patches=root.GetComponentsInChildren<SoftGroundRuts>().Length};
 }
}
