using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Builds complementary terrain and road meshes: never leaves an invisible terrain sheet under a road.
public static class ExplorationTileBuilder
{
    private sealed class Surface
    {
        public readonly List<Vector3> vertices=new List<Vector3>();
        public readonly List<int> triangles=new List<int>();
        public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {
            int k=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);
            triangles.AddRange(new[]{k,k+2,k+1,k,k+3,k+2});
        }
        public Mesh Build(string name,ExplorationMap map,List<ExplorationMap.Road> roads,Vector3 origin,bool roadUV)
        {
            var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);
            var uv=new Vector2[vertices.Count];for(int i=0;i<uv.Length;i++){var p=new Vector2(vertices[i].x+origin.x,vertices[i].z+origin.z);if(roadUV){map.RoadDistance(p,roads,out var road);uv[i]=map.RoadUV(p,road);}else uv[i]=p*.4f;}mesh.uv=uv;var weights=new Color[vertices.Count];var coords=new Vector2[vertices.Count];for(int i=0;i<vertices.Count;i++){var p=new Vector2(vertices[i].x+origin.x,vertices[i].z+origin.z);weights[i]=ForestSurfaceModel.Weights(map,p,roads);map.RoadDistance(p,roads,out var nearest);if(nearest!=null){var coord=map.RoadUV(p,nearest);coords[i]=new Vector2((coord.x-.5f)*6.5f,coord.y*6.5f);}}mesh.colors=weights;mesh.uv2=coords;mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
        }
    }
    public static GameObject Build(ExplorationWorld world,Vector2Int key)
    {
        var map=world.Map;var palette=world.palette;float ox=-5000+key.x*ExplorationWorld.TileSize,oz=-5000+key.y*ExplorationWorld.TileSize;
        var origin=new Vector3(ox,0,oz);float width=Mathf.Min(ExplorationWorld.TileSize,5000-ox),length=Mathf.Min(ExplorationWorld.TileSize,5000-oz);
        var roads=map.Nearby(new Vector2(ox+width*.5f,oz+length*.5f),100);
        var pools=ForestSurfaceModel.WetlandPools(map,roads,origin,width,length);
        var surfaces=new Surface[7];for(int i=0;i<surfaces.Length;i++)surfaces[i]=new Surface();
        var tile=new GameObject("Exploration tile "+key.x+","+key.y);tile.SetActive(false);tile.transform.SetParent(world.transform,false);tile.transform.position=origin;
        int nx=Mathf.CeilToInt(width/2),nz=Mathf.CeilToInt(length/2);
        for(int z=0;z<nz;z++)for(int x=0;x<nx;x++)
        {
            float x0=ox+x*2,z0=oz+z*2,x1=Mathf.Min(ox+width,x0+2),z1=Mathf.Min(oz+length,z0+2);
            var centre=new Vector2((x0+x1)*.5f,(z0+z1)*.5f);
            int subdivisions=map.RoadDistance(centre,roads,out _) < 7 ? 8 : ForestSurfaceModel.Bog(centre)>.8f&&map.Height(centre)<6&&map.LakeRadius(centre)>1.25f ? 4 : 1;
            float ha=ForestSurfaceModel.GroundHeight(map,new Vector2(x0,z0),roads),hb=ForestSurfaceModel.GroundHeight(map,new Vector2(x1,z0),roads),hc=ForestSurfaceModel.GroundHeight(map,new Vector2(x1,z1),roads),hd=ForestSurfaceModel.GroundHeight(map,new Vector2(x0,z1),roads);
            System.Func<float,float,Vector3> point=(u,v)=>{
                float px=Mathf.Lerp(x0,x1,u),pz=Mathf.Lerp(z0,z1,v);
                float h=u>=v ? ha+(hb-ha)*u+(hc-hb)*v : ha+(hc-hd)*u+(hd-ha)*v;
                h=ForestSurfaceModel.WetlandHeight(h,new Vector2(px,pz),pools);h-=map.PuddleDepth(new Vector2(px,pz),roads);h+=ForestSurfaceModel.RoadRelief(map,new Vector2(px,pz),roads);return new Vector3(px-ox,h,pz-oz);
            };
            for(int j=0;j<subdivisions;j++)for(int i=0;i<subdivisions;i++)
            {
                float u=i/(float)subdivisions,v=j/(float)subdivisions,du=1f/subdivisions;
                var p=new Vector2(Mathf.Lerp(x0,x1,u+du*.5f),Mathf.Lerp(z0,z1,v+du*.5f));
                float distance=map.RoadDistance(p,roads,out var road);int category=distance<ForestSurfaceModel.HalfWidth(p,road)+1 ? road.adventure : map.LakeRadius(p)<1.25f ? 5 : ForestSurfaceModel.Bog(p)>.8f&&map.Height(p)<6 ? 6 : 4;
                surfaces[category].Quad(point(u,v),point(u+du,v),point(u+du,v+du),point(u,v+du));
            }
        }
        for(int i=0;i<surfaces.Length;i++)if(surfaces[i].triangles.Count>0)
        {
            var go=new GameObject(i<4||i==6 ? "Road adventure "+i : i==4?"Forest ground":"Rocky lake shore");go.transform.SetParent(tile.transform,false);
            var mesh=surfaces[i].Build(go.name,map,roads,origin,i<3);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial=i==6?palette.bog:i==3?palette.mud:i<4?palette.road:i==4?palette.forest:palette.shore;
            go.AddComponent<MeshCollider>().sharedMesh=mesh;go.AddComponent<RuntimeMeshOwner>().Bind(mesh);
            var driving=go.AddComponent<ForestDrivingSurface>();driving.cover=i<4||i==6?ForestDrivingSurface.Cover.Mud:ForestDrivingSurface.Cover.Gravel;driving.forwardGrip=i<4?.85f:.65f;driving.lateralGrip=i<4?.8f:.65f;driving.rollingResistance=i<4?.025f:.055f;
            if(i<4||i==6)
            {
                var soil=go.AddComponent<SoftGroundRuts>();soil.profile=palette.roadSoil;soil.persistentId=world.StateKey(key,i);
                var rain=go.AddComponent<RainSoakedRoad>();rain.weather=world.weather;rain.useLocalRain=true;rain.localRain=ExplorationClimate.Sample(new Vector2(ox,oz),i,Time.time).x;rain.initialSaturation=world.RestoreWetness(key,i);
                var tracks=go.AddComponent<TireTrackRenderer>();tracks.resolution=128;tracks.persistentId=soil.persistentId;
                if(i==1)
                {
                    var erosion=go.AddComponent<RainRoadErosion>();var points=new List<Vector3>();foreach(var road in roads)if(road.adventure==i)foreach(var p in road.points)if(p.x>=ox&&p.x<ox+width&&p.y>=oz&&p.y<oz+length)points.Add(new Vector3(p.x,map.Height(p),p.y));erosion.runoffPoints=points.ToArray();
                }
            }
        }
        Plant(world,tile,key,roads,origin,width,length);
        tile.AddComponent<ExplorationUnderstory>().Build(world,key,roads,origin,width,length,pools);
        AddWetlandWater(world,tile,pools,origin,width,length);
        AddPuddles(world,tile,roads,origin,width,length);
        AddBlockages(world,tile,roads,origin,width,length);
        tile.SetActive(true);return tile;
    }
    private static void Plant(ExplorationWorld world,GameObject tile,Vector2Int key,List<ExplorationMap.Road> roads,Vector3 origin,float width,float length)
    {
        var random=new System.Random(unchecked(world.seed+key.x*73856093+key.y*19349663));
        int planted=0;var pools=ForestSurfaceModel.WetlandPools(world.Map,roads,origin,width,length);
        for(int i=0;i<185;i++)
        {
            var p=new Vector2(origin.x+(float)random.NextDouble()*width,origin.z+(float)random.NextDouble()*length);
            if(pools.Exists(pool=>pool.RadiusSquared(p)<1.2f))continue;
            float clearance=world.Map.RoadDistance(p,roads,out var nearestRoad);if(world.Map.LakeRadius(p)<1.06f||clearance<ForestSurfaceModel.HalfWidth(p,nearestRoad)+2.2f)continue;
            var available=clearance<25?System.Array.FindAll(world.palette.trees,t=>t.name.StartsWith("Tree Detailed")):world.palette.trees;var prefab=available[random.Next(available.Length)];var tree=Object.Instantiate(prefab,tile.transform);
            tree.transform.SetPositionAndRotation(new Vector3(p.x,ForestSurfaceModel.WetlandHeight(ForestSurfaceModel.GroundHeight(world.Map,p,roads),p,pools)-.04f,p.y),Quaternion.Euler(0,(float)random.NextDouble()*360,0));tree.transform.localScale=Vector3.one*(.65f+(float)random.NextDouble()*.45f);
            foreach(var renderer in tree.GetComponentsInChildren<Renderer>())renderer.shadowCastingMode=ShadowCastingMode.On;
            tree.AddComponent<ForestTreeWind>().useMaterialSettings=true;
            if(prefab.name.StartsWith("Tree Detailed"))tree.AddComponent<WinchAnchorSurface>();planted++;
        }
        for(int i=0;i<8;i++)
        {
            var p=new Vector2(origin.x+(float)random.NextDouble()*width,origin.z+(float)random.NextDouble()*length);if(world.Map.LakeRadius(p)<1.02f||world.Map.RoadDistance(p,roads,out _)<4.7f)continue;
            var rock=Object.Instantiate(world.palette.rocks[random.Next(world.palette.rocks.Length)],tile.transform);rock.transform.SetPositionAndRotation(new Vector3(p.x,world.Map.Height(p),p.y),Quaternion.Euler(0,(float)random.NextDouble()*360,0));
        }
    }
    private static void AddPuddles(ExplorationWorld world,GameObject tile,List<ExplorationMap.Road> roads,Vector3 origin,float width,float length)
    {
        foreach(var road in roads)if(road.adventure==3)
        {
            var centre=road.points[8];if(centre.x<origin.x||centre.x>=origin.x+width||centre.y<origin.z||centre.y>=origin.z+length)continue;
            var go=new GameObject("Deep mud puddle");go.transform.SetParent(tile.transform);var delta=road.points[9]-road.points[7];go.transform.SetPositionAndRotation(new Vector3(centre.x,world.Map.Height(centre),centre.y),Quaternion.LookRotation(new Vector3(delta.x,0,delta.y)));
            var box=go.AddComponent<BoxCollider>();box.isTrigger=true;box.size=new Vector3(3.2f,.5f,7);box.center=Vector3.down*.25f;
            var surface=new GameObject("Puddle water");surface.layer=4;surface.transform.SetParent(go.transform,false);surface.AddComponent<MeshFilter>().sharedMesh=world.palette.puddleDisk;surface.AddComponent<MeshRenderer>().sharedMaterial=world.palette.water;
            var water=go.AddComponent<WaterVolume>();water.surfaceMesh=surface.GetComponent<MeshFilter>();
            var puddle=go.AddComponent<RainRoadPuddle>();puddle.surface=surface.transform;puddle.basinRadii=new Vector2(1.6f,3.5f);puddle.basinDepth=.1f;
            foreach(var rain in tile.GetComponentsInChildren<RainSoakedRoad>(true))if(rain.name=="Road adventure 3")puddle.road=rain;
        }
    }
    private static void AddBlockages(ExplorationWorld world,GameObject tile,List<ExplorationMap.Road> roads,Vector3 origin,float width,float length)
    {
        foreach(var road in roads)if((unchecked((uint)road.id*2654435761u ^ (uint)world.SessionHazardSeed))%7==0)
        {
            var p=road.points[5];if(p.x<origin.x||p.x>=origin.x+width||p.y<origin.z||p.y>=origin.z+length||p.sqrMagnitude<900)continue;
            var d=(road.points[6]-road.points[4]).normalized;var centre=new Vector3(p.x,world.Map.Height(p),p.y);
            var tree=Object.Instantiate(world.palette.fallenTrees[road.id%world.palette.fallenTrees.Length],tile.transform);tree.name="Pre-existing fallen tree "+road.id;tree.AddComponent<FallenTreeObstacle>().Place(centre,new Vector3(d.y,0,-d.x),13);
        }
    }
    private static void AddWetlandWater(ExplorationWorld world,GameObject tile,List<ForestSurfaceModel.Pool> pools,Vector3 origin,float width,float length)
    {
        foreach(var pool in pools)
        {
            if(pool.centre.x<origin.x||pool.centre.x>=origin.x+width||pool.centre.y<origin.z||pool.centre.y>=origin.z+length)continue;
            var go=new GameObject("Forest bog standing water");go.transform.SetParent(tile.transform);go.transform.position=new Vector3(pool.centre.x,pool.height-.045f,pool.centre.y);
            var box=go.AddComponent<BoxCollider>();box.isTrigger=true;box.size=new Vector3(pool.radii.x*1.64f,.5f,pool.radii.y*1.64f);box.center=Vector3.down*.25f;
            var surface=new GameObject("Bog water surface");surface.layer=4;surface.transform.SetParent(go.transform,false);surface.transform.localScale=new Vector3(pool.radii.x*.82f,1,pool.radii.y*.82f);surface.AddComponent<MeshFilter>().sharedMesh=world.palette.puddleDisk;var renderer=surface.AddComponent<MeshRenderer>();renderer.sharedMaterial=world.palette.water;renderer.shadowCastingMode=ShadowCastingMode.Off;
            var water=go.AddComponent<WaterVolume>();water.surfaceMesh=surface.GetComponent<MeshFilter>();
        }
    }
}







