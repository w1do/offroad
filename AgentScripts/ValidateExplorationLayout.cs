using System;using System.Linq;using System.Collections.Generic;using UnityEngine;
public static class ValidateExplorationLayout {
 public static object Main(){var map=new ExplorationMap(311026);var visited=new HashSet<int>{0};bool changed=true;while(changed){changed=false;foreach(var road in map.Roads)if(visited.Contains(road.from)||visited.Contains(road.to)){changed|=visited.Add(road.from);changed|=visited.Add(road.to);}}
 int junctions=Enumerable.Range(0,map.Nodes.Count).Count(n=>map.Roads.Count(r=>r.from==n||r.to==n)>=3);float length=0;foreach(var road in map.Roads)for(int i=1;i<road.points.Length;i++){length+=Vector2.Distance(road.points[i-1],road.points[i]);if(map.LakeRadius(road.points[i])<1)throw new Exception("Road submerged");}
 var repeated=new ExplorationMap(311026);if(visited.Count!=map.Nodes.Count||junctions<20||map.Roads.Count<60||map.Roads.Count!=repeated.Roads.Count)throw new Exception("Invalid graph");
 var variants=map.Roads.GroupBy(r=>r.adventure).Select(g=>new{adventure=g.Key,roads=g.Count()}).ToArray();return new{success=true,mapSize=10000,nodes=map.Nodes.Count,roads=map.Roads.Count,junctions,connectedNodes=visited.Count,totalRoadKm=length/1000,lakeWidth=1640,lakeLength=1160,variants};}
}
