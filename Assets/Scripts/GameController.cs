using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameController : MonoBehaviour
{
    private Rigidbody2D player1, player2;
    public GameObject completeLevelUI;
    private bool isGameOver=false;

    [HideInInspector]
    public bool isLevelCompleted = false;
    //private float restartDelay = 1.5f;

    void Awake()
    {
        //Get Players Rigidbody2D using Players' Tags
        player1 = GameObject.FindGameObjectWithTag("Player1").GetComponent<Rigidbody2D>();
        player2 = GameObject.FindGameObjectWithTag("Player2").GetComponent<Rigidbody2D>();
    }
    void GameOver()
    {
        if (!isGameOver)
        {
            isGameOver = true;
            FindObjectOfType<LevelLoader>().StartLevel(SceneManager.GetActiveScene().buildIndex);
        }
    }

    // Update is called once per frame
    void Update()
    {
        //Game Over
        if(player1.gravityScale==1 || player2.gravityScale == 1)
        {
            GameOver();
        }

        //Winning State
        if (player1.GetComponent<PlayerController>().isPlayerWinning && player2.GetComponent<PlayerController>().isPlayerWinning)
        {
            CompleteLevel();
        }
    }

    void CompleteLevel()
    {
        if (!isLevelCompleted)
        {
            isLevelCompleted = true;
            completeLevelUI.SetActive(true);
            
            if (SaveSystem.LoadData() == null)
                Debug.LogError("Saved Level is not found !");

            int savedLevel = SaveSystem.LoadData().level;
            int nextLevel = SceneManager.GetActiveScene().buildIndex + 1;

            if (savedLevel < nextLevel) //Save level when playableLevel become greater
            {
                SaveSystem.SaveData(nextLevel);
            }            
        }
    }
}
