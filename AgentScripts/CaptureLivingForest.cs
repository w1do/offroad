using System;
using System.IO;
using System.Linq;
using UnityEngine;
public static class CaptureLivingForest
{
    public static string Main()
    {
        var river=UnityEngine.Object.FindObjectsByType<RiverCurrent>().OrderBy(r=>r.name).First();var camera=Camera.main;
        var position=camera.transform.position;var rotation=camera.transform.rotation;var target=camera.targetTexture;var active=RenderTexture.active;
        var rt=RenderTexture.GetTemporary(1280,720,24);camera.transform.position=river.transform.TransformPoint(new Vector3(-7,6,-10));camera.transform.LookAt(river.transform.position+Vector3.up*0.5f);
        try
        {
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();
            string path="Assets/Game/Generated/LivingForest/RiverPreview.png";File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);return "Saved "+path;
        }
        finally{camera.targetTexture=target;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);camera.transform.SetPositionAndRotation(position,rotation);}
    }
}
