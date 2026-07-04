using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// HUD "retry" button — reloads the current level. Drives the restart from code (added as a new
/// component, like GoToMenuButton) so it works regardless of any per-scene onClick overrides.
/// </summary>
[RequireComponent(typeof(Button))]
public class RestartButton : MonoBehaviour
{
    void Start()
    {
        GetComponent<Button>().onClick.AddListener(Restart);
    }

    void Restart()
    {
        Time.timeScale = 1f; // in case we were paused
        PauseMenu.isGamePaused = false;
        int idx = SceneManager.GetActiveScene().buildIndex;
        var loader = FindObjectOfType<LevelLoader>();
        if (loader != null)
            loader.StartLevel(idx); // same fade transition as level changes
        else
            SceneManager.LoadScene(idx);
    }
}
