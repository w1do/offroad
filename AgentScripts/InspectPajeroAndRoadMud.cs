using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
public static class InspectPajeroAndRoadMud
{
 public static void Capture(Vector3 position,Vector3 look,string path,int mask=-1){var c=Camera.main;var p=c.transform.position;var q=c.transform.rotation;var target=c.targetTexture;var active=RenderTexture.active;int oldMask=c.cullingMask;var rt=RenderTexture.GetTemporary(1280,800,24);try{c.cullingMask=mask;c.transform.position=position;c.transform.LookAt(look);c.targetTexture=rt;c.Render();RenderTexture.active=rt;var t=new Texture2D(1280,800,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1280,800),0,0);t.Apply();Directory.CreateDirectory("work");File.WriteAllBytes(path,t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);}finally{c.transform.SetPositionAndRotation(p,q);c.targetTexture=target;c.cullingMask=oldMask;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);}}
 public static string Main(){var car=UnityEngine.Object.FindAnyObjectByType<VehiclePhysics>();Capture(car.transform.position+car.transform.forward*7+car.transform.right*6+Vector3.up*3,car.transform.position+Vector3.up*.9f,"work/PajeroRestoredForest.png");
 var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Art/RoadMud/forest-puddle.fbx");var mud=(GameObject)PrefabUtility.InstantiatePrefab(source);mud.name="TEMP road mud inspection";mud.transform.position=new Vector3(1000,20,0);mud.transform.localScale*=.25f;foreach(var t in mud.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
 try{foreach(var r in mud.GetComponentsInChildren<MeshRenderer>()){var mats=r.sharedMaterials;for(int i=0;i<mats.Length;i++){string n=mats[i].name;int number=n.EndsWith("2")?2:n.EndsWith("1")?1:0;string suffix=number==0?"":number.ToString();var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/Art/RoadMud/forest-puddle"+suffix+".jpg"));m.SetFloat("_Smoothness",.45f);m.SetFloat("_Cull",0);mats[i]=m;}r.sharedMaterials=mats;}var bounds=mud.GetComponentInChildren<MeshRenderer>().bounds;Capture(bounds.center+new Vector3(5,4,5),bounds.center,"work/OriginalRoadMud.png",1<<31);return "Pajero and original mud preview captured; mud scaled bounds="+bounds;}
 finally{UnityEngine.Object.DestroyImmediate(mud);}
 }
}
