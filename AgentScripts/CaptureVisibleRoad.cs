using System.IO;
using System.Linq;
using UnityEngine;
public static class CaptureVisibleRoad
{
 public static string Main(){
 var car=Object.FindAnyObjectByType<VehiclePhysics>();var camera=Camera.main;
 var water=Object.FindObjectsByType<WaterVolume>().Where(w=>w.name=="Basin puddles").OrderBy(w=>(w.transform.position-car.transform.position).sqrMagnitude).First();
 var centre=water.surfaceMesh.GetComponent<Renderer>().bounds.center;
 var pos=camera.transform.position;var rot=camera.transform.rotation;float fov=camera.fieldOfView;var target=camera.targetTexture;var active=RenderTexture.active;
 var rt=RenderTexture.GetTemporary(1440,900,24);
 try{
 camera.transform.position=centre-water.transform.forward*7+water.transform.right*3+Vector3.up*3.2f;camera.transform.LookAt(centre+Vector3.up*.2f);camera.fieldOfView=58;
 camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
 var texture=new Texture2D(1440,900,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1440,900),0,0);texture.Apply();File.WriteAllBytes("work/VisibleBrownMudPuddles.png",texture.EncodeToPNG());Object.DestroyImmediate(texture);
 return "Captured nearest road puddle at "+centre;
 }finally{camera.transform.SetPositionAndRotation(pos,rot);camera.fieldOfView=fov;camera.targetTexture=target;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);}
 }
}
