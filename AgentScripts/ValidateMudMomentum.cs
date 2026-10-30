using System;
using System.Linq;
using UnityEngine;
using UnityEditor;

public static class ValidateMudMomentum
{
    static string Trial(VehiclePhysics source,MudRoadSection section,float speed,out float travel,out float depth,float offset=0,bool approach=false)
    {
        GameObject clone=null,soilClone=null;var original=section.gameObject;bool enabled=original.activeSelf;
        try
        {
            original.SetActive(false);soilClone=UnityEngine.Object.Instantiate(original,original.transform.position,original.transform.rotation);soilClone.GetComponent<SoftGroundRuts>().persistentId="";
            if(section.profile.fullWidthFurrows)
            {
                var asset=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Game/Generated/FullWidthFurrows/Bed main0.asset");
                soilClone.GetComponent<MeshFilter>().sharedMesh=asset;soilClone.GetComponent<MeshCollider>().sharedMesh=asset;
                soilClone.GetComponent<TireTrackRenderer>().persistentId="";
            }
            soilClone.SetActive(true);
            var soil=soilClone.GetComponent<SoftGroundRuts>();
            var entry=section.transform.TransformPoint(new Vector3(section.laneCentreMetres+offset,0,approach?-section.halfSize.y-2:-2f));
            var hit=Physics.RaycastAll(entry+Vector3.up*12,Vector3.down,30,~0,QueryTriggerInteraction.Ignore).Where(h=>!h.collider.transform.IsChildOf(source.transform)&&h.normal.y>.4f).OrderByDescending(h=>h.point.y).First();
            clone=UnityEngine.Object.Instantiate(source.gameObject,hit.point+Vector3.up*.65f,Quaternion.LookRotation(section.transform.forward));
            var vehicle=clone.GetComponent<VehiclePhysics>();var rb=clone.GetComponent<Rigidbody>();rb.isKinematic=false;
            vehicle.automaticSimulation=false;vehicle.externalControl=true;clone.GetComponent<VehiclePlayerInput>().enabled=false;clone.GetComponent<VehicleAudio>().enabled=false;
            vehicle.Initialize();vehicle.SetFourWheelDrive(true);vehicle.SetTransmission(false,true);vehicle.SetInput(new VehicleControlInput{brake=1});Physics.SyncTransforms();
            Action step=()=>{vehicle.Simulate(.01f);Physics.Simulate(.01f);vehicle.SendMessage("LateUpdate");};for(int i=0;i<150;i++)step();
            var start=rb.position;rb.linearVelocity=section.transform.forward*speed;vehicle.SetInput(new VehicleControlInput{throttle=1});float spin=0;int contacts=0;
            for(int i=0;i<700;i++){step();spin=Mathf.Max(spin,vehicle.WheelspinSpeed);contacts=Mathf.Max(contacts,vehicle.MudContacts);}
            soil.Commit();travel=Vector3.Dot(rb.position-start,section.transform.forward);depth=soil.DugDepth;
            return "entry="+speed+"m/s offset="+offset+" approach="+approach+" travel="+travel.ToString("F2")+"m depth="+depth.ToString("F3")+"m slip="+spin.ToString("F1")+" contacts="+contacts;
        }
        finally{if(clone!=null)UnityEngine.Object.DestroyImmediate(clone);if(soilClone!=null)UnityEngine.Object.DestroyImmediate(soilClone);original.SetActive(enabled);}
    }
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Requires Play");var source=UnityEngine.Object.FindAnyObjectByType<VehiclePhysics>();var section=UnityEngine.Object.FindObjectsByType<MudRoadSection>().First(s=>s.GetComponent<SoftGroundRuts>().persistentId=="main0/natural");
        var body=source.GetComponent<Rigidbody>();bool kinematic=body.isKinematic;var velocity=body.linearVelocity;var angular=body.angularVelocity;var mode=Physics.simulationMode;bool paused=EditorApplication.isPaused;
        try
        {
            Physics.simulationMode=SimulationMode.Script;EditorApplication.isPaused=true;body.isKinematic=true;
            string fast=Trial(source,section,7,out float fastTravel,out float fastDepth);string slow=Trial(source,section,0,out float slowTravel,out float slowDepth);
            if(fastTravel<12||fastTravel>100||fastTravel<slowTravel+3)throw new Exception("Momentum calibration: "+fast+"; "+slow);
            string approaches="";
            if(section.profile.fullWidthFurrows)
            {
                foreach(float offset in new[]{-2.2f,0,2.2f})
                {
                    string moving=Trial(source,section,7,out float movingTravel,out _,offset,true);
                    string crawling=Trial(source,section,0,out float crawlingTravel,out _,offset,true);
                    float exitDistance=section.halfSize.y*2+3.5f;
                    if(movingTravel<exitDistance||crawlingTravel>=exitDistance||movingTravel<crawlingTravel+3)
                        throw new Exception("Full-width approach failed: "+moving+"; "+crawling);
                    approaches+="\n"+moving+"; "+crawling;
                }
            }
            return "PASS 4WD LOW same lane, 7s: "+fast+"; "+slow+approaches;
        }
        finally{body.isKinematic=kinematic;if(!kinematic){body.linearVelocity=velocity;body.angularVelocity=angular;}Physics.simulationMode=mode;EditorApplication.isPaused=paused;}
    }
}

