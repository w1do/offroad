using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
public static class TuneProgressiveForestSoil{
 static void AfterStop(PlayModeStateChange state){if(state!=PlayModeStateChange.EnteredEditMode)return;EditorApplication.playModeStateChanged-=AfterStop;try{Debug.Log("Progressive forest: "+Main());}catch(Exception e){Debug.LogException(e);}}
 public static object Main(){if(Application.isPlaying){EditorApplication.playModeStateChanged+=AfterStop;EditorApplication.isPaused=false;EditorApplication.isPlaying=false;return new{queued=true};}
 var scene=SceneManager.GetActiveScene();var root=GameObject.Find("Soft forest floor - wheel ruts");if(scene.path!="Assets/MountainDrive/MountainPass.unity"||root==null)throw new Exception("Expected forest scene");
 const string backup="Assets/Game/Scenes/BeforeProgressiveForestSoil.unity";if(!File.Exists(backup)&&!EditorSceneManager.SaveScene(scene,backup,true))throw new Exception("Backup failed");
 var model=AssetDatabase.LoadAssetAtPath<SoilProfile>("Assets/Game/Generated/MuddyForestFloor/Wet forest soil.asset");var profile=AssetDatabase.LoadAssetAtPath<SoftGroundProfile>("Assets/Game/Generated/SoftForestFloor/Forest floor soil.asset");
 model.soilType=SoilType.WetDirt;model.moisture=.75f;model.waterContent=.6f;model.compaction=.15f;model.bearingStrengthPascal=45000;model.cohesionPascal=3500;model.deformationResistancePascal=1450000;model.rutDepthLimitMetres=.09f;model.rollingResistance=.05f;model.viscousResistanceSecondsPerMetre=.25f;model.longitudinalGrip=.20f;model.lateralGrip=.32f;
 profile.maximumDepthMetres=.09f;profile.settlementDepthMetres=.035f;profile.rollingCompactionMetresPerSecond=.07f;profile.diggingMetresPerSecond=.075f;profile.maximumBermHeightMetres=.02f;
 foreach(var soil in root.GetComponentsInChildren<SoftGroundRuts>()){soil.maximumDepth=.09f;var surface=soil.GetComponent<ForestDrivingSurface>();surface.forwardGrip=.80f;surface.lateralGrip=.75f;surface.rollingResistance=.035f;}
 EditorUtility.SetDirty(model);EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Save failed");return new{success=true,maximumRut=.09f,resistance=model.rollingResistance,viscosity=model.viscousResistanceSecondsPerMetre,patches=root.GetComponentsInChildren<SoftGroundRuts>().Length};
 }
}
