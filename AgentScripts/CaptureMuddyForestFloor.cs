using System.IO;
using UnityEngine;
public static class CaptureMuddyForestFloor{
 public static object Main(){
 var go=new GameObject("Road preview camera");var cam=go.AddComponent<Camera>();var rt=new RenderTexture(1200,800,24);var tex=new Texture2D(1200,800,TextureFormat.RGB24,false);var previous=RenderTexture.active;
 try{go.transform.position=new Vector3(22,32,-35);go.transform.LookAt(new Vector3(0,0,8));cam.farClipPlane=300;cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1200,800),0,0);tex.Apply();Directory.CreateDirectory("work");File.WriteAllBytes("work/muddy-forest-preview.png",tex.EncodeToPNG());return new{success=true};}
 finally{RenderTexture.active=previous;cam.targetTexture=null;Object.DestroyImmediate(go);Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);}
 }
}

