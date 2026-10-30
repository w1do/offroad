using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class AddRoadDamage
{
    public static string Main()
    {
        var route=Object.FindAnyObjectByType<MountainRoute>();
        if(GameObject.Find("Potholes and asphalt repairs")!=null)throw new System.Exception("Damage already exists.");
        var root=new GameObject("Potholes and asphalt repairs");
        var centers=new List<Vector3>();var radii=new List<float>();
        for(int i=670;i<1670;i+=9)
        {
            if(i>870&&i<1490)continue;
            Vector3 right=Vector3.Cross(Vector3.up,route.points[i+1]-route.points[i]).normalized;
            centers.Add(route.points[i]+right*Mathf.Sin(i*7.1f)*2.4f);radii.Add(0.72f+Mathf.Abs(Mathf.Sin(i*3.71f))*.85f);
        }
        foreach(var surface in Object.FindObjectsByType<RoadSurface>())
        {
            if(surface.kind!=DriveSurface.Broken)continue;
            var filter=surface.GetComponent<MeshFilter>();var mesh=filter.sharedMesh;var vertices=mesh.vertices;
            for(int j=0;j<vertices.Length;j++)for(int k=0;k<centers.Count;k++)
            {
                float d=Vector2.Distance(new Vector2(vertices[j].x,vertices[j].z),new Vector2(centers[k].x,centers[k].z));
                float depression=Mathf.Clamp01(1-d/(radii[k]+.55f));vertices[j].y-=depression*depression*.20f;
            }
            mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();
            var collider=surface.GetComponent<MeshCollider>();collider.sharedMesh=null;collider.sharedMesh=mesh;EditorUtility.SetDirty(mesh);
        }
        Physics.SyncTransforms();
        var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/MountainDrive/Generated/Gravel shoulder.mat");
        for(int k=0;k<centers.Count;k++)
        {
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            for(int j=0;j<11;j++)
            {
                float angle=j==0?0:(j-1)*Mathf.PI*2/9;
                float radius=j==0?0:radii[k]*(.75f+.22f*Mathf.Sin(j*7+k));
                Vector3 p=centers[k]+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
                if(Physics.Raycast(p+Vector3.up*3,Vector3.down,out RaycastHit hit,6))p.y=hit.point.y+.018f;
                vertices.Add(p);uv.Add(new Vector2(p.x*.7f,p.z*.7f));
                if(j>1){triangles.Add(0);triangles.Add(j);triangles.Add(j-1);}
            }
            var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,uv);mesh.RecalculateNormals();
            AssetDatabase.CreateAsset(mesh,"Assets/MountainDrive/Generated/Pothole"+k+".asset");
            var patch=new GameObject("Exposed aggregate pothole");patch.transform.SetParent(root.transform);
            patch.AddComponent<MeshFilter>().sharedMesh=mesh;patch.AddComponent<MeshRenderer>().sharedMaterial=mat;
        }
        var ice=AssetDatabase.LoadAssetAtPath<Material>("Assets/MountainDrive/Generated/Black ice.mat");ice.SetFloat("_BumpScale",.23f);ice.SetColor("_BaseColor",new Color(.35f,.43f,.48f));EditorUtility.SetDirty(ice);
        AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(root.scene);
        return "Added "+centers.Count+" physical potholes with exposed aggregate; ice glaze refined; scene saved.";
    }
}
