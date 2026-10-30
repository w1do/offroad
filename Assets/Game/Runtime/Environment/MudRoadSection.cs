using UnityEngine;

[RequireComponent(typeof(SoftGroundRuts))]
public sealed class MudRoadSection : MonoBehaviour
{
    public MudRoadProfile profile;
    public string sourceArchive;
    public Vector2 halfSize;
    public float laneCentreMetres;
    public float mudHalfWidthMetres=2;
    public static float EdgeWeight(Vector2 p,Vector2 halfSize,float blend)
    {
        float edge=Mathf.Min(halfSize.x-Mathf.Abs(p.x),halfSize.y-Mathf.Abs(p.y));
        float width=blend*(1+.18f*Mathf.Sin(p.y*2.6f+p.x*3.1f)+.12f*Mathf.Sin(p.y*6.7f));
        return Mathf.SmoothStep(0,1,Mathf.Clamp01(edge/Mathf.Max(.1f,width)));
    }
    public float WeightAt(Vector3 point){var p=transform.InverseTransformPoint(point);return EdgeWeight(new Vector2(p.x-laneCentreMetres,p.z),new Vector2(mudHalfWidthMetres,halfSize.y),profile.edgeBlendMetres);}
    public float RutAt(Vector3 local)
    {
        float x=local.x-laneCentreMetres-profile.laneWanderMetres*Mathf.Sin(local.z*.65f);
        float left=Mathf.Exp(-Mathf.Pow((x+profile.trackHalfSpacingMetres)/profile.trackHalfWidthMetres,2));
        float right=Mathf.Exp(-Mathf.Pow((x-profile.trackHalfSpacingMetres)/profile.trackHalfWidthMetres,2));
        return (left*profile.leftRutDepthMetres+right*profile.rightRutDepthMetres)*EdgeWeight(new Vector2(local.x-laneCentreMetres,local.z),new Vector2(mudHalfWidthMetres,halfSize.y),profile.edgeBlendMetres);
    }
    public float SoftnessAt(Vector3 point)
    {
        if(profile.fullWidthFurrows)return WeightAt(point);
        var p=transform.InverseTransformPoint(point);float rut=RutAt(p)/Mathf.Max(.01f,profile.rightRutDepthMetres);
        return Mathf.Clamp01(WeightAt(point)*.4f+rut*.65f);
    }
    public float GripAt(Vector3 point)=>Mathf.Lerp(profile.firmGripMultiplier,profile.softGripMultiplier,SoftnessAt(point));
    public float DigAt(Vector3 point)=>Mathf.Lerp(profile.firmDigMultiplier,profile.softDigMultiplier,SoftnessAt(point));
    public float ResistanceAt(Vector3 point,float depth)
    {
        float initialBurial=profile.fullWidthFurrows?profile.fullWidthBedDepthMetres:RutAt(transform.InverseTransformPoint(point));
        return profile.buriedRollingResistance*Mathf.Clamp01((depth+initialBurial)/.3f)*SoftnessAt(point);
    }
}
