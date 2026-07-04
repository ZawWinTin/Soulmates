using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    public static bool isGamePaused = false;
    public GameObject pauseMenuUI;
    public GameObject pauseContent; // buttons panel; force-shown on pause (scenes leave it off)
    public GameObject helpScreen; // the How-to-Play overlay; Esc closes it instead of pausing
    private GameObject gameController;

    void Start()
    {
        gameController = FindObjectOfType<GameController>().gameObject;
        // The overlay starts active in the scene; hide it so it doesn't block the HUD
        // (?, ||) at level start. PauseGame() shows it on demand.
        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(false);
    }

    void Update()
    {
        if (
            Keyboard.current[Key.Escape].wasPressedThisFrame
            && !gameController.GetComponent<GameController>().isLevelCompleted
        ) //New Input System
        {
            // If the How-to-Play overlay is open, Esc just closes it (don't also open Pause — that
            // looked like a duplicate panel). PauseWhileActive on the overlay restores time itself.
            if (helpScreen != null && helpScreen.activeSelf)
            {
                helpScreen.SetActive(false);
            }
            else if (isGamePaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }

    public void ResumeGame()
    {
        pauseMenuUI.SetActive(false);
        Time.timeScale = 1f;
        isGamePaused = false;
    }

    public void PauseGame()
    {
        pauseMenuUI.SetActive(true);
        if (pauseContent != null)
            pauseContent.SetActive(true); // scenes leave this off, so force it on
        Time.timeScale = 0f; //Freeze time
        isGamePaused = true;
    }

    //For not Freezing
    public void ReleaseTimeScale()
    {
        Time.timeScale = 1f;
    }

    public void QuitGame()
    {
        Helper.QuitGame();
    }

    // Pause → main menu. Must unfreeze time first (the level loader's coroutine waits on scaled
    // time, which is 0 while paused) then run the normal scene transition to the Menu scene (index 0).
    public void LoadMenu()
    {
        Time.timeScale = 1f;
        isGamePaused = false;
        var loader = FindObjectOfType<LevelLoader>();
        if (loader != null)
            loader.StartLevel(0); // Menu is build index 0
        else
            SceneManager.LoadScene(0);
    }
}
