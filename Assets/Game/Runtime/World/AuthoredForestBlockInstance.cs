using UnityEngine;

// Serialized identity survives an Editor script reload; the route's lookup does not.
[DisallowMultipleComponent]
public sealed class AuthoredForestBlockInstance : MonoBehaviour
{
    [HideInInspector] public int arm;
    [HideInInspector] public int block;
}
