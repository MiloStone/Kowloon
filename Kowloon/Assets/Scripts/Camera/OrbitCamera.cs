using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Perspective camera that orbits a fixed point on a circular track.
/// Right-click drag rotates around the Y axis and adjusts elevation.
/// Scroll wheel zooms by changing orbit distance.
/// PanTargetTo() smoothly moves the orbit target (used for floor transitions).
/// </summary>
[RequireComponent(typeof(Camera))]
public class OrbitCamera : MonoBehaviour
{
    [Header("Target")]
    public Vector3 target = Vector3.zero;

    [Header("Orbit")]
    [Tooltip("Distance from the target point.")]
    public float distance = 14f;

    [Tooltip("Degrees above the horizon (30 = classic isometric).")]
    public float elevationDeg = 35f;

    [Tooltip("Starting horizontal angle in degrees.")]
    public float startAngleDeg = 45f;

    [Tooltip("Mouse sensitivity for right-click drag (horizontal orbit).")]
    public float sensitivity = 0.4f;

    [Tooltip("Mouse sensitivity for right-click drag (vertical elevation).")]
    public float elevationSensitivity = 0.3f;

    [Tooltip("Clamp range for elevation in degrees.")]
    public float elevationMin = 5f;
    public float elevationMax = 85f;

    [Header("Zoom")]
    public float zoomSpeed = 8f;
    public float zoomMin   = 4f;
    public float zoomMax   = 30f;

    // ── private ───────────────────────────────────────────────────────────────

    private float      _angle;
    private Camera     _cam;
    private Coroutine  _panCoroutine;

    // ── lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _cam                  = GetComponent<Camera>();
        _cam.orthographic     = false;
        _angle                = startAngleDeg;
        ApplyTransform();
    }

    void Update()
    {
        if (Mouse.current.rightButton.isPressed)
        {
            float dx = Mouse.current.delta.x.ReadValue();
            float dy = Mouse.current.delta.y.ReadValue();
            _angle       += dx * sensitivity;
            elevationDeg  = Mathf.Clamp(elevationDeg - dy * elevationSensitivity, elevationMin, elevationMax);
            ApplyTransform();
        }

        float scroll = Mouse.current.scroll.y.ReadValue();
        if (scroll != 0f)
        {
            distance = Mathf.Clamp(distance - scroll * zoomSpeed * Time.deltaTime, zoomMin, zoomMax);
            ApplyTransform();
        }
    }

    // ── pan API ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Smoothly moves the orbit target to <paramref name="destination"/> over
    /// <paramref name="duration"/> seconds, shaped by <paramref name="curve"/>.
    /// Any in-progress pan is cancelled and replaced.
    /// </summary>
    public void PanTargetTo(Vector3 destination, float duration, AnimationCurve curve)
    {
        if (_panCoroutine != null) StopCoroutine(_panCoroutine);
        _panCoroutine = StartCoroutine(PanRoutine(destination, duration, curve));
    }

    IEnumerator PanRoutine(Vector3 destination, float duration, AnimationCurve curve)
    {
        Vector3 start   = target;
        float   elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t  = curve.Evaluate(Mathf.Clamp01(elapsed / duration));
            target   = Vector3.Lerp(start, destination, t);
            ApplyTransform();
            yield return null;
        }

        target        = destination;
        _panCoroutine = null;
        ApplyTransform();
    }

    // ── position math ─────────────────────────────────────────────────────────

    void ApplyTransform()
    {
        float yRad    = _angle * Mathf.Deg2Rad;
        float elevRad = elevationDeg * Mathf.Deg2Rad;

        float hDist = distance * Mathf.Cos(elevRad);
        float vDist = distance * Mathf.Sin(elevRad);

        Vector3 offset = new Vector3(
            hDist * Mathf.Sin(yRad),
            vDist,
            hDist * Mathf.Cos(yRad)
        );

        transform.position = target + offset;
        transform.LookAt(target, Vector3.up);
    }
}
