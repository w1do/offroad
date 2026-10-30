using UnityEngine;

public sealed class ForestWind : MonoBehaviour
{
    public Vector2 direction = new Vector2(0.85f,0.35f);
    public float strength = 0.8f;
    public Transform[] wetAreas;
    public float wetRadius = 15;
    public float WindTime { get; private set; }
    private void OnEnable(){UpdateShader();}
    private void Update(){WindTime+=Time.deltaTime;UpdateShader();}
    public void UpdateShader()
    {
        Vector2 d=direction.normalized;
        Shader.SetGlobalVector("_ForestWindState",new Vector4(d.x,d.y,strength,WindTime));
        for(int i=0;i<2;i++)
        {
            Vector4 area=new Vector4(100000,0,100000,1);
            if(wetAreas!=null&&i<wetAreas.Length&&wetAreas[i]!=null){Vector3 p=wetAreas[i].position;area=new Vector4(p.x,p.y,p.z,wetRadius);}
            Shader.SetGlobalVector(i==0?"_ForestWetArea0":"_ForestWetArea1",area);
        }
    }
    private void OnDisable(){Shader.SetGlobalVector("_ForestWindState",Vector4.zero);}
}
