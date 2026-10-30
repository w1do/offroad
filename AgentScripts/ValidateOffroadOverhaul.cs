using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

public static class ValidateOffroadOverhaul
{
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    static readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
    static T Own<T>(T item) where T : UnityEngine.Object { owned.Add(item); return item; }
    static Mesh Grid()
    {
        const int n = 40; var vertices = new Vector3[(n + 1) * (n + 1)]; var triangles = new int[n * n * 6];
        for (int z = 0; z <= n; z++) for (int x = 0; x <= n; x++) vertices[z * (n + 1) + x] = new Vector3(-2 + x * .1f, 0, -2 + z * .1f);
        int index = 0;
        for (int z = 0; z < n; z++) for (int x = 0; x < n; x++)
        { int a = z * (n + 1) + x; triangles[index++] = a; triangles[index++] = a + n + 1; triangles[index++] = a + 1; triangles[index++] = a + 1; triangles[index++] = a + n + 1; triangles[index++] = a + n + 2; }
        var mesh = Own(new Mesh { vertices = vertices, triangles = triangles }); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }
    static SoftGroundRuts Patch(SoftGroundProfile profile, Vector3 position, string id = "")
    {
        var go = Own(new GameObject("Validation soil")); go.SetActive(false); go.transform.position = position;
        go.AddComponent<MeshFilter>().sharedMesh = Grid(); go.AddComponent<MeshRenderer>(); go.AddComponent<MeshCollider>();
        var ruts = go.AddComponent<SoftGroundRuts>(); ruts.profile = profile; ruts.fieldHalfSize = Vector2.one * 2; ruts.persistentId = id;
        go.SetActive(true); return ruts;
    }
    static float Stamp(SoftGroundProfile profile, float load, float pressureMultiplier, float slip, float step, Vector3 position)
    {
        var patch = Patch(profile, position);
        for (int i = 0; i < Mathf.RoundToInt(4 / step); i++) SoftGroundRuts.Contact(position, Vector3.forward, .285f, load, slip, pressureMultiplier, step, .0627f / pressureMultiplier);
        patch.Commit(); return patch.DepthAt(position);
    }
    public static string Main()
    {
        Require(Application.isPlaying, "Requires Play mode");
        var rows = new List<object>();var car = UnityEngine.Object.FindAnyObjectByType<VehiclePhysics>();
        try
        {
            foreach(float load in new[]{-5000f,0,1000,10000})foreach(float speed in new[]{-.5f,0,3,20})
            {
                Vector3 v=Vector3.forward*speed;Vector3 resistance=VehiclePhysics.SoilResistanceForce(v,load,.2f,2100,4,9.81f,.01f);
                Require(Vector3.Dot(v,resistance)<=.0001f,"Soil resistance must never add energy");
                if(load<=0)Require(resistance==Vector3.zero,"Unloaded contact must not propel chassis");
            }
            rows.Add(new {test="soil resistance dissipates energy even with negative WheelHit.force",passed=true});
            var soil = Own(ScriptableObject.CreateInstance<SoilProfile>());
            var profile = Own(ScriptableObject.CreateInstance<SoftGroundProfile>()); profile.soil = soil;
            var origin = new Vector3(11000, 10, 11000);
            float light = Stamp(profile, 1500, 1, 0, .02f, origin);
            float loaded = Stamp(profile, 6000, 1, 0, .02f, origin + Vector3.right * 8);
            float spin = Stamp(profile, 6000, 1, 10, .02f, origin + Vector3.right * 16);
            float deflated = Stamp(profile, 6000, .58f, 10, .02f, origin + Vector3.right * 24);
            float halfStep = Stamp(profile, 6000, 1, 10, .01f, origin + Vector3.right * 32);
            Require(loaded > light + .01f, "Load must increase settlement");
            Require(spin > loaded + .03f, "Slip must deepen ruts");
            Require(deflated < spin - .02f, "Deflation must reduce sinkage");
            Require(Mathf.Abs(halfStep - spin) < .015f, "Step-size sensitivity");
            Require(spin <= soil.rutDepthLimitMetres + .001f, "Rut depth cap");
            rows.Add(new { test = "soil pressure, spin, deflation, step", light, loaded, spin, deflated, halfStep });

            string id = "validation-" + Guid.NewGuid(); Vector3 point = origin + Vector3.right * 48;
            var first = Patch(profile, point, id);
            for (int i = 0; i < 100; i++) SoftGroundRuts.Contact(point, Vector3.forward, .285f, 6000, 8, 1, .02f);
            float depth = first.DepthAt(point), packed = first.CompactionAt(point); first.gameObject.SetActive(false);
            var restored = Patch(profile, point, id);
            Require(Mathf.Abs(restored.DepthAt(point) - depth) < .0001f && Mathf.Abs(restored.CompactionAt(point) - packed) < .0001f, "Streaming must preserve depth and compaction");
            restored.Commit(); Physics.SyncTransforms();
            Require(restored.GetComponent<MeshCollider>().Raycast(new Ray(point + Vector3.up, Vector3.down), out var hit, 3), "Mud collider ray");
            Require(Mathf.Abs(hit.point.y - (point.y - depth)) < .005f, "Render/collision height agreement");
            rows.Add(new { test = "streaming and collider", depth, packed, colliderHeight = hit.point.y });
            float bank=restored.GetComponent<MeshFilter>().sharedMesh.vertices.Max(v=>v.y);
            Require(bank>.001f,"Ruts must displace side banks");
            var tracks=restored.gameObject.AddComponent<TireTrackRenderer>();tracks.BindPersistence(id);
            tracks.Stamp(point,Vector3.forward,.285f,.6f,0,.16f,.8f);
            float imprint=tracks.TrackAt(point);Require(imprint>0,"Tread mask");
            tracks.enabled=false;tracks.enabled=true;Require(Mathf.Abs(tracks.TrackAt(point)-imprint)<.001f,"Persistent tread mask");
            rows.Add(new {test="banks and persistent tread mask",bankHeightMetres=bank,imprint});

            var patches = new List<SoftGroundRuts>();
            for (int i = 0; i < 6; i++) { var patch = Patch(profile, origin + Vector3.forward * (8 + i * 8)); patch.Dig(patch.transform.position, 1, .2f); patches.Add(patch); }
            SoftGroundRuts.AdvanceAll(1);
            int commits = patches.Sum(p => p.CommitCount); Require(commits <= 2, "Global collider cook budget exceeded");
            for (int i = 0; i < 6; i++) SoftGroundRuts.AdvanceAll(1);
            Require(patches.All(p => p.CommitCount > 0), "Collider scheduler starvation");
            rows.Add(new { test = "collider budget and fairness", firstStepCommits = commits });

            var sample = new WaterBodySample { displacedVolumeCubicMetres = .25f, dragAreaSquareMetres = .4f, fullSubmersionMetres = 1 };
            Vector3 force = VehicleWaterInteraction.SampleForce(Vector3.right * 5, Vector3.zero, .5f, sample, 1000, 1, 9.81f, .02f, 300, 12000);
            Require(force.x < 0 && force.y > 0 && force.magnitude < 12000, "Water must resist motion and provide bounded lift");
            Require(VehicleWaterInteraction.SampleForce(Vector3.right, Vector3.zero, 0, sample, 1000, 1, 9.81f, .02f, 300, 12000) == Vector3.zero, "Dry water force");
            rows.Add(new { test = "water force", force = force.ToString("F2") });
            var waterObject=Own(new GameObject("Validation water volume"));waterObject.SetActive(false);waterObject.transform.position=origin+Vector3.right*80;
            var box=waterObject.AddComponent<BoxCollider>();box.size=new Vector3(6,4,6);box.center=Vector3.down;
            var volume=waterObject.AddComponent<WaterVolume>();volume.localSurfaceHeight=0;waterObject.SetActive(true);
            Vector3 under=waterObject.transform.position-Vector3.up*.5f;
            Require(WaterVolume.Sample(under,out var found,out float waterDepth)&&found==volume&&Mathf.Abs(waterDepth-.5f)<.001f,"Water coordinates");
            Require(!WaterVolume.Sample(under+Vector3.right*8,out _,out _),"No water outside volume");
            var testCar=Own(new GameObject("Validation water body"));testCar.transform.position=under;testCar.AddComponent<Rigidbody>();testCar.AddComponent<VehiclePhysics>();
            var interaction=testCar.AddComponent<VehicleWaterInteraction>();var waterProfile=Own(ScriptableObject.CreateInstance<VehicleWaterProfile>());
            waterProfile.bodySamples=new[]{sample};waterProfile.intakeLocalPosition=Vector3.zero;waterProfile.intakeFloodSeconds=.2f;interaction.profile=waterProfile;
            interaction.Simulate(.1f);Require(!interaction.IntakeFlooded&&interaction.LastForceNewtons.y>0,"Delayed intake hazard and buoyancy");
            interaction.Simulate(.1f);Require(interaction.IntakeFlooded,"Flooding must latch after exposure");
            testCar.transform.position+=Vector3.up*3;interaction.Simulate(1);Require(interaction.IntakeFlooded,"Leaving water must not repair intake");
            rows.Add(new {test="water volume and intake exposure",waterDepth,latched=interaction.IntakeFlooded});

            var lockFixture=Own(UnityEngine.Object.Instantiate(car.gameObject,origin+Vector3.right*200,Quaternion.identity));
            lockFixture.GetComponent<Rigidbody>().isKinematic=true;var lockCar=lockFixture.GetComponent<VehiclePhysics>();lockCar.Initialize();lockCar.externalControl=true;lockCar.automaticSimulation=false;
            Require(lockCar.SetDifferentialLock(true) && lockCar.DifferentialLocked, "Differential lock must engage while stopped");
            Require(car.profile.ValidationError(car.axles.Length) == null, "Vehicle profile validation");
            Require(car.GetComponent<VehicleWaterInteraction>() != null && car.GetComponent<VehicleWaterInteraction>().profile.bodySamples.Length > 0, "Water wiring");
            Require(car.profile.axles.All(a => a.tire != null), "Tire wiring");
            rows.Add(new { test = "Pajero wiring and lock", car.profile.ratedPowerKw, car.profile.peakTorqueNm });

            string report = Newtonsoft.Json.JsonConvert.SerializeObject(new { passed = true, checks = rows }, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText("docs/reports/offroad-validation.json", report); return report;
        }
        finally { for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) UnityEngine.Object.DestroyImmediate(owned[i]); owned.Clear(); }
    }
}
