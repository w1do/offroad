using UnityEngine;

// Add to an actual fixed trunk/rock collider, never to a road or decorative billboard.
[RequireComponent(typeof(Collider))]
public sealed class WinchAnchorSurface : MonoBehaviour { }
