using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class AuthoredForestArm
{
    public string name;
    public Vector3 entrance;
    public float yaw;
    public GameObject[] modules;
    [NonSerialized]public AuthoredForestBlock[] descriptions;
    [NonSerialized]public ForestRouteRecord[] records;
    [NonSerialized]public int current;
    [NonSerialized]public double progress;
}
public struct ForestRouteRecord {public double x,y,z,distance,yaw;public int module;}

// The long world streams meshes made from the user's forest, never the former generated mountains.
public sealed class AuthoredForestRoute : MonoBehaviour
{
    public Transform vehicle;
    public AuthoredForestArm[] arms;
    public double lengthPerDirection=10000000;
    public int ahead=18,behind=4;
    public GameObject editorPreview;
    public double OriginX {get;private set;}
    public double OriginZ {get;private set;}
    public int OriginShifts {get;private set;}
    public int LoadedBlocks=>loaded.Count;
    public int ActiveArm {get;private set;}
    public double Distance=>arms==null||arms.Length==0?0:arms[ActiveArm].progress;
    readonly Dictionary<(int,int),GameObject> loaded=new Dictionary<(int,int),GameObject>();
    public void Prepare()
    {
        if(arms==null)return;
        foreach(var arm in arms)
        {
            if(arm.records!=null&&arm.records.Length>1)continue;
            arm.descriptions=new AuthoredForestBlock[arm.modules.Length];for(int i=0;i<arm.modules.Length;i++)arm.descriptions[i]=arm.modules[i].GetComponent<AuthoredForestBlock>();
            float shortest=float.MaxValue;foreach(var d in arm.descriptions)shortest=Mathf.Min(shortest,d.length);
            int count=(int)Math.Ceiling(lengthPerDirection/shortest)+2;arm.records=new ForestRouteRecord[count];
            double x=arm.entrance.x,y=arm.entrance.y,z=arm.entrance.z,yaw=arm.yaw,distance=0;
            int[] pattern={0,1,1,0,-1,-1,0,0};
            for(int i=0;i<count;i++)
            {
                int bend=pattern[i%8]*((i/8)%2==0?1:-1);int height=(i/8)%2;int module=height*3+bend+1;arm.records[i]=new ForestRouteRecord{x=x,y=y,z=z,yaw=yaw,distance=distance,module=module};var d=arm.descriptions[module];double rad=yaw*Math.PI/180;double sin=Math.Sin(rad),cos=Math.Cos(rad);x+=d.exit.x*cos+d.exit.z*sin;z+=-d.exit.x*sin+d.exit.z*cos;y+=d.exit.y;yaw+=d.exitYaw;distance+=d.length;
            }
        }
    }
    public Vector3 Position(int arm,int block){Prepare();var r=arms[arm].records[block];return new Vector3((float)(r.x-OriginX),(float)r.y,(float)(r.z-OriginZ));}
    public Quaternion Rotation(int arm,int block)=>Quaternion.Euler(0,(float)arms[arm].records[block].yaw,0);
    public Vector3 RoutePoint(int arm,int block,float localDistance){var r=arms[arm].records[block];return Position(arm,block)+Rotation(arm,block)*arms[arm].descriptions[r.module].Point(localDistance);}
    public GameObject CreateBlock(int arm,int block)
    {
        Prepare();var r=arms[arm].records[block];var go=Instantiate(arms[arm].modules[r.module],Position(arm,block),Rotation(arm,block),transform);go.name=arms[arm].name+" / "+(r.distance/1000).ToString("F2")+" km";var soils=go.GetComponentsInChildren<SoftGroundRuts>();for(int i=0;i<soils.Length;i++)soils[i].BindPersistence("route/"+arm+"/"+block+"/"+i);return go;
    }
    public void LoadWindow(int arm,int block){Prepare();ActiveArm=arm;arms[arm].current=Mathf.Clamp(block,0,arms[arm].records.Length-1);arms[arm].progress=arms[arm].records[arms[arm].current].distance;EnsureWindow(true);}
    void Start(){if(editorPreview!=null)editorPreview.SetActive(false);Prepare();EnsureWindow(true);}
    void Update()
    {
        if(vehicle==null)return;Prepare();float best=float.MaxValue;int active=ActiveArm;
        for(int a=0;a<arms.Length;a++)
        {
            var arm=arms[a];int lo=Math.Max(0,arm.current-5),hi=Math.Min(arm.records.Length-1,arm.current+7);
            if(arm.current>ahead+5&&Vector3.Distance(vehicle.position,new Vector3((float)(arm.entrance.x-OriginX),arm.entrance.y,(float)(arm.entrance.z-OriginZ)))<300){arm.current=0;arm.progress=0;lo=0;hi=Math.Min(7,arm.records.Length-1);}
            for(int b=lo;b<=hi;b++)
            {
                var r=arm.records[b];var d=arm.descriptions[r.module];Vector3 origin=Position(a,b);if((origin-vehicle.position).sqrMagnitude>400*400)continue;Quaternion rotation=Rotation(a,b);Vector3 local=Quaternion.Inverse(rotation)*(vehicle.position-origin);
                for(int j=0;j<d.road.Length-1;j++){Vector3 delta=d.road[j+1]-d.road[j];float t=Mathf.Clamp01(Vector3.Dot(local-d.road[j],delta)/delta.sqrMagnitude);float error=(local-(d.road[j]+delta*t)).sqrMagnitude;if(error<best){best=error;active=a;arm.current=b;arm.progress=r.distance+Mathf.Lerp(d.distance[j],d.distance[j+1],t);}}
            }
        }
        if(best<150*150)ActiveArm=active;EnsureWindow(false);
    }
    void FixedUpdate(){if(vehicle!=null&&new Vector2(vehicle.position.x,vehicle.position.z).sqrMagnitude>1500*1500)ShiftOrigin(new Vector3(Mathf.Round(vehicle.position.x/100)*100,0,Mathf.Round(vehicle.position.z/100)*100));}
    public void EnsureWindow(bool immediate)
    {
        Prepare();var wanted=new HashSet<(int,int)>();
        for(int a=0;a<arms.Length;a++)
        {
            int current=arms[a].current;bool nearSource=(vehicle==null||Vector3.Distance(vehicle.position,new Vector3((float)(arms[a].entrance.x-OriginX),arms[a].entrance.y,(float)(arms[a].entrance.z-OriginZ)))<400);
            int from=a==ActiveArm?Math.Max(0,current-behind):0,to=a==ActiveArm?Math.Min(arms[a].records.Length-1,current+ahead):nearSource?2:-1;
            for(int b=from;b<=to;b++)wanted.Add((a,b));
        }
        int built=0;foreach(var key in wanted)if(!loaded.ContainsKey(key)){loaded[key]=CreateBlock(key.Item1,key.Item2);if(!immediate&&++built>=2)break;}
        var remove=new List<(int,int)>();foreach(var pair in loaded)if(!wanted.Contains(pair.Key)){var winch=vehicle==null?null:vehicle.GetComponent<VehicleWinch>();if(winch!=null&&winch.Pulling&&winch.AnchorTransform!=null&&winch.AnchorTransform.IsChildOf(pair.Value.transform))continue;remove.Add(pair.Key);}foreach(var key in remove){if(Application.isPlaying)Destroy(loaded[key]);else DestroyImmediate(loaded[key]);loaded.Remove(key);}
        if(vehicle!=null&&loaded.ContainsKey((ActiveArm,arms[ActiveArm].current))&&vehicle.TryGetComponent<VehiclePhysics>(out var physics)){int i=arms[ActiveArm].current;var d=arms[ActiveArm].descriptions[arms[ActiveArm].records[i].module];var p=RoutePoint(ActiveArm,i,Mathf.Min(5,d.length))+.6f*Vector3.up;var dir=RoutePoint(ActiveArm,i,Mathf.Min(8,d.length))-RoutePoint(ActiveArm,i,Mathf.Min(5,d.length));physics.SetRecoveryPoint(p,Quaternion.LookRotation(dir));}
    }
    public void ShiftOrigin(Vector3 delta)
    {
        var bodies=UnityEngine.Object.FindObjectsByType<Rigidbody>();var positions=new Vector3[bodies.Length];for(int i=0;i<bodies.Length;i++)positions[i]=bodies[i].position-delta;
        foreach(var root in gameObject.scene.GetRootGameObjects())root.transform.position-=delta;
        for(int i=0;i<bodies.Length;i++)bodies[i].position=positions[i];foreach(var p in UnityEngine.Object.FindObjectsByType<VehiclePhysics>())p.ShiftOrigin(delta);
        foreach(var soil in UnityEngine.Object.FindObjectsByType<SoftGroundRuts>())soil.ShiftOrigin(delta);
        foreach(var ripple in UnityEngine.Object.FindObjectsByType<WaterRippleRenderer>())ripple.ShiftOrigin(delta);
        foreach(var particles in UnityEngine.Object.FindObjectsByType<ParticleSystem>())if(particles.main.simulationSpace==ParticleSystemSimulationSpace.World){var p=new ParticleSystem.Particle[particles.particleCount];int n=particles.GetParticles(p);for(int i=0;i<n;i++)p[i].position-=delta;particles.SetParticles(p,n);}
        OriginX+=delta.x;OriginZ+=delta.z;OriginShifts++;Shader.SetGlobalVector("_ForestOriginOffset",new Vector4((float)OriginX,0,(float)OriginZ,0));Physics.SyncTransforms();
    }
    void OnDestroy(){Shader.SetGlobalVector("_ForestOriginOffset",Vector4.zero);}
    void OnGUI(){GUI.Box(new Rect(18,174,405,56),"ЛЕСНАЯ ДОРОГА ИЗ ВАШЕЙ МОДЕЛИ");GUI.Label(new Rect(30,198,385,26),$"{Distance/1000:F2} / {lengthPerDirection/1000:F0} км   Направление {ActiveArm+1}");}
}
