using UnityEngine;

// Attached only to generated meshes, never to imported/shared asset meshes.
public sealed class RuntimeMeshOwner : MonoBehaviour
{
    [SerializeField, HideInInspector] Mesh ownedMesh;
    public void Bind(Mesh mesh) { ownedMesh=mesh; }
    void OnDestroy(){if(ownedMesh!=null)Destroy(ownedMesh);}
}
