using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
public static class ConfigurePlantedForestWind
{
 public static object Main(){
 var scene=SceneManager.GetActiveScene();if(Application.isPlaying||scene.isDirty)throw new Exception("Expected saved Edit Mode scene");
 var root=GameObject.Find("Low poly forest - planted territory");if(root==null)throw new Exception("Missing planted forest");
 const string backup="Assets/Game/Scenes/BeforeForestWind.unity";if(!File.Exists(backup))EditorSceneManager.SaveScene(scene,backup,true);
 var shader=Shader.Find("Game/Living Forest");if(shader==null)throw new Exception("Missing wind shader");
 int count=0;
 foreach(Transform tree in root.transform){if(tree.GetComponent<CapsuleCollider>()==null)continue;
 foreach(var r in tree.GetComponentsInChildren<Renderer>()){
 var source=r.sharedMaterial;string path="Assets/Game/Generated/LowPolyForest/Animated "+source.name+".mat";
 var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(mat==null){mat=new Material(source);var texture=source.GetTexture("_BaseMap");float cutoff=source.GetFloat("_Cutoff");float clip=source.GetFloat("_AlphaClip");float cull=source.GetFloat("_Cull");mat.shader=shader;mat.SetTexture("_BaseMap",texture);mat.SetFloat("_AlphaClip",clip);mat.SetFloat("_Cutoff",cutoff);mat.SetFloat("_Cull",cull);mat.SetFloat("_SnowAmount",0);mat.SetFloat("_LocalWindAnchor",1);mat.SetFloat("_TreeSway",.055f);mat.SetFloat("_LeafFlutter",source.name.StartsWith("Trunk")?0:.045f);mat.SetFloat("_FoliageLight",.18f);AssetDatabase.CreateAsset(mat,path);}r.sharedMaterial=mat;
 GameObjectUtility.SetStaticEditorFlags(r.gameObject,0);
 }
 GameObjectUtility.SetStaticEditorFlags(tree.gameObject,0);
 if(tree.GetComponent<ForestTreeWind>()==null)tree.gameObject.AddComponent<ForestTreeWind>();count++;
 }
 var wind=UnityEngine.Object.FindAnyObjectByType<ForestWind>();if(wind==null)wind=root.AddComponent<ForestWind>();wind.strength=.85f;
 var audio=root.GetComponent<ForestWindAudio>();if(audio==null)audio=root.AddComponent<ForestWindAudio>();audio.wind=wind;audio.forestRoot=root.transform;
 AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
 return new{success=true,animatedTrees=count,windStrength=wind.strength,voiceLimit=4};
 }
}

