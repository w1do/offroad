using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
public static class AuditVisibleTerrainCollision
{
 public static string Main(){if(Application.isPlaying)throw new Exception("Stop Play for independent mesh audit");var proxy=new GameObject("TEMP visible-surface audit");var expected=new HashSet<Collider>();var skins=UnityEngine.Object.FindObjectsByType<ForestGroundPatch>().Where(p=>p.GetComponent<MeshRenderer>()!=null&&p.GetComponent<MeshRenderer>().enabled&&p.GetComponent<MeshFilter>()!=null).ToArray();Bounds bounds=new Bounds();bool first=true,back=Physics.queriesHitBackfaces;int tested=0,missing=0,bad=0;float max=0;Vector3 worst=Vector3.zero;
 try{foreach(var skin in skins){var f=skin.GetComponent<MeshFilter>();var go=new GameObject("Visible reference");go.transform.SetParent(proxy.transform,false);go.transform.SetPositionAndRotation(f.transform.position,f.transform.rotation);go.transform.localScale=f.transform.lossyScale;var c=go.AddComponent<MeshCollider>();c.sharedMesh=f.sharedMesh;expected.Add(c);var b=f.GetComponent<Renderer>().bounds;if(first){bounds=b;first=false;}else bounds.Encapsulate(b);}Physics.queriesHitBackfaces=true;Physics.SyncTransforms();for(float z=bounds.min.z+.43f;z<bounds.max.z-.43f;z+=4.1f)for(float x=bounds.min.x+.43f;x<bounds.max.x-.43f;x+=4.1f){var hits=Physics.RaycastAll(new Vector3(x,200,z),Vector3.down,500,~0,QueryTriggerInteraction.Ignore);var visible=hits.Where(h=>expected.Contains(h.collider)).OrderBy(h=>h.distance).ToArray();if(visible.Length==0)continue;var physical=hits.Where(h=>h.collider.GetComponent<ForestGroundPatch>()!=null).OrderBy(h=>h.distance).ToArray();if(physical.Length==0){missing++;continue;}float error=Mathf.Abs(visible[0].point.y-physical[0].point.y);tested++;if(error>.08f)bad++;if(error>max){max=error;worst=visible[0].point;}}
 if(missing>0||bad>0)throw new Exception("Ground mismatch: samples="+tested+" missing="+missing+" >8cm="+bad+" max="+max+" at "+worst);return "PASS independent visible-render-mesh vs physical-skin audit: "+skins.Length+" ground pieces, "+tested+" spatial samples, missing="+missing+", maximum error="+max.ToString("F5")+"m.";
 }finally{Physics.queriesHitBackfaces=back;UnityEngine.Object.DestroyImmediate(proxy);Physics.SyncTransforms();}
 }
}
