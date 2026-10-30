using UnityEngine;

public struct SoftGroundSurfaceSample
{
    public SoftGroundRuts ground;
    public Vector3 point, normal;
    public float colliderHeightMetres;
    public float ColliderHeightErrorMetres => point.y - colliderHeightMetres;
}
