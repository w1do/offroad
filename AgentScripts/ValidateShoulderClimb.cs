using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ValidateShoulderClimb
{
    static void Capture(VehiclePhysics car,GameObject ground)
    {
        var cameraRoot=new GameObject("TEMP edge contact capture camera");SceneManager.MoveGameObjectToScene(cameraRoot,car.gameObject.scene);var camera=cameraRoot.AddComponent<Camera>();camera.enabled=false;camera.useOcclusionCulling=false;camera.nearClipPlane=.1f;camera.farClipPlane=80;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.15f,.17f);
        var objects=car.GetComponentsInChildren<Transform>().Select(t=>t.gameObject).Concat(new[]{ground}).ToArray();var layers=objects.Select(g=>g.layer).ToArray();
        var position=camera.transform.position;var rotation=camera.transform.rotation;int mask=camera.cullingMask;float fov=camera.fieldOfView;var target=camera.targetTexture;var active=RenderTexture.active;bool fog=RenderSettings.fog;
        var oldMaterial=ground.GetComponent<MeshRenderer>().sharedMaterial;var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.color=new Color(.30f,.21f,.13f);material.SetFloat("_Smoothness",.12f);
        var rt=RenderTexture.GetTemporary(1400,900,24);Texture2D texture=null;
        try
        {
            foreach(var go in objects)go.layer=31;ground.GetComponent<MeshRenderer>().sharedMaterial=material;RenderSettings.fog=false;
            camera.cullingMask=1<<31;camera.transform.position=car.transform.position+new Vector3(5,2.5f,5);camera.transform.LookAt(car.transform.position+Vector3.up*.7f);camera.fieldOfView=48;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            texture=new Texture2D(1400,900,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1400,900),0,0);texture.Apply();File.WriteAllBytes("work/ShoulderClimb.png",texture.EncodeToPNG());
        }
        finally{for(int i=0;i<objects.Length;i++)objects[i].layer=layers[i];ground.GetComponent<MeshRenderer>().sharedMaterial=oldMaterial;camera.transform.SetPositionAndRotation(position,rotation);camera.cullingMask=mask;camera.fieldOfView=fov;camera.targetTexture=target;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);RenderSettings.fog=fog;if(texture!=null)UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(material);UnityEngine.Object.DestroyImmediate(cameraRoot);}
    }
    static object Trial(VehiclePhysics source,bool support,bool escape,out float lift,out float progress,float grip=.8f,float dt=.01f)
    {
        var scene=SceneManager.CreateScene("Shoulder edge climb "+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        var physics=scene.GetPhysicsScene();var owned=new List<UnityEngine.Object>();
        try
        {
            var ground=new GameObject("Visible measured bank");ground.SetActive(false);SceneManager.MoveGameObjectToScene(ground,scene);
            const int nx=120,nz=160;var vertices=new Vector3[(nx+1)*(nz+1)];var triangles=new int[nx*nz*6];
            for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++){float px=-3+x*.075f;vertices[z*(nx+1)+x]=new Vector3(px,.22f*Mathf.Clamp01((px-.74f)/.36f),-5+z*.15f);}
            int t=0;for(int z=0;z<nz;z++)for(int x=0;x<nx;x++){int a=z*(nx+1)+x;triangles[t++]=a;triangles[t++]=a+nx+1;triangles[t++]=a+1;triangles[t++]=a+1;triangles[t++]=a+nx+1;triangles[t++]=a+nx+2;}
            var mesh=new Mesh{vertices=vertices,triangles=triangles};owned.Add(mesh);mesh.RecalculateNormals();mesh.RecalculateBounds();
            ground.AddComponent<MeshFilter>().sharedMesh=mesh;ground.AddComponent<MeshRenderer>();ground.AddComponent<MeshCollider>();
            var profile=ScriptableObject.CreateInstance<SoftGroundProfile>();owned.Add(profile);profile.diggingMetresPerSecond=0;profile.rollingCompactionMetresPerSecond=0;
            var soil=ground.AddComponent<SoftGroundRuts>();soil.profile=profile;
            var surface=ground.AddComponent<ForestDrivingSurface>();surface.cover=ForestDrivingSurface.Cover.Mud;surface.forwardGrip=grip;surface.lateralGrip=grip<.2f?grip:.9f;surface.rollingResistance=.015f;ground.SetActive(true);
            var clone=UnityEngine.Object.Instantiate(source.gameObject,new Vector3(0,.65f,0),Quaternion.identity);SceneManager.MoveGameObjectToScene(clone,scene);
            var car=clone.GetComponent<VehiclePhysics>();var body=clone.GetComponent<Rigidbody>();body.isKinematic=false;
            var config=UnityEngine.Object.Instantiate(source.profile);owned.Add(config);config.rutContact.shoulderSupport=support;car.profile=config;
            car.automaticSimulation=false;car.externalControl=true;clone.GetComponent<VehiclePlayerInput>().enabled=false;
            car.Initialize();car.SetFourWheelDrive(true);car.SetTransmission(false,true);car.SetInput(new VehicleControlInput{brake=1});Physics.SyncTransforms();
            for(int i=0;i<Mathf.RoundToInt(2/dt);i++){car.Simulate(dt);physics.Simulate(dt);}
            if(car.Wheels.Count(w=>w.isGrounded)<3)throw new Exception("Bank fixture did not settle");
            car.GetWheelPose(0,out var left,out _);car.GetWheelPose(1,out var right,out _);lift=right.y-left.y;
            Vector3 start=body.position;bool escaped=false,captured=false;float spin=0;int shoulderFrames=0;float peakGain=0,peakSpeed=0,maximumGap=0;
            if(escape)for(int i=0;i<Mathf.RoundToInt(6.5f/dt);i++)
            {
                float yaw=Mathf.DeltaAngle(0,body.rotation.eulerAngles.y);
                car.SetInput(new VehicleControlInput{throttle=.6f,steering=Mathf.Clamp((25-yaw)*.05f-body.angularVelocity.y*.15f,-.7f,.7f)});
                car.Simulate(dt);physics.Simulate(dt);car.SendMessage("LateUpdate");if(!captured&&support&&grip>.5f&&dt<.015f&&car.ShoulderSupports>=2&&body.position.x>.15f){Capture(car,ground);captured=true;}spin=Mathf.Max(spin,car.WheelspinSpeed);
                shoulderFrames+=car.ShoulderSupports;peakGain=Mathf.Max(peakGain,car.MaximumShoulderHeightGainMetres);peakSpeed=Mathf.Max(peakSpeed,body.linearVelocity.magnitude);
                foreach(var wheel in car.Wheels)if(wheel.GetGroundHit(out var hit)&&hit.collider==soil.GetComponent<MeshCollider>()&&soil.SampleSurface(hit.point,out var actual))maximumGap=Mathf.Max(maximumGap,Mathf.Abs(hit.point.y-actual.point.y));
                bool outOfRut=true;for(int w=0;w<car.Wheels.Length;w++){car.GetWheelPose(w,out var hub,out _);if(hub.x<1.25f||hub.y<.22f+car.Wheels[w].radius-.06f)outOfRut=false;}
                if(outOfRut){escaped=true;break;}
            }
            progress=body.position.x-start.x;
            float minimumHubX=float.MaxValue;float minimumHubHeight=float.MaxValue;
            for(int w=0;w<car.Wheels.Length;w++){car.GetWheelPose(w,out var hub,out _);minimumHubX=Mathf.Min(minimumHubX,hub.x);minimumHubHeight=Mathf.Min(minimumHubHeight,hub.y);}
            if(escape&&grip>.5f&&!escaped)throw new Exception("Good grip must complete the edge climb");
            if(escape&&grip<.1f&&(escaped||spin<2))throw new Exception("Weak grip must remain insufficient and produce slip");
            if(peakSpeed>12||maximumGap>.006f||body.position.y>1.4f)throw new Exception("Edge contact launch or non-visible support: support="+support+" escape="+escape+" speed="+peakSpeed+" gap="+maximumGap+" pos="+body.position+" lift="+lift+" supports="+shoulderFrames+" gain="+peakGain);
            return new{support,escape,grip,stepSeconds=dt,escaped,peakSlipSpeedMps=spin,stationaryRightMinusLeftHubMetres=lift,lateralProgressMetres=progress,finalPosition=body.position.ToString("F3"),minimumHubX,minimumHubHeight,shoulderFrames,peakGainMetres=peakGain,peakSpeedMps=peakSpeed,maximumVisibleSurfaceGapMetres=maximumGap};
        }
        finally{foreach(var root in scene.GetRootGameObjects())UnityEngine.Object.DestroyImmediate(root);SceneManager.UnloadSceneAsync(scene);foreach(var asset in owned)if(asset!=null)UnityEngine.Object.DestroyImmediate(asset);}
    }
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Requires Play mode");var source=UnityEngine.Object.FindAnyObjectByType<VehiclePhysics>();
        var central=Trial(source,false,false,out float oldLift,out _);var shoulder=Trial(source,true,false,out float newLift,out _);
        var escape=Trial(source,true,true,out _,out float progress);
        var weak=Trial(source,true,true,out _,out float weakProgress,.06f);
        var largerStep=Trial(source,true,true,out _,out float largerProgress,.8f,.02f);
        if(newLift<oldLift+.01f||progress<2||weakProgress>progress-1||Mathf.Abs(largerProgress-progress)>.25f)throw new Exception("Shoulder support/climb/grip/step regression: lift="+(newLift-oldLift)+" progress="+progress+" weak="+weakProgress+" step="+largerProgress);
        string report=Newtonsoft.Json.JsonConvert.SerializeObject(new{passed=true,central,shoulder,escape,weak,largerStep,liftDifferenceMetres=newLift-oldLift,progress},Newtonsoft.Json.Formatting.Indented);
        File.WriteAllText("docs/reports/shoulder-climb.json",report);return report;
    }
}
