using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

// Visual tree instances beyond the physical streaming ring. No far-away colliders or extra ground support.
public sealed class ExplorationDistantForest : MonoBehaviour
{
    private ExplorationWorld world;
    private Material material;
    private Mesh[] meshes;
    private readonly Dictionary<Vector2Int,Matrix4x4[]> batches=new Dictionary<Vector2Int,Matrix4x4[]>();
    private Vector2Int previous=new Vector2Int(int.MinValue,int.MinValue);
    public void Bind(ExplorationWorld owner)
    {
        world=owner;
        var prefabs=world.palette.trees.Where(p=>p.name.StartsWith("Tree Background")).ToArray();
        meshes=prefabs.Select(p=>p.GetComponentInChildren<MeshFilter>().sharedMesh).ToArray();
        material=new Material(Shader.Find("Game/Exploration Distant Trees")){name="Instanced distant forest",enableInstancing=true};
        material.SetTexture("_BaseMap",prefabs[0].GetComponentInChildren<Renderer>().sharedMaterial.GetTexture("_BaseMap"));
    }
    private void Update()
    {
        if(world==null||world.Map==null||material==null)return;
        var position=world.vehicle.position;var centre=new Vector2Int(Mathf.FloorToInt(position.x/512),Mathf.FloorToInt(position.z/512));
        if(centre!=previous)
        {
            previous=centre;var wanted=new HashSet<Vector2Int>();
            for(int z=-3;z<=3;z++)for(int x=-3;x<=3;x++)
            {
                var key=centre+new Vector2Int(x,z);wanted.Add(key);if(batches.ContainsKey(key))continue;
                var instances=new List<Matrix4x4>();var random=new System.Random(unchecked(world.seed+key.x*19349663+key.y*73856093));var nearby=world.Map.Nearby(new Vector2(key.x*512+256,key.y*512+256),400);
                for(int iz=0;iz<22;iz++)for(int ix=0;ix<22;ix++)
                {
                    var p=new Vector2(key.x*512+(ix+(float)random.NextDouble())*512/22,key.y*512+(iz+(float)random.NextDouble())*512/22);
                    if(Mathf.Abs(p.x)>5000||Mathf.Abs(p.y)>5000||world.Map.LakeRadius(p)<1.06f||world.Map.RoadDistance(p,nearby,out _)<6)continue;
                    instances.Add(Matrix4x4.TRS(new Vector3(p.x,world.Map.Height(p)-.05f,p.y),Quaternion.Euler(0,(float)random.NextDouble()*360,0),Vector3.one*(.7f+(float)random.NextDouble()*.4f)));
                }
                batches[key]=instances.ToArray();
            }
            var remove=new List<Vector2Int>();foreach(var key in batches.Keys)if(!wanted.Contains(key))remove.Add(key);foreach(var key in remove)batches.Remove(key);
        }
        foreach(var pair in batches)if(pair.Value.Length>0)
        {
            int variant=(int)((uint)(pair.Key.x*17+pair.Key.y*31)%meshes.Length);
            Graphics.DrawMeshInstanced(meshes[variant],0,material,pair.Value,pair.Value.Length,null,ShadowCastingMode.Off,true,gameObject.layer);
        }
    }
    private void OnDestroy(){if(material!=null)Destroy(material);batches.Clear();}
}
