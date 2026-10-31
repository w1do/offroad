using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Shared asset meshes + GPU instances: understory has no second collider or invented ground support.
public sealed class ExplorationUnderstory : MonoBehaviour
{
    private ExplorationWorld world;
    private Vector3 centre;
    private readonly List<Matrix4x4[]> grass=new List<Matrix4x4[]>(),shrubs=new List<Matrix4x4[]>();
    public int GrassCount {get;private set;}
    public int ShrubCount {get;private set;}
    public void Build(ExplorationWorld owner,Vector2Int key,List<ExplorationMap.Road> roads,Vector3 origin,float width,float length,List<ForestSurfaceModel.Pool> pools)
    {
        world=owner;centre=origin+new Vector3(width*.5f,0,length*.5f);
        if(world.palette.grassClumps==null||world.palette.shrubClumps==null)return;
        var random=new System.Random(unchecked(world.seed+key.x*3541+key.y*9323));var g=new List<Matrix4x4>();var bushes=new List<Matrix4x4>();
        for(int i=0;i<9000;i++)
        {
            var p=new Vector2(origin.x+(float)random.NextDouble()*width,origin.z+(float)random.NextDouble()*length);
            float distance=world.Map.RoadDistance(p,roads,out var road);float half=ForestSurfaceModel.HalfWidth(p,road);
            if(world.Map.LakeRadius(p)<1.07f||pools.Exists(pool=>pool.RadiusSquared(p)<1.2f))continue;
            bool roadCentre=distance<.43f&&(road==null||road.adventure!=3);bool verge=distance>1.35f&&distance<half+4;
            if(!roadCentre&&!verge&&random.NextDouble()>.24)continue;
            if(distance>.44f&&distance<1.35f)continue;
            float height=ForestSurfaceModel.WetlandHeight(ForestSurfaceModel.GroundHeight(world.Map,p,roads),p,pools)+ForestSurfaceModel.RoadRelief(world.Map,p,roads)-world.Map.PuddleDepth(p,roads);
            if(distance<half&&road!=null&&road.adventure==3)continue;
            float scale=roadCentre?.15f+(float)random.NextDouble()*.2f:.35f+(float)random.NextDouble()*.5f;
            g.Add(Matrix4x4.TRS(new Vector3(p.x,height-.03f,p.y),Quaternion.Euler(0,(float)random.NextDouble()*360,0),Vector3.one*scale));
        }
        for(int i=0;i<240;i++)
        {
            var p=new Vector2(origin.x+(float)random.NextDouble()*width,origin.z+(float)random.NextDouble()*length);float distance=world.Map.RoadDistance(p,roads,out var road);
            if(pools.Exists(pool=>pool.RadiusSquared(p)<1.3f)||world.Map.LakeRadius(p)<1.09f||distance<ForestSurfaceModel.HalfWidth(p,road)+.7f)continue;
            float scale=.5f+(float)random.NextDouble()*.65f;
            bushes.Add(Matrix4x4.TRS(new Vector3(p.x,ForestSurfaceModel.WetlandHeight(ForestSurfaceModel.GroundHeight(world.Map,p,roads),p,pools)-.06f,p.y),Quaternion.Euler(0,(float)random.NextDouble()*360,0),Vector3.one*scale));
        }
        GrassCount=g.Count;ShrubCount=bushes.Count;Pack(g,grass);Pack(bushes,shrubs);
    }
    private static void Pack(List<Matrix4x4> source,List<Matrix4x4[]> batches){for(int i=0;i<source.Count;i+=900)batches.Add(source.GetRange(i,Mathf.Min(900,source.Count-i)).ToArray());}
    private void Update()
    {
        if(world==null||world.vehicle==null)return;
        float distance=Vector2.Distance(new Vector2(centre.x,centre.z),new Vector2(world.vehicle.position.x,world.vehicle.position.z));
        if(distance>230)return;
        for(int i=0;i<grass.Count;i++)if(world.palette.grassClumps.Length>0)Graphics.DrawMeshInstanced(world.palette.grassClumps[i%world.palette.grassClumps.Length],0,world.palette.grassDetail,grass[i],grass[i].Length,null,ShadowCastingMode.Off,true,gameObject.layer);
        if(distance>190)return;
        for(int i=0;i<shrubs.Count;i++)if(world.palette.shrubClumps.Length>0)Graphics.DrawMeshInstanced(world.palette.shrubClumps[i%world.palette.shrubClumps.Length],0,world.palette.shrubDetail,shrubs[i],shrubs[i].Length,null,ShadowCastingMode.On,true,gameObject.layer);
    }
}


