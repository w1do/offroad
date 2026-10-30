using System.IO;
using UnityEngine;
public static class CaptureSerpentine
{
 public static string Main(){var camera=Camera.main;var pos=camera.transform.position;var rot=camera.transform.rotation;var target=camera.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(1400,900,24);
 try{camera.transform.position=new Vector3(365,245,-45);camera.transform.LookAt(new Vector3(110,60,155));camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var t=new Texture2D(1400,900,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1400,900),0,0);t.Apply();Directory.CreateDirectory("work");File.WriteAllBytes("work/VisibleSerpentine.png",t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);return "Saved overview";}
 finally{camera.targetTexture=target;RenderTexture.active=active;camera.transform.SetPositionAndRotation(pos,rot);RenderTexture.ReleaseTemporary(rt);}
 }
}
