using System.IO;
using UnityEngine;
public static class CaptureRoadBlockage{
 public static object Main(){
 var controller=Object.FindAnyObjectByType<ForestRoadBlockages>();int seed=controller.SessionSeed;controller.Generate(7);var obstacle=controller.GetComponentsInChildren<FallenTreeObstacle>()[0];var site=controller.sites[int.Parse(obstacle.name.Substring("Fallen tree at site ".Length))];var go=new GameObject("Road preview camera");var cam=go.AddComponent<Camera>();var rt=new RenderTexture(1200,800,24);var tex=new Texture2D(1200,800,TextureFormat.RGB24,false);var previous=RenderTexture.active;
 try{go.transform.position=site.centre-site.forward*10+Vector3.up*2.8f;go.transform.LookAt(site.centre+Vector3.up*.8f);cam.farClipPlane=300;cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1200,800),0,0);tex.Apply();Directory.CreateDirectory("work");File.WriteAllBytes("work/random-road-blockage.png",tex.EncodeToPNG());return new{success=true};}
 finally{controller.Generate(seed);RenderTexture.active=previous;cam.targetTexture=null;Object.DestroyImmediate(go);Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);}
 }
}





