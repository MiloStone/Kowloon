using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Coordinates floor transitions: increments the floor counter, moves the grid
/// up, clears occupied state, and triggers the camera pan.
/// </summary>
public class FloorManager : MonoBehaviour
{
    [Header("References")]
    public GridManager  grid;
    public OrbitCamera  orbitCamera;
    public ScoreManager scoreManager;

    [Header("Transition")]
    [Tooltip("Duration of the camera pan in seconds.")]
    public float         panDuration = 0.6f;
    [Tooltip("Ease curve for the camera pan. Default is smooth ease-in-out.")]
    public AnimationCurve panCurve;

    [Tooltip("Vertical gap inserted between completed floors to avoid clipping.")]
    public float floorGap = 0.01f;

    [Header("Floor-completion ripple")]
    [Tooltip("Per-tile bounce duration during the ripple.")]
    public float rippleBounceDuration = 0.20f;
    [Tooltip("Bounce height as a fraction of placed tile height.")]
    public float rippleBounceHeight = 0.30f;
    [Tooltip("Delay between successive diagonals of the ripple, in seconds.")]
    public float rippleStepDelay = 0.05f;
    [Tooltip("Extra wait after the ripple finishes before moving up.")]
    public float ripplePostDelay = 0.15f;

    public int      CurrentFloor    { get; private set; } = 1;
    public Scenario CurrentScenario { get; private set; }

    private readonly List<PlacedTile>   _currentFloorTiles = new();
    private readonly ConnectivityGraph  _graph             = new();

    public ConnectivityGraph Graph => _graph;

    public bool ConstraintsSatisfied =>
        CurrentScenario != null && CurrentScenario.IsSatisfied(_graph);

    /// <summary>Fires whenever the connectivity graph or scenario changes.</summary>
    public event Action ContractChanged;
    public void RaiseContractChanged() => ContractChanged?.Invoke();

    // ── lifecycle ─────────────────────────────────────────────────────────────

    void Reset()
    {
        // Called when component is first added — provides a sensible default curve
        panCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }

    void Awake()
    {
        if (grid         == null) grid         = FindFirstObjectByType<GridManager>();
        if (orbitCamera  == null) orbitCamera  = FindFirstObjectByType<OrbitCamera>();
        if (scoreManager == null) scoreManager = FindFirstObjectByType<ScoreManager>();

        if (panCurve == null || panCurve.length == 0)
            panCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        RollScenario();
    }

    // ── public API ────────────────────────────────────────────────────────────

    public void RegisterPlacedTile(PlacedTile tile)
    {
        _currentFloorTiles.Add(tile);
        _graph.AddNode(tile);
    }

    public void Connect(PlacedTile a, PlacedTile b) => _graph.Connect(a, b);

    public void CompleteFloor() => StartCoroutine(CompleteFloorRoutine());

    IEnumerator CompleteFloorRoutine()
    {
        // Pick a random corner; ripple travels diagonally toward the opposite.
        // Bucket tiles by Manhattan distance from the start corner so each
        // diagonal "front" bounces together.
        var startCorner = (UnityEngine.Random.value < 0.5f ? 0 : grid.GridSize - 1,
                           UnityEngine.Random.value < 0.5f ? 0 : grid.GridSize - 1);

        float maxDelay = 0f;
        foreach (var tile in _currentFloorTiles)
        {
            int dx    = Mathf.Abs(tile.Anchor.x - startCorner.Item1);
            int dy    = Mathf.Abs(tile.Anchor.y - startCorner.Item2);
            float delay = (dx + dy) * rippleStepDelay;
            maxDelay = Mathf.Max(maxDelay, delay);
            tile.StartCoroutine(tile.AnimateBounce(
                delay, rippleBounceDuration, rippleBounceHeight * grid.PlacedHeight));
        }

        yield return new WaitForSeconds(maxDelay + rippleBounceDuration + ripplePostDelay);

        foreach (var tile in _currentFloorTiles)
        {
            tile.SealAllClosedDoors();
            tile.RevealTop();
        }
        _currentFloorTiles.Clear();
        _graph.Clear();

        scoreManager?.AddFloorComplete();

        CurrentFloor++;
        float newY = (CurrentFloor - 1) * (grid.PlacedHeight + floorGap);

        grid.MoveTo(newY);
        grid.ClearOccupied();
        orbitCamera.PanTargetTo(new Vector3(0f, newY, 0f), panDuration, panCurve);

        RollScenario();
    }

    void RollScenario()
    {
        CurrentScenario = ScenarioPools.RollForFloor(CurrentFloor);
        Debug.Log($"Floor {CurrentFloor} constraint: {CurrentScenario.Describe()}");
        ContractChanged?.Invoke();
    }
}
