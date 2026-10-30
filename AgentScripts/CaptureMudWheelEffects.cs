using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

public static class CaptureMudWheelEffects
{
    public static string Main()
    {
        var car=UnityEngine.Object.FindAnyObjectByType<VehiclePhysics>();var effects=car.GetComponent<VehicleWheelEffects>();
        var camera=Camera.main;var position=camera.transform.position;var rotation=camera.transform.rotation;
        var target=camera.targetTexture;var active=RenderTexture.active;float fov=camera.fieldOfView;bool paused=EditorApplication.isPaused;
        var rt=RenderTexture.GetTemporary(1280,800,24);Texture2D texture=null;
        var systems=effects.GetComponentsInChildren<ParticleSystem>();
        try
        {
            EditorApplication.isPaused=true;
            int index=car.Wheels.Length-1;var wheel=car.Wheels[index];
            Vector3 point=wheel.transform.position-Vector3.up*wheel.radius;Vector3 normal=Vector3.up;
            if(wheel.GetGroundHit(out var hit)){point=hit.point;normal=hit.normal;}
            foreach(var ps in systems)ps.Clear();
            for(int n=0;n<25;n++)
            {
                effects.Spray(index,point,normal,car.transform.forward,16,1,.02f,true);
                foreach(var ps in systems)ps.Simulate(.02f,false,false);
            }
            camera.transform.position=point+car.transform.right*3.3f-car.transform.forward*3.5f+Vector3.up*1.5f;
            camera.transform.LookAt(point-car.transform.forward*.65f+Vector3.up*.45f);camera.fieldOfView=55;
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            texture=new Texture2D(1280,800,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,800),0,0);texture.Apply();
            File.WriteAllBytes("work/MudWheelEffects.png",texture.EncodeToPNG());return "work/MudWheelEffects.png";
        }
        finally
        {
            camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fov;camera.targetTexture=target;
            RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);if(texture!=null)UnityEngine.Object.DestroyImmediate(texture);
            foreach(var ps in systems)ps.Clear();EditorApplication.isPaused=paused;
        }
    }
}
