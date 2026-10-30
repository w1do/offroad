using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter),typeof(MeshCollider))]
public sealed class SoftGroundRuts : MonoBehaviour
{
    public SoftGroundProfile profile;
    public string persistentId;
    public Vector2 fieldHalfSize;
    public float maximumDepth=.38f,diggingMetresPerSecond=.065f,radius=.34f;
    public float DugDepth {get;private set;}
    public int CommitCount {get;private set;}
    Mesh runtimeMesh,collisionMesh;MeshCollider physical;Vector3[] original,vertices;
    float[] depths;float clock;bool dirty;
    readonly Dictionary<Vector2Int,List<int>> grid=new Dictionary<Vector2Int,List<int>>();
    static readonly List<SoftGroundRuts> active=new List<SoftGroundRuts>();
    static readonly Dictionary<Vector2Int,List<SoftGroundRuts>> neighbourhood=new Dictionary<Vector2Int,List<SoftGroundRuts>>();
    readonly List<Vector2Int> registeredCells=new List<Vector2Int>();
    Bounds contactBounds;
    static Vector2Int Region(Vector3 p)=>new Vector2Int(Mathf.FloorToInt(p.x/4),Mathf.FloorToInt(p.z/4));
    void Unregister(){foreach(var cell in registeredCells)if(neighbourhood.TryGetValue(cell,out var list)){list.Remove(this);if(list.Count==0)neighbourhood.Remove(cell);}registeredCells.Clear();}
    void Register(){Unregister();if(runtimeMesh==null)return;contactBounds=GetComponent<Renderer>().bounds;contactBounds.Expand(new Vector3(2,(profile==null?maximumDepth:profile.maximumDepthMetres)+2,2));var lo=Region(contactBounds.min);var hi=Region(contactBounds.max);for(int z=lo.y;z<=hi.y;z++)for(int x=lo.x;x<=hi.x;x++){var cell=new Vector2Int(x,z);if(!neighbourhood.TryGetValue(cell,out var list))neighbourhood[cell]=list=new List<SoftGroundRuts>();list.Add(this);registeredCells.Add(cell);}}
    static readonly Dictionary<string,float[]> retained=new Dictionary<string,float[]>();
    const float CellSize=.2f;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void NewSession(){retained.Clear();active.Clear();neighbourhood.Clear();}
    public void ShiftOrigin(Vector3 delta){Register();} // Grid uses offsets relative to the patch, so it moves with its Transform.
    void OnEnable()
    {
        if(!Application.isPlaying)return;if(!active.Contains(this))active.Add(this);if(runtimeMesh!=null){BuildGrid();Register();return;}
        physical=GetComponent<MeshCollider>();var filter=GetComponent<MeshFilter>();if(filter.sharedMesh==null)return;
        runtimeMesh=Instantiate(filter.sharedMesh);runtimeMesh.name="Deformable visible mud";runtimeMesh.MarkDynamic();filter.sharedMesh=runtimeMesh;
        original=runtimeMesh.vertices;vertices=(Vector3[])original.Clone();depths=new float[vertices.Length];
        collisionMesh=Instantiate(runtimeMesh);collisionMesh.name="Matching mud collision";var front=runtimeMesh.triangles;var both=new int[front.Length*2];System.Array.Copy(front,both,front.Length);for(int i=0;i<front.Length;i+=3){both[front.Length+i]=front[i];both[front.Length+i+1]=front[i+2];both[front.Length+i+2]=front[i+1];}collisionMesh.subMeshCount=1;collisionMesh.triangles=both;physical.sharedMesh=collisionMesh;
        BuildGrid();
        if(!string.IsNullOrEmpty(persistentId))RestoreRetained();
        Register();
    }
    void BuildGrid(){grid.Clear();if(original==null)return;for(int i=0;i<original.Length;i++){var cell=Cell(transform.TransformPoint(original[i])-transform.position);if(!grid.TryGetValue(cell,out var list))grid[cell]=list=new List<int>();list.Add(i);}}
    // Editor/LOD rebuilds can replace a derived mesh without erasing the current tracks.
    public void RebuildFromSource(Mesh source)
    {
        if(source==null||runtimeMesh==null)return;var fresh=source.vertices;var saved=new float[fresh.Length];
        var samples=new Dictionary<Vector2Int,float>();const float sample=.08f;
        for(int i=0;i<original.Length;i++){var key=new Vector2Int(Mathf.RoundToInt(original[i].x/sample),Mathf.RoundToInt(original[i].z/sample));samples.TryGetValue(key,out float old);samples[key]=Mathf.Max(old,depths[i]);}
        for(int i=0;i<fresh.Length;i++){var key=new Vector2Int(Mathf.RoundToInt(fresh[i].x/sample),Mathf.RoundToInt(fresh[i].z/sample));float value=0;for(int z=-1;z<=1;z++)for(int x=-1;x<=1;x++)if(samples.TryGetValue(key+new Vector2Int(x,z),out float depth))value=Mathf.Max(value,depth);saved[i]=value;}
        var oldVisible=runtimeMesh;var oldPhysical=collisionMesh;Unregister();runtimeMesh=null;collisionMesh=null;DugDepth=0;GetComponent<MeshFilter>().sharedMesh=source;OnEnable();
        for(int i=0;i<saved.Length;i++){depths[i]=saved[i];vertices[i]=original[i]+transform.InverseTransformVector(Vector3.down*saved[i]);DugDepth=Mathf.Max(DugDepth,saved[i]);}dirty=true;Commit();Register();Destroy(oldVisible);Destroy(oldPhysical);
    }
    void OnDisable(){active.Remove(this);Unregister();Retain();}
    static Vector2Int Cell(Vector3 p)=>new Vector2Int(Mathf.FloorToInt(p.x/CellSize),Mathf.FloorToInt(p.z/CellSize));
    public void BindPersistence(string key){persistentId=key;RestoreRetained();}
    void Retain(){if(depths!=null&&!string.IsNullOrEmpty(persistentId)&&DugDepth>0)retained[persistentId]=(float[])depths.Clone();}
    void RestoreRetained(){if(depths==null||!retained.TryGetValue(persistentId,out var saved)||saved.Length!=depths.Length)return;System.Array.Copy(saved,depths,depths.Length);for(int i=0;i<vertices.Length;i++){vertices[i]=original[i]+transform.InverseTransformVector(Vector3.down*depths[i]);DugDepth=Mathf.Max(DugDepth,depths[i]);}dirty=true;Commit();}
    public static void Contact(Vector3 point,Vector3 forward,float width,float loadNewtons,float slipSpeed,float pressureLoadMultiplier,float seconds)
    {
        if(!neighbourhood.TryGetValue(Region(point),out var nearby))return;
        foreach(var soil in nearby){if(soil.runtimeMesh==null||soil.profile==null||!soil.contactBounds.Contains(point))continue;
            float load=Mathf.Clamp(loadNewtons/Mathf.Max(1,soil.profile.nominalWheelLoadNewtons),.05f,2);
            float spin=Mathf.Clamp((slipSpeed-soil.profile.minimumDigSlipMetresPerSecond)/Mathf.Max(.1f,soil.profile.fullDigSlipMetresPerSecond),0,2);
            var section=soil.GetComponent<MudRoadSection>();float softness=section==null||section.profile==null?1:section.DigAt(point);
            float rate=(soil.profile.rollingCompactionMetresPerSecond+soil.profile.diggingMetresPerSecond*spin)*load*Mathf.Clamp(pressureLoadMultiplier,.2f,2)*softness;
            soil.Stamp(point,forward,width*.65f,soil.profile.footprintLengthMetres*.5f/Mathf.Sqrt(Mathf.Clamp(pressureLoadMultiplier,.3f,2)),rate*seconds,spin>.05f?soil.profile.maximumDepthMetres:soil.profile.settlementDepthMetres);
        }
    }
    // Compatibility for existing diagnostics; contacts in VehiclePhysics use the load-aware path.
    public void Dig(Vector3 point,float intensity,float seconds){Stamp(point,transform.forward,radius,profile==null?radius:profile.footprintLengthMetres*.5f,(profile==null?diggingMetresPerSecond:profile.diggingMetresPerSecond)*Mathf.Max(0,intensity)*seconds);}
    void Stamp(Vector3 point,Vector3 forward,float halfWidth,float halfLength,float amount,float depthLimit=float.MaxValue)
    {
        if(runtimeMesh==null||amount<=0)return;var centre=Cell(point-transform.position);Vector3 along=Vector3.ProjectOnPlane(forward,Vector3.up).normalized;if(along.sqrMagnitude<.1f)along=Vector3.forward;Vector3 across=Vector3.Cross(Vector3.up,along);float reach=Mathf.Max(halfWidth,halfLength);int cells=Mathf.CeilToInt(reach/CellSize)+1;float cap=Mathf.Min(depthLimit,profile==null?maximumDepth:profile.maximumDepthMetres);
        for(int z=-cells;z<=cells;z++)for(int x=-cells;x<=cells;x++)if(grid.TryGetValue(centre+new Vector2Int(x,z),out var indices))foreach(int i in indices){Vector3 world=transform.TransformPoint(original[i]);if(Mathf.Abs(world.y-point.y)>cap+.5f)continue;Vector3 delta=world-point;float side=Vector3.Dot(delta,across)/Mathf.Max(.05f,halfWidth),length=Vector3.Dot(delta,along)/Mathf.Max(.05f,halfLength);float distance=side*side+length*length;if(distance>=1)continue;float edge=1;if(fieldHalfSize.x>0&&fieldHalfSize.y>0){float metres=Mathf.Min(fieldHalfSize.x-Mathf.Abs(original[i].x),fieldHalfSize.y-Mathf.Abs(original[i].z));edge=Mathf.SmoothStep(0,1,Mathf.Clamp01(metres/Mathf.Max(.1f,profile==null?.6f:profile.edgeTaperMetres)));}float next=Mathf.Max(depths[i],Mathf.Min(cap,depths[i]+amount*(1-distance)*(1-distance)*edge));if(next<=depths[i])continue;depths[i]=next;vertices[i]=original[i]+transform.InverseTransformVector(Vector3.down*next);DugDepth=Mathf.Max(DugDepth,next);dirty=true;}
    }
    public static void AdvanceAll(float seconds){foreach(var soil in active)if(soil.dirty){soil.clock+=seconds;if(soil.clock>=(soil.profile==null?.16f:soil.profile.collisionUpdateSeconds))soil.Commit();}}
    public void Commit(){if(!dirty||runtimeMesh==null)return;runtimeMesh.vertices=vertices;runtimeMesh.RecalculateNormals();runtimeMesh.RecalculateBounds();runtimeMesh.RecalculateTangents();collisionMesh.vertices=vertices;collisionMesh.RecalculateBounds();physical.sharedMesh=null;physical.sharedMesh=collisionMesh;dirty=false;clock=0;CommitCount++;}
    public float DepthAt(Vector3 point){if(depths==null)return 0;var centre=Cell(point-transform.position);float best=float.MaxValue,value=0;for(int z=-1;z<=1;z++)for(int x=-1;x<=1;x++)if(grid.TryGetValue(centre+new Vector2Int(x,z),out var indices))foreach(int i in indices){var world=transform.TransformPoint(original[i]);float d=new Vector2(world.x-point.x,world.z-point.z).sqrMagnitude;if(d<best){best=d;value=depths[i];}}return value;}
    void OnDestroy(){active.Remove(this);Unregister();Retain();if(runtimeMesh!=null)Destroy(runtimeMesh);if(collisionMesh!=null)Destroy(collisionMesh);}
}
