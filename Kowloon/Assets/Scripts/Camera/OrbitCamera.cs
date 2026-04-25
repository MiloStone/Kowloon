using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Orthographic camera that orbits a fixed point on a circular track.
/// Right-click drag rotates around the Y axis; scroll wheel zooms.
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

    [Tooltip("Mouse sensitivity for right-click drag.")]
    public float sensitivity = 0.4f;

    [Header("Orthographic")]
    public float orthoSize = 6f;

    [Header("Zoom")]
    public float zoomSpeed = 1.5f;
    public float zoomMin   = 2f;
    public float zoomMax   = 14f;

    // ── private ───────────────────────────────────────────────────────────────

    private float      _angle;
    private float      _currentSize;
    private Camera     _cam;
    private Coroutine  _panCoroutine;

    // ── lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _cam                  = GetComponent<Camera>();
        _cam.orthographic     = true;
        _currentSize          = orthoSize;
        _cam.orthographicSize = _currentSize;
        _angle                = startAngleDeg;
        ApplyTransform();
    }

    void Update()
    {
        if (Mouse.current.rightButton.isPressed)
        {
            float dx = Mouse.current.delta.x.ReadValue();
            _angle += dx * sensitivity;
            ApplyTransform();
        }

        float scroll = Mouse.current.scroll.y.ReadValue();
        if (scroll != 0f)
        {
            _currentSize = Mathf.Clamp(_currentSize - scroll * zoomSpeed * Time.deltaTime, zoomMin, zoomMax);
            _cam.orthographicSize = _currentSize;
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
