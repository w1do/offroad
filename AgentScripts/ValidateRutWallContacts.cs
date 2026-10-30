using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ValidateRutWallContacts
{
    static readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
    static T Own<T>(T value) where T:UnityEngine.Object {owned.Add(value);return value;}
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
    static SoftGroundRuts Ground(Scene scene,Vector3 origin,bool wall)
    {
        var go=Own(new GameObject("Measured fresh rut wall"));go.SetActive(false);SceneManager.MoveGameObjectToScene(go,scene);go.transform.position=origin;
        const int nx=40,nz=40;var vertices=new Vector3[(nx+1)*(nz+1)];var triangles=new int[nx*nz*6];
        for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++)
        {float px=-1+x*.05f;vertices[z*(nx+1)+x]=new Vector3(px,wall?Mathf.Clamp01((px-.48f)/.27f)*.35f:0,-1+z*.05f);}
        int t=0;for(int z=0;z<nz;z++)for(int x=0;x<nx;x++){int a=z*(nx+1)+x;triangles[t++]=a;triangles[t++]=a+nx+1;triangles[t++]=a+1;triangles[t++]=a+1;triangles[t++]=a+nx+1;triangles[t++]=a+nx+2;}
        var mesh=Own(new Mesh{vertices=vertices,triangles=triangles});mesh.RecalculateNormals();mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>();go.AddComponent<MeshCollider>();
        var profile=Own(ScriptableObject.CreateInstance<SoftGroundProfile>());var ground=go.AddComponent<SoftGroundRuts>();ground.profile=profile;go.SetActive(true);return ground;
    }
    public static string Main()
    {
        Require(Application.isPlaying,"Requires Play mode");
        var scene=SceneManager.CreateScene("Rut contact validation "+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        var physics=scene.GetPhysicsScene();
        try
        {
            var flat=Ground(scene,new Vector3(14000,0,14000),false);var bank=Ground(scene,new Vector3(14004,0,14000),true);
            var go=Own(new GameObject("Shoulder force fixture"));SceneManager.MoveGameObjectToScene(go,scene);
            var body=go.AddComponent<Rigidbody>();body.mass=350;body.useGravity=false;body.constraints=RigidbodyConstraints.FreezeRotation;
            var settings=new WheelRutContactSettings();
            body.position=flat.transform.position+new Vector3(.45f,.4f,0);Physics.SyncTransforms();
            var noWall=WheelRutContact.Evaluate(body.position,Vector3.right,Vector3.forward,Vector3.up,.4f,.285f,1,scene,body,settings,1);
            Require(!noWall.Active,"Flat ground must not add shoulder force");
            body.position=bank.transform.position+new Vector3(.45f,.4f,0);Physics.SyncTransforms();
            var contact=WheelRutContact.Evaluate(body.position,Vector3.right,Vector3.forward,Vector3.up,.4f,.285f,1,scene,body,settings,1);
            var narrow=WheelRutContact.Evaluate(body.position,Vector3.right,Vector3.forward,Vector3.up,.4f,.10f,1,scene,body,settings,1);
            var soft=WheelRutContact.Evaluate(body.position,Vector3.right,Vector3.forward,Vector3.up,.4f,.285f,1,scene,body,settings,.7f);
            Require(contact.Active&&contact.force.x<0&&Mathf.Abs(contact.force.y)<.001f,"Shoulder must oppose the real wall without vertical support");
            Require(contact.force.magnitude<=settings.maximumForcePerWheelNewtons+.01f,"Bounded shoulder reaction");
            Require(narrow.force.magnitude<contact.force.magnitude&&soft.force.magnitude<contact.force.magnitude,"Width and tyre compliance must matter");

            Vector3 dig=flat.transform.position;flat.Dig(dig,2,1);
            Require(flat.SampleSurface(dig,out var before)&&before.ColliderHeightErrorMetres<-.01f,"Surface query must see uncooked deformation");
            int cooks=flat.CommitCount;flat.RequestWheelContactUpdate();SoftGroundRuts.AdvanceAll(.001f);
            bool sampledAfter=flat.SampleSurface(dig,out var after);Require(flat.CommitCount==cooks+1&&sampledAfter&&Mathf.Abs(after.ColliderHeightErrorMetres)<.0001f,"Wheel contact must be cooked ahead of normal interval");
            Require(bank.SampleSurface(bank.transform.position+new Vector3(.60f,1,0),out var wall),"Triangle sample");
            float analytic=(.60f-.48f)/.27f*.35f;
            Require(Mathf.Abs(wall.point.y-analytic)<.001f&&wall.normal.x<-.5f,"Barycentric height and wall normal");

            body.position=bank.transform.position+new Vector3(.25f,.4f,0);body.linearVelocity=Vector3.right*2;body.angularVelocity=Vector3.zero;Physics.SyncTransforms();
            float maximumX=.25f,maximumSpeed=2;int contacts=0;
            for(int i=0;i<150;i++)
            {
                var sample=WheelRutContact.Evaluate(body.position,Vector3.right,Vector3.forward,Vector3.up,.4f,.285f,1,scene,body,settings,1);
                if(sample.Active){body.AddForceAtPosition(sample.force,sample.point);contacts++;}
                physics.Simulate(.01f);maximumX=Mathf.Max(maximumX,body.position.x-bank.transform.position.x);maximumSpeed=Mathf.Max(maximumSpeed,body.linearVelocity.magnitude);
            }
            Require(contacts>0&&maximumX<.65f&&maximumSpeed<2.2f,"Actual sidewall approach must be bounded, without launch: "+maximumX+" / "+maximumSpeed);
            bank.gameObject.SetActive(false);bank.transform.rotation=Quaternion.Euler(0,37,0);bank.transform.localScale=new Vector3(1.3f,.8f,.9f);bank.gameObject.SetActive(true);Physics.SyncTransforms();
            Vector3 transformed=bank.transform.TransformPoint(new Vector3(.60f,1,0));
            Require(SoftGroundRuts.TrySampleSurface(transformed,scene,out var rotated),"Rotated/scaled surface lookup");
            float expectedHeight=bank.transform.TransformPoint(new Vector3(.60f,analytic,0)).y;
            Require(Mathf.Abs(rotated.point.y-expectedHeight)<.003f,"Rotated/scaled barycentric height");
            Vector3 shift=new Vector3(100,0,200);bank.transform.position-=shift;bank.ShiftOrigin(shift);
            Require(SoftGroundRuts.TrySampleSurface(transformed-shift,scene,out var shifted)&&Mathf.Abs(shifted.point.y-rotated.point.y)<.001f,"Fresh surface after floating origin");
            string report=Newtonsoft.Json.JsonConvert.SerializeObject(new {passed=true,flatForceNewtons=noWall.force.magnitude,wallForceNewtons=contact.force.magnitude,narrowForceNewtons=narrow.force.magnitude,softTyreForceNewtons=soft.force.magnitude,freshHeightErrorMetres=before.ColliderHeightErrorMetres,heightErrorAfterPriorityCook=after.ColliderHeightErrorMetres,wallNormal=wall.normal.ToString(),rotatedHeightErrorMetres=rotated.point.y-expectedHeight,originShiftHeightErrorMetres=shifted.point.y-rotated.point.y,contacts,maximumX,maximumSpeed},Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText("docs/reports/rut-wall-contact.json",report);return report;
        }
        finally{foreach(var root in scene.GetRootGameObjects())UnityEngine.Object.DestroyImmediate(root);SceneManager.UnloadSceneAsync(scene);for(int i=owned.Count-1;i>=0;i--)if(owned[i]!=null)UnityEngine.Object.DestroyImmediate(owned[i]);owned.Clear();}
    }
}
