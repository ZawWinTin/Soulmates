using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelCompletePopup : MonoBehaviour
{
    public void ReplayLevel()
    {
        FindObjectOfType<LevelLoader>().StartLevel(SceneManager.GetActiveScene().buildIndex);
    }

    public void NextLevel()
    {
        FindObjectOfType<LevelLoader>().StartLevel(SceneManager.GetActiveScene().buildIndex+1);
    }
}
