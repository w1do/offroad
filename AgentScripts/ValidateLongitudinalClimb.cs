using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ValidateLongitudinalClimb
{
    static object Trial(VehiclePhysics source,float height,bool enabled,float grip,bool coast,float dt,out float travel,bool reverse=false,bool field=false,int sampling=1)
    {
        var scene=SceneManager.CreateScene("Longitudinal wheel envelope "+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        var physics=scene.GetPhysicsScene();VehiclePhysicsProfile config=null;Mesh mesh=null;SoftGroundProfile soilConfig=null;VehicleEquipmentProfile equipment=null;
        try
        {
            float direction=reverse?-1:1;
            if(field)
            {
                var ground=new GameObject("Matching visible mesh step");ground.SetActive(false);SceneManager.MoveGameObjectToScene(ground,scene);
                float[] zs={-10,-.012f,0,20};var vertices=new Vector3[8];var triangles=new int[18];
                for(int z=0;z<4;z++)for(int x=0;x<2;x++)vertices[z*2+x]=new Vector3(x==0?-6:6,zs[z]>=0?height:0,zs[z]);
                for(int z=0;z<3;z++){int a=z*2,t=z*6;triangles[t]=a;triangles[t+1]=a+2;triangles[t+2]=a+1;triangles[t+3]=a+1;triangles[t+4]=a+2;triangles[t+5]=a+3;}
                mesh=new Mesh{vertices=vertices,triangles=triangles};mesh.RecalculateNormals();mesh.RecalculateBounds();ground.AddComponent<MeshFilter>().sharedMesh=mesh;ground.AddComponent<MeshRenderer>();ground.AddComponent<MeshCollider>();
                soilConfig=ScriptableObject.CreateInstance<SoftGroundProfile>();soilConfig.diggingMetresPerSecond=0;soilConfig.rollingCompactionMetresPerSecond=0;ground.AddComponent<SoftGroundRuts>().profile=soilConfig;
                var surface=ground.AddComponent<ForestDrivingSurface>();surface.cover=ForestDrivingSurface.Cover.Gravel;surface.forwardGrip=grip;surface.lateralGrip=.8f;surface.rollingResistance=.015f;ground.SetActive(true);
            }
            else for(int i=0;i<2;i++)
            {
                var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(ground,scene);
                ground.name=i==0?"Floor":"Actual solid step";ground.transform.position=i==0?new Vector3(0,-.5f,6):new Vector3(0,(height-1)*.5f,10*direction);
                ground.transform.localScale=i==0?new Vector3(12,1,32):new Vector3(12,height+1,20);
                var surface=ground.AddComponent<ForestDrivingSurface>();surface.cover=ForestDrivingSurface.Cover.Gravel;surface.forwardGrip=grip;surface.lateralGrip=.8f;surface.rollingResistance=.015f;
            }
            var clone=UnityEngine.Object.Instantiate(source.gameObject,new Vector3(0,.65f,(coast||grip<.2f?-1.6f:-3)*direction),Quaternion.identity);SceneManager.MoveGameObjectToScene(clone,scene);
            var car=clone.GetComponent<VehiclePhysics>();var body=clone.GetComponent<Rigidbody>();body.isKinematic=false;
            config=UnityEngine.Object.Instantiate(source.profile);
            // Isolate envelope geometry from the separately tested loaded tyre radius.
            if(config.equipment!=null){equipment=UnityEngine.Object.Instantiate(config.equipment);equipment.roadTyreCompression=0;equipment.deflatedTyreCompression=0;config.equipment=equipment;}
            config.rutContact.longitudinalSupport=enabled;
            config.rutContact.longitudinalSamples=sampling==0?25:9;config.rutContact.adaptiveLongitudinalSampling=sampling==1;
            config.rutContact.arcRefinementSteps=5;config.rutContact.arcRefinementHeightMetres=.01f;
            car.profile=config;car.automaticSimulation=false;car.externalControl=true;clone.GetComponent<VehiclePlayerInput>().enabled=false;
            car.Initialize();car.SetFourWheelDrive(true);car.SetTransmission(reverse,true);car.SetInput(new VehicleControlInput{brake=1});Physics.SyncTransforms();
            for(int i=0;i<Mathf.RoundToInt(2/dt);i++){car.Simulate(dt);physics.Simulate(dt);}
            Vector3 start=body.position;car.SetNeutral(coast);car.SetInput(new VehicleControlInput{throttle=coast?0:.55f});if(coast)body.linearVelocity=Vector3.forward*(.5f*direction);
            float[] referenceExtension=new float[car.Wheels.Length],referenceCompression=new float[car.Wheels.Length];for(int w=0;w<car.Wheels.Length;w++){var wheel=car.Wheels[w];wheel.GetWorldPose(out var native,out _);referenceExtension[w]=Vector3.Dot(wheel.transform.TransformPoint(wheel.center)-native,wheel.transform.up);wheel.GetGroundHit(out var hit);referenceCompression[w]=Mathf.Max(0,hit.force)/Mathf.Max(1,wheel.suspensionSpring.spring);}
            float referenceHeight=body.worldCenterOfMass.y;float initialEnergy=BodyEnergy(body,referenceHeight);float initialMechanicalEnergy=MechanicalEnergy(car,body,referenceHeight,referenceExtension,referenceCompression);float peakMechanicalEnergy=initialMechanicalEnergy;float peakEnergy=initialEnergy,peakSpeed=body.linearVelocity.magnitude,peakHeight=body.position.y,peakReaction=0,spin=0;int arcContacts=0;bool completed=false;
            object worstPenetration=null;float maximumTyrePenetration=0;float minimumHubY=0,minimumHubZ=0;var watch=System.Diagnostics.Stopwatch.StartNew();
            for(int i=0;i<Mathf.RoundToInt((coast?3:6)/dt);i++)
            {
                car.Simulate(dt);physics.Simulate(dt);peakEnergy=Mathf.Max(peakEnergy,BodyEnergy(body,referenceHeight));peakMechanicalEnergy=Mathf.Max(peakMechanicalEnergy,MechanicalEnergy(car,body,referenceHeight,referenceExtension,referenceCompression));
                for(int w=0;w<car.Wheels.Length;w++)
                {
                    var axle=config.axles[w/2];float radius=axle.tire==null?axle.wheelRadius:axle.tire.radiusMetres;
                    float mass=axle.tire==null?axle.wheelMass:axle.tire.massKg;
                    if(Mathf.Abs(car.Wheels[w].radius-radius)>.00001f||Mathf.Abs(car.Wheels[w].mass-mass)>.00001f)
                        throw new Exception("Contact changed native wheel shape/inertia");
                }
                peakSpeed=Mathf.Max(peakSpeed,body.linearVelocity.magnitude);peakHeight=Mathf.Max(peakHeight,body.position.y);peakReaction=Mathf.Max(peakReaction,car.ArcNormalReactionNewtons);arcContacts+=car.ArcSupports;spin=Mathf.Max(spin,car.WheelspinSpeed);
                minimumHubY=float.MaxValue;minimumHubZ=float.MaxValue;for(int w=0;w<car.Wheels.Length;w++){car.GetWheelPose(w,out var hub,out _);minimumHubY=Mathf.Min(minimumHubY,hub.y);minimumHubZ=Mathf.Min(minimumHubZ,hub.z*direction);float distance=-hub.z*direction,radius=car.Wheels[w].radius;
                    if(distance<radius){float supportHeight=distance<=0?height+radius:height+Mathf.Sqrt(Mathf.Max(0,radius*radius-distance*distance));if(supportHeight-hub.y>maximumTyrePenetration){maximumTyrePenetration=supportHeight-hub.y;car.Wheels[w].GetWorldPose(out var nativePose,out _);car.Wheels[w].GetGroundHit(out var hit);var env=car.Wheels[w].GetComponent<WheelContactEnvelope>();worstPenetration=new{w,hub=hub.ToString("F4"),native=nativePose.ToString("F4"),centre=car.Wheels[w].center.ToString("F4"),point=hit.point.ToString("F4"),hit=hit.collider==null?"none":hit.collider.name,angle=env.ArcDegrees,body=body.position.ToString("F4"),radius,requiredHeight=supportHeight};}}}
                if(minimumHubZ>.3f&&minimumHubY>height+car.Wheels[0].radius-.04f){completed=true;break;}
            }
            watch.Stop();travel=(body.position.z-start.z)*direction;
            var result=new{height,enabled,grip,coast,reverse,field,sampling,stepSeconds=dt,completed,travelMetres=travel,minimumHubY,minimumHubZ,arcContacts,peakReactionNewtons=peakReaction,peakSpeedMps=peakSpeed,peakHeight,peakSlipMps=spin,initialKineticJoules=initialEnergy,peakBodyEnergyJoules=peakEnergy,initialEstimatedMechanicalEnergyJoules=initialMechanicalEnergy,peakEstimatedMechanicalEnergyJoules=peakMechanicalEnergy,maximumTyrePenetrationMetres=maximumTyrePenetration,worstPenetration,simulationWallMs=watch.Elapsed.TotalMilliseconds,finalPosition=body.position.ToString("F3")};
            if(peakSpeed>18||peakHeight>1.5f||body.position.y<-1)throw new Exception("Unstable longitudinal contact: "+Newtonsoft.Json.JsonConvert.SerializeObject(result));
            return result;
        }
        finally{foreach(var root in scene.GetRootGameObjects())UnityEngine.Object.DestroyImmediate(root);SceneManager.UnloadSceneAsync(scene);if(config!=null)UnityEngine.Object.DestroyImmediate(config);if(equipment!=null)UnityEngine.Object.DestroyImmediate(equipment);if(mesh!=null)UnityEngine.Object.DestroyImmediate(mesh);if(soilConfig!=null)UnityEngine.Object.DestroyImmediate(soilConfig);}
    }
    static float BodyEnergy(Rigidbody body,float referenceHeight)
    {
        Vector3 omega=Quaternion.Inverse(body.rotation*body.inertiaTensorRotation)*body.angularVelocity;Vector3 inertia=body.inertiaTensor;
        return .5f*body.mass*body.linearVelocity.sqrMagnitude+.5f*(inertia.x*omega.x*omega.x+inertia.y*omega.y*omega.y+inertia.z*omega.z*omega.z)+body.mass*Physics.gravity.magnitude*(body.worldCenterOfMass.y-referenceHeight);
    }
    static float MechanicalEnergy(VehiclePhysics car,Rigidbody body,float referenceHeight,float[] referenceExtension,float[] referenceCompression)
    {
        float energy=BodyEnergy(body,referenceHeight);
        for(int w=0;w<car.Wheels.Length;w++)
        {
            var wheel=car.Wheels[w];
            wheel.GetWorldPose(out var native,out _);
            float extension=Vector3.Dot(wheel.transform.TransformPoint(wheel.center)-native,wheel.transform.up);
            // Infer elastic preload from the settled native load; hard-stop reactions make this an estimate.
            float deflection=Mathf.Max(0,referenceCompression[w]+referenceExtension[w]-extension);
            energy+=.5f*wheel.suspensionSpring.spring*deflection*deflection;
            float omega=wheel.rpm*Mathf.PI/30;
            energy+=.25f*wheel.mass*wheel.radius*wheel.radius*omega*omega;
        }
        return energy;
    }
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Requires Play mode");var source=UnityEngine.Object.FindAnyObjectByType<VehiclePhysics>();
        var central=Trial(source,.10f,false,.85f,false,.01f,out _);
        var previousGrid=Trial(source,.10f,true,.85f,false,.01f,out _,sampling:0);
        var coarseGrid=Trial(source,.10f,true,.85f,false,.01f,out _,sampling:2);
        var small=Trial(source,.10f,true,.85f,false,.01f,out _);
        var tall=Trial(source,.60f,true,.85f,false,.01f,out _);
        var weak=Trial(source,.10f,true,.06f,false,.01f,out _);
        var nativeCoasting=Trial(source,.10f,false,.85f,true,.01f,out _);
        var coasting=Trial(source,.10f,true,.85f,true,.01f,out _);
        var backing=Trial(source,.10f,true,.85f,false,.01f,out _,reverse:true);
        var deformable=Trial(source,.10f,true,.85f,false,.01f,out _,field:true);
        var largerStep=Trial(source,.10f,true,.85f,false,.02f,out _);
        var data=Newtonsoft.Json.Linq.JObject.FromObject(new{central,previousGrid,coarseGrid,small,tall,weak,nativeCoasting,coasting,backing,deformable,largerStep});
        foreach(string name in new[]{"small","backing","deformable","largerStep"})if(!data[name].Value<bool>("completed")||data[name].Value<int>("arcContacts")==0)throw new Exception("Expected physical arc climb: "+name);
        if(data["tall"].Value<bool>("completed")||data["weak"].Value<bool>("completed")||data["weak"].Value<int>("arcContacts")==0||data["weak"].Value<float>("peakSlipMps")<2)throw new Exception("Height/grip limit failed");
        if(data["coasting"].Value<bool>("completed")||data["coasting"].Value<float>("peakBodyEnergyJoules")>data["coasting"].Value<float>("initialKineticJoules")+50||data["coasting"].Value<float>("peakSpeedMps")>.6f)throw new Exception("Unpowered contact added excessive body energy");
        if(Mathf.Abs(data["small"].Value<float>("travelMetres")-data["largerStep"].Value<float>("travelMetres"))>.15f)throw new Exception("Longitudinal step-size sensitivity");
        foreach(string name in new[]{"small","backing","deformable","largerStep"})if(data[name].Value<float>("maximumTyrePenetrationMetres")>.065f)throw new Exception("Excessive tyre penetration: "+name);
        if(data["small"].Value<float>("maximumTyrePenetrationMetres")>data["central"].Value<float>("maximumTyrePenetrationMetres")*.65f)throw new Exception("No geometric improvement over central ray");
        if(data["small"].Value<float>("maximumTyrePenetrationMetres")>data["previousGrid"].Value<float>("maximumTyrePenetrationMetres")*.85f)
            throw new Exception("Adaptive arc did not improve over the previous 25-sample grid");
        if(data["small"].Value<float>("maximumTyrePenetrationMetres")>data["coarseGrid"].Value<float>("maximumTyrePenetrationMetres")*.75f)
            throw new Exception("Edge refinement did not improve over the coarse grid");
        if(data["coasting"].Value<float>("peakBodyEnergyJoules")>data["coasting"].Value<float>("initialKineticJoules")+10)
            throw new Exception("Adaptive unpowered contact added excessive body energy");
        data["passed"]=true;
        string json=Newtonsoft.Json.JsonConvert.SerializeObject(data,Newtonsoft.Json.Formatting.Indented);File.WriteAllText("docs/reports/longitudinal-climb.json",json);return json;
    }
}
