using System;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;
public static class ValidateTyreShapePerformance
{
    public static object Main()
    {
        var v=UnityEngine.Object.FindAnyObjectByType<VehiclePhysics>();
        if(v==null||!Application.isPlaying)throw new Exception("Requires active vehicle in Play mode");
        var p=v.GetComponent<VehicleTyrePressure>();
        var update=typeof(VehicleTyrePressure).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
        update.Invoke(p,null);
        var shapes=(TyreWheelShape[])typeof(VehicleTyrePressure).GetField("shapes",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(p);
        var drawn=typeof(TyreWheelShape).GetField("drawn",BindingFlags.Instance|BindingFlags.NonPublic);
        var timer=Stopwatch.StartNew();
        for(int i=0;i<60;i++)update.Invoke(p,null);
        double unchanged=timer.Elapsed.TotalMilliseconds/60;
        timer.Restart();
        for(int i=0;i<60;i++){foreach(var shape in shapes)drawn.SetValue(shape,false);update.Invoke(p,null);}
        double changing=timer.Elapsed.TotalMilliseconds/60;timer.Stop();
        var result=new { dateUtc=DateTime.UtcNow.ToString("O"), unchangedMilliseconds=unchanged, forcedUpdateMilliseconds=changing, baselineMilliseconds=44.7533, meshCount=p.DeformedMeshCount, note="Synchronous local microbenchmark; not full-game FPS. Forced update includes mesh uploads, skips disabled renderers." };
        System.IO.File.WriteAllText("docs/reports/tyre-shape-performance.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
        return result;
    }
}
