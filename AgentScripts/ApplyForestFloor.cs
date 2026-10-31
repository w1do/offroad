using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
public static class ApplyForestFloor
{
 static void AfterStop(PlayModeStateChange state){if(state!=PlayModeStateChange.EnteredEditMode)return;EditorApplication.playModeStateChanged-=AfterStop;try{Debug.Log("Forest floor: "+Main());}catch(Exception e){Debug.LogException(e);}}
 public static object Main(){
 if(Application.isPlaying){EditorApplication.playModeStateChanged+=AfterStop;EditorApplication.isPaused=false;EditorApplication.isPlaying=false;return new{queued=true};}
 var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/MountainDrive/MountainPass.unity")throw new Exception("Expected MountainPass");
 var ground=GameObject.Find("Ground - square 100 x 100 m");var road=GameObject.Find("Winding road - persistent wheel ruts");if(ground==null||road==null)throw new Exception("Ground or road missing");
 var roadMaterials=road.GetComponentsInChildren<MeshRenderer>().Select(r=>r.sharedMaterial).ToArray();var roadMeshes=road.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh).ToArray();
 const string folder="Assets/Game/Generated/ForestFloor";const string maps="Assets/Game/Art/ForestFloor/";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
 const string backup="Assets/Game/Scenes/BeforeForestFloor.unity";if(!File.Exists(backup)&&!EditorSceneManager.SaveScene(scene,backup,true))throw new Exception("Backup failed");
 foreach(var file in Directory.GetFiles(maps,"*.png")){
 var importer=(TextureImporter)AssetImporter.GetAtPath(file.Replace('\\','/'));importer.textureType=file.Contains("Normal")?TextureImporterType.NormalMap:TextureImporterType.Default;importer.sRGBTexture=file.Contains("Base_Color");importer.wrapMode=TextureWrapMode.Repeat;importer.anisoLevel=8;importer.SaveAndReimport();}
 var mat=AssetDatabase.LoadAssetAtPath<Material>(folder+"/Forest floor.mat");if(mat==null){mat=new Material(Shader.Find("Game/Living Forest"));AssetDatabase.CreateAsset(mat,folder+"/Forest floor.mat");}
 mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(maps+"DefaultMaterial_Base_Color_resized.png"));mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(maps+"DefaultMaterial_Normal_OpenGL_resized.png"));mat.SetTexture("_RoughnessMap",AssetDatabase.LoadAssetAtPath<Texture2D>(maps+"DefaultMaterial_Roughness_reized.png"));mat.SetFloat("_UseRoughness",1);mat.SetFloat("_BumpScale",.65f);mat.SetFloat("_SnowAmount",0);mat.SetFloat("_Cull",2);mat.SetTextureScale("_BaseMap",new Vector2(50,50));
 ground.GetComponent<MeshRenderer>().sharedMaterial=mat;
 if(!roadMaterials.SequenceEqual(road.GetComponentsInChildren<MeshRenderer>().Select(r=>r.sharedMaterial))||!roadMeshes.SequenceEqual(road.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh)))throw new Exception("Road changed");
 if(ground.GetComponent<MeshFilter>().sharedMesh!=ground.GetComponent<MeshCollider>().sharedMesh)throw new Exception("Ground mismatch");
 EditorUtility.SetDirty(mat);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Save failed");
 return new{success=true,material=mat.name,tileMetres=2,roadChunks=roadMeshes.Length,ruts=road.GetComponentsInChildren<SoftGroundRuts>().Length,tracks=road.GetComponentsInChildren<TireTrackRenderer>().Length};
 }
}
