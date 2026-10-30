MOUNTAIN PASS

UPDATE: MountainPass.unity now contains the imported forest and KAMAZ.
See Assets/Game/README.txt for current controls, scope and validation.
Backup: Assets/Game/Scenes/MountainPass_BeforeForest.unity.
The remaining text describes the previous mountain/SUV prototype.

Open Assets/MountainDrive/MountainPass.unity and press Play.
Click the Game window to give it keyboard focus.

W / Up: accelerate
S / Down: brake, then reverse
A D / Left Right: steer
Space: handbrake
R: recover onto the nearest road point
C: switch chase / bonnet camera

The 7.23 km mountain serpentine climbs approximately 252 metres.
The scene contains a 3.2 x 3.2 km Terrain, a driveable four-wheel-drive SUV,
three road surfaces, collision guardrails, road signs and 43 potholes.
Ice reduces forward and lateral wheel grip. Damaged asphalt includes
physical mesh irregularities and depressions. Gravel shoulders have reduced grip.
Reach the summit to complete the route.

MODEL
Kenney Car Kit 3.1, SUV FBX, Creative Commons CC0 1.0.
https://kenney.nl/assets/car-kit
Downloaded from the Kenney release mirrored on OpenGameArt:
https://opengameart.org/content/car-kit
Original license: Art/Kenney/License.txt
The imported SUV is a stylized low-poly model.

TEXTURES (ambientCG, Creative Commons CC0 1.0)
Clean asphalt: https://ambientcg.com/view?id=Asphalt006
Cracked asphalt: https://ambientcg.com/view?id=Asphalt012
Earth / grass: https://ambientcg.com/view?id=Ground037
Rock: https://ambientcg.com/view?id=Rock030
Ice: https://ambientcg.com/view?id=Ice004
License: https://docs.ambientcg.com/license/
Materials use downloaded 1K color and OpenGL normal maps with URP shaders.
Source-only Blender/USD files were stored outside Assets in SourceAssets.

SCENE AUTHORING
AgentScripts/BuildMountainDrive.cs builds the initial scene through Unity CLI.
AgentScripts/RefineMountains.cs applies the final smooth road cut and terrain painting.
AgentScripts/AddRoadDamage.cs adds the final physical potholes and ice material tuning.
The builder and damage pass refuse to overwrite an existing result.
AgentScripts/ValidateDriving.cs checks acceleration and braking across all surfaces,
reverse, steering, recovery and actual frame progression in Play mode.
Existing landscape scenes are retained separately in the project.
