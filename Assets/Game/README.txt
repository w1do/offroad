CURRENT SCENE: AUTHORED FOREST ROUTE + SHARED VEHICLE PHYSICS
Scene: Assets/MountainDrive/MountainPass.unity.
Source forest FBX and textures preserved; derived modules and local joining corridors
extend the supplied road with curves, climbs and descents. AuthoredForestRoute streams
10,000 km per arm using 12 repeating source-derived module prefabs and floating origin.
Old invented LongRouteStream terrain and river sheets remain disabled.
Pajero uses VehiclePhysics + its own VehiclePhysicsProfile, independent of legacy Kamaz.
User targets: 200 km/h, 162 kW at 6250 rpm, 281 Nm at 4000 rpm.
Original mud scans support bounded runtime ruts; exact visible-ground collision skins.
See docs/vehicles/adding-a-vehicle.md and decisions 0003/0004.
Validation: shared physics, vehicle layouts, drift, mud digging, offroad, route,
and independent visible terrain collision scripts in AgentScripts.
Full 10,000 km traversal and standalone build performance have not been certified.

OLDER DEVELOPMENT NOTES FOLLOW (may describe disabled prototypes):
FOREST + KAMAZ — FIRST DRIVING POLYGON

LIVING FOREST / GRAVEL / RIVER FORDS — CURRENT UPDATE
Current scene remains Assets/MountainDrive/MountainPass.unity, using the
original supplied forest layout with light snow rather than the old generated
mountain corridor. Backup: Assets/Game/Scenes/BeforeLivingForest.unity.

Visuals: warm sun, soft 4096 shadow atlas / four cascades / 120 m range,
4x MSAA and SMAA, restrained bloom and ACES, procedural sky, 128 px realtime
reflection probe, physically based normal/roughness maps and wet bank shading.
Quality settings clone: Assets/Game/Generated/LivingForest/ForestQuality.asset.
The source PC pipeline asset remains unchanged. QualitySettings selects the
clone for the active quality level. No renderer/package upgrade was required.

Trees/foliage: ForestWind drives GPU trunk/canopy sway and leaf flutter.
1137 baked anchor candidates; 6 vegetation meshes / 288747 vertices carry
root data in TEXCOORD2. Root height pins trunks; shadows/depth use the same
displacement. Static trunk colliders intentionally do not simulate branch sway.
Source FBX/materials remain unchanged; generated meshes and materials are
under Assets/Game/Generated/LivingForest. Wind is global, not 1000 Update calls.

Textures: 2K diffuse, OpenGL normal and roughness maps for Gravel Floor 04,
Forest Ground 04 and River Small Rocks from Poly Haven (CC0). See
Assets/Game/Art/ForestEnhancement/SOURCES.txt for authors and exact links.
The existing road retains its layout and decorative details; gravel uses world
mapping at the source material's approximate physical size.

Rivers: two crossings ahead on the original road, around (-40.62,43.15,44.16)
and (-72.29,40.28,45.86). Current speeds 2.7 and 3.1 m/s. Central bed depth is
approximately 0.6 m, increasing away from the marked crossing lane. Banks and
colliders are carved together; localized subdivision prevents coarse original
triangles from covering the water surface. Only local generated meshes change.
Animated reflective water, wet gravel beds, bank stones, lane markers and
timber warning signs identify the crossings. Water remains unfrozen locally.

RiverCurrent applies drag from relative current/point velocity and buoyancy
at six chassis samples. Water density 1000 kg/m3, finite effective displaced
volume and wetted area, force cap 65000 N per vehicle/river application.
No Transform motion, velocity override or scripted sideways kick drives the
truck. Water unloads tire contacts and can pull the truck downstream. The
numeric model is a calibrated gameplay approximation, not a fluid solver.
Use LOW range and steer upstream; stay between markers. Chains do not cancel
the river's force. Existing brakes, chain fitting and truck audio are retained.

Ambience: actual field recordings of birds, wind and running water, CC0 by
Thimras on OpenGameArt. Trimmed/crossfaded 26 s loops, mono 24 kHz. Ambient
wind/birds are quieter in the cab; river sounds are spatial and local.
Source geography and edits: Assets/Game/Audio/Forest/SOURCES.txt.
Truck engine audio remains the separate synthesized prototype sound set.

Verification in Editor Play mode:
- Both physical river volumes: dry and no-current cases, bounded forces.
- An isolated 11 tonne test body drifted 2.83 m / 3.52 m over 3 seconds under
  current; peak measured forces 22419 N / 24733 N, below configured cap.
- Actual KAMAZ crossed both fords in LOW with steering correction; maximum
  side offset 1.51 m / 1.68 m, wet sample fraction 0.35 / 0.37; no fall-through.
- Four recorded ambient sources produced nonzero output (minimum RMS 0.000714).
- Wind clock advances; anchor vertex streams and wind-aware shadow/depth
  passes are present; shaders compile without errors.
- Visual camera captures inspected. Standalone build and hardware-specific
  FPS target were not tested; no performance-target claim is made.

Authoring order for this saved milestone:
BuildLivingForest.cs (guarded one-time build), RefineRiverBeds.cs,
PolishForestFords.cs, FinishLivingForest.cs, TessellateRiverBanks.cs.
Do not rerun one-time builders over later user edits. Saved assets/scenes are
the authoritative result. ValidateForestRivers.cs / ValidateActualFords.cs /
ValidateForestAmbience.cs exercise the meaningful integration behavior.
Runtime classes: Environment/ForestWind.cs, Environment/ForestAmbience.cs,
Water/RiverCurrent.cs. Rendering: LivingForest.shader, ForestWind.hlsl,
ForestRiver.shader. Each source has independent tunable parameters.
Older notes below document previous milestones and settings.

CURRENT LOOK — ORIGINAL FOREST WITH LIGHT SNOW
MountainPass again opens on the supplied forest's original road and scenery.
Original mesh geometry, tree positions, stones, roadside details, colliders and
texture maps are retained. Separate material variants add sparse upward-facing
snow patches; the original summer materials are unchanged. The road remains
mostly visible. A light, player-following snowfall is capped at 600 particles.
Shader: Assets/Game/Shaders/ForestLightSnow.shader.
Snow amount per material: Assets/Game/Generated/ForestLightSnow.
Winter effects are visual only; snow patches do not silently change tire grip.
Truck physics, brakes, chains, camera controls and audio are preserved.
The generated mountain stage is disabled in this scene. Its playable version
with audio is preserved as Assets/Game/Scenes/MountainClimb_WithAudio.unity.
The 740 m climb tests described below apply to that separate mountain scene.
Authoring: RestoreForestLightSnow.cs, a one-time restore with a guarded backup.
Verified original-scene render and Play-mode audio after restoration.
Older milestone notes below describe prior scene layouts.

TRUCK AUDIO UPDATE
9 original synthesized mono WAV clips (32 kHz PCM), no downloaded recordings.
Engine idle/load layers react to throttle, driven-wheel RPM and low/high range;
wheelspin raises engine tone even when vehicle speed stays low. Tire noise is
contact/speed based; slip reacts to wheel/lateral slip; chain rattle requires
installed chains and wheel motion. Pneumatic release plays after braking,
gear changes and chain fitting have one-shot mechanical sounds.
Hold H for the truck horn. Audio sources are attached to the engine, wheel and
rear-axle positions; doppler is disabled to avoid pitch jumps while orbiting.
Configuration: Assets/Game/Audio/KamazAudio.asset. Master and individual layer
volumes, RPM response and attenuation distances are editable in this profile.
Runtime: Assets/Game/Runtime/Audio/TruckAudio.cs and TruckAudioProfile.cs.
ConfigureTruckAudio.cs wires the sources and imports assets. It replaces only
the generated Truck audio child, leaving vehicle/world settings intact.
ValidateTruckAudio.cs passed: nine decodable non-clipping clips; engine response
600 -> 1473 RPM; playing engine source output RMS 0.0349; brake release, one
gear-change event, horn fade and silent chains when uninstalled.
This verifies Unity playback/data, not subjective listening on the user's
speakers. The clips are prototype approximations, not real KAMAZ recordings.

MOUNTAIN CLIMB UPDATE — CURRENT SCENE
MountainPass now contains a 740 m authored winter mountain test route.
Backup of the imported forest polygon: Assets/Game/Scenes/BeforeMountainClimb.unity.
The old forest root is retained disabled. 650 placed tree instances reuse three
variants extracted from the supplied forest's trunk/foliage meshes.
The new route contains mild climbs, steep descents, stopping platforms and
a difficult ice climb from roughly 260 to 400 m with alternating crossfall.
Road meshes and their colliders are identical; terrain is recessed below the
road corridor to avoid interpenetration at mesh edges.

CHAINS
Stop on the flat platform around 240 m. Hold Space and press T; keep holding
Space for 4 seconds. T also removes chains under the same conditions.
Movement above 0.3 km/h or releasing Space cancels the operation. No grip
bonus before completion. Visible chains cover the four driven rear wheel
groups, including paired rear tires; front wheels stay unchained.
Use L to select LOW while stopped, then release the brake and drive uphill.
Chains improve the configured snow/ice profile only. They do not create an
unlimited grip override or prevent unsafe-speed slides and rollovers.
Under longitudinal wheelspin, unchained driven tires lose lateral grip;
the road's bank direction determines left/right drift, with no random force.
Current prototype ice coefficients: forward 0.10, lateral 0.35 before combined
slip reduction; rear chained forward 1.05 and lateral 0.85. These are tuning
parameters, not measured real-world tire coefficients.

VALIDATION
ValidateMountainChains.cs passed: 20-degree icy slope, no chains = 27.14 m
rollback with peak forward slip 1.85; chains = 17.42 m uphill travel. Opposite
10-degree side slopes under throttle produced opposite downhill lateral drift
up to 1.02 m. Installation safety, timing, cancellation and visuals passed.
The bare sample ran for 4 seconds; the chained sample for 8 seconds. These
distances show failed versus successful climbing, not an equal-time speed test.
ValidateActualClimb.cs passed on the actual scene from 240 m to the 410 m
summit with chains and LOW; maximum lateral offset was 0.88 m, no fall-through.
ValidateTractionBrakes.cs also passed: mud wheelspin, 15-degree braking/holding
and stop lamps. Full 740 m manual traversal and standalone build not tested.
Runtime references are rebuilt in OnEnable so Editor recompilation during
Play does not lose the wheel-contact arrays. R reset ignores automated input.

AUTHORING
BuildMountainClimb.cs constructs the stage once and refuses duplicate builds.
RefineMountainClimb.cs applied a one-time road-clearance / snow / sign pass.
TuneMountainSurface.cs saves the final ice lateral profile.
Do not rerun older forest builders over this scene or over user changes.
The older import/physics notes below describe previous milestones.

TRACTION / BRAKES / AXLE UPDATE (supersedes earlier settings below)
W: throttle; S or Space: service brake with low-speed hill holding.
V: forward/reverse, L: low/high range, both only when stopped.
RMB: look/orbit; wheel: zoom; C: camera; R: debug reset.
Inboard metal is now excluded at the triangle level from all six wheel
animations. Only outboard disc surfaces and rubber spin; the fixed metal
geometry remains a separate chassis child. Spare remains fixed as well.
Peak torque 2400 Nm/driven wheel, low-range multiplier 3, wheel power 150 kW.
Power limit uses actual driven-wheel RPM, including when the tires spin.
Mud forward grip is 0.12 versus base dirt 0.65. Insufficient traction allows
wheel RPM to rise without the corresponding vehicle speed. Uphill in a high
range can also run out of torque; low range enables the higher torque needed
to exceed available traction. It does not create grip.
Braking below 0.5 m/s hands longitudinal tire force to a static-friction
solver bounded by measured normal loads and the surface grip. WheelCollider
longitudinal stiffness is zero in that regime, avoiding double tire forces.
It does not freeze the Rigidbody or override the velocity. Low-grip slopes
can still exceed the available holding force.
Two emissive red stop lamps and local red lights follow actual braking,
including S braking while driving forward. They switch off under throttle.
ConfigureTruckTraction.cs saves current setup after any rig rebuild.
ValidateTractionBrakes.cs runs fixed-step simulations on a removable fixture.
Observed after final brake-force handoff: dry acceleration 13.82 km/h after 2.5s; mud 0.04 km/h with rear
wheels at 444 RPM and forward slip 1.00; brakes stopped and held a 15-degree
dirt slope with 0.002m measured drift; slippery uphill slip 0.94. S braking
and brake-light on/off checks passed. These are prototype scenario results,
not claims of a calibrated real-world KAMAZ model.

PHYSICS REPAIR UPDATE
Current truck mass is 11000 kg. Rear tandem drive shares 150 kW wheel power;
1500 Nm peak per driven wheel, gradual throttle, force-based rolling/aero
resistance and engine braking. These remain prototype tuning values.
Wheels rebuilt by connected geometry, excluding the spare and internal metal
fittings. Only tires and full-size outer rims spin. Internal axle details are
separate chassis children and do not inherit spin or steering.
Grass_Close, road trails and gravel edges now have actual surface colliders.
3741 sampled upward-facing surface points passed collider coverage checks.
Mud uses the visible puddle mesh and ForestDrivingSurface per wheel contact;
reduced lateral/longitudinal grip and increased resistance. No random yaw.
Fast corners progressively reduce rear grip, allowing countersteering.
RMB drag: orbit/look in cabin. Scroll: exterior zoom. C: exterior/cabin.
Debug R still resets to the initial spawn.
AgentScripts/RepairForestTruck.cs repairs geometry and scene settings.
AgentScripts/ValidateTruckRepair.cs uses a temporary flat test fixture which
is removed afterward. Confirmed: 6 grounded wheels; speed 8.09 km/h after 3s;
7.22 km/h after 1.2s coasting; 0 after braking; 1.72 m/s lateral slide in a
50 km/h turn; countersteer responds; mud detected at six contacts; camera
orbit checked at 90 degrees. Static axle transform stayed unchanged when
wheel visuals were rotated 90 degrees. Standalone build not tested.
The original import notes below describe earlier settings/results.

Open Assets/MountainDrive/MountainPass.unity and press Play.
Previous scene: Assets/Game/Scenes/MountainPass_BeforeForest.unity.
The previous mountain terrain, serpentine and SUV are replaced in MountainPass.

WASD / arrows: drive. Space: brake. C: camera. R: debug reset to start.

Source archives remain unchanged under Models/Forest and Models/Cars.
Imported Unity copies: Assets/Game/Art/Forest and Assets/Game/Art/Kamaz.
Forest FBX was extracted from the nested Dirt Road Forest Scene.rar archive.
URP materials, packed foliage alpha textures and animated wheel meshes are
stored in Assets/Game/Generated. Source textures remain unchanged.

Forest: [UPDATE] Dirt Road Through Forest by 99.Miles.
https://sketchfab.com/3d-models/update-dirt-road-through-forest-c4676cdf7715484382400ff63faffd45
Source page identifies CC Attribution 4.0:
https://creativecommons.org/licenses/by/4.0/
Changes: URP materials, packed foliage alpha, collision meshes, scene placement.
Truck source: Models/Cars/kamaz-body-lowpoly.zip, source/KAMAZ.fbx.
Truck author/license was not established during the local import. Retain the
source and establish attribution/distribution terms before publishing.

Truck: 8500 kg Rigidbody, body BoxCollider, six WheelColliders, front steering.
Combined tire/rim meshes are split into animated wheel groups.
Road, ground, rocks, trunks and fences have static MeshColliders.
DriveCamera now has configurable chase distance, height and cabin offset;
the previous default offsets are retained for other scenes.

LIMITATIONS
This is a short imported forest polygon, not a 100 km route. Original non-winter
appearance retained. Chains, ranges, cargo, trailer, weather and economy are
not yet implemented. Driving physics is an initial prototype, not a calibrated
heavy-truck simulation. Materials use base-color/opacity and simple smoothness;
normal/roughness source maps are retained for a later complete PBR pass.
The HUD does not report the old mountain route's progress.

AUTHORING
AgentScripts/BuildForestKamaz.cs: initial scene construction; refuses repeat
execution if the backup exists. Do not overwrite subsequent user changes.
AgentScripts/FinishForestImport.cs: shared grass alpha and camera configuration.
AgentScripts/ValidateForestKamaz.cs: Play-mode smoke test.
Confirmed CLI on this machine: Unity CLI 1.0.0-beta.14, Unity 6000.6.7f1.
Confirmed invocations, specific to that installed CLI:
  unity status --json
  unity command script run --file AgentScripts/BuildForestKamaz.cs --timeout 180
  unity command editor.playmode start
  unity command script run --file AgentScripts/ValidateForestKamaz.cs --timeout 45
  unity command editor.playmode stop

VERIFICATION
Play-mode result: six grounded wheels; acceleration to 24.16 km/h in 2 seconds;
6.90 m travel; 0.16 km/h after 1.5 seconds braking; front axle steering verified;
286 frames advanced. Scene saved, no old SUV or Terrain, no missing material
references. No standalone build or complete road traversal was performed.

