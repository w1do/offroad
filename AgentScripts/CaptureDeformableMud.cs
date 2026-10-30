using System.IO;
using System.Linq;
using UnityEngine;
public static class CaptureDeformableMud {
public static string Main(){var soil=Object.FindObjectsByType<SoftGroundRuts>().First(s=>s.profile!=null&&s.persistentId=="main0/natural");var root=soil.transform.parent;Vector3 target=root.position+root.right*.7f;var camera=Camera.main;var pos=camera.transform.position;var rot=camera.transform.rotation;var rt=RenderTexture.GetTemporary(1280,800,24);var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;float fov=camera.fieldOfView;try{camera.transform.position=target+Vector3.up*2.6f+root.forward*2.8f+root.right*2.5f;camera.transform.LookAt(target);camera.fieldOfView=58;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var texture=new Texture2D(1280,800,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,800),0,0);texture.Apply();File.WriteAllBytes("work/DeformableMud.png",texture.EncodeToPNG());Object.DestroyImmediate(texture);return "work/DeformableMud.png";}finally{camera.transform.SetPositionAndRotation(pos,rot);camera.targetTexture=oldTarget;camera.fieldOfView=fov;RenderTexture.active=oldActive;RenderTexture.ReleaseTemporary(rt);}}}

