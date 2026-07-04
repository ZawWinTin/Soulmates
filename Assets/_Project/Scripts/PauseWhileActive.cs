using UnityEngine;

/// <summary>
/// Freezes the game (Time.timeScale = 0) while this object is active and restores the
/// previous time scale when it's hidden. Used by the Help screen so the game pauses
/// while you read the instructions. Restoring the *previous* scale means it stays
/// paused if Help was opened from an already-paused state.
/// </summary>
public class PauseWhileActive : MonoBehaviour
{
    private float previousTimeScale = 1f;

    private void OnEnable()
    {
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
    }

    private void OnDisable()
    {
        Time.timeScale = previousTimeScale;
    }
}
