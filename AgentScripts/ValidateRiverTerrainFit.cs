using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class ValidateRiverTerrainFit
{
 public static string Main()
 {
  var rivers=UnityEngine.Object.FindObjectsByType<RiverCurrent>().OrderBy(r=>r.name).ToArray(); string result="";
  var camera=Camera.main;var position=camera.transform.position;var rotation=camera.transform.rotation;var target=camera.targetTexture;var active=RenderTexture.active;
  Directory.CreateDirectory("work");
  try {for(int k=0;k<rivers.Length;k++) {
   var r=rivers[k];var bed=r.transform.Find("Gravel ford bed - physical").GetComponent<MeshCollider>();float maxDepth=0;
   for(float x=r.MinimumAlong+2;x<r.MaximumAlong-2;x+=0.5f) {
    float y=r.SurfaceHeight(x);var p=r.transform.TransformPoint(new Vector3(x,y-r.transform.position.y+2,r.ChannelOffset(x)));
    if(!bed.Raycast(new Ray(p,Vector3.down),out var hit,4))throw new Exception("Missing river bed "+r.name+" x="+x);
    float depth=y-hit.point.y;if(depth<-.15f||depth>.8f)throw new Exception("Invalid bed depth="+depth+" x="+x);maxDepth=Mathf.Max(maxDepth,depth);
   }
   var mesh=r.transform.Find("Animated current surface").GetComponent<MeshFilter>().sharedMesh;
   foreach(var v in mesh.vertices)if(Mathf.Abs(r.transform.TransformPoint(v).y-r.SurfaceHeight(v.x))>.002f)throw new Exception("Visual/physical water mismatch");
   camera.transform.position=r.transform.TransformPoint(new Vector3((r.MinimumAlong+r.MaximumAlong)/2,20,-30));
   camera.transform.LookAt(r.transform.TransformPoint(new Vector3((r.MinimumAlong+r.MaximumAlong)/2,-3,0)));
   var rt=RenderTexture.GetTemporary(1280,720,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
   var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("work/RiverFull"+k+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);camera.targetTexture=target;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);
   result+=r.name+" max bed depth="+maxDepth.ToString("F2")+"m; ";
  }}finally {camera.targetTexture=target;RenderTexture.active=active;camera.transform.SetPositionAndRotation(position,rotation);}
  AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());return "PASS full-length bed support and matching water physics. "+result;
 }
}
