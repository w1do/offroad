using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(200)]
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
    float[] depths,compaction,berms,vertexAreas;float clock;bool dirty;
    public float LastPressurePascal {get;private set;}
    public float MaximumCompaction {get;private set;}
    static int commitCursor;
    static readonly Dictionary<string,float[]> retainedCompaction=new Dictionary<string,float[]>();
    static readonly Dictionary<string,float[]> retainedBerms=new Dictionary<string,float[]>();
    readonly Dictionary<Vector2Int,List<int>> grid=new Dictionary<Vector2Int,List<int>>();
    static readonly List<SoftGroundRuts> active=new List<SoftGroundRuts>();
    static readonly Dictionary<Vector2Int,List<SoftGroundRuts>> neighbourhood=new Dictionary<Vector2Int,List<SoftGroundRuts>>();
    readonly List<Vector2Int> registeredCells=new List<Vector2Int>();
    Bounds contactBounds;
    readonly Dictionary<Vector2Int,List<int>> triangleGrid=new Dictionary<Vector2Int,List<int>>();
    Vector3[] worldOffsets;
    int[] surfaceTriangles;
    float[] cookedDisplacement;
    float pendingHeightError;
    bool contactPriority;
    public float PendingHeightErrorMetres=>pendingHeightError;
    static Vector2Int Region(Vector3 p)=>new Vector2Int(Mathf.FloorToInt(p.x/4),Mathf.FloorToInt(p.z/4));
    void Unregister(){foreach(var cell in registeredCells)if(neighbourhood.TryGetValue(cell,out var list)){list.Remove(this);if(list.Count==0)neighbourhood.Remove(cell);}registeredCells.Clear();}
    void Register(){Unregister();if(runtimeMesh==null)return;contactBounds=GetComponent<Renderer>().bounds;contactBounds.Expand(new Vector3(2,(profile==null?maximumDepth:profile.maximumDepthMetres)+2,2));var lo=Region(contactBounds.min);var hi=Region(contactBounds.max);for(int z=lo.y;z<=hi.y;z++)for(int x=lo.x;x<=hi.x;x++){var cell=new Vector2Int(x,z);if(!neighbourhood.TryGetValue(cell,out var list))neighbourhood[cell]=list=new List<SoftGroundRuts>();list.Add(this);registeredCells.Add(cell);}}
    static readonly Dictionary<string,float[]> retained=new Dictionary<string,float[]>();
    const float CellSize=.2f;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void NewSession(){retained.Clear();retainedCompaction.Clear();retainedBerms.Clear();active.Clear();neighbourhood.Clear();commitCursor=0;}
    public void ShiftOrigin(Vector3 delta){Register();} // Grid uses offsets relative to the patch, so it moves with its Transform.
    void OnEnable()
    {
        if(!Application.isPlaying)return;if(!active.Contains(this))active.Add(this);if(runtimeMesh!=null){BuildGrid();Register();return;}
        physical=GetComponent<MeshCollider>();var filter=GetComponent<MeshFilter>();if(filter.sharedMesh==null)return;
        runtimeMesh=Instantiate(filter.sharedMesh);runtimeMesh.name="Deformable visible mud";runtimeMesh.MarkDynamic();filter.sharedMesh=runtimeMesh;
        original=runtimeMesh.vertices;vertices=(Vector3[])original.Clone();depths=new float[vertices.Length];compaction=new float[vertices.Length];berms=new float[vertices.Length];vertexAreas=new float[vertices.Length];
        var sourceTriangles=runtimeMesh.triangles;for(int t=0;t<sourceTriangles.Length;t+=3){int a=sourceTriangles[t],b=sourceTriangles[t+1],c=sourceTriangles[t+2];float area=Mathf.Abs(Vector3.Cross(transform.TransformVector(original[b]-original[a]),transform.TransformVector(original[c]-original[a])).y)/6;vertexAreas[a]+=area;vertexAreas[b]+=area;vertexAreas[c]+=area;}
        collisionMesh=Instantiate(runtimeMesh);collisionMesh.name="Matching mud collision";var front=runtimeMesh.triangles;for(int i=0;i<front.Length;i+=3){Vector3 ab=transform.TransformVector(original[front[i+1]]-original[front[i]]),ac=transform.TransformVector(original[front[i+2]]-original[front[i]]);if(Vector3.Cross(ab,ac).y<0){int swap=front[i+1];front[i+1]=front[i+2];front[i+2]=swap;}}collisionMesh.subMeshCount=1;collisionMesh.triangles=front;physical.sharedMesh=collisionMesh;
        BuildGrid();
        if(!string.IsNullOrEmpty(persistentId))RestoreRetained();
        Register();
    }
    void BuildGrid()
    {
        grid.Clear();triangleGrid.Clear();if(original==null)return;
        worldOffsets=new Vector3[original.Length];if(cookedDisplacement==null||cookedDisplacement.Length!=original.Length)cookedDisplacement=new float[original.Length];
        for(int i=0;i<original.Length;i++){worldOffsets[i]=transform.TransformVector(original[i]);var cell=Cell(worldOffsets[i]);if(!grid.TryGetValue(cell,out var list))grid[cell]=list=new List<int>();list.Add(i);}
        if(profile==null){surfaceTriangles=System.Array.Empty<int>();return;}
        surfaceTriangles=runtimeMesh.triangles;
        for(int t=0;t<surfaceTriangles.Length;t+=3)
        {
            var a=worldOffsets[surfaceTriangles[t]];var b=worldOffsets[surfaceTriangles[t+1]];var c=worldOffsets[surfaceTriangles[t+2]];
            var lo=Cell(Vector3.Min(a,Vector3.Min(b,c)));var hi=Cell(Vector3.Max(a,Vector3.Max(b,c)));
            for(int z=lo.y;z<=hi.y;z++)for(int x=lo.x;x<=hi.x;x++){var key=new Vector2Int(x,z);if(!triangleGrid.TryGetValue(key,out var list))triangleGrid[key]=list=new List<int>();list.Add(t);}
        }
    }
    public static bool TrySampleSurface(Vector3 point,UnityEngine.SceneManagement.Scene scene,out SoftGroundSurfaceSample sample)
    {
        sample=default;bool found=false;
        if(!neighbourhood.TryGetValue(Region(point),out var candidates))return false;
        foreach(var ground in candidates)
        {
            if(ground.profile==null||ground.gameObject.scene!=scene||!ground.contactBounds.Contains(point)||!ground.SampleSurface(point,out var candidate))continue;
            if(!found||candidate.point.y>sample.point.y){sample=candidate;found=true;}
        }
        return found;
    }
    public bool SampleSurface(Vector3 point,out SoftGroundSurfaceSample sample)
    {
        sample=default;if(worldOffsets==null||!triangleGrid.TryGetValue(Cell(point-transform.position),out var candidates))return false;
        Vector3 p=point-transform.position;bool found=false;float best=float.NegativeInfinity;
        foreach(int t in candidates)
        {
            int ia=surfaceTriangles[t],ib=surfaceTriangles[t+1],ic=surfaceTriangles[t+2];
            var a=worldOffsets[ia];var b=worldOffsets[ib];var c=worldOffsets[ic];
            float det=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);if(Mathf.Abs(det)<.0000001f)continue;
            float u=((b.z-c.z)*(p.x-c.x)+(c.x-b.x)*(p.z-c.z))/det;
            float v=((c.z-a.z)*(p.x-c.x)+(a.x-c.x)*(p.z-c.z))/det;
            if(u<-.0001f||v<-.0001f||u+v>1.0001f)continue;float w=1-u-v;
            float cooked=a.y*u+b.y*v+c.y*w+cookedDisplacement[ia]*u+cookedDisplacement[ib]*v+cookedDisplacement[ic]*w;
            a.y+=berms[ia]-depths[ia];b.y+=berms[ib]-depths[ib];c.y+=berms[ic]-depths[ic];
            float height=a.y*u+b.y*v+c.y*w;if(height<best)continue;Vector3 normal=Vector3.Cross(b-a,c-a).normalized;if(normal.y<0)normal=-normal;
            sample=new SoftGroundSurfaceSample{ground=this,point=new Vector3(point.x,transform.position.y+height,point.z),normal=normal,colliderHeightMetres=transform.position.y+cooked};best=height;found=true;
        }
        return found;
    }
    void NotePending(int i){pendingHeightError=Mathf.Max(pendingHeightError,Mathf.Abs(berms[i]-depths[i]-cookedDisplacement[i]));}
    public void RequestWheelContactUpdate(){contactPriority=true;}
    bool Urgent=>dirty&&contactPriority&&pendingHeightError>=(profile==null?.006f:profile.wheelContactHeightToleranceMetres);
    // Editor/LOD rebuilds can replace a derived mesh without erasing the current tracks.
    public void RebuildFromSource(Mesh source)
    {
        if(source==null||runtimeMesh==null)return;var fresh=source.vertices;var saved=new float[fresh.Length];var savedPacking=new float[fresh.Length];var savedBanks=new float[fresh.Length];
        var packing=new Dictionary<Vector2Int,float>();var banks=new Dictionary<Vector2Int,float>();var samples=new Dictionary<Vector2Int,float>();const float sample=.08f;
        for(int i=0;i<original.Length;i++){var key=new Vector2Int(Mathf.RoundToInt(original[i].x/sample),Mathf.RoundToInt(original[i].z/sample));samples.TryGetValue(key,out float old);samples[key]=Mathf.Max(old,depths[i]);packing.TryGetValue(key,out float oldPack);packing[key]=Mathf.Max(oldPack,compaction[i]);banks.TryGetValue(key,out float oldBank);banks[key]=Mathf.Max(oldBank,berms[i]);}
        for(int i=0;i<fresh.Length;i++){var key=new Vector2Int(Mathf.RoundToInt(fresh[i].x/sample),Mathf.RoundToInt(fresh[i].z/sample));float value=0;for(int z=-1;z<=1;z++)for(int x=-1;x<=1;x++)if(samples.TryGetValue(key+new Vector2Int(x,z),out float depth))value=Mathf.Max(value,depth);saved[i]=value;packing.TryGetValue(key,out savedPacking[i]);banks.TryGetValue(key,out savedBanks[i]);}
        var oldVisible=runtimeMesh;var oldPhysical=collisionMesh;Unregister();runtimeMesh=null;collisionMesh=null;DugDepth=0;GetComponent<MeshFilter>().sharedMesh=source;OnEnable();
        for(int i=0;i<saved.Length;i++){depths[i]=saved[i];compaction[i]=savedPacking[i];berms[i]=savedBanks[i];vertices[i]=original[i]+transform.InverseTransformVector(Vector3.up*(berms[i]-depths[i]));DugDepth=Mathf.Max(DugDepth,saved[i]);}dirty=true;Commit();Register();Destroy(oldVisible);Destroy(oldPhysical);
    }
    void OnDisable(){active.Remove(this);Unregister();Retain();}
    static Vector2Int Cell(Vector3 p)=>new Vector2Int(Mathf.FloorToInt(p.x/CellSize),Mathf.FloorToInt(p.z/CellSize));
    public void BindPersistence(string key){persistentId=key;RestoreRetained();var tracks=GetComponent<TireTrackRenderer>();if(tracks!=null)tracks.BindPersistence(key);}
    void Retain(){if(depths!=null&&!string.IsNullOrEmpty(persistentId)&&DugDepth>0){retained[persistentId]=(float[])depths.Clone();retainedCompaction[persistentId]=(float[])compaction.Clone();retainedBerms[persistentId]=(float[])berms.Clone();}}
    void RestoreRetained(){if(depths==null||!retained.TryGetValue(persistentId,out var saved)||saved.Length!=depths.Length)return;System.Array.Copy(saved,depths,depths.Length);if(retainedBerms.TryGetValue(persistentId,out var banks)&&banks.Length==berms.Length)System.Array.Copy(banks,berms,banks.Length);if(retainedCompaction.TryGetValue(persistentId,out var packed)&&packed.Length==compaction.Length){System.Array.Copy(packed,compaction,packed.Length);foreach(float c in packed)MaximumCompaction=Mathf.Max(MaximumCompaction,c);}for(int i=0;i<vertices.Length;i++){vertices[i]=original[i]+transform.InverseTransformVector(Vector3.up*(berms[i]-depths[i]));DugDepth=Mathf.Max(DugDepth,depths[i]);}dirty=true;Commit();}
    public static void Contact(Vector3 point,Vector3 forward,float width,float loadNewtons,float slipSpeed,float pressureLoadMultiplier,float seconds,float contactAreaSquareMetres=0,UnityEngine.SceneManagement.Scene? contactScene=null)
    {
        if(loadNewtons<=0||seconds<=0||!neighbourhood.TryGetValue(Region(point),out var nearby))return;
        foreach(var soil in nearby){if(soil.runtimeMesh==null||soil.profile==null||(contactScene.HasValue&&soil.gameObject.scene!=contactScene.Value)||!soil.contactBounds.Contains(point))continue;
            float load=Mathf.Clamp(loadNewtons/Mathf.Max(1,soil.profile.nominalWheelLoadNewtons),.05f,2);
            float spin=Mathf.Clamp((slipSpeed-soil.profile.minimumDigSlipMetresPerSecond)/Mathf.Max(.1f,soil.profile.fullDigSlipMetresPerSecond),0,2);
            var section=soil.GetComponent<MudRoadSection>();float softness=section==null||section.profile==null?1:section.DigAt(point);
            float rate=(soil.profile.rollingCompactionMetresPerSecond+soil.profile.diggingMetresPerSecond*spin)*load*Mathf.Clamp(pressureLoadMultiplier,.2f,2)*softness;
            float limit=spin>.05f?soil.profile.maximumDepthMetres:soil.profile.settlementDepthMetres;
            if(soil.profile.soil!=null){var model=soil.profile.soil;float area=contactAreaSquareMetres>0?contactAreaSquareMetres:width*soil.profile.footprintLengthMetres/Mathf.Clamp(pressureLoadMultiplier,.2f,2);soil.LastPressurePascal=Mathf.Max(0,loadNewtons)/Mathf.Max(.001f,area);float packed=soil.CompactionAt(point);float target=model.SinkageMetres(soil.LastPressurePascal,packed,slipSpeed);limit=Mathf.Min(soil.profile.maximumDepthMetres,model.rutDepthLimitMetres);float remaining=Mathf.Max(0,target-soil.DepthAt(point));rate=Mathf.Min(rate,remaining/Mathf.Max(.001f,seconds));}
            soil.Stamp(point,forward,width*.65f,soil.profile.footprintLengthMetres*.5f/Mathf.Sqrt(Mathf.Clamp(pressureLoadMultiplier,.3f,2)),rate*seconds,limit);soil.RequestWheelContactUpdate();
        }
    }
    public static void RecordTread(Vector3 point,Vector3 forward,float width,float length,float phase,float repeat,float wetness,UnityEngine.SceneManagement.Scene scene)
    {
        if(!neighbourhood.TryGetValue(Region(point),out var nearby))return;
        foreach(var ground in nearby)
        {
            if(ground.gameObject.scene!=scene||!ground.contactBounds.Contains(point))continue;
            // Native road decals can overlap the road bed. Mark both visible layers.
            var tracks=ground.GetComponent<TireTrackRenderer>();
            if(tracks!=null)tracks.Stamp(point,forward,width,length,phase,repeat,wetness);
        }
    }
    // Compatibility for existing diagnostics; contacts in VehiclePhysics use the load-aware path.
    public void Dig(Vector3 point,float intensity,float seconds){Stamp(point,transform.forward,radius,profile==null?radius:profile.footprintLengthMetres*.5f,(profile==null?diggingMetresPerSecond:profile.diggingMetresPerSecond)*Mathf.Max(0,intensity)*seconds);}
    void Stamp(Vector3 point,Vector3 forward,float halfWidth,float halfLength,float amount,float depthLimit=float.MaxValue)
    {
        if(runtimeMesh==null||amount<=0)return;var centre=Cell(point-transform.position);Vector3 along=Vector3.ProjectOnPlane(forward,Vector3.up).normalized;if(along.sqrMagnitude<.1f)along=Vector3.forward;Vector3 across=Vector3.Cross(Vector3.up,along);float reach=Mathf.Max(halfWidth,halfLength);int cells=Mathf.CeilToInt(reach*1.7f/CellSize)+1;float displacedVolume=0;float cap=Mathf.Min(depthLimit,profile==null?maximumDepth:profile.maximumDepthMetres);
        for(int z=-cells;z<=cells;z++)for(int x=-cells;x<=cells;x++)if(grid.TryGetValue(centre+new Vector2Int(x,z),out var indices))foreach(int i in indices){Vector3 world=transform.TransformPoint(original[i]);if(Mathf.Abs(world.y-point.y)>cap+.5f)continue;Vector3 delta=world-point;float side=Vector3.Dot(delta,across)/Mathf.Max(.05f,halfWidth),length=Vector3.Dot(delta,along)/Mathf.Max(.05f,halfLength);float distance=side*side+length*length;if(distance>=1)continue;float edge=1;if(fieldHalfSize.x>0&&fieldHalfSize.y>0){float metres=Mathf.Min(fieldHalfSize.x-Mathf.Abs(original[i].x),fieldHalfSize.y-Mathf.Abs(original[i].z));edge=Mathf.SmoothStep(0,1,Mathf.Clamp01(metres/Mathf.Max(.1f,profile==null?.6f:profile.edgeTaperMetres)));}float next=Mathf.Max(depths[i],Mathf.Min(cap,depths[i]+amount*(1-distance)*(1-distance)*edge));if(next<=depths[i])continue;float change=next-depths[i];float removedBank=Mathf.Min(berms[i],change);berms[i]-=removedBank;displacedVolume+=(change+removedBank)*vertexAreas[i];compaction[i]=Mathf.Clamp01(compaction[i]+(next-depths[i])*.5f);MaximumCompaction=Mathf.Max(MaximumCompaction,compaction[i]);depths[i]=next;vertices[i]=original[i]+transform.InverseTransformVector(Vector3.up*(berms[i]-next));DugDepth=Mathf.Max(DugDepth,next);NotePending(i);dirty=true;}
        if(profile!=null&&displacedVolume>0&&profile.maximumBermHeightMetres>0)
        {
            float weightArea=0;
            for(int z=-cells;z<=cells;z++)for(int x=-cells;x<=cells;x++)if(grid.TryGetValue(centre+new Vector2Int(x,z),out var indices))foreach(int i in indices)weightArea+=BermWeight(i,point,along,across,halfWidth,halfLength,cap)*vertexAreas[i];
            if(weightArea>0)for(int z=-cells;z<=cells;z++)for(int x=-cells;x<=cells;x++)if(grid.TryGetValue(centre+new Vector2Int(x,z),out var indices))foreach(int i in indices){float weight=BermWeight(i,point,along,across,halfWidth,halfLength,cap);if(weight<=0)continue;berms[i]=Mathf.Min(profile.maximumBermHeightMetres,berms[i]+displacedVolume*profile.displacedSoilFraction*weight/weightArea);vertices[i]=original[i]+transform.InverseTransformVector(Vector3.up*(berms[i]-depths[i]));NotePending(i);dirty=true;}
        }
    }
    float BermWeight(int i,Vector3 point,Vector3 along,Vector3 across,float halfWidth,float halfLength,float cap)
    {
        Vector3 world=transform.TransformPoint(original[i]);if(Mathf.Abs(world.y-point.y)>cap+.5f)return 0;
        Vector3 delta=world-point;float side=Mathf.Abs(Vector3.Dot(delta,across))/Mathf.Max(.05f,halfWidth),length=Mathf.Abs(Vector3.Dot(delta,along))/Mathf.Max(.05f,halfLength);
        if(side<1||side>1.65f||length>1)return 0;
        float edge=1;if(fieldHalfSize.x>0&&fieldHalfSize.y>0)edge=Mathf.Clamp01(Mathf.Min(fieldHalfSize.x-Mathf.Abs(original[i].x),fieldHalfSize.y-Mathf.Abs(original[i].z))/Mathf.Max(.1f,profile.edgeTaperMetres));
        return Mathf.Sin((side-1)/.65f*Mathf.PI)*(1-length*length)*edge*(.85f+.15f*Mathf.Sin(world.x*17+world.z*11));
    }
    void FixedUpdate(){if(active.Count>0&&active[0]==this)AdvanceAll(Time.fixedDeltaTime);}
    public static void AdvanceAll(float seconds)
    {
        if(active.Count==0)return;int budget=int.MaxValue;
        foreach(var soil in active){soil.Recover(seconds);if(soil.dirty)soil.clock+=seconds;budget=Mathf.Min(budget,soil.profile==null?2:Mathf.Max(1,soil.profile.maximumColliderCommitsPerStep));}
        int start=commitCursor%active.Count,last=start;
        for(int pass=0;pass<2&&budget>0;pass++)for(int visited=0;visited<active.Count&&budget>0;visited++)
        {
            int index=(start+visited)%active.Count;var ground=active[index];
            bool eligible=pass==0?ground.Urgent:ground.dirty&&ground.clock>=(ground.profile==null?.16f:ground.profile.collisionUpdateSeconds);
            if(!eligible)continue;ground.Commit();budget--;last=(index+1)%active.Count;
        }
        commitCursor=last;
    }

    void Recover(float seconds)
    {
        float rate=profile==null||profile.soil==null?0:profile.soil.recoveryMetresPerSecond;if(rate<=0||depths==null)return;
        DugDepth=0;for(int i=0;i<depths.Length;i++){float depth=Mathf.Max(0,depths[i]-rate*seconds),bank=Mathf.Max(0,berms[i]-rate*seconds);if(depth!=depths[i]||bank!=berms[i]){depths[i]=depth;berms[i]=bank;vertices[i]=original[i]+transform.InverseTransformVector(Vector3.up*(bank-depth));NotePending(i);dirty=true;}DugDepth=Mathf.Max(DugDepth,depth);}
    }
    public float CompactionAt(Vector3 point)
    {
        if(compaction==null)return 0;var centre=Cell(point-transform.position);float best=float.MaxValue,value=0;
        for(int z=-1;z<=1;z++)for(int x=-1;x<=1;x++)if(grid.TryGetValue(centre+new Vector2Int(x,z),out var indices))foreach(int i in indices){var w=transform.TransformPoint(original[i]);float d=new Vector2(w.x-point.x,w.z-point.z).sqrMagnitude;if(d<best){best=d;value=compaction[i];}}
        return value;
    }
    public void Commit(){if(!dirty||runtimeMesh==null)return;runtimeMesh.vertices=vertices;runtimeMesh.RecalculateNormals();runtimeMesh.RecalculateBounds();runtimeMesh.RecalculateTangents();collisionMesh.vertices=vertices;collisionMesh.RecalculateBounds();physical.sharedMesh=null;physical.sharedMesh=collisionMesh;for(int i=0;i<cookedDisplacement.Length;i++)cookedDisplacement[i]=berms[i]-depths[i];pendingHeightError=0;contactPriority=false;dirty=false;clock=0;CommitCount++;}
    public float DepthAt(Vector3 point){if(depths==null)return 0;var centre=Cell(point-transform.position);float best=float.MaxValue,value=0;for(int z=-1;z<=1;z++)for(int x=-1;x<=1;x++)if(grid.TryGetValue(centre+new Vector2Int(x,z),out var indices))foreach(int i in indices){var world=transform.TransformPoint(original[i]);float d=new Vector2(world.x-point.x,world.z-point.z).sqrMagnitude;if(d<best){best=d;value=Mathf.Max(0,depths[i]-berms[i]);}}return value;}
    void OnDestroy(){active.Remove(this);Unregister();Retain();if(runtimeMesh!=null)Destroy(runtimeMesh);if(collisionMesh!=null)Destroy(collisionMesh);}
}
