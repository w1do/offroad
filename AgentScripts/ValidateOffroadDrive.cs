using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ValidateOffroadDrive
{
    static object Trial(VehiclePhysics source, bool soft, out float travel)
    {
        var scene = SceneManager.CreateScene("Isolated offroad validation " + Guid.NewGuid(), new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        var physics = scene.GetPhysicsScene(); var owned = new List<UnityEngine.Object>();
        try
        {
            var soilProfile = ScriptableObject.CreateInstance<SoilProfile>(); owned.Add(soilProfile);
            if (!soft) { soilProfile.moisture = .1f; soilProfile.waterContent = .1f; soilProfile.bearingStrengthPascal = 1000000; soilProfile.rollingResistance = .02f; }
            var deformation = ScriptableObject.CreateInstance<SoftGroundProfile>(); owned.Add(deformation); deformation.soil = soilProfile;
            var ground = new GameObject("Measured soil lane"); ground.SetActive(false); SceneManager.MoveGameObjectToScene(ground, scene);
            const int nx = 40, nz = 120; var vertices = new Vector3[(nx + 1) * (nz + 1)]; var triangles = new int[nx * nz * 6];
            for (int z = 0; z <= nz; z++) for (int x = 0; x <= nx; x++) vertices[z * (nx + 1) + x] = new Vector3(-4 + x * .2f, 0, -8 + z * .2f);
            int t = 0; for (int z = 0; z < nz; z++) for (int x = 0; x < nx; x++) { int a = z * (nx + 1) + x; triangles[t++] = a; triangles[t++] = a + nx + 1; triangles[t++] = a + 1; triangles[t++] = a + 1; triangles[t++] = a + nx + 1; triangles[t++] = a + nx + 2; }
            var mesh = new Mesh { vertices = vertices, triangles = triangles }; owned.Add(mesh); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            ground.AddComponent<MeshFilter>().sharedMesh = mesh; ground.AddComponent<MeshRenderer>(); ground.AddComponent<MeshCollider>();
            var ruts = ground.AddComponent<SoftGroundRuts>(); ruts.profile = deformation;
            var surface = ground.AddComponent<ForestDrivingSurface>(); surface.cover = ForestDrivingSurface.Cover.Mud;
            surface.forwardGrip = soft ? .12f : .55f; surface.lateralGrip = soft ? .35f : .65f; surface.rollingResistance = soft ? .06f : .02f;
            ground.SetActive(true);
            var clone = UnityEngine.Object.Instantiate(source.gameObject, new Vector3(0, .65f, 0), Quaternion.identity); SceneManager.MoveGameObjectToScene(clone, scene);
            var vehicle = clone.GetComponent<VehiclePhysics>(); var body = clone.GetComponent<Rigidbody>(); body.isKinematic = false;
            vehicle.externalControl = true; vehicle.automaticSimulation = false;
            foreach (var input in clone.GetComponents<VehiclePlayerInput>()) input.enabled = false;
            vehicle.Initialize(); vehicle.SetFourWheelDrive(true); vehicle.SetTransmission(false, true);
            vehicle.SetInput(new VehicleControlInput { brake = 1 }); Physics.SyncTransforms();
            for (int i = 0; i < 200; i++) { vehicle.Simulate(.01f); physics.Simulate(.01f); }
            if (vehicle.Wheels.Count(w => w.isGrounded) < 3) throw new Exception("Fixture did not settle");
            Vector3 start = body.position; vehicle.SetInput(new VehicleControlInput { throttle = 1 });
            float maxSpeed = 0, spin = 0, maximumHeight = start.y;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 400; i++)
            {
                vehicle.Simulate(.01f); physics.Simulate(.01f);
                maxSpeed = Mathf.Max(maxSpeed, body.linearVelocity.magnitude); spin = Mathf.Max(spin, vehicle.WheelspinSpeed); maximumHeight = Mathf.Max(maximumHeight, body.position.y);
            }
            watch.Stop(); travel = body.position.z - start.z;
            if (maxSpeed > 20 || maximumHeight > start.y + 1 || body.position.y < -1) throw new Exception("Unphysical fixture result: speed=" + maxSpeed + " height=" + maximumHeight + " final=" + body.position);
            return new { soft, seconds = 4, travelMetres = travel, maximumSpeedMps = maxSpeed, spinMps = spin, rutDepthMetres = ruts.DugDepth, simulationWallMs = watch.Elapsed.TotalMilliseconds };
        }
        finally
        {
            foreach (var root in scene.GetRootGameObjects()) UnityEngine.Object.DestroyImmediate(root);
            SceneManager.UnloadSceneAsync(scene);
            foreach (var item in owned) if (item != null) UnityEngine.Object.DestroyImmediate(item);
        }
    }
    public static string Main()
    {
        if (!Application.isPlaying) throw new Exception("Requires Play mode");
        var source = UnityEngine.Object.FindAnyObjectByType<VehiclePhysics>();
        var firm = Trial(source, false, out float firmTravel); var mud = Trial(source, true, out float mudTravel);
        if (firmTravel <= mudTravel + .5f) throw new Exception("Bearing/grip must make the firm lane more passable: " + firmTravel + "/" + mudTravel);
        string report = Newtonsoft.Json.JsonConvert.SerializeObject(new { passed = true, firm, mud }, Newtonsoft.Json.Formatting.Indented);
        File.WriteAllText("docs/reports/offroad-drive.json", report); return report;
    }
}
