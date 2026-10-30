using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
public static class ValidateContinuousGround
{
 public static string Main(){var root=GameObject.Find("Continuous forest ground");if(root==null)throw new Exception("Ground missing");Physics.SyncTransforms();var colliders=root.GetComponentsInChildren<MeshCollider>();Bounds b=colliders[0].bounds;foreach(var c in colliders){if(!c.enabled||c.sharedMesh!=c.GetComponent<MeshFilter>().sharedMesh)throw new Exception("Visual/collider mismatch");b.Encapsulate(c.bounds);}int tested=0;float maxJump=0;
 for(float z=b.min.z+1;z<b.max.z-1;z+=3.1f)for(float x=b.min.x+1;x<b.max.x-1;x+=3.1f){var p=new Vector3(x,150,z);var hit=Physics.RaycastAll(p,Vector3.down,300,~0,QueryTriggerInteraction.Ignore).Where(h=>h.collider.transform.IsChildOf(root.transform)).OrderBy(h=>h.distance).ToArray();if(hit.Length==0)throw new Exception("Hole at "+p);tested++;}
 foreach(var c in colliders){var m=c.sharedMesh;var v=m.vertices;for(int i=0;i<v.Length;i+=4)maxJump=Mathf.Max(maxJump,Mathf.Abs(v[i].y-v[i+1].y),Mathf.Abs(v[i].y-v[i+2].y));}
 return "PASS "+tested+" terrain raycasts without holes; "+colliders.Length+" chunks use identical visible/physical meshes; max grid edge height change="+maxJump.ToString("F2")+"m. Bounds="+b;
 }
}
