using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
public static class ValidateRuntimeResourceCycles
{
    public static object Main()
    {
        if(!Application.isPlaying)throw new Exception("Requires Play mode");
        var errors=new List<string>();Application.LogCallback onLog=(message,trace,type)=>{if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message);};
        Application.logMessageReceived+=onLog;
        try{
        var route=UnityEngine.Object.FindAnyObjectByType<AuthoredForestRoute>();var source=UnityEngine.Object.FindAnyObjectByType<VehiclePhysics>();
        string historyPrefix="test/memory-cycles/"+Guid.NewGuid().ToString("N")+"/";
        var owned=new List<UnityEngine.Object>();
        var generatedRoot=new GameObject("Memory audit generated mesh owner");var generatedMesh=new Mesh{name="Memory audit owned mesh"};generatedRoot.AddComponent<RuntimeMeshOwner>().Bind(generatedMesh);owned.Add(generatedMesh);UnityEngine.Object.DestroyImmediate(generatedRoot);
        var clock=System.Diagnostics.Stopwatch.StartNew();
        for(int pass=0;pass<3;pass++){
            GameObject block=null,staging=null,car=null;
            try{
                block=route.CreateBlock(0,1000);
                var testSoils=block.GetComponentsInChildren<SoftGroundRuts>();for(int s=0;s<testSoils.Length;s++)testSoils[s].BindPersistence(historyPrefix+s);
                foreach(var soil in block.GetComponentsInChildren<SoftGroundRuts>()){
                    var mesh=soil.GetComponent<MeshFilter>().sharedMesh;var collider=soil.GetComponent<MeshCollider>().sharedMesh;
                    if(mesh!=null&&mesh.name=="Deformable visible mud")owned.Add(mesh);
                    if(collider!=null&&collider.name=="Matching mud collision")owned.Add(collider);
                }
                var patch=block.GetComponentsInChildren<SoftGroundRuts>().First(s=>s.profile!=null);
                var centre=patch.GetComponent<Renderer>().bounds.center;patch.Dig(centre,1,.5f);patch.Commit();
                var tracks=patch.GetComponent<TireTrackRenderer>();if(tracks!=null){tracks.Stamp(centre,patch.transform.forward,.4f,.8f,0,.16f,.8f);var mask=(Texture2D)typeof(TireTrackRenderer).GetField("mask",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(tracks);if(mask!=null)owned.Add(mask);}
                staging=new GameObject("Memory audit staging");staging.SetActive(false);
                car=UnityEngine.Object.Instantiate(source.gameObject,staging.transform);car.SetActive(false);car.transform.SetParent(null);car.transform.position=source.transform.position+Vector3.up*200;
                car.GetComponent<VehiclePhysics>().automaticSimulation=false;car.GetComponent<VehiclePhysics>().externalControl=true;
                foreach(var audio in car.GetComponentsInChildren<AudioSource>(true))audio.enabled=false;
                car.GetComponent<VehicleAudio>().enabled=false;car.GetComponent<VehiclePlayerInput>().enabled=false;
                car.SetActive(true);
                foreach(var filter in car.GetComponentsInChildren<MeshFilter>(true))if(filter.sharedMesh!=null&&filter.sharedMesh.name.Contains("(loaded tyre instance)"))owned.Add(filter.sharedMesh);
                foreach(var ripple in car.GetComponents<WaterRippleRenderer>()){var mesh=(Mesh)typeof(WaterRippleRenderer).GetField("mesh",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(ripple);if(mesh!=null)owned.Add(mesh);}
                foreach(var effects in car.GetComponents<VehicleWheelEffects>()){var mesh=(Mesh)typeof(VehicleWheelEffects).GetField("markMesh",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(effects);if(mesh!=null)owned.Add(mesh);}
            }finally{if(car!=null)UnityEngine.Object.DestroyImmediate(car);if(staging!=null)UnityEngine.Object.DestroyImmediate(staging);if(block!=null)UnityEngine.Object.DestroyImmediate(block);}
        }
        // Destruction of owned native resources is deferred until the frame ends.
        foreach(var type in new[]{typeof(SoftGroundRuts),typeof(TireTrackRenderer)}){
            var cache=(System.Collections.IDictionary)type.GetField("retained",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).GetValue(null);
            foreach(var key in cache.Keys.Cast<string>().Where(k=>k.StartsWith(historyPrefix)).ToArray())cache.Remove(key);
        }
        if(errors.Count>0)throw new Exception("Lifecycle logged errors: "+string.Join("; ",errors.Distinct()));
        var ids=owned.Where(o=>o!=null).Select(o=>o.GetEntityId().ToString()).Distinct().ToArray();
        var report=new {cycles=3,trackedNativeObjects=owned.Count,pendingNativeIds=ids,wallMilliseconds=clock.Elapsed.TotalMilliseconds};
        System.IO.File.WriteAllText("work/memory-cycle-pending.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
        return report;
        }finally{Application.logMessageReceived-=onLog;}
    }
    public static object Complete()
    {
        var saved=Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText("work/memory-cycle-pending.json"));
        var ids=new HashSet<string>(saved["pendingNativeIds"].ToObject<string[]>());
        var survivors=Resources.FindObjectsOfTypeAll<UnityEngine.Object>().Where(o=>ids.Contains(o.GetEntityId().ToString())).Select(o=>new {o.name,type=o.GetType().Name}).ToArray();
        var report=new {passed=survivors.Length==0,cycles=(int)saved["cycles"],trackedNativeObjects=(int)saved["trackedNativeObjects"],survivors,retainedSoilBytes=SoftGroundRuts.RetainedBytes,retainedTrackBytes=TireTrackRenderer.RetainedBytes,note="Checks owned meshes/textures after deferred destruction; intentional run history remains."};
        System.IO.File.WriteAllText("docs/reports/runtime-memory-cycles.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
        if(survivors.Length>0)throw new Exception(Newtonsoft.Json.JsonConvert.SerializeObject(report));return report;
    }
}
