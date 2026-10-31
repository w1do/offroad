using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
public static class ConfigureRoadBlockages {
 static float Centre(float z){float s=Mathf.Sin(z*Mathf.PI/50);return 12*s*s*s;}
 public static object Main(){
 var scene=SceneManager.GetActiveScene();if(Application.isPlaying||scene.isDirty||scene.path!="Assets/MountainDrive/MountainPass.unity")throw new Exception("Expected saved MountainPass in Edit Mode");
 const string backup="Assets/Game/Scenes/BeforeRandomRoadBlockages.unity";if(!File.Exists(backup))EditorSceneManager.SaveScene(scene,backup,true);
 var root=GameObject.Find("Random road blockages");if(root==null)root=new GameObject("Random road blockages");var controller=root.GetComponent<ForestRoadBlockages>();if(controller==null)controller=root.AddComponent<ForestRoadBlockages>();
 controller.treePrefabs=new GameObject[4];string[] names={"Detailed Tree_Trunk_01","Detailed Tree_Trunk_01.001","Detailed Tree_Trunk_01.002","Detailed Tree_Trunk_02"};for(int i=0;i<4;i++){controller.treePrefabs[i]=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Generated/LowPolyForest/"+names[i]+".prefab");if(controller.treePrefabs[i]==null)throw new Exception("Missing prefab "+names[i]);}
 controller.sites=new ForestRoadBlockages.Site[8];for(int i=0;i<8;i++){float z=-42+i*12;controller.sites[i]=new ForestRoadBlockages.Site{centre=new Vector3(Centre(z),0,z),forward=new Vector3(Centre(z+.1f)-Centre(z-.1f),0,.2f).normalized};}
 var car=UnityEngine.Object.FindAnyObjectByType<VehiclePhysics>();if(car==null)throw new Exception("Missing active car");controller.vehicle=car.transform;controller.randomSeed=true;
 EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);return new{success=true,sites=8,maximumBlockages=controller.maximumBlockages,chancePerSite=controller.chancePerSite,spawnClearance=controller.spawnClearance};
 }
}
