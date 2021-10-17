using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayOptions : MonoBehaviour
{
    private SavedData data;
    private int playableLevel;

    public GameObject mainMenu, playMenu;

    void Awake()
    {
        LoadLevel();
    }

    private void LoadLevel()
    {
        data = SaveSystem.LoadData();

        if (data == null)
        {
            playableLevel = 1;  //Intial Playable Level for New Player
            SaveSystem.SaveData(playableLevel);  
        }
        else
        {
            playableLevel = data.level;
        }
        Debug.Log(playableLevel);

        //Make Buttons to Enable and Disable 
        playMenu.SetActive(true);   // GameObject cannot access Tag which is not active !
        GameObject[] levelButtoons = GameObject.FindGameObjectsWithTag("LevelButton");
        for (int i = 0; i < levelButtoons.Length; i++)
        {            
            //Level Start from 1
            if (i + 1 <= playableLevel)
            {
                levelButtoons[i].GetComponent<Button>().interactable = true;
            }
            else
            {
                levelButtoons[i].GetComponent<Button>().interactable = false;
            }
        }
        playMenu.SetActive(false);
    }

    public void PlayButtonClicked()
    {
        if (playableLevel != 1)
        {           
            playMenu.SetActive(true);
            mainMenu.SetActive(false);
        }
        else
        {
            //For New Player
            FindObjectOfType<LevelLoader>().StartLevel(playableLevel);
        }
    }
}
