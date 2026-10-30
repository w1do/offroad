using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
public static class ValidateOriginalForestGeometry
{
 public static string Main(){if(Application.isPlaying)throw new Exception("Stop Play for source mesh comparison");var forest=GameObject.Find("Forest - imported dirt road environment");var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Art/Forest/Dirt road scene.fbx").GetComponentsInChildren<MeshFilter>().ToDictionary(f=>f.name);int count=0,vertices=0;
 foreach(var f in forest.GetComponentsInChildren<MeshFilter>()){var original=source[f.name].sharedMesh;if(!f.sharedMesh.vertices.SequenceEqual(original.vertices)||!f.sharedMesh.triangles.SequenceEqual(original.triangles))throw new Exception("Original forest geometry changed: "+f.name);if(!f.GetComponent<Renderer>().enabled)throw new Exception("Original renderer hidden "+f.name);count++;vertices+=original.vertexCount;}
 var car=UnityEngine.Object.FindAnyObjectByType<ForestKamazController>();if(car.wheels.Length!=4)throw new Exception("Wrong wheels");var fixedModel=car.transform.Find("Original Pajero model - fixed chassis");var fixedRotation=fixedModel.localRotation;float wobble=0;foreach(var hub in car.visuals){var r=hub.GetComponentsInChildren<MeshRenderer>().Where(x=>!x.name.Contains("Chain")).ToArray();var initial=r.Select(x=>x.bounds.center).ToArray();var q=hub.localRotation;hub.localRotation=Quaternion.Euler(90,0,0);for(int i=0;i<r.Length;i++)wobble=Mathf.Max(wobble,Vector3.Distance(initial[i],r[i].bounds.center));hub.localRotation=q;}if(wobble>.025f||fixedModel.localRotation!=fixedRotation)throw new Exception("Wheel pivot error "+wobble);
 return "PASS "+count+" forest meshes / "+vertices+" vertices exactly match original supplied FBX geometry; all original renderers visible. Pajero hub wobble="+wobble.ToString("F4")+"m; chassis remains fixed.";
 }
}
