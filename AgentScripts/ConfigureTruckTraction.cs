using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class ConfigureTruckTraction
{
    public static string Main()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first.");
        var car=UnityEngine.Object.FindAnyObjectByType<ForestKamazController>();
        car.motorTorqueNm=2400;car.brakeTorqueNm=8500;car.dirtForwardGrip=0.65f;
        foreach(var surface in UnityEngine.Object.FindObjectsByType<ForestDrivingSurface>())surface.forwardGrip=0.12f;
        foreach(var wheel in car.wheels)
        {
            var f=wheel.forwardFriction;f.extremumSlip=0.08f;f.extremumValue=1;f.asymptoteSlip=0.4f;f.asymptoteValue=0.65f;f.stiffness=0.65f;wheel.forwardFriction=f;
        }
        var old=car.transform.Find("Brake lights");if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
        string path="Assets/Game/Generated/BrakeLamp.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
        material.EnableKeyword("_EMISSION");material.SetColor("_BaseColor",new Color(0.22f,0.01f,0.005f));material.SetColor("_EmissionColor",Color.black);EditorUtility.SetDirty(material);
        var root=new GameObject("Brake lights");root.transform.SetParent(car.transform,false);
        var brakes=root.AddComponent<TruckBrakeLights>();brakes.controller=car;brakes.lamps=new Renderer[2];brakes.lights=new Light[2];
        for(int i=0;i<2;i++)
        {
            var lamp=GameObject.CreatePrimitive(PrimitiveType.Cube);lamp.name=i==0?"Left stop lamp":"Right stop lamp";
            lamp.transform.SetParent(root.transform,false);lamp.transform.localPosition=new Vector3(i==0?-1.02f:1.02f,0.68f,-3.66f);lamp.transform.localScale=new Vector3(0.24f,0.12f,0.045f);
            UnityEngine.Object.DestroyImmediate(lamp.GetComponent<Collider>());brakes.lamps[i]=lamp.GetComponent<Renderer>();brakes.lamps[i].sharedMaterial=material;
            var glow=new GameObject("Red brake glow");glow.transform.SetParent(root.transform,false);glow.transform.localPosition=lamp.transform.localPosition+Vector3.back*0.08f;
            var light=glow.AddComponent<Light>();light.type=LightType.Point;light.color=Color.red;light.range=1.6f;light.intensity=2;light.enabled=false;brakes.lights[i]=light;
        }
        EditorUtility.SetDirty(car);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        if(!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))throw new Exception("Save failed.");
        return "Saved lower mud traction, wheel-speed power limit, stronger service brakes, forward/reverse selector, two brake lamps.";
    }
}
