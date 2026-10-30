using UnityEngine;

// Contact classification baked together with the collision surface, independently of decorative layers.
public sealed class ForestGroundPatch : MonoBehaviour
{
    public float step = .75f;
    public int columns, rows;
    public int[] cover;
    public int fallbackCover;
    public int CoverAt(Vector3 world)
    {
        if(cover==null||columns<=0||rows<=0||cover.Length!=columns*rows)return fallbackCover;
        Vector3 p=transform.InverseTransformPoint(world);
        int x=Mathf.Clamp(Mathf.FloorToInt(p.x/step),0,columns-1);
        int z=Mathf.Clamp(Mathf.FloorToInt(p.z/step),0,rows-1);
        return cover!=null&&cover.Length==columns*rows ? cover[z*columns+x] : 0;
    }
}
