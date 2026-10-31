using System.Collections.Generic;
using UnityEngine;

// Owns the streamed exploration world and only its generated resources. No vehicle propulsion lives here.
[DefaultExecutionOrder(-100)]
public sealed class ExplorationWorld : MonoBehaviour
{
    public const float TileSize=128;
    public int seed=311026;
    [Range(1,3)] public int loadRadius=2;
    public Transform vehicle;
    public ForestDayNightWeather weather;
    public ForestWind wind;
    public ExplorationPalette palette;
    public GameObject[] replacedRoots;
    public ExplorationMap Map { get; private set; }
    public int LoadedTiles => loaded.Count;
    public int CurrentAdventure { get; private set; }
    public int SessionHazardSeed { get; private set; }
    private readonly Dictionary<Vector2Int,GameObject> loaded=new Dictionary<Vector2Int,GameObject>();
    private readonly Dictionary<string,Vector2> wetness=new Dictionary<string,Vector2>();
    private bool[] previousActive;
    private ForestWeatherProfile originalWeather,runtimeWeather;
    private GameObject lake, distantGround;
    private Texture2D loadedMask;
    private Material distantMaterial;
    private float nextUpdate,nextClimate;
    private readonly List<Vector2Int> wanted=new List<Vector2Int>();

    private void OnEnable()
    {
        if(!Application.isPlaying||palette==null||vehicle==null)return;
        Map=new ExplorationMap(seed);SessionHazardSeed=System.Guid.NewGuid().GetHashCode();wetness.Clear();
        previousActive=new bool[replacedRoots.Length];for(int i=0;i<replacedRoots.Length;i++)if(replacedRoots[i]!=null){previousActive[i]=replacedRoots[i].activeSelf;replacedRoots[i].SetActive(false);}
        originalWeather=weather.profile;runtimeWeather=Instantiate(palette.weather==null?originalWeather:palette.weather);runtimeWeather.automaticWeather=false;runtimeWeather.clearFogDensity=.0006f;runtimeWeather.rainFogDensity=.0048f;weather.profile=runtimeWeather;
        CreateDistantGround();CreateLake();EnsureWindow(true);Physics.SyncTransforms();
    }
    public string StateKey(Vector2Int key,int adventure) => "exploration/organic-v2/"+seed+"/"+key.x+"/"+key.y+"/"+adventure;
    public float RestoreWetness(Vector2Int key,int adventure)
    {
        string id=StateKey(key,adventure);float initial=adventure==6?.98f:adventure==3?.9f:adventure==1?.7f:.08f;
        if(!wetness.TryGetValue(id,out var state))return initial;
        float value=state.x;float elapsed=Mathf.Max(0,Time.time-state.y);var p=new Vector2(-5000+key.x*TileSize,-5000+key.y*TileSize);
        for(float t=0;t<elapsed;t+=5)
        {
            float rain=ExplorationClimate.Sample(p,adventure,state.y+t).x;
            float absorption=Mathf.Lerp(rain*.18f,1,Mathf.InverseLerp(.55f,1,rain));
            value=Mathf.Clamp01(value+(absorption/70-Mathf.Pow(1-rain,3)/420)*Mathf.Min(5,elapsed-t));
        }
        return value;
    }
    public Vector2Int TileAt(Vector3 position) => new Vector2Int(Mathf.FloorToInt((position.x+5000)/TileSize),Mathf.FloorToInt((position.z+5000)/TileSize));
    private bool Valid(Vector2Int key) => key.x>=0&&key.y>=0&&key.x<79&&key.y<79;
    public void EnsureWindow(bool immediate)
    {
        if(Map==null||vehicle==null)return;
        var centre=TileAt(vehicle.position);wanted.Clear();
        for(int z=-loadRadius;z<=loadRadius;z++)for(int x=-loadRadius;x<=loadRadius;x++){var key=centre+new Vector2Int(x,z);if(Valid(key))wanted.Add(key);}
        wanted.Sort((a,b)=>(a-centre).sqrMagnitude.CompareTo((b-centre).sqrMagnitude));
        int built=0;
        foreach(var key in wanted)if(!loaded.ContainsKey(key))
        {
            // Always make the immediate safety ring synchronously before releasing distant colliders.
            if(!immediate&&built>=1)break;
            if(immediate&&(key-centre).sqrMagnitude>2)continue;
            loaded.Add(key,ExplorationTileBuilder.Build(this,key));loadedMask.SetPixel(key.x,key.y,Color.white);loadedMask.Apply();built++;
        }
        var remove=new List<Vector2Int>();foreach(var pair in loaded)if(!wanted.Contains(pair.Key))remove.Add(pair.Key);
        foreach(var key in remove)
        {
            var tile=loaded[key];var winch=vehicle.GetComponent<VehicleWinch>();if(winch!=null&&winch.Pulling&&winch.AnchorTransform!=null&&winch.AnchorTransform.IsChildOf(tile.transform))continue;
            foreach(var rain in tile.GetComponentsInChildren<RainSoakedRoad>()){int adventure=int.Parse(rain.name.Substring("Road adventure ".Length));wetness[StateKey(key,adventure)]=new Vector2(rain.Saturation,Time.time);}
            tile.SetActive(false);Destroy(tile);loaded.Remove(key);loadedMask.SetPixel(key.x,key.y,Color.black);loadedMask.Apply();
        }
    }
    private void Update()
    {
        if(Map==null)return;
        if(Time.time>=nextUpdate){nextUpdate=Time.time+.08f;EnsureWindow(false);}
        if(Time.time>=nextClimate)
        {
            nextClimate=Time.time+1;
            var p=new Vector2(vehicle.position.x,vehicle.position.z);var nearby=Map.Nearby(p,600);Map.RoadDistance(p,nearby,out var nearest);CurrentAdventure=nearest==null?0:nearest.adventure;
            var climate=ExplorationClimate.Sample(p,CurrentAdventure,Time.time);weather.SetWeather(Mathf.Lerp(.35f,1,climate.x),climate.x);
            foreach(var rain in GetComponentsInChildren<RainSoakedRoad>()){int adventure=int.Parse(rain.name.Substring("Road adventure ".Length));rain.localRain=ExplorationClimate.Sample(new Vector2(rain.transform.position.x,rain.transform.position.z),adventure,Time.time).x;}
        }
        var current=ExplorationClimate.Sample(new Vector2(vehicle.position.x,vehicle.position.z),CurrentAdventure,Time.time);
        wind.strength=Mathf.MoveTowards(wind.strength,current.y,Time.deltaTime*.15f);
    }
    private void CreateDistantGround()
    {
        loadedMask=new Texture2D(79,79,TextureFormat.RGBA32,false,true){name="Loaded exploration tiles",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
        loadedMask.SetPixels(new Color[79*79]);loadedMask.Apply();Shader.SetGlobalTexture("_ExplorationLoadedMask",loadedMask);
        distantGround=new GameObject("Distant terrain - visual LOD only");distantGround.transform.SetParent(transform,false);
        const int n=200;var vertices=new Vector3[(n+1)*(n+1)];var triangles=new int[n*n*6];
        for(int z=0;z<=n;z++)for(int x=0;x<=n;x++){var p=new Vector2(-5000+x*50,-5000+z*50);vertices[z*(n+1)+x]=new Vector3(p.x,Map.Height(p),p.y);if(x<n&&z<n){int k=(z*n+x)*6,a=z*(n+1)+x;triangles[k]=a;triangles[k+1]=a+n+1;triangles[k+2]=a+1;triangles[k+3]=a+1;triangles[k+4]=a+n+1;triangles[k+5]=a+n+2;}}
        var mesh=new Mesh{name="Distant 10 km terrain"};mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();distantGround.AddComponent<MeshFilter>().sharedMesh=mesh;
        distantMaterial=new Material(Shader.Find("Game/Exploration Distant Ground"));distantMaterial.SetTexture("_BaseMap",palette.forest.HasProperty("_MossMap")?palette.forest.GetTexture("_MossMap"):palette.forest.GetTexture("_BaseMap"));
        var renderer=distantGround.AddComponent<MeshRenderer>();renderer.sharedMaterial=distantMaterial;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;distantGround.AddComponent<RuntimeMeshOwner>().Bind(mesh);distantGround.AddComponent<ExplorationDistantForest>().Bind(this);
    }
    private void CreateLake()
    {
        lake=new GameObject("Forest lake - 1640 x 1160 m");lake.transform.SetParent(transform,false);lake.transform.position=new Vector3(Map.LakeCentre.x,ExplorationMap.LakeLevel,Map.LakeCentre.y);
        var box=lake.AddComponent<BoxCollider>();box.isTrigger=true;box.size=new Vector3(Map.LakeRadii.x*2,25,Map.LakeRadii.y*2);box.center=Vector3.down*12.5f;
        var surface=new GameObject("Lake water");surface.layer=4;surface.transform.SetParent(lake.transform,false);
        const int n=512;var vertices=new Vector3[n+1];var colors=new Color[n+1];var triangles=new int[n*3];colors[0]=new Color(1,0,0,1);
        for(int i=0;i<n;i++){float a=i*Mathf.PI*2/n;vertices[i+1]=new Vector3(Mathf.Cos(a)*Map.LakeRadii.x,0,Mathf.Sin(a)*Map.LakeRadii.y);colors[i+1]=new Color(.02f,0,0,1);triangles[i*3]=0;triangles[i*3+1]=(i+1)%n+1;triangles[i*3+2]=i+1;}
        var mesh=new Mesh{name="Exploration lake surface"};mesh.vertices=vertices;mesh.colors=colors;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();surface.AddComponent<MeshFilter>().sharedMesh=mesh;surface.AddComponent<MeshRenderer>().sharedMaterial=palette.lakeWater==null?palette.water:palette.lakeWater;surface.AddComponent<RuntimeMeshOwner>().Bind(mesh);
        var water=lake.AddComponent<WaterVolume>();water.ellipticalBounds=true;water.BuildSurface();
    }
    private void OnDisable()
    {
        foreach(var pair in loaded)if(pair.Value!=null){pair.Value.SetActive(false);Destroy(pair.Value);}loaded.Clear();
        if(lake!=null)Destroy(lake);
        if(distantGround!=null)Destroy(distantGround);
        Shader.SetGlobalTexture("_ExplorationLoadedMask",Texture2D.blackTexture);
        if(loadedMask!=null)Destroy(loadedMask);if(distantMaterial!=null)Destroy(distantMaterial);
        if(weather!=null&&weather.profile==runtimeWeather)weather.profile=originalWeather;
        if(runtimeWeather!=null)Destroy(runtimeWeather);
        if(previousActive!=null)for(int i=0;i<previousActive.Length;i++)if(replacedRoots[i]!=null)replacedRoots[i].SetActive(previousActive[i]);
        Map=null;
    }
}









