using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
public static class ValidateMatchedGround
{
 public static string Main(){if(Application.isPlaying)throw new Exception("Stop Play for collision audit");var forest=GameObject.Find("Forest - imported dirt road environment");var root=GameObject.Find("Forest solid collision - visible surface");if(root==null)throw new Exception("Missing matched ground");var source=forest.GetComponentsInChildren<MeshCollider>().Where(c=>{string n=c.GetComponent<Renderer>().sharedMaterial.name;return n.Contains("Terrain_Far")||n.Contains("Aerial_Grass")||n.Contains("Ground_Dirt")||n.Contains("Dirt_Road")||n.Contains("Road_Edge")||n.Contains("Grass_Close")||n.Contains("Sloped_Rock")||n.Contains("Tall_Cliff")||n.Contains("Cobblestone")||n.Contains("Mud_Pile");}).ToArray();var flags=source.Select(c=>c.enabled).ToArray();var set=new HashSet<Collider>(source);bool back=Physics.queriesHitBackfaces;var colliders=root.GetComponentsInChildren<MeshCollider>();var bounds=colliders[0].bounds;foreach(var c in colliders)bounds.Encapsulate(c.bounds);int tested=0,missing=0,large=0;float max=0;var errors=new List<float>();Vector3 worst=Vector3.zero;
 try{foreach(var c in source)c.enabled=true;Physics.queriesHitBackfaces=true;Physics.SyncTransforms();for(float z=bounds.min.z+1;z<bounds.max.z-1;z+=3.7f)for(float x=bounds.min.x+1;x<bounds.max.x-1;x+=3.7f){var hits=Physics.RaycastAll(new Vector3(x,200,z),Vector3.down,400,~0,QueryTriggerInteraction.Ignore);var visible=hits.Where(h=>set.Contains(h.collider)).OrderBy(h=>h.distance).ToArray();if(visible.Length==0)continue;var physical=hits.Where(h=>h.collider.GetComponent<ForestGroundPatch>()!=null).OrderBy(h=>h.distance).ToArray();if(physical.Length==0){missing++;continue;}float error=Mathf.Abs(physical[0].point.y-visible[0].point.y);errors.Add(error);tested++;if(error>.25f)large++;if(error>max){max=error;worst=visible[0].point;}}
 errors.Sort();return "AUDIT "+tested+" visible-ground samples; missing="+missing+"; p99 error="+errors[(int)(errors.Count*.99f)].ToString("F3")+"m; >0.25m="+large+"; maximum="+max.ToString("F3")+"m at "+worst;
 }finally{for(int i=0;i<source.Length;i++)source[i].enabled=flags[i];Physics.queriesHitBackfaces=back;Physics.SyncTransforms();}
 }
}

