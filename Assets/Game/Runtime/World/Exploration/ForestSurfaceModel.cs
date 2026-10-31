using System.Collections.Generic;
using UnityEngine;

// Shared surface description for rendering, authored ruts and contact geometry.
public static class ForestSurfaceModel
{
    public static float Moss(Vector2 p) => Mathf.SmoothStep(0,1,Mathf.InverseLerp(.28f,.7f,Mathf.PerlinNoise((p.x+4200)*.012f,(p.y+1900)*.012f)));
    public static float Bog(Vector2 p) => Mathf.SmoothStep(0,1,Mathf.InverseLerp(.53f,.76f,Mathf.PerlinNoise((p.x+21000)*.0025f,(p.y+12000)*.0025f)));
    public static float HalfWidth(Vector2 p,ExplorationMap.Road road)
    {
        if(road==null)return 2.2f;
        return (road.adventure==3?2.6f:2.15f)+.22f*Mathf.Sin(p.x*.13f+p.y*.11f)+.22f*(Mathf.PerlinNoise(p.x*.08f+100,p.y*.08f+100)-.5f);
    }
    public static float GroundHeight(ExplorationMap map,Vector2 p,List<ExplorationMap.Road> roads)
    {
        float distance=map.RoadDistance(p,roads,out _);
        float micro=(Mathf.PerlinNoise((p.x+400)*.17f,(p.y+900)*.17f)-.5f)*.3f;
        return map.Height(p)+micro*Mathf.SmoothStep(0,1,Mathf.InverseLerp(2,6,distance));
    }
    public static float RoadRelief(ExplorationMap map,Vector2 p,List<ExplorationMap.Road> roads)
    {
        float distance=map.RoadDistance(p,roads,out var road);if(road==null||distance>5.5f)return 0;
        var uv=map.RoadUV(p,road);float along=uv.y*6.5f;
        float endpoint=Mathf.Min(Vector2.Distance(p,road.points[0]),Vector2.Distance(p,road.points[road.points.Length-1]));
        float junction=Mathf.SmoothStep(0,1,Mathf.Clamp01(endpoint/14));
        float rut=Mathf.Exp(-Mathf.Pow((distance-.79f)/.31f,2)*2);
        float depth=road.adventure==3?.085f:road.adventure==1?.065f:.035f;
        depth*=.7f+.3f*Mathf.Sin(along*.12f+road.id);
        float crown=.025f*Mathf.Exp(-distance*distance*3);
        float ditch=-.065f*Mathf.Exp(-Mathf.Pow((distance-HalfWidth(p,road)-.65f)/.45f,2)*2);
        return (crown-rut*depth+ditch)*junction;
    }
    public static Color Weights(ExplorationMap map,Vector2 p,List<ExplorationMap.Road> roads)
    {
        float distance=map.RoadDistance(p,roads,out var road);float width=HalfWidth(p,road);
        float edge=(Mathf.PerlinNoise(p.x*.7f+30,p.y*.7f+70)-.5f)*.4f;
        float coverage=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(width*.55f,width+.8f,distance+edge));
        float grass=(Mathf.PerlinNoise(p.x*.045f+17,p.y*.045f+89));
        return new Color(coverage,Moss(p),Bog(p),grass);
    }
    public sealed class Pool
    {
        public Vector2 centre;
        public float height;
        public Vector2 radii;
        public float RadiusSquared(Vector2 p){var d=p-centre;return d.x*d.x/(radii.x*radii.x)+d.y*d.y/(radii.y*radii.y);}
    }
    public static List<Pool> WetlandPools(ExplorationMap map,List<ExplorationMap.Road> roads,Vector3 origin,float width,float length)
    {
        var pools=new List<Pool>();
        for(int z=Mathf.FloorToInt(origin.z/64);z<=Mathf.FloorToInt((origin.z+length)/64);z++)for(int x=Mathf.FloorToInt(origin.x/64);x<=Mathf.FloorToInt((origin.x+width)/64);x++)
        {
            var p=new Vector2(x*64+32+Mathf.Sin(x*7+z*11)*9,z*64+32+Mathf.Cos(x*13-z*5)*9);
            if(Bog(p)<.9f||map.Height(p)>6||map.LakeRadius(p)<1.3f||map.RoadDistance(p,roads,out _)<12)continue;
            pools.Add(new Pool{centre=p,height=GroundHeight(map,p,roads),radii=new Vector2(3.8f,5.5f)});
        }
        return pools;
    }
    public static float WetlandHeight(float height,Vector2 p,List<Pool> pools)
    {
        foreach(var pool in pools){float r2=pool.RadiusSquared(p);if(r2>=4)continue;height=Mathf.Lerp(height,pool.height,Mathf.SmoothStep(0,1,Mathf.Clamp01(2-Mathf.Sqrt(r2))));height-=.14f*Mathf.Clamp01(1-r2);}
        return height;
    }
}

