using System.Linq;
using UnityEngine;
public static class SurveyForestRiver
{
    public static string Main()
    {
        var car=Object.FindAnyObjectByType<ForestKamazController>();string s="truck="+car.transform.position+" forward="+car.transform.forward+"\n";
        var forest=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().First(g=>g.name.StartsWith("Forest - imported"));
        var road=forest.GetComponentsInChildren<MeshFilter>().First(f=>f.GetComponent<Renderer>().sharedMaterial.name.Contains("Forest_Dirt_Road_LightSnow"));
        var v=road.sharedMesh.vertices;var t=road.sharedMesh.triangles;
        foreach(float distance in new[]{22f,55f,85f})
        {
            var target=car.transform.position+car.transform.forward*distance;Vector3 best=Vector3.zero;float score=float.MaxValue;
            for(int i=0;i<t.Length;i+=3){var p=road.transform.TransformPoint((v[t[i]]+v[t[i+1]]+v[t[i+2]])/3);float d=(p-target).sqrMagnitude;if(d<score){score=d;best=p;}}
            s+="ahead "+distance+" center="+best+" nearestError="+Mathf.Sqrt(score)+"\n";
        }
        return s;
    }
}
