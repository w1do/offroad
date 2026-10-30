using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Global route coordinates use doubles; only the currently loaded neighbourhood uses Unity floats.
[DefaultExecutionOrder(-200)]
public sealed class LongRouteStream : MonoBehaviour
{
    public double lengthMetres = 10000000;
    public Vector3 entrance = new Vector3(-94, 36, 28);
    public Vector3 initialForward = Vector3.left;
    public Vector3[] openingWaypoints;
    public Transform truck;
    public Material roadMaterial, groundMaterial;
    public GameObject[] treeTemplates;
    public GameObject editorPreview;
    public int seed = 104729;
    public int chunkMetres = 200;
    public int ahead = 5, behind = 2;
    public float originThreshold = 1500;
    public float[] joinHeights;
    public int joinRows = 81;
    public float[] originalGroundHeights;
    public Vector2 originalGroundMinimum;
    public int originalGroundWidth,originalGroundDepth;
    public float originalGroundStep=8;
    public const int AcrossCount = 19;
    public double ProgressMetres { get; private set; }
    public double OriginX { get; private set; }
    public double OriginY { get; private set; }
    public double OriginZ { get; private set; }
    public int LoadedChunks => chunks.Count;
    public int OriginShifts { get; private set; }
    public double FurthestMetres { get; private set; }
    const double SampleStep = 20;
    [NonSerialized] double[] pathX, pathZ, pathY;
    readonly Dictionary<int, GameObject> chunks = new Dictionary<int, GameObject>();
    readonly Dictionary<Vector2Int,GameObject> terrainTiles = new Dictionary<Vector2Int,GameObject>();
    readonly Dictionary<Vector2Int,List<int>> pathCells = new Dictionary<Vector2Int,List<int>>();
    int lastChunk = -1;
    static readonly float[] across = { -180,-130,-90,-60,-40,-25,-14,-8,-5,0,5,8,14,25,40,60,90,130,180 };
    public void ResetPath(){pathX=null;pathCells.Clear();}

    public void EnsurePath()
    {
        if (pathX != null && pathX.Length>1 && pathZ!=null && pathY!=null && pathZ.Length==pathX.Length && pathY.Length==pathX.Length && pathCells.Count>0) return;
        int count = (int)Math.Ceiling(lengthMetres / SampleStep) + 1;
        pathX = new double[count]; pathZ = new double[count]; pathY = new double[count];
        pathX[0] = entrance.x; pathY[0] = entrance.y; pathZ[0] = entrance.z;
        double baseHeading = Math.Atan2(initialForward.x, initialForward.z);
        var intro = new List<Vector3>();var lengths=new List<double>();double introLength=0;
        if(openingWaypoints!=null&&openingWaypoints.Length>2)
        {
            for(int segment=0;segment<openingWaypoints.Length-1;segment++)for(int n=0;n<100;n++)
            {
                float t=n/100f;Vector3 a=openingWaypoints[Math.Max(0,segment-1)],b=openingWaypoints[segment],c=openingWaypoints[segment+1],d=openingWaypoints[Math.Min(openingWaypoints.Length-1,segment+2)];
                Vector3 p=.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t);
                if(intro.Count>0)introLength+=Vector3.Distance(p,intro[intro.Count-1]);intro.Add(p);lengths.Add(introLength);
            }
            introLength+=Vector3.Distance(intro[intro.Count-1],openingWaypoints[openingWaypoints.Length-1]);intro.Add(openingWaypoints[openingWaypoints.Length-1]);lengths.Add(introLength);
        }
        int introIndex=0;double startProcedural=0;
        for (int i = 1; i < count; i++)
        {
            double s = (i - .5) * SampleStep;
            if(i*SampleStep<=introLength)
            {
                double at=i*SampleStep;while(introIndex<lengths.Count-2&&lengths[introIndex+1]<at)introIndex++;
                var p=Vector3.Lerp(intro[introIndex],intro[introIndex+1],(float)((at-lengths[introIndex])/(lengths[introIndex+1]-lengths[introIndex])));
                pathX[i]=p.x;pathZ[i]=p.z;double introHorizontal=Math.Sqrt(Math.Pow(p.x-pathX[i-1],2)+Math.Pow(p.z-pathZ[i-1],2));pathY[i]=Math.Max(pathY[i-1]-.175*introHorizontal,Math.Min(pathY[i-1]+.175*introHorizontal,p.y));
                baseHeading=Math.Atan2(pathX[i]-pathX[i-1],pathZ[i]-pathZ[i-1]);startProcedural=i*SampleStep;continue;
            }
            double q=s-startProcedural,blend = Math.Min(1, q / 220);
            double heading = baseHeading + blend * (1.10 * Math.Sin(q / 410) + .65 * Math.Sin(q / 137) + .24 * Math.Sin(q / 57));
            double grade = blend * (.09 * Math.Sin(s / 185) + .07 * Math.Sin(s / 670) + .045 * Math.Sin(s / 61));
            grade = Math.Max(-.18, Math.Min(.18, grade));
            double horizontal = SampleStep / Math.Sqrt(1 + grade * grade);
            pathX[i] = pathX[i-1] + Math.Sin(heading) * horizontal;
            pathZ[i] = pathZ[i-1] + Math.Cos(heading) * horizontal;
            pathY[i] = pathY[i-1] + grade * horizontal;
        }
        for(int i=0;i<count-1;i++){var key=new Vector2Int((int)Math.Floor(pathX[i]/128),(int)Math.Floor(pathZ[i]/128));if(!pathCells.TryGetValue(key,out var list))pathCells[key]=list=new List<int>();list.Add(i);}
    }

    public Vector3 Point(double distance)
    {
        EnsurePath(); double t = Math.Max(0, Math.Min(lengthMetres, distance)) / SampleStep;
        int i = Math.Min(pathX.Length - 2, (int)t); double u = t-i;
        return new Vector3((float)(pathX[i]+(pathX[i+1]-pathX[i])*u-OriginX),
            (float)(pathY[i]+(pathY[i+1]-pathY[i])*u-OriginY),
            (float)(pathZ[i]+(pathZ[i+1]-pathZ[i])*u-OriginZ));
    }
    public Vector3 Tangent(double s) => (Point(Math.Min(lengthMetres,s+2))-Point(Math.Max(0,s-2))).normalized;
    public float TerrainHeight(double x,double z)
    {
        EnsurePath();var key=new Vector2Int((int)Math.Floor(x/128),(int)Math.Floor(z/128));double best=double.MaxValue,sBest=0,cross=0,totalWeight=0,weightedHeight=0;
        for(int oz=-2;oz<=2;oz++)for(int ox=-2;ox<=2;ox++)if(pathCells.TryGetValue(key+new Vector2Int(ox,oz),out var list))foreach(int i in list)
        {
            double dx=pathX[i+1]-pathX[i],dz=pathZ[i+1]-pathZ[i],px=x-pathX[i],pz=z-pathZ[i];double t=Math.Max(0,Math.Min(1,(px*dx+pz*dz)/(dx*dx+dz*dz)));double ex=px-t*dx,ez=pz-t*dz,dist=ex*ex+ez*ez;
            double weight=1/((dist+400)*(dist+400));totalWeight+=weight;weightedHeight+=(pathY[i]+t*(pathY[i+1]-pathY[i]))*weight;
            if(dist<best){best=dist;sBest=(i+t)*SampleStep;cross=(ex*dz-ez*dx)/Math.Sqrt(dx*dx+dz*dz);}
        }
        if(best==double.MaxValue)return -50-(float)OriginY;
        double distance=Math.Sqrt(best),edge=Math.Max(0,distance-7);
        float hill=(float)(edge*(.13+.08*Math.Sin(x/130+z/190))+Math.Min(1,edge/60)*10*Math.Sin(x/210-z/170));
        float terrain=(float)(weightedHeight/totalWeight-OriginY)+hill;
        if(originalGroundHeights!=null&&originalGroundHeights.Length==originalGroundWidth*originalGroundDepth)
        {
            double gx=(x-originalGroundMinimum.x)/originalGroundStep,gz=(z-originalGroundMinimum.y)/originalGroundStep;
            if(gx>=0&&gz>=0&&gx<originalGroundWidth-1&&gz<originalGroundDepth-1){int ix=(int)gx,iz=(int)gz;float old=Mathf.Lerp(Mathf.Lerp(originalGroundHeights[iz*originalGroundWidth+ix],originalGroundHeights[iz*originalGroundWidth+ix+1],(float)(gx-ix)),Mathf.Lerp(originalGroundHeights[(iz+1)*originalGroundWidth+ix],originalGroundHeights[(iz+1)*originalGroundWidth+ix+1],(float)(gx-ix)),(float)(gz-iz))-(float)OriginY;
                float boundary=(float)Math.Min(Math.Min(gx,gz),Math.Min(originalGroundWidth-1-gx,originalGroundDepth-1-gz))*originalGroundStep;
                float retain=Mathf.SmoothStep(0,1,boundary/24)*Mathf.SmoothStep(0,1,Mathf.InverseLerp(12,65,(float)distance));terrain=Mathf.Lerp(terrain,old-.18f,retain);
            }
        }
        return Mathf.Lerp(Point(sBest).y-.25f,terrain,Mathf.SmoothStep(0,1,Mathf.InverseLerp(9,16,(float)distance)));
    }
    public float DistanceToRoad(double x,double z)
    {
        EnsurePath();
        var key=new Vector2Int((int)Math.Floor(x/128),(int)Math.Floor(z/128));double best=double.MaxValue;
        for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)if(pathCells.TryGetValue(key+new Vector2Int(dx,dz),out var list))foreach(int i in list){double vx=pathX[i+1]-pathX[i],vz=pathZ[i+1]-pathZ[i],px=x-pathX[i],pz=z-pathZ[i],t=Math.Max(0,Math.Min(1,(px*vx+pz*vz)/(vx*vx+vz*vz)));best=Math.Min(best,(px-t*vx)*(px-t*vx)+(pz-t*vz)*(pz-t*vz));}return (float)Math.Sqrt(best);
    }
    public HashSet<Vector2Int> DesiredTerrain(int current)
    {
        var desired=new HashSet<Vector2Int>();int final=(int)Math.Ceiling(lengthMetres/chunkMetres)-1;
        for(int i=Math.Max(0,current-behind);i<=Math.Min(final,current+ahead);i++){var p=Point(i*chunkMetres);int x=(int)Math.Floor((p.x+OriginX)/64),z=(int)Math.Floor((p.z+OriginZ)/64);for(int dz=-3;dz<=3;dz++)for(int dx=-3;dx<=3;dx++)desired.Add(new Vector2Int(x+dx,z+dz));}return desired;
    }
    public GameObject BuildTerrainTile(Vector2Int key)
    {
        const int side=33;var root=new GameObject("Terrain tile "+key.x+" "+key.y);root.transform.SetParent(transform,false);root.transform.position=new Vector3((float)(key.x*64.0-OriginX),-(float)OriginY,(float)(key.y*64.0-OriginZ));var v=new Vector3[side*side];var uv=new Vector2[v.Length];var triangles=new List<int>();
        for(int z=0;z<side;z++)for(int x=0;x<side;x++){int n=z*side+x;double wx=key.x*64.0+x*2,wz=key.y*64.0+z*2;v[n]=new Vector3(x*2,TerrainHeight(wx,wz)+(float)OriginY,z*2);uv[n]=new Vector2(x*.5f,z*.5f);if(x<side-1&&z<side-1)triangles.AddRange(new[]{n,n+side,n+1,n+1,n+side,n+side+1});}
        var mesh=new Mesh{name=root.name};mesh.vertices=v;mesh.uv=uv;mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();root.AddComponent<MeshFilter>().sharedMesh=mesh;root.AddComponent<MeshRenderer>().sharedMaterial=groundMaterial;root.AddComponent<MeshCollider>().sharedMesh=mesh;return root;
    }
    public float CrossHeight(double s, float cross)
    {
        if(Mathf.Abs(cross)<=8)return Point(s).y-Mathf.Abs(cross)*.012f;
        float edge = Mathf.Max(0,Mathf.Abs(cross)-6);
        float hill = edge * (.16f + .18f*(float)Math.Sin(s/270 + (cross>0?0:2)))
            + Mathf.SmoothStep(0,1,edge/70)*18*(float)Math.Sin(s/170 + cross/65);
        float procedural = Point(s).y + (Mathf.Abs(cross)<=5 ? -Mathf.Abs(cross)*.012f : hill);
        if (s<=800 && joinHeights!=null && joinHeights.Length==joinRows*AcrossCount)
        {
            float row=(float)s/10;int a=Mathf.Clamp(Mathf.FloorToInt(row),0,joinRows-2);int j=0;
            while(j<AcrossCount-2&&across[j+1]<cross)j++;
            float u=Mathf.InverseLerp(across[j],across[j+1],cross);
            float original=Mathf.Lerp(Mathf.Lerp(joinHeights[a*AcrossCount+j],joinHeights[a*AcrossCount+j+1],u),Mathf.Lerp(joinHeights[(a+1)*AcrossCount+j],joinHeights[(a+1)*AcrossCount+j+1],u),row-a)-(float)OriginY;
            float roadBlend=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(6,20,Mathf.Abs(cross)));
            float endBlend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(400,800,(float)s));
            return Mathf.Lerp(original,procedural,Mathf.Max(roadBlend,endBlend));
        }
        return procedural;
    }

    void Start(){if(editorPreview!=null)editorPreview.SetActive(false);EnsurePath();EnsureWindow(0,true);}
    void Update()
    {
        if(truck==null)return;
        UpdateProgress();
        int current=(int)(ProgressMetres/chunkMetres);
        EnsureWindow(current,false);
    }
    void FixedUpdate()
    {
        if(truck!=null && truck.position.sqrMagnitude>originThreshold*originThreshold) ShiftOrigin(truck.position);
    }
    public void UpdateProgress()
    {
        if(truck==null)return;
        double best=double.MaxValue,bestS=ProgressMetres;
        int middle=(int)(ProgressMetres/SampleStep),lo=Math.Max(0,middle-40),hi=Math.Min(pathX.Length-2,middle+80);
        for(int i=lo;i<=hi;i++)
        {
            Vector3 a=Point(i*SampleStep),b=Point((i+1)*SampleStep),d=b-a;
            float t=Mathf.Clamp01(Vector3.Dot(truck.position-a,d)/d.sqrMagnitude);
            double ds=(truck.position-(a+d*t)).sqrMagnitude;
            if(ds<best){best=ds;bestS=(i+t)*SampleStep;}
        }
        if(best<140*140){ProgressMetres=bestS;FurthestMetres=Math.Max(FurthestMetres,bestS);}
    }
    public void EnsureWindow(int current,bool immediate)
    {
        int final=(int)Math.Ceiling(lengthMetres/chunkMetres)-1;
        current=Mathf.Clamp(current,0,final);
        for(int i=Math.Max(0,current-behind);i<=Math.Min(final,current+ahead);i++)
            if(!chunks.ContainsKey(i)){chunks.Add(i,BuildChunk(i));if(!immediate)break;}
        // Never unload the occupied chunk or its neighbours, even when generation takes multiple frames.
        var remove=new List<int>();foreach(var pair in chunks)if(pair.Key<current-behind-1||pair.Key>current+ahead+1)remove.Add(pair.Key);
        foreach(int i in remove){DisposeChunk(chunks[i]);chunks.Remove(i);}
        var desired=DesiredTerrain(current);int built=0;foreach(var key in desired)if(!terrainTiles.ContainsKey(key)){terrainTiles.Add(key,BuildTerrainTile(key));if(!immediate&&++built>=3)break;}
        var oldTiles=new List<Vector2Int>();foreach(var pair in terrainTiles)if(!desired.Contains(pair.Key))oldTiles.Add(pair.Key);foreach(var key in oldTiles){DisposeChunk(terrainTiles[key]);terrainTiles.Remove(key);}
        if(lastChunk!=current && chunks.ContainsKey(current))
        {
            lastChunk=current;
            if(truck!=null && ProgressMetres>40 && truck.TryGetComponent<ForestKamazController>(out var car))
                car.SetDebugRecovery(Point(current*chunkMetres+10)+Vector3.up*.6f,Quaternion.LookRotation(Tangent(current*chunkMetres+10),Vector3.up));
        }
    }
    public GameObject BuildChunk(int index)
    {
        double start=(double)index*chunkMetres,end=Math.Min(lengthMetres,start+chunkMetres);
        int rows=(int)Math.Ceiling((end-start)/5)+1;
        var root=new GameObject("Route km "+(start/1000).ToString("F1"));root.transform.SetParent(transform,false);
        root.transform.position=Point(start);Vector3 origin=root.transform.position;
        const int roadColumns=5;float[] roadAcross={-8,-5,0,5,8};var vertices=new Vector3[rows*roadColumns];var uv=new Vector2[vertices.Length];var road=new List<int>();var soil=new List<int>();
        for(int i=0;i<rows;i++)
        {
            double s=Math.Min(end,start+i*5);Vector3 center=Point(s),right=Vector3.Cross(Vector3.up,Tangent(s)).normalized;
            for(int j=0;j<roadColumns;j++)
            {
                int n=i*roadColumns+j;Vector3 p=center+right*roadAcross[j];p.y=CrossHeight(s,roadAcross[j]);
                vertices[n]=p-origin;uv[n]=new Vector2(roadAcross[j]*.25f,(float)(s%4000)*.25f);
                if(i<rows-1&&j<roadColumns-1){var list=j>=1&&j<=2?road:soil;list.AddRange(new[]{n,n+roadColumns,n+1,n+1,n+roadColumns,n+roadColumns+1});}
            }
        }
        var mesh=new Mesh{name=root.name,indexFormat=IndexFormat.UInt32};mesh.vertices=vertices;mesh.uv=uv;mesh.subMeshCount=2;mesh.SetTriangles(road,0);mesh.SetTriangles(soil,1);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
        root.AddComponent<MeshFilter>().sharedMesh=mesh;root.AddComponent<MeshRenderer>().sharedMaterials=new[]{roadMaterial,groundMaterial};root.AddComponent<MeshCollider>().sharedMesh=mesh;
        var surface=root.AddComponent<ForestDrivingSurface>();surface.cover=ForestDrivingSurface.Cover.PackedSnow;surface.forwardGrip=.55f;surface.lateralGrip=.65f;surface.rollingResistance=.02f;surface.chainsImproveGrip=true;surface.chainedForwardGrip=.95f;surface.chainedLateralGrip=.85f;
        if(treeTemplates!=null&&treeTemplates.Length>0)
        {
            var random=new System.Random(seed^index*7919);
            for(int i=0;i<100;i++)
            {
                double s=start+random.NextDouble()*(end-start);float cross=(float)(12+random.NextDouble()*145)*(i%2==0?1:-1);
                if(s<500)continue; // Existing hand-authored forest remains at the entrance.
                Vector3 p=Point(s)+Vector3.Cross(Vector3.up,Tangent(s)).normalized*cross;p.y=TerrainHeight(p.x+OriginX,p.z+OriginZ);
                if(DistanceToRoad(p.x+OriginX,p.z+OriginZ)<13)continue;
                if(Mathf.Abs(p.y-Point(s).y)<.3f)continue;
                var tree=Instantiate(treeTemplates[random.Next(treeTemplates.Length)],p,Quaternion.Euler(0,(float)random.NextDouble()*360,0),root.transform);tree.SetActive(true);float scale=.8f+(float)random.NextDouble()*.5f;tree.transform.localScale=Vector3.one*scale;
            }
        }
        return root;
    }
    static void DisposeChunk(GameObject root)
    {
        var mesh=root.GetComponent<MeshFilter>().sharedMesh;Destroy(root);Destroy(mesh);
    }
    public void ShiftOrigin(Vector3 delta)
    {
        var bodies=UnityEngine.Object.FindObjectsByType<Rigidbody>();
        var positions=new Vector3[bodies.Length];for(int i=0;i<bodies.Length;i++)positions[i]=bodies[i].position-delta;
        foreach(var root in gameObject.scene.GetRootGameObjects())root.transform.position-=delta;
        for(int i=0;i<bodies.Length;i++)bodies[i].position=positions[i];
        foreach(var car in UnityEngine.Object.FindObjectsByType<ForestKamazController>())car.ShiftDebugOrigin(delta);
        foreach(var river in UnityEngine.Object.FindObjectsByType<RiverCurrent>())
            if(river.surfaceHeights!=null)for(int i=0;i<river.surfaceHeights.Length;i++)river.surfaceHeights[i]-=delta.y;
        foreach(var particles in UnityEngine.Object.FindObjectsByType<ParticleSystem>())
            if(particles.main.simulationSpace==ParticleSystemSimulationSpace.World){var p=new ParticleSystem.Particle[particles.particleCount];int n=particles.GetParticles(p);for(int i=0;i<n;i++)p[i].position-=delta;particles.SetParticles(p,n);}
        OriginX+=delta.x;OriginY+=delta.y;OriginZ+=delta.z;OriginShifts++;
        Shader.SetGlobalVector("_ForestOriginOffset",new Vector4((float)OriginX,(float)OriginY,(float)OriginZ,0));
        Physics.SyncTransforms();
    }
    void OnDestroy(){Shader.SetGlobalVector("_ForestOriginOffset",Vector4.zero);}
    void OnGUI()
    {
        GUI.Box(new Rect(18,110,310,72),"ПРОДОЛЖЕНИЕ МАРШРУТА — 10 000 км");
        GUI.Label(new Rect(30,136,290,42),$"Пройдено: {ProgressMetres/1000:F2} км\nОсталось: {(lengthMetres-ProgressMetres)/1000:F2} км");
    }
}


