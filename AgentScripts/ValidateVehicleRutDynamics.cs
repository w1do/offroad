using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ValidateVehicleRutDynamics
{
    static object Trial(VehiclePhysics source,bool enabled,float dt,out float maximumOffset)
    {
        var scene=SceneManager.CreateScene("Pajero rut dynamics "+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        var physics=scene.GetPhysicsScene();var owned=new List<UnityEngine.Object>();
        try
        {
            float[] centres=source.Wheels.Select(w=>source.transform.InverseTransformPoint(w.transform.position).x).ToArray();
            var ground=new GameObject("Twin measured ruts");ground.SetActive(false);SceneManager.MoveGameObjectToScene(ground,scene);
            const int nx=100,nz=120;var vertices=new Vector3[(nx+1)*(nz+1)];var triangles=new int[nx*nz*6];
            for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++)
            {
                float px=-3+x*.06f;float distance=centres.Min(c=>Mathf.Abs(px-c));
                vertices[z*(nx+1)+x]=new Vector3(px,.30f*Mathf.Clamp01((distance-.18f)/.18f),-5+z*.1f);
            }
            int t=0;for(int z=0;z<nz;z++)for(int x=0;x<nx;x++){int a=z*(nx+1)+x;triangles[t++]=a;triangles[t++]=a+nx+1;triangles[t++]=a+1;triangles[t++]=a+1;triangles[t++]=a+nx+1;triangles[t++]=a+nx+2;}
            var mesh=new Mesh{vertices=vertices,triangles=triangles};owned.Add(mesh);mesh.RecalculateNormals();mesh.RecalculateBounds();
            ground.AddComponent<MeshFilter>().sharedMesh=mesh;ground.AddComponent<MeshRenderer>();ground.AddComponent<MeshCollider>();
            var soft=ScriptableObject.CreateInstance<SoftGroundProfile>();owned.Add(soft);soft.diggingMetresPerSecond=0;soft.rollingCompactionMetresPerSecond=0;
            var ruts=ground.AddComponent<SoftGroundRuts>();ruts.profile=soft;
            var surface=ground.AddComponent<ForestDrivingSurface>();surface.cover=ForestDrivingSurface.Cover.Mud;surface.forwardGrip=.2f;surface.lateralGrip=.06f;surface.rollingResistance=.01f;
            ground.SetActive(true);
            var clone=UnityEngine.Object.Instantiate(source.gameObject,new Vector3(.03f,.75f,0),Quaternion.identity);SceneManager.MoveGameObjectToScene(clone,scene);
            var vehicle=clone.GetComponent<VehiclePhysics>();var body=clone.GetComponent<Rigidbody>();body.isKinematic=false;
            var config=UnityEngine.Object.Instantiate(source.profile);owned.Add(config);config.rutContact.enabled=enabled;vehicle.profile=config;
            vehicle.externalControl=true;vehicle.automaticSimulation=false;clone.GetComponent<VehiclePlayerInput>().enabled=false;
            vehicle.Initialize();vehicle.SetNeutral(true);vehicle.SetInput(new VehicleControlInput{brake=1});Physics.SyncTransforms();
            for(int i=0;i<Mathf.RoundToInt(2/dt);i++){vehicle.Simulate(dt);physics.Simulate(dt);}
            if(vehicle.Wheels.Count(w=>w.isGrounded)<3)throw new Exception("Pajero rut fixture did not settle");
            Vector3 start=body.position;body.linearVelocity=new Vector3(1.3f,0,2);vehicle.SetInput(default);
            maximumOffset=0;float peakReaction=0,peakSpeed=body.linearVelocity.magnitude,peakRoll=0,maximumHeight=start.y;int contacts=0;
            for(int i=0;i<Mathf.RoundToInt(1.5f/dt);i++)
            {
                vehicle.Simulate(dt);physics.Simulate(dt);
                maximumOffset=Mathf.Max(maximumOffset,body.position.x-start.x);peakReaction=Mathf.Max(peakReaction,vehicle.RutWallForceNewtons);contacts+=vehicle.RutWallContacts;
                peakSpeed=Mathf.Max(peakSpeed,body.linearVelocity.magnitude);peakRoll=Mathf.Max(peakRoll,Mathf.Abs(Mathf.DeltaAngle(0,body.rotation.eulerAngles.z)));maximumHeight=Mathf.Max(maximumHeight,body.position.y);
                for(int w=0;w<vehicle.Wheels.Length;w++)if(vehicle.WheelAgainstRutWall(w)&&vehicle.Wheels[w].sidewaysFriction.stiffness>.001f)throw new Exception("Duplicate native lateral friction at rut wall");
            }
            if(enabled&&(contacts==0||peakReaction<=0))throw new Exception("Full vehicle did not contact rut shoulders");
            if(peakSpeed>5||maximumHeight>start.y+.8f||body.position.y<-.5f)throw new Exception("Rut caused a launch or fall");
            return new{enabled,stepSeconds=dt,maximumLateralOffsetMetres=maximumOffset,finalLateralOffsetMetres=body.position.x-start.x,contacts,peakReactionNewtons=peakReaction,peakSpeedMps=peakSpeed,peakRollDegrees=peakRoll,finalPosition=body.position.ToString("F3")};
        }
        finally{foreach(var root in scene.GetRootGameObjects())UnityEngine.Object.DestroyImmediate(root);SceneManager.UnloadSceneAsync(scene);foreach(var item in owned)if(item!=null)UnityEngine.Object.DestroyImmediate(item);}
    }
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Requires Play mode");var source=UnityEngine.Object.FindAnyObjectByType<VehiclePhysics>();
        var native=Trial(source,false,.01f,out float nativeOffset);var contact=Trial(source,true,.01f,out float contactOffset);var largerStep=Trial(source,true,.02f,out float largerOffset);
        if(contactOffset>=nativeOffset-.02f)throw new Exception("Rut shoulders must measurably oppose lateral crossing: "+contactOffset+" vs "+nativeOffset);
        if(Mathf.Abs(largerOffset-contactOffset)>.10f)throw new Exception("Rut contact step sensitivity: "+largerOffset+" vs "+contactOffset);
        string report=Newtonsoft.Json.JsonConvert.SerializeObject(new{passed=true,native,contact,largerStep},Newtonsoft.Json.Formatting.Indented);
        File.WriteAllText("docs/reports/vehicle-rut-dynamics.json",report);return report;
    }
}
