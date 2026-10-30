using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
public static class ValidateForestRivers
{
    static void Place(Rigidbody body,Vector3 position,Quaternion rotation)
    {body.transform.SetPositionAndRotation(position,rotation);body.position=position;body.rotation=rotation;body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;Physics.SyncTransforms();}
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Requires Play mode.");
        var rivers=UnityEngine.Object.FindObjectsByType<RiverCurrent>().OrderBy(r=>r.name).ToArray();if(rivers.Length!=2)throw new Exception("Expected two river fords.");
        var oldMode=Physics.simulationMode;bool paused=EditorApplication.isPaused;Physics.simulationMode=SimulationMode.Script;EditorApplication.isPaused=true;
        var fixture=GameObject.CreatePrimitive(PrimitiveType.Cube);fixture.name="TEMP river force test";fixture.GetComponent<Collider>().enabled=false;var body=fixture.AddComponent<Rigidbody>();body.mass=11000;body.useGravity=false;body.constraints=RigidbodyConstraints.FreezePositionY|RigidbodyConstraints.FreezeRotation;
        float largest=0;string result="";
        try
        {
            foreach(var river in rivers)
            {
                float speed=river.currentSpeed;
                try
                {
                    var point=river.transform.position-Vector3.up*0.65f;Place(body,point,river.transform.rotation);river.currentSpeed=0;
                    for(int i=0;i<100;i++){river.ApplyForces(body);Physics.Simulate(0.02f);}
                    float still=Vector3.Distance(body.position,point);if(still>0.05f)throw new Exception("Stationary water moves test body="+still);
                    river.currentSpeed=speed;Place(body,point,river.transform.rotation);float maxForce=0;
                    for(int i=0;i<150;i++){river.ApplyForces(body);maxForce=Mathf.Max(maxForce,river.LastForce.magnitude);Physics.Simulate(0.02f);}
                    float pushed=Vector3.Dot(body.position-point,river.transform.right)*Mathf.Sign(speed);if(pushed<0.4f||maxForce>river.maximumForceNewtons+1)throw new Exception("Current failed/broke force cap: drift="+pushed+" force="+maxForce);
                    largest=Mathf.Max(largest,pushed);
                    Place(body,river.transform.position+river.transform.forward*(river.halfWidth+15),river.transform.rotation);river.ApplyForces(body);
                    if(river.WetSamples!=0||river.LastForce.sqrMagnitude>0.01f)throw new Exception("Water force outside river.");
                    result+=river.name+": 3s lateral drift="+pushed.ToString("F2")+"m maxForce="+maxForce.ToString("F0")+"N; ";
                }
                finally{river.currentSpeed=speed;}
            }
            var wind=UnityEngine.Object.FindAnyObjectByType<ForestWind>();if(wind==null||wind.strength<=0)throw new Exception("Wind disabled.");
            foreach(var name in new[]{"Game/Living Forest","Game/Forest River"})
            {var shader=Shader.Find(name);var errors=ShaderUtil.GetShaderMessages(shader).Where(m=>m.severity.ToString()=="Error").ToArray();if(errors.Length>0)throw new Exception(string.Join(";",errors.Select(m=>m.message)));}
            var ambience=UnityEngine.Object.FindAnyObjectByType<ForestAmbience>();if(ambience==null||ambience.birds.clip==null||ambience.wind.clip==null)throw new Exception("Forest ambience not configured.");
            return "PASS "+result+"dry/no-current cases passed; shaders clean, wind enabled, recorded birds/wind configured.";
        }
        finally{UnityEngine.Object.DestroyImmediate(fixture);Physics.simulationMode=oldMode;EditorApplication.isPaused=paused;}
    }
}

