using UnityEngine;
using UnityEngine.InputSystem;

public sealed class DriveCamera : MonoBehaviour
{
    public Transform target;
    public bool hood;
    public Vector3 hoodOffset = new Vector3(0, 1.95f, 0.8f);
    public float followDistance = 8.8f;
    public float followHeight = 4.3f;
    public float mouseSensitivity = 0.15f;
    public float zoomStep = 1.2f;
    public float minimumDistance = 4f;
    public float maximumDistance = 22f;
    public float orbitYaw;
    public float orbitPitch = 18f;
    private float cabinYaw;
    private float cabinPitch;

    private void LateUpdate()
    {
        if (target == null) return;
        if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame) hood = !hood;
        var mouse = Mouse.current;
        if (mouse != null)
        {
            if (mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue() * mouseSensitivity;
                if (hood)
                {
                    cabinYaw = Mathf.Clamp(cabinYaw + delta.x, -110, 110);
                    cabinPitch = Mathf.Clamp(cabinPitch - delta.y, -60, 60);
                }
                else
                {
                    orbitYaw += delta.x;
                    orbitPitch = Mathf.Clamp(orbitPitch - delta.y, -10, 75);
                }
            }
            if (!hood)
                followDistance = Mathf.Clamp(followDistance - mouse.scroll.ReadValue().y / 120f * zoomStep, minimumDistance, maximumDistance);
        }
        Vector3 look = target.position + Vector3.up * 1.6f;
        Quaternion orbit = Quaternion.Euler(orbitPitch, target.eulerAngles.y + orbitYaw, 0);
        Vector3 desired = hood ? target.TransformPoint(hoodOffset) : look - orbit * Vector3.forward * followDistance;
        if (!hood)
        {
            Vector3 offset = desired - look;
            float available = offset.magnitude;
            foreach (var hit in Physics.SphereCastAll(look, 0.2f, offset.normalized, available, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(target)) available = Mathf.Min(available, Mathf.Max(0.6f, hit.distance - 0.15f));
            desired = look + offset.normalized * available;
        }
        transform.position = hood ? desired : Vector3.Lerp(transform.position, desired, 1 - Mathf.Exp(-12 * Time.deltaTime));
        Quaternion rotation = hood ? target.rotation * Quaternion.Euler(cabinPitch, cabinYaw, 0) : Quaternion.LookRotation(look - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, rotation, 1 - Mathf.Exp(-10 * Time.deltaTime));
    }
}
