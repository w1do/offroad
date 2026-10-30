using UnityEngine;

public sealed class LightSnowfall : MonoBehaviour
{
    public Transform target;
    public float height = 13f;
    private void LateUpdate()
    { if(target!=null) transform.position=target.position+Vector3.up*height; }
}
