using System;
using System.Collections.Generic;
using UnityEngine;

// Pure deterministic layout: discovery does not depend on loading order or Unity's global random state.
public sealed class ExplorationMap
{
    public const float HalfSize = 5000;
    public readonly Vector2 LakeCentre = new Vector2(2100, 1700);
    public readonly Vector2 LakeRadii = new Vector2(820, 580);
    public const float LakeLevel = -6;
    public sealed class Road
    {
        public int id, from, to, adventure;
        public Vector2[] points;
    }
    public readonly List<Vector2> Nodes = new List<Vector2>();
    public readonly List<Road> Roads = new List<Road>();
    public readonly int Seed;
    public ExplorationMap(int seed)
    {
        Seed = seed;
        var random = new System.Random(seed);
        int[,] indices = new int[9, 9];
        for (int z = 0; z < 9; z++) for (int x = 0; x < 9; x++)
        {
            var p = new Vector2((x - 4) * 1100, (z - 4) * 1100);
            if (x != 4 || z != 4) p += new Vector2((float)random.NextDouble() * 260 - 130, (float)random.NextDouble() * 260 - 130);
            indices[x, z] = LakeRadius(p) < 1.3f ? -1 : Nodes.Count;
            if (indices[x, z] >= 0) Nodes.Add(p);
        }
        var candidates = new List<Road>();
        for (int z = 0; z < 9; z++) for (int x = 0; x < 9; x++)
        {
            if (x < 8) AddCandidate(indices[x,z], indices[x+1,z], random, candidates);
            if (z < 8) AddCandidate(indices[x,z], indices[x,z+1], random, candidates);
        }
        // Random spanning tree first, then loops. All retained nodes stay reachable.
        for (int i = candidates.Count - 1; i > 0; i--) { int j = random.Next(i+1); var r=candidates[i]; candidates[i]=candidates[j]; candidates[j]=r; }
        var parent = new int[Nodes.Count]; for (int i=0;i<parent.Length;i++) parent[i]=i;
        Func<int,int> find = null; find = n => parent[n] == n ? n : parent[n] = find(parent[n]);
        foreach (var road in candidates)
        {
            int a=find(road.from), b=find(road.to);
            if (a != b || random.NextDouble() < .32) { parent[a]=b; road.id=Roads.Count; Roads.Add(road); }
        }
        int entrance=Nodes.FindIndex(p=>p.sqrMagnitude<.01f), entranceRoad=0;
        foreach(var road in Roads)if(road.from==entrance||road.to==entrance)road.adventure=(entranceRoad++)%4;
        for(int branch=0;branch<4;branch++)
        {
            float angle=branch*Mathf.PI*.5f+.3f;
            var radial=new Vector2(Mathf.Cos(angle)*LakeRadii.x,Mathf.Sin(angle)*LakeRadii.y);
            var shore=LakeCentre+radial*1.07f;var approach=LakeCentre+radial*1.55f;
            int nearest=0;for(int i=1;i<Nodes.Count;i++)if((Nodes[i]-approach).sqrMagnitude<(Nodes[nearest]-approach).sqrMagnitude)nearest=i;
            int endpoint=Nodes.Count;Nodes.Add(shore);
            var road=new Road{id=Roads.Count,from=nearest,to=endpoint,adventure=branch,points=new Vector2[17]};
            for(int i=0;i<17;i++)road.points[i]=i<=8?Vector2.Lerp(Nodes[nearest],approach,i/8f):Vector2.Lerp(approach,shore,(i-8)/8f);
            Roads.Add(road);
        }
    }
    private void AddCandidate(int a, int b, System.Random random, List<Road> candidates)
    {
        if (a < 0 || b < 0) return;
        var road = new Road { from=a, to=b, adventure=random.Next(4), points=new Vector2[17] };
        Vector2 direction=(Nodes[b]-Nodes[a]).normalized, side=new Vector2(-direction.y,direction.x);
        float bend=(float)random.NextDouble()*140-70;
        for(int i=0;i<road.points.Length;i++)
        {
            float t=i/(float)(road.points.Length-1);
            road.points[i]=Vector2.Lerp(Nodes[a],Nodes[b],t)+side*Mathf.Sin(t*Mathf.PI)*bend;
            if(LakeRadius(road.points[i])<1.2f)return;
        }
        candidates.Add(road);
    }
    public float LakeRadius(Vector2 p) { var d=p-LakeCentre; return new Vector2(d.x/LakeRadii.x,d.y/LakeRadii.y).magnitude; }
    public float Height(Vector2 p)
    {
        float h=(Mathf.PerlinNoise((p.x+13000)*.0007f,(p.y+17000)*.0007f)-Mathf.PerlinNoise(13000*.0007f,17000*.0007f))*38;
        h+=(Mathf.PerlinNoise((p.x+23000)*.002f,(p.y+11000)*.002f)-Mathf.PerlinNoise(23000*.002f,11000*.002f))*9;
        h*=Mathf.SmoothStep(0,1,p.magnitude/160);
        float r=LakeRadius(p);
        float basin=LakeLevel+Mathf.Clamp((r-1)*45,-18,22);
        return Mathf.Lerp(basin,h,Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.12f,1.65f,r)));
    }
    public Vector2 RoadUV(Vector2 p,Road road)
    {
        float best=float.MaxValue,along=0,cumulative=0,signed=0;
        for(int i=1;i<road.points.Length;i++)
        {
            var a=road.points[i-1];var b=road.points[i];float distance=SegmentDistance(p,a,b,out var closest);var direction=(b-a).normalized;
            if(distance<best){best=distance;along=cumulative+Vector2.Distance(a,closest);signed=Vector2.Dot(p-closest,new Vector2(direction.y,-direction.x));}
            cumulative+=Vector2.Distance(a,b);
        }
        return new Vector2(.5f+signed/6.5f,along/6.5f);
    }
    public static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b, out Vector2 closest)
    {
        var d=b-a; closest=a+d*Mathf.Clamp01(Vector2.Dot(p-a,d)/Mathf.Max(.001f,d.sqrMagnitude));return Vector2.Distance(p,closest);
    }
    public List<Road> Nearby(Vector2 centre, float radius)
    {
        var result=new List<Road>();
        foreach(var road in Roads)for(int i=1;i<road.points.Length;i++)if(SegmentDistance(centre,road.points[i-1],road.points[i],out _)<=radius){result.Add(road);break;}
        return result;
    }
    public float RoadDistance(Vector2 p, List<Road> candidates, out Road nearest)
    {
        float best=float.MaxValue; nearest=null;
        foreach(var road in candidates)for(int i=1;i<road.points.Length;i++)
        {
            float d=SegmentDistance(p,road.points[i-1],road.points[i],out _);
            if(d<best){best=d;nearest=road;}
        }
        return best;
    }
    public float PuddleDepth(Vector2 p, List<Road> roads)
    {
        float depth=0;
        foreach(var road in roads)if(road.adventure==3)
        {
            var centre=road.points[8];var forward=(road.points[9]-road.points[7]).normalized;var side=new Vector2(forward.y,-forward.x);var delta=p-centre;
            float r2=Mathf.Pow(Vector2.Dot(delta,side)/1.6f,2)+Mathf.Pow(Vector2.Dot(delta,forward)/3.5f,2);
            if(r2<4) {
                float blend=Mathf.SmoothStep(0,1,Mathf.Clamp01(2-Mathf.Sqrt(r2)));
                depth=Height(p)-Mathf.Lerp(Height(p),Height(centre),blend)+.1f*Mathf.Clamp01(1-r2);return depth;
            }
        }
        return depth;
    }
}





