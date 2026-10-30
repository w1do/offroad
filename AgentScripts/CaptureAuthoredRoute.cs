using System.IO;
using UnityEngine;
public static class CaptureAuthoredRoute
{
 public static string Main(){var route=UnityEngine.Object.FindAnyObjectByType<AuthoredForestRoute>();var c=Camera.main;var p=c.transform.position;var q=c.transform.rotation;var target=c.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(1280,800,24);Directory.CreateDirectory("work");try{for(int a=0;a<2;a++){var arm=route.arms[a];Vector3 forward=Quaternion.Euler(0,arm.yaw,0)*Vector3.forward;c.transform.position=arm.entrance-forward*18+Vector3.up*5;c.transform.LookAt(arm.entrance+forward*35+Vector3.up*1.5f);c.targetTexture=rt;c.Render();RenderTexture.active=rt;var t=new Texture2D(1280,800,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1280,800),0,0);t.Apply();File.WriteAllBytes("work/AuthoredForestJoin"+a+".png",t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);}return "Captured both original-model road continuations";}finally{c.transform.SetPositionAndRotation(p,q);c.targetTexture=target;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);}}
}
