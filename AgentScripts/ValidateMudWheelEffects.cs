using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;

public static class ValidateMudWheelEffects
{
    public static async Task<string> Main()
    {
        if(!Application.isPlaying)throw new Exception("Requires Play");
        var car=UnityEngine.Object.FindAnyObjectByType<VehiclePhysics>();var effects=car.GetComponent<VehicleWheelEffects>();
        if(effects.WheelEmitterCount!=car.Wheels.Length)throw new Exception("Emitter count mismatch");
        var systems=effects.GetComponentsInChildren<ParticleSystem>().Where(p=>p.name.StartsWith("Mud clods")||p.name.StartsWith("Wet mud spray")).ToArray();
        var clods=systems.First(p=>p.name=="Mud clods 0");var droplets=systems.First(p=>p.name=="Wet mud spray 0");
        var point=new Vector3(500,101,0);GameObject floor=null;
        try
        {
            foreach(var p in systems)p.Clear();int start=effects.EmittedParticles;
            effects.Spray(0,point,Vector3.up,Vector3.forward,0,1,.02f,true);
            effects.Spray(0,point,Vector3.up,Vector3.forward,12,0,.02f,true);
            if(effects.EmittedParticles!=start)throw new Exception("Emission without slip or pedal");
            effects.Spray(0,point,Vector3.up,Vector3.forward,2,1,.1f,true);int low=effects.EmittedParticles-start;
            start=effects.EmittedParticles;effects.Spray(0,point,Vector3.up,Vector3.forward,12,1,.1f,true);int high=effects.EmittedParticles-start;
            if(low<=0||high<=low||droplets.particleCount==0||clods.particleCount==0)throw new Exception("Layers/rate not responding");
            foreach(var p in systems)p.Clear();effects.Spray(0,point,Vector3.up,Vector3.right,12,1,.1f,true);
            var particles=new ParticleSystem.Particle[1024];int n=clods.GetParticles(particles);float forward=particles.Take(n).Average(p=>p.velocity.x);
            foreach(var p in systems)p.Clear();effects.Spray(0,point,Vector3.up,Vector3.right,-12,1,.1f,true);
            n=clods.GetParticles(particles);float reverse=particles.Take(n).Average(p=>p.velocity.x);
            if(forward> -1||reverse<1)throw new Exception("Steering/reverse throw failed "+forward+" "+reverse);
            foreach(var p in systems)p.Clear();int wet=effects.EmittedDroplets;effects.Spray(0,point,Vector3.up,Vector3.forward,12,1,.1f,false);
            if(effects.EmittedDroplets!=wet||droplets.particleCount!=0)throw new Exception("Wet spray on dry soil");
            floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="TEMP mud collision target";floor.transform.position=new Vector3(500,99.5f,0);floor.transform.localScale=new Vector3(30,1,30);Physics.SyncTransforms();
            int marks=effects.ImpactMarks;
            for(int i=0;i<20;i++){effects.Spray(0,point,Vector3.up,Vector3.forward,10,1,.02f,true);await Task.Delay(50);}
            await Task.Delay(1400);
            int impacts=effects.ImpactMarks-marks;if(impacts<1)throw new Exception("No real particle collision marks");
            if(floor.GetComponentsInChildren<MeshRenderer>().Length<=1)throw new Exception("Marks did not attach to target");
            foreach(var p in systems)if(p.particleCount>car.profile.wheelEffects.maximumParticlesPerLayer)throw new Exception("Unbounded particles");
            var shader=Shader.Find("Game/Wet Mud Spray");if(ShaderUtil.GetShaderMessages(shader).Any(m=>m.severity.ToString()=="Error"))throw new Exception("Shader errors");
            return "PASS "+effects.WheelEmitterCount+" wheels, 2 layers/wheel; no slip/no pedal silent; low/high="+low+"/"+high+"; turned forward/reverse x="+forward.ToString("F2")+"/"+reverse.ToString("F2")+"m/s; dry soil has no wet spray; actual collision marks="+impacts+"; bounded particles; URP shader compiles.";
        }
        finally{foreach(var p in systems)if(p!=null)p.Clear();if(floor!=null)UnityEngine.Object.DestroyImmediate(floor);}
    }
}
