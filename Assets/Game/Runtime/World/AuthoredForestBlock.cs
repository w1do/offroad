using UnityEngine;

public sealed class AuthoredForestBlock : MonoBehaviour
{
    public Vector3[] road;
    public float[] distance;
    public float length;
    public Vector3 exit;
    public float exitYaw;
    public Vector3 Point(float metres)
    {
        if(road==null||road.Length<2)return Vector3.zero;
        int low=0,high=distance.Length-1;while(high-low>1){int m=(low+high)/2;if(distance[m]<metres)low=m;else high=m;}
        return Vector3.Lerp(road[low],road[high],Mathf.InverseLerp(distance[low],distance[high],metres));
    }
}
