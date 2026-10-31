using System.Collections.Generic;
using UnityEngine;

// Per-instance mesh copies only. Native WheelCollider remains the only tyre force solver.
public sealed class TyreWheelShape
{
    sealed class Part
    {
        public MeshFilter filter;
        public Mesh source, mesh;
        public Vector3[] original, vertices, offsets;
        public float[] radial, axial, rubber;
        public int[] flexible;
        public float rimFraction=-1;
        public bool wasActive;
        public Renderer renderer;
    }
    readonly List<Part> parts = new List<Part>();
    readonly WheelCollider wheel;
    readonly Transform visual;
    readonly float radius;
    float compression;
    Vector3 normal;
    Vector3 lastNormal;
    float lastCompression=-1,lastDeflation=-1,lastRim=-1,lastBulge=-1;
    bool drawn;
    public int MeshCount => parts.Count;

    public TyreWheelShape(WheelCollider collider, Transform root, float unloadedRadius)
    {
        wheel=collider;visual=root;radius=unloadedRadius;
        if(root==null)return;
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true)){
            var source=filter.sharedMesh;
            if(source==null||!source.isReadable){Debug.LogWarning("Tyre deformation needs a readable mesh: "+filter.name,filter);continue;}
            float outerRadius=0;
            foreach(var vertex in source.vertices)outerRadius=Mathf.Max(outerRadius,Vector3.ProjectOnPlane(filter.transform.TransformPoint(vertex)-root.position,root.right).magnitude);
            // Entirely internal pieces (rim, hub, bolts) remain rigid and shared.
            if(outerRadius<radius*.8f)continue;
            var copy=Object.Instantiate(source);copy.name=source.name+" (loaded tyre instance)";copy.MarkDynamic();
            var original=source.vertices;var offsets=new Vector3[original.Length];var radial=new float[original.Length];var axial=new float[original.Length];
            var toRoot=root.worldToLocalMatrix*filter.transform.localToWorldMatrix;
            // Wheel hubs use unit scale; retain authored child scales in this cache.
            for(int i=0;i<original.Length;i++){
                offsets[i]=Vector3.Scale(toRoot.MultiplyPoint3x4(original[i]),root.lossyScale);
                axial[i]=offsets[i].x;radial[i]=Mathf.Sqrt(offsets[i].y*offsets[i].y+offsets[i].z*offsets[i].z);
            }
            parts.Add(new Part{filter=filter,source=source,mesh=copy,original=original,vertices=(Vector3[])original.Clone(),offsets=offsets,radial=radial,axial=axial,renderer=filter.GetComponent<Renderer>()});
            var bounds=source.bounds;bounds.Expand(radius*.6f/Mathf.Max(.001f,filter.transform.lossyScale.magnitude));copy.bounds=bounds;
            filter.sharedMesh=copy;
        }
    }
    public void Simulate(VehicleEquipmentProfile config,float deflation,float referenceLoad,float dt)
    {
        bool grounded=wheel.GetGroundHit(out var hit);
        normal=grounded?hit.normal:wheel.transform.up;
        float load=grounded?Mathf.Clamp(hit.force/Mathf.Max(1,referenceLoad),0,1.8f):0;
        float target=Mathf.Min(radius*.2f,radius*Mathf.Lerp(config.roadTyreCompression,config.deflatedTyreCompression,deflation)*load);
        compression=Mathf.Lerp(compression,target,1-Mathf.Exp(-Mathf.Max(0,dt)/Mathf.Max(.01f,config.tyreShapeResponseSeconds)));
        wheel.radius=Mathf.Max(radius*.8f,radius-compression);
    }
    public void Draw(VehicleEquipmentProfile config,float deflation,Vector3 hub,Quaternion rotation)
    {
        if(visual==null)return;
        Vector3 localNormal=Quaternion.Inverse(rotation)*normal;
        bool activationChanged=false;
        foreach(var part in parts){
            bool active=part.filter!=null&&part.filter.gameObject.activeInHierarchy&&(part.renderer==null||part.renderer.enabled);
            if(active!=part.wasActive)activationChanged=true;
            part.wasActive=active;
        }
        if(drawn&&!activationChanged&&config.deflatedSidewallBulge==lastBulge&&Mathf.Abs(compression-lastCompression)<.0004f&&Mathf.Abs(deflation-lastDeflation)<.002f
            &&config.rigidRimRadiusFraction==lastRim&&(localNormal-lastNormal).sqrMagnitude<.00001f)return;
        drawn=true;lastNormal=localNormal;lastCompression=compression;lastDeflation=deflation;lastRim=config.rigidRimRadiusFraction;lastBulge=config.deflatedSidewallBulge;
        foreach(var part in parts){
            if(!part.wasActive)continue;
            // One matrix per mesh, no Transform calls inside the vertex loop.
            var toMesh=part.filter.transform.worldToLocalMatrix*visual.localToWorldMatrix;
            Vector3 inverseScale=new Vector3(1/visual.lossyScale.x,1/visual.lossyScale.y,1/visual.lossyScale.z);
            Vector3 normalInMesh=toMesh.MultiplyVector(Vector3.Scale(localNormal,inverseScale));
            Vector3 axleInMesh=toMesh.MultiplyVector(Vector3.Scale(Vector3.right,inverseScale));
            if(part.rimFraction!=config.rigidRimRadiusFraction){
                part.rimFraction=config.rigidRimRadiusFraction;
                float rim=radius*part.rimFraction,range=Mathf.Max(.001f,radius*.96f-rim);
                part.rubber=new float[part.original.Length];var flexible=new List<int>();
                for(int i=0;i<part.original.Length;i++){
                    float amount=Mathf.Clamp01((part.radial[i]-rim)/range);
                    part.rubber[i]=amount*amount*(3-2*amount);
                    if(amount>0)flexible.Add(i);
                }
                part.flexible=flexible.ToArray();
                System.Array.Copy(part.original,part.vertices,part.original.Length);
            }
            float bulge=config.deflatedSidewallBulge*deflation*Mathf.Clamp01(compression/(radius*.06f));
            float bottomHeight=-(radius-compression),inverseRadius=1/radius;
            for(int n=0;n<part.flexible.Length;n++){
                int i=part.flexible[n];float rubber=part.rubber[i];
                var offset=part.offsets[i];
                float height=offset.x*localNormal.x+offset.y*localNormal.y+offset.z*localNormal.z;
                float bottom=Mathf.Clamp01((-height*inverseRadius-.3f)/.7f);
                float lift=Mathf.Max(0,bottomHeight-height)*rubber;
                float side=part.axial[i]*bulge*bottom*rubber;
                var original=part.original[i];
                part.vertices[i]=new Vector3(original.x+normalInMesh.x*lift+axleInMesh.x*side,
                    original.y+normalInMesh.y*lift+axleInMesh.y*side,
                    original.z+normalInMesh.z*lift+axleInMesh.z*side);
            }
            // Preserve authored split normals/tangents: recalculating normals on
            // these imported meshes was the dominant cost and erased hard seams.
            part.mesh.SetVertices(part.vertices,0,part.vertices.Length,UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds);
        }
    }

    public void Dispose()
    {
        foreach(var part in parts){if(part.filter!=null&&part.filter.sharedMesh==part.mesh)part.filter.sharedMesh=part.source;Object.Destroy(part.mesh);}
        parts.Clear();if(wheel!=null)wheel.radius=radius;
    }
}
