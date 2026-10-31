using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
public static class ApplyMuddyForestFloor{
 static void AfterStop(PlayModeStateChange state){if(state!=PlayModeStateChange.EnteredEditMode)return;EditorApplication.playModeStateChanged-=AfterStop;try{Debug.Log("Muddy forest: "+Main());}catch(Exception e){Debug.LogException(e);}}
 public static object Main(){if(Application.isPlaying){EditorApplication.playModeStateChanged+=AfterStop;EditorApplication.isPaused=false;EditorApplication.isPlaying=false;return new{queued=true};}
 var scene=SceneManager.GetActiveScene();var root=GameObject.Find("Soft forest floor - wheel ruts");if(scene.path!="Assets/MountainDrive/MountainPass.unity"||root==null)throw new Exception("Expected scene with soft forest");
 const string folder="Assets/Game/Generated/MuddyForestFloor";const string maps="Assets/Game/Art/MuddyForestFloor/";Directory.CreateDirectory(folder);AssetDatabase.Refresh();const string backup="Assets/Game/Scenes/BeforeMuddyForestFloor.unity";if(!File.Exists(backup)&&!EditorSceneManager.SaveScene(scene,backup,true))throw new Exception("Backup failed");
 foreach(var file in Directory.GetFiles(maps,"*.png")){var ti=(TextureImporter)AssetImporter.GetAtPath(file.Replace('\\','/'));ti.textureType=file.Contains("normal")?TextureImporterType.NormalMap:TextureImporterType.Default;ti.sRGBTexture=file.Contains("albedo");ti.wrapMode=TextureWrapMode.Repeat;ti.anisoLevel=8;ti.SaveAndReimport();}
 var mat=AssetDatabase.LoadAssetAtPath<Material>(folder+"/Muddy forest.mat");if(mat==null){mat=new Material(Shader.Find("Game/Living Forest"));AssetDatabase.CreateAsset(mat,folder+"/Muddy forest.mat");}
 mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(maps+"muddyleaves_0004_albedo.png"));mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(maps+"muddyleaves_0003_normal.png"));mat.SetTexture("_RoughnessMap",AssetDatabase.LoadAssetAtPath<Texture2D>(maps+"muddyleaves_0001_roughness.png"));mat.SetFloat("_UseRoughness",1);mat.SetFloat("_BumpScale",.8f);mat.SetFloat("_SnowAmount",0);mat.SetFloat("_Cull",2);mat.SetTextureScale("_BaseMap",new Vector2(40,40));
 var model=AssetDatabase.LoadAssetAtPath<SoilProfile>(folder+"/Wet forest soil.asset");if(model==null){model=ScriptableObject.CreateInstance<SoilProfile>();AssetDatabase.CreateAsset(model,folder+"/Wet forest soil.asset");}
 model.soilType=SoilType.DeepMud;model.moisture=.9f;model.waterContent=.8f;model.compaction=.1f;model.bearingStrengthPascal=18000;model.cohesionPascal=1500;model.deformationResistancePascal=180000;model.rutDepthLimitMetres=.22f;model.longitudinalGrip=.12f;model.lateralGrip=.28f;model.rollingResistance=.45f;model.recoveryMetresPerSecond=0;
 var profile=AssetDatabase.LoadAssetAtPath<SoftGroundProfile>("Assets/Game/Generated/SoftForestFloor/Forest floor soil.asset");profile.soil=model;profile.maximumDepthMetres=.22f;profile.settlementDepthMetres=.12f;profile.rollingCompactionMetresPerSecond=.065f;profile.diggingMetresPerSecond=.09f;profile.maximumBermHeightMetres=.035f;
 foreach(var r in root.GetComponentsInChildren<MeshRenderer>())r.sharedMaterial=mat;
 foreach(var soil in root.GetComponentsInChildren<SoftGroundRuts>()){soil.maximumDepth=.22f;var surface=soil.GetComponent<ForestDrivingSurface>();surface.forwardGrip=.65f;surface.lateralGrip=.70f;surface.rollingResistance=.06f;}
 EditorUtility.SetDirty(profile);EditorUtility.SetDirty(model);EditorUtility.SetDirty(mat);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Save failed");return new{success=true,patches=root.GetComponentsInChildren<SoftGroundRuts>().Length,depth=.22f,resistance=.45f};
 }
}
