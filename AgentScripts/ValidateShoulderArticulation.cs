using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ValidateShoulderArticulation
{
    static readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
    static T Own<T>(T item) where T:UnityEngine.Object{owned.Add(item);return item;}
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    static SoftGroundRuts Ground(Scene scene,Func<float,float,float> height,float halfWidth,float halfLength)
    {
        var go=new GameObject("Measured articulation ground");go.SetActive(false);SceneManager.MoveGameObjectToScene(go,scene);
        const int nx=100,nz=100;var vertices=new Vector3[(nx+1)*(nz+1)];var indices=new int[nx*nz*6];
        for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++){float px=Mathf.Lerp(-halfWidth,halfWidth,(float)x/nx),pz=Mathf.Lerp(-halfLength,halfLength,(float)z/nz);vertices[z*(nx+1)+x]=new Vector3(px,height(px,pz),pz);}
        int t=0;for(int z=0;z<nz;z++)for(int x=0;x<nx;x++){int a=z*(nx+1)+x;indices[t++]=a;indices[t++]=a+nx+1;indices[t++]=a+1;indices[t++]=a+1;indices[t++]=a+nx+1;indices[t++]=a+nx+2;}
        var mesh=Own(new Mesh{vertices=vertices,triangles=indices});mesh.RecalculateNormals();mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>();go.AddComponent<MeshCollider>();
        var config=Own(ScriptableObject.CreateInstance<SoftGroundProfile>());config.diggingMetresPerSecond=0;config.rollingCompactionMetresPerSecond=0;
        var soil=go.AddComponent<SoftGroundRuts>();soil.profile=config;
        var surface=go.AddComponent<ForestDrivingSurface>();surface.cover=ForestDrivingSurface.Cover.Mud;surface.forwardGrip=.8f;surface.lateralGrip=.9f;surface.rollingResistance=.015f;
        go.SetActive(true);return soil;
    }
    static VehiclePhysics Car(VehiclePhysics source,Scene scene,Vector3 position,Quaternion rotation)
    {
        var go=UnityEngine.Object.Instantiate(source.gameObject,position,rotation);SceneManager.MoveGameObjectToScene(go,scene);
        var car=go.GetComponent<VehiclePhysics>();go.GetComponent<Rigidbody>().isKinematic=false;
        car.profile=Own(UnityEngine.Object.Instantiate(source.profile));car.automaticSimulation=false;car.externalControl=true;go.GetComponent<VehiclePlayerInput>().enabled=false;
        car.Initialize();car.SetNeutral(true);car.SetInput(new VehicleControlInput{brake=1});Physics.SyncTransforms();return car;
    }
    static void Clean(Scene scene)
    {
        foreach(var root in scene.GetRootGameObjects())UnityEngine.Object.DestroyImmediate(root);SceneManager.UnloadSceneAsync(scene);
        for(int i=owned.Count-1;i>=0;i--)if(owned[i]!=null)UnityEngine.Object.DestroyImmediate(owned[i]);owned.Clear();
    }
    static object Slide(VehiclePhysics source,float grip,out float travel)
    {
        var scene=SceneManager.CreateScene("Unpowered sidehill "+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));var physics=scene.GetPhysicsScene();
        try
        {
            float slope=Mathf.Tan(20*Mathf.Deg2Rad);var ground=Ground(scene,(x,z)=>slope*x,9,8);
            var car=Car(source,scene,new Vector3(0,.75f,0),Quaternion.Euler(0,0,20));var body=car.GetComponent<Rigidbody>();
            for(int i=0;i<200;i++){car.Simulate(.01f);physics.Simulate(.01f);}
            ground.GetComponent<ForestDrivingSurface>().lateralGrip=grip;car.SetInput(default);Vector3 start=body.position;float maximumSpeed=0;int shoulderContacts=0;
            for(int i=0;i<200;i++){car.Simulate(.01f);physics.Simulate(.01f);maximumSpeed=Mathf.Max(maximumSpeed,body.linearVelocity.magnitude);shoulderContacts+=car.ShoulderSupports;}
            travel=body.position.x-start.x;Require(body.position.y>-5&&maximumSpeed<10,"Sidehill launch/fall");
            return new{grip,slopeDegrees=20,seconds=2,travelXMetres=travel,maximumSpeedMps=maximumSpeed,shoulderContacts,finalPosition=body.position.ToString("F3")};
        }
        finally{Clean(scene);}
    }
    static object Articulation(VehiclePhysics source)
    {
        var scene=SceneManager.CreateScene("Diagonal unsupported wheels "+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));var physics=scene.GetPhysicsScene();
        try
        {
            Vector3 a=source.transform.InverseTransformPoint(source.Wheels[0].transform.position),b=source.transform.InverseTransformPoint(source.Wheels[3].transform.position);
            var ground=Ground(scene,(x,z)=>{float d=Mathf.Min(new Vector2(x-a.x,z-a.z).magnitude,new Vector2(x-b.x,z-b.z).magnitude);return .30f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.26f,.46f,d));},3,4);
            var car=Car(source,scene,new Vector3(0,1,0),Quaternion.identity);var body=car.GetComponent<Rigidbody>();
            int diagonalFrames=0;float biggestGap=0,peakSpeed=0;int finalGrounded=0;
            for(int i=0;i<350;i++)
            {
                car.Simulate(.01f);physics.Simulate(.01f);peakSpeed=Mathf.Max(peakSpeed,body.linearVelocity.magnitude);
                if(!car.Wheels[0].isGrounded&&!car.Wheels[3].isGrounded&&car.Wheels[1].isGrounded&&car.Wheels[2].isGrounded)diagonalFrames++;
                for(int w=0;w<car.Wheels.Length;w++)if(car.Wheels.Count(q=>q.isGrounded)>=2&&!car.Wheels[w].isGrounded){car.GetWheelPose(w,out var hub,out _);if(ground.SampleSurface(hub,out var sample))biggestGap=Mathf.Max(biggestGap,hub.y-car.Wheels[w].radius-sample.point.y);}
                finalGrounded=car.Wheels.Count(w=>w.isGrounded);
            }
            Vector3 beforeForce=body.GetAccumulatedForce(.01f),beforeTorque=body.GetAccumulatedTorque(.01f);
            typeof(VehiclePhysics).GetMethod("AntiRoll",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(car,new object[]{0,1,car.profile.axles[0].antiRollN});
            float antiRollNetForce=(body.GetAccumulatedForce(.01f)-beforeForce).magnitude,antiRollTorque=(body.GetAccumulatedTorque(.01f)-beforeTorque).magnitude;
            Require(antiRollNetForce<.01f&&antiRollTorque>1,"Anti-roll must create a torque without additional lift: "+antiRollNetForce+" N / "+antiRollTorque+" Nm");
            float load=car.Wheels.Sum(w=>w.GetGroundHit(out var hit)?Mathf.Max(0,hit.force):0);
            Require(diagonalFrames>=3&&biggestGap>.08f,"No actual diagonal articulation: frames="+diagonalFrames+" gap="+biggestGap);
            Require(finalGrounded>=2&&peakSpeed<5&&body.position.y>0,"Unstable articulation fixture");
            Require(load>body.mass*Physics.gravity.magnitude*.75f&&load<body.mass*Physics.gravity.magnitude*1.25f,"Total support must remain close to vehicle weight: "+load);
            return new{diagonalFrames,biggestUnsupportedTreadGapMetres=biggestGap,finalGrounded,reportedTyreNormalLoadNewtons=load,antiRollNetForceNewtons=antiRollNetForce,antiRollTorqueNm=antiRollTorque,weightNewtons=body.mass*Physics.gravity.magnitude,peakSpeedMps=peakSpeed,finalRollDegrees=Mathf.DeltaAngle(0,body.rotation.eulerAngles.z),finalPitchDegrees=Mathf.DeltaAngle(0,body.rotation.eulerAngles.x),finalPosition=body.position.ToString("F3")};
        }
        finally{Clean(scene);}
    }
    static object Belly(VehiclePhysics source)
    {
        var scene=SceneManager.CreateScene("Chassis high-centre "+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));var physics=scene.GetPhysicsScene();
        try
        {
            var ground=Ground(scene,(x,z)=>.72f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,.25f,Mathf.Abs(z))),3,4);
            var car=Car(source,scene,new Vector3(0,1.1f,0),Quaternion.identity);var body=car.GetComponent<Rigidbody>();car.SetNeutral(false);car.SetFourWheelDrive(true);car.SetTransmission(false,true);car.SetDifferentialLock(true);
            for(int i=0;i<300;i++){car.Simulate(.01f);physics.Simulate(.01f);}
            int groundedAtStart=car.Wheels.Count(w=>w.isGrounded);Vector3 start=body.position;car.SetInput(new VehicleControlInput{throttle=1});float rpm=0;
            for(int i=0;i<300;i++){car.Simulate(.01f);physics.Simulate(.01f);rpm=Mathf.Max(rpm,car.Wheels.Max(w=>Mathf.Abs(w.rpm)));}
            float motion=Vector3.ProjectOnPlane(body.position-start,Vector3.up).magnitude;
            Require(groundedAtStart==0&&car.Wheels.All(w=>!w.isGrounded),"Belly fixture must leave the wheels unsupported");
            Require(rpm>100&&motion<.15f&&body.position.y>.25f,"Airborne tyres must not pull the high-centred chassis off: rpm="+rpm+" motion="+motion+" y="+body.position.y);
            return new{groundedAtStart,peakWheelRpm=rpm,planarMotionMetres=motion,finalPosition=body.position.ToString("F3"),shoulderSupports=car.ShoulderSupports};
        }
        finally{Clean(scene);}
    }
    static object Lifecycle(VehiclePhysics source)
    {
        var scene=SceneManager.CreateScene("Wheel envelope lifecycle "+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        try
        {
            var car=Car(source,scene,Vector3.up,Quaternion.identity);var baseline=car.Wheels.Select(w=>w.center).ToArray();
            var mounts=car.Wheels.Select(w=>w.transform.localPosition).ToArray();
            car.Wheels[0].center+=new Vector3(.11f,.012f,0);Vector3 shifted=car.Wheels[0].center;
            car.Wheels[0].transform.localPosition+=new Vector3(0,.025f,.11f);
            Vector3 shiftedMount=car.Wheels[0].transform.localPosition;
            var copy=UnityEngine.Object.Instantiate(car.gameObject);SceneManager.MoveGameObjectToScene(copy,scene);var cloned=copy.GetComponent<VehiclePhysics>();cloned.Initialize();
            Require((cloned.Wheels[0].center-baseline[0]).sqrMagnitude<.00000001f,"Clone captured a transient shoulder offset as its base geometry");
            Require((cloned.Wheels[0].transform.localPosition-mounts[0]).sqrMagnitude<.00000001f,"Clone captured a transient arc mount");
            Require((car.Wheels[0].center-shifted).sqrMagnitude<.00000001f,"Cloning altered source wheel geometry");
            Require((car.Wheels[0].transform.localPosition-shiftedMount).sqrMagnitude<.00000001f,"Cloning altered source mount");
            var envelope=car.Wheels[0].GetComponent<WheelContactEnvelope>();envelope.enabled=false;envelope.Prepare(car.profile.rutContact,.285f);
            Require((car.Wheels[0].center-baseline[0]).sqrMagnitude<.00000001f,"Disabled envelope did not restore centre");
            Require((car.Wheels[0].transform.localPosition-mounts[0]).sqrMagnitude<.00000001f,"Disabled envelope did not restore mount");
            envelope.enabled=true;car.Wheels[0].center+=Vector3.right*.1f;car.Initialize();
            Require(car.Wheels.Select((w,i)=>(w.center-baseline[i]).sqrMagnitude).All(d=>d<.00000001f),"Repeated initialization changed canonical axle geometry");
            Require(car.Wheels.Select((w,i)=>(w.transform.localPosition-mounts[i]).sqrMagnitude).All(d=>d<.00000001f),"Repeated initialization changed canonical mounts");
            return new{clonePreservesCanonicalCentre=true,disabledRestoresCentre=true,reinitializeRestoresCentre=true,canonicalMountsRestored=true};
        }
        finally{Clean(scene);}
    }
    public static string Main()
    {
        Require(Application.isPlaying,"Requires Play mode");var source=UnityEngine.Object.FindAnyObjectByType<VehiclePhysics>();
        var firm=Slide(source,.85f,out float firmTravel);var slippery=Slide(source,.04f,out float slipperyTravel);var articulation=Articulation(source);var belly=Belly(source);var lifecycle=Lifecycle(source);
        Require(Mathf.Abs(firmTravel)<.25f&&slipperyTravel<firmTravel-1,"Insufficient grip must permit physical slide: "+firmTravel+" / "+slipperyTravel);
        string report=Newtonsoft.Json.JsonConvert.SerializeObject(new{passed=true,firm,slippery,articulation,belly,lifecycle},Newtonsoft.Json.Formatting.Indented);
        File.WriteAllText("docs/reports/shoulder-articulation.json",report);return report;
    }
}
