using UnityEngine;
using UnityEditor;
public static class InspectSUV {
 public static string Main(){
 var p="Assets/MountainDrive/Art/Kenney/Models/FBX format/suv.fbx";
 var model=AssetDatabase.LoadAssetAtPath<GameObject>(p);
 if(model==null)throw new System.Exception("Missing imported SUV");
 var s="";foreach(var t in model.GetComponentsInChildren<Transform>())s+=t.name+" "+t.localPosition+" scale="+t.localScale+"\n";
 foreach(var r in model.GetComponentsInChildren<Renderer>())s+="bounds="+r.bounds+" material="+r.sharedMaterial+"\n";
 return s;
 }
}
