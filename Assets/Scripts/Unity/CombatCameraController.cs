using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Mouse-driven camera controls for the Combat scene.
/// Scroll wheel zooms in/out along the camera's forward axis.
/// Right-click drag moves the camera up/down (world Y).
/// </summary>
public sealed class CombatCameraController : MonoBehaviour
{
    [Header("Zoom")]
    [Min(0.1f)] public float MinDistance = 3f;
    [Min(0.1f)] public float MaxDistance = 20f;
    public float ZoomSpeed = 2f;

    [Header("Pan")]
    public float PanSpeed = 0.02f;

    [Header("Rotation")]
    public float RotationSpeed = 0.2f;

    private Camera _camera;
    private Vector3 _initialPosition;
    private Quaternion _initialRotation;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        _initialPosition = transform.position;
        _initialRotation = transform.rotation;
    }

    private void Update()
    {
        HandleReset();

        if (Mouse.current == null)
            return;

        HandleZoom();
        HandlePan();
        HandleRotation();
    }

    private void HandleZoom()
    {
        var mouse = Mouse.current;
        if (mouse == null)
            return;

        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Approximately(scroll, 0f) || !TryGetBoardTarget(out Vector3 zoomTarget))
            return;

        float currentDistance = Vector3.Distance(transform.position, zoomTarget);
        float targetDistance = Mathf.Clamp(
            currentDistance - scroll * ZoomSpeed,
            MinDistance,
            MaxDistance);

        transform.position = zoomTarget - _camera.transform.forward * targetDistance;
    }

    private bool TryGetBoardTarget(out Vector3 boardTarget)
    {
        var boardPlane = new Plane(Vector3.up, Vector3.zero);
        var centerRay = new Ray(transform.position, _camera.transform.forward);

        if (boardPlane.Raycast(centerRay, out float distance))
        {
            boardTarget = centerRay.GetPoint(distance);
            return true;
        }

        boardTarget = default;
        return false;
    }

    private void HandlePan()
    {
        var mouse = Mouse.current;
        if (mouse == null
            || !IsCameraModifierHeld()
            || !mouse.rightButton.isPressed)
            return;

        Vector2 delta = mouse.delta.ReadValue();
        if (Mathf.Approximately(delta.magnitude, 0f))
            return;

        Vector3 horizontal = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        transform.position += horizontal * (-delta.x * PanSpeed)
                              + Vector3.up * (-delta.y * PanSpeed);
    }

private void HandleRotation()
    {
        var mouse = Mouse.current;
        if (mouse == null
            || !IsCameraModifierHeld()
            || !mouse.leftButton.isPressed
            || !TryGetBoardTarget(out Vector3 boardTarget))
            return;

        Vector2 delta = mouse.delta.ReadValue();
        if (Mathf.Approximately(delta.magnitude, 0f))
            return;

        transform.RotateAround(boardTarget, Vector3.up, delta.x * RotationSpeed);

        Vector3 offset = transform.position - boardTarget;
        float elevation = Mathf.Asin(offset.normalized.y) * Mathf.Rad2Deg;
        float targetElevation = Mathf.Clamp(
            elevation - delta.y * RotationSpeed,
            10f,
            80f);
        transform.RotateAround(boardTarget, transform.right, targetElevation - elevation);
        transform.LookAt(boardTarget, Vector3.up);
    }

    private void HandleReset()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null
            && IsCameraModifierHeld()
            && keyboard.spaceKey.wasPressedThisFrame)
        {
            transform.SetPositionAndRotation(_initialPosition, _initialRotation);
        }
    }

    private static bool IsCameraModifierHeld()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null
               && ((keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed)
                   || (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed));
    }
}
