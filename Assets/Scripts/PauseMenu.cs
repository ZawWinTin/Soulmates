using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PauseMenu : MonoBehaviour
{
    public static bool isGamePaused = false;
    public GameObject pauseMenuUI;
    private GameObject gameController;

    void Start()
    {
        gameController= FindObjectOfType<GameController>().gameObject;
    }
    void Update()
    {
        if (Keyboard.current[Key.Escape].wasPressedThisFrame && !gameController.GetComponent<GameController>().isLevelCompleted) //New Input System
        {            
            if (isGamePaused)
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
    
    void PauseGame()
    {
        pauseMenuUI.SetActive(true);
        Time.timeScale = 0f;    //Freeze time
        isGamePaused = true;
    }

    //For not Freezing
    public void ReleaseTimeScale()
    {
        Time.timeScale = 1f;
    }
    public void QuitGame()
    {
        Debug.Log("Game Quit!");
        Application.Quit();
    }
}
