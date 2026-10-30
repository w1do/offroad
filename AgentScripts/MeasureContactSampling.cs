using System;
using System.IO;
using System.Linq;
using UnityEngine;
public static class MeasureContactSampling
{
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Requires Play mode");
        var source=UnityEngine.Object.FindAnyObjectByType<VehiclePhysics>();
        var clone=UnityEngine.Object.Instantiate(source.gameObject,source.transform.position,source.transform.rotation);
        var car=clone.GetComponent<VehiclePhysics>();car.automaticSimulation=false;car.externalControl=true;
        var configuration=UnityEngine.Object.Instantiate(source.profile);car.profile=configuration;
        car.Initialize();Physics.SyncTransforms();
        try
        {
            var envelope=car.Wheels.Select(w=>w.GetComponent<WheelContactEnvelope>()).ToArray();
            double[] baseline=new double[80],previous=new double[80],arc=new double[80];
            for(int mode=0;mode<3;mode++)
            {
                configuration.rutContact.longitudinalSupport=mode!=0;
                configuration.rutContact.longitudinalSamples=mode==1?25:9;
                configuration.rutContact.adaptiveLongitudinalSampling=mode==2;
                configuration.rutContact.arcRefinementSteps=5;configuration.rutContact.arcRefinementHeightMetres=.01f;
                for(int iteration=-10;iteration<80;iteration++)
                {
                    var watch=System.Diagnostics.Stopwatch.StartNew();
                    for(int w=0;w<envelope.Length;w++)
                    {
                        var axle=configuration.axles[w/2];
                        envelope[w].Prepare(configuration.rutContact,axle.tire==null?axle.tyreWidthMetres:axle.tire.widthMetres);
                    }
                    watch.Stop();
                    foreach(var item in envelope)item.Restore();
                    if(iteration>=0)(mode==0?baseline:mode==1?previous:arc)[iteration]=watch.Elapsed.TotalMilliseconds;
                }
            }
            Array.Sort(baseline);Array.Sort(previous);Array.Sort(arc);
            var report=new{utc=DateTime.UtcNow.ToString("O"),position=source.transform.position.ToString("F3"),
                processor=SystemInfo.processorType,wheels=car.Wheels.Length,iterations=arc.Length,
                baselineMeanMs=baseline.Average(),previousGridMeanMs=previous.Average(),previousGridP95Ms=previous[75],
                arcMeanMs=arc.Average(),arcP95Ms=arc[75],samplingSpeedup=previous.Average()/arc.Average(),
                scope="Contact sampling only, current main scene position; not frame time or full-route performance"};
            string json=Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText("docs/reports/contact-sampling-performance.json",json);return json;
        }
        finally{UnityEngine.Object.DestroyImmediate(clone);UnityEngine.Object.DestroyImmediate(configuration);}
    }
}
