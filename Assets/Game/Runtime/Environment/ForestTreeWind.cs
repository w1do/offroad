using UnityEngine;

/// <summary>Configures GPU bending without moving the trunk collider or allocating meshes.</summary>
[DisallowMultipleComponent]
public sealed class ForestTreeWind : MonoBehaviour
{
    public bool useMaterialSettings;
    [Range(0, 0.15f)] public float bend = 0.055f;
    [Range(0, 0.15f)] public float leafFlutter = 0.045f;
    private Renderer[] renderers;
    private Bounds[] originalBounds;

    private void OnEnable()
    {
        renderers = GetComponentsInChildren<Renderer>();
        originalBounds = new Bounds[renderers.Length];
        var block = new MaterialPropertyBlock();
        for (int i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            originalBounds[i] = renderer.localBounds;
            if (!useMaterialSettings)
            {
                renderer.GetPropertyBlock(block);
                block.SetFloat("_TreeSway", bend);
                block.SetFloat("_LocalWindAnchor", 1);
                // Normalized generated meshes share a root at local (0, 0, 0).
                block.SetFloat("_LeafFlutter", renderer.name.StartsWith("Tree_Trunk") ? 0 : leafFlutter);
                renderer.SetPropertyBlock(block);
            }
            var bounds = renderer.localBounds;
            bounds.Expand(new Vector3(16, 1, 16));
            renderer.localBounds = bounds;
        }
    }

    private void OnDisable()
    {
        if (renderers == null) return;
        var block = new MaterialPropertyBlock();
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            if (!useMaterialSettings)
            {
                renderers[i].GetPropertyBlock(block);
                block.SetFloat("_TreeSway", 0);
                block.SetFloat("_LeafFlutter", 0);
                renderers[i].SetPropertyBlock(block);
            }
            renderers[i].localBounds = originalBounds[i];
        }
    }
}



