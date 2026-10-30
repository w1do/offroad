using UnityEngine;

public sealed class MountainClimbStage : MonoBehaviour
{
    public Vector3[] centerline;
    public ForestKamazController truck;
    private void OnGUI()
    {
        if(truck==null || centerline==null || centerline.Length<2)return;
        int nearest=0;float best=float.MaxValue;
        for(int i=0;i<centerline.Length;i++){float d=(truck.transform.position-centerline[i]).sqrMagnitude;if(d<best){best=d;nearest=i;}}
        GUI.Box(new Rect(Screen.width-300,18,280,80),"MOUNTAIN TEST ROUTE");
        GUI.Label(new Rect(Screen.width-285,45,260,45),$"Section {nearest*2} / 740 m\nIce climb: stop at 240 m, fit chains + LOW");
    }
}
