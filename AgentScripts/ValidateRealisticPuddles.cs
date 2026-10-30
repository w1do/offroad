using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

public static class ValidateRealisticPuddles
{
    public static string Main()
    {
        var source=UnityEngine.Object.FindAnyObjectByType<VehiclePhysics>();
        if(source==null)throw new Exception("Missing vehicle");
        var volumes=UnityEngine.Object.FindObjectsByType<WaterVolume>().Where(w=>w.name=="Basin puddles").ToArray();
        int checkedPoints=0;float smallest=float.MaxValue,largest=0;
        foreach(var volume in volumes)
        {
            volume.BuildSurface(); var mesh=volume.surfaceMesh.sharedMesh;var v=mesh.vertices;var t=mesh.triangles;
            if(t.Length==0)continue;
            var b=volume.surfaceMesh.GetComponent<Renderer>().bounds;
            float area=0;
            for(int j=0;j<t.Length;j+=3)area+=Vector3.Cross(volume.surfaceMesh.transform.TransformVector(v[t[j+1]]-v[t[j]]),volume.surfaceMesh.transform.TransformVector(v[t[j+2]]-v[t[j]])).magnitude*.5f;
            smallest=Mathf.Min(smallest,area);largest=Mathf.Max(largest,area);
            for(int i=0;i<t.Length;i+=Mathf.Max(1,t.Length/90/3)*3)
            {
                Vector3 p=volume.surfaceMesh.transform.TransformPoint((v[t[i]]+v[t[i+1]]+v[t[i+2]])/3);
                if(Mathf.Abs(volume.Depth(p-Vector3.up*.02f)-.02f)>.004f)throw new Exception("Water physics differs from rendered surface at "+p+" depth="+volume.Depth(p-Vector3.up*.02f)+" local="+volume.transform.InverseTransformPoint(p)+" scale="+volume.transform.lossyScale+" box="+volume.GetComponent<BoxCollider>().size+" center="+volume.GetComponent<BoxCollider>().center+" enabled="+volume.GetComponent<BoxCollider>().enabled+" level="+volume.localSurfaceHeight+" rot="+volume.transform.eulerAngles+" mesh="+mesh.name);
                if(volume.Depth(p+Vector3.up*.02f)>0)throw new Exception("Water above rendered surface");
                checkedPoints++;
            }
            if(volume.GetComponents<Collider>().Any(c=>!c.isTrigger))throw new Exception("Solid invisible water support");
            var material=volume.surfaceMesh.GetComponent<Renderer>().sharedMaterial;
            if(ShaderUtil.ShaderHasError(material.shader))throw new Exception("Water shader compile error");
        }
        if(checkedPoints==0||largest<7||smallest>4)throw new Exception("Expected both small and large water basins; actual areas="+smallest+".."+largest);
        if(!Application.isPlaying)return "PASS surface samples="+checkedPoints+"; puddle bounding areas="+smallest+".."+largest+" m2; shader compiled";
        var fixture=new GameObject("Temporary splash validation volume");
        GameObject clone=null;
        try
        {
            fixture.transform.position=new Vector3(0,-10000,0);
            var box=fixture.AddComponent<BoxCollider>();box.size=new Vector3(40,8,40);box.isTrigger=true;
            var volume=fixture.AddComponent<WaterVolume>();volume.localSurfaceHeight=1;
            clone=UnityEngine.Object.Instantiate(source.gameObject,fixture.transform.position,Quaternion.identity);
            var vehicle=clone.GetComponent<VehiclePhysics>();vehicle.automaticSimulation=false;vehicle.externalControl=true;
            vehicle.Initialize();
            var body=clone.GetComponent<Rigidbody>();body.useGravity=false;
            var splash=clone.GetComponent<VehiclePuddleSplash>();splash.SendMessage("Start");
            body.linearVelocity=Vector3.zero;
            for(int i=0;i<10;i++)splash.SendMessage("FixedUpdate");
            if(splash.EmittedDroplets!=0)throw new Exception("Stationary splash");
            body.linearVelocity=Vector3.forward*6;
            for(int i=0;i<10;i++)splash.SendMessage("FixedUpdate");
            int moving=splash.EmittedDroplets;
            if(moving==0||splash.WetWheels!=vehicle.Wheels.Length)throw new Exception("No coasting splash");
            body.linearVelocity=Vector3.back*6;
            for(int i=0;i<10;i++)splash.SendMessage("FixedUpdate");
            if(splash.EmittedDroplets<=moving)throw new Exception("No reversing splash");
            vehicle.GetWheelPose(0,out var firstWheel,out _);
            box.center=fixture.transform.InverseTransformPoint(firstWheel);box.size=new Vector3(.3f,8,.3f);
            splash.SendMessage("FixedUpdate");
            if(splash.WetWheels!=1)throw new Exception("Expected only one wheel in the narrow puddle");
            fixture.SetActive(false);int before=splash.EmittedDroplets;
            for(int i=0;i<10;i++)splash.SendMessage("FixedUpdate");
            if(splash.EmittedDroplets!=before||splash.WetWheels!=0)throw new Exception("Spray on dry ground");
            string report="PASS: "+checkedPoints+" water surface probes; areas "+smallest+".."+largest+" m2; stopped=0; coasting="+moving+"; reverse, single wet wheel and dry-ground checks passed; wheels="+vehicle.Wheels.Length;
            File.WriteAllText("work/puddle-validation.txt",report);return report;
        }
        finally{if(clone!=null)UnityEngine.Object.DestroyImmediate(clone);UnityEngine.Object.DestroyImmediate(fixture);}
    }
}



