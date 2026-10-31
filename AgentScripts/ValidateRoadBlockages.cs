using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
public static class ValidateRoadBlockages {
 public static object Main(){
 if(!Application.isPlaying)throw new Exception("Requires Play Mode");var controller=UnityEngine.Object.FindAnyObjectByType<ForestRoadBlockages>();
 var fingerprints=new HashSet<string>();int empty=0,total=0,solidHits=0;int session=controller.SessionSeed;
 try{
 for(int seed=0;seed<100;seed++){
 controller.Generate(seed);var obstacles=controller.GetComponentsInChildren<FallenTreeObstacle>();if(obstacles.Length!=controller.BlockageCount||obstacles.Length>2)throw new Exception("Count mismatch");if(obstacles.Length==0)empty++;
 string expected=string.Join(";",obstacles.Select(o=>o.name+o.transform.position.ToString("F3")));fingerprints.Add(expected);
 foreach(var obstacle in obstacles){
 var colliders=obstacle.GetComponentsInChildren<MeshCollider>().Where(c=>c.enabled).ToArray();if(colliders.Length==0)throw new Exception("No solid trunk");
 var site=controller.sites[int.Parse(obstacle.name.Substring("Fallen tree at site ".Length))];
 // A ray along the road must meet the real trunk in each lane, not just a crown bounding box.
 var forward=site.forward.normalized;var across=Vector3.Cross(Vector3.up,forward);int lanes=0;
 foreach(float lane in new[]{-1.4f,0,1.4f}){bool hit=false;for(float height=.025f;height<=2f;height+=.025f){var ray=new Ray(site.centre+across*lane-forward*5+Vector3.up*height,forward);foreach(var c in colliders)if(c.Raycast(ray,out var h,10))hit=true;}if(hit)lanes++;}
 if(lanes!=3)throw new Exception("Trunk misses lane at seed "+seed+" "+obstacle.name+" lanes="+lanes);
 if(Vector3.Distance(site.centre,controller.vehicle.position)<controller.spawnClearance)throw new Exception("Spawn blocked");solidHits+=lanes;total++;
 }
 controller.Generate(seed);var repeat=controller.GetComponentsInChildren<FallenTreeObstacle>();string signature=string.Join(";",repeat.Select(o=>o.name+o.transform.position.ToString("F3")));if(signature!=expected)throw new Exception("Unstable seed");
 }
 if(fingerprints.Count<10||empty==0||total==0)throw new Exception("Insufficient randomness");
 return new{success=true,seeds=100,distinctLayouts=fingerprints.Count,emptyLayouts=empty,fallenTrees=total,verifiedLaneBlocks=solidHits};
 }finally{controller.Generate(session);}
 }
}



