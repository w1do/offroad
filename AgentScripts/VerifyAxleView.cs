using System;
using System.IO;
using UnityEngine;

public static class VerifyAxleView
{
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Requires Edit mode.");
        var car=UnityEngine.Object.FindAnyObjectByType<ForestKamazController>();
        var camera=Camera.main;Vector3 pos=camera.transform.position;Quaternion rot=camera.transform.rotation;
        var rotations=new Quaternion[6];for(int i=0;i<6;i++)rotations[i]=car.visuals[i].localRotation;
        var fixedPart=car.transform.Find("Fixed axle details");var matrix=fixedPart.localToWorldMatrix;
        Directory.CreateDirectory("work");
        try
        {
            camera.transform.position=car.transform.TransformPoint(new Vector3(0,0.55f,7.5f));
            camera.transform.LookAt(car.transform.TransformPoint(new Vector3(0,0.65f,-0.3f)));
            for(int i=0;i<6;i++)car.visuals[i].localRotation=Quaternion.identity;
            Render(camera,"work/axle-before.png");
            for(int i=0;i<6;i++)car.visuals[i].localRotation=Quaternion.Euler(120,0,0);
            Render(camera,"work/axle-after.png");
            if(matrix!=fixedPart.localToWorldMatrix)throw new Exception("Fixed axle moved.");
            return "Saved before/after 120deg wheel rotation; axle hardware transform unchanged.";
        }
        finally
        {for(int i=0;i<6;i++)car.visuals[i].localRotation=rotations[i];camera.transform.SetPositionAndRotation(pos,rot);}
    }
    static void Render(Camera cam,string path)
    {
        var target=cam.targetTexture;var previous=RenderTexture.active;
        var rt=RenderTexture.GetTemporary(1200,700,24);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;
        var image=new Texture2D(1200,700,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1200,700),0,0);image.Apply();
        File.WriteAllBytes(path,image.EncodeToPNG());cam.targetTexture=target;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(image);
    }
}
