using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Press N to reload the active scene — wipes everything (graph, score,
/// floor count, placed tiles) and starts the run over.
/// </summary>
public class GameReset : MonoBehaviour
{
    public Key resetKey = Key.N;

    void Update()
    {
        if (Keyboard.current == null) return;
        if (Keyboard.current[resetKey].wasPressedThisFrame)
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
