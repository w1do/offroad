# Sources — realistic forest materials

Downloaded 2026-10-31 from Poly Haven using the public files API. Asset license: [CC0](https://polyhaven.com/license). Original JPG bytes are preserved; `sources.json` records individual URLs, byte lengths and MD5 checksums, verified against the API at download.

Active 2K PBR sets (Diffuse, OpenGL normal, Roughness):

- [Forest Leaves 02](https://polyhaven.com/a/forest_leaves_02) — moss and organic litter; source scale 3 m.
- [Forest Ground 03](https://polyhaven.com/a/forrest_ground_03) — fallen pine needles; source scale 2 m. Asset ID deliberately uses `forrest`.
- [Mud Forest](https://polyhaven.com/a/mud_forest) — wet soil and organic debris; source scale 2.3 m.
- [Leafy Grass](https://polyhaven.com/a/leafy_grass) — green growth with leaf litter.

[Aerial Grass Rock](https://polyhaven.com/a/aerial_grass_rock) was downloaded and tried during selection; the final grass slot uses Leafy Grass. It remains available as a source, not an active claim of another biome.

Normal maps are imported as Unity normal maps. Roughness maps use linear sampling (sRGB off). Albedo colour adjustment and biome blending happen in RealisticForestGround.shader; the downloaded images are not repainted. Existing Gravel Floor 04, low-poly forest models, bushes and vegetation are reused from the project. No preview renders from the website are used as terrain textures.
