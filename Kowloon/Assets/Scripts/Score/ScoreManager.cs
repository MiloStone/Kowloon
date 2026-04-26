using System;
using UnityEngine;

/// <summary>
/// Owns the run's score. +10 plus +5 per extra room in the placed tile's suite
/// on placement, +50 per completed floor. Reset by reloading the scene.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    [Header("Tunables")]
    public int tilePlacedBase     = 10;
    public int tilePlacedPerExtra = 5;
    public int floorCompleteBonus = 50;

    public int Score { get; private set; }

    public event Action ScoreChanged;

    public void AddTilePlaced(int suiteSize)
    {
        Score += tilePlacedBase + tilePlacedPerExtra * Mathf.Max(0, suiteSize - 1);
        ScoreChanged?.Invoke();
    }

    public void AddFloorComplete()
    {
        Score += floorCompleteBonus;
        ScoreChanged?.Invoke();
    }
}
