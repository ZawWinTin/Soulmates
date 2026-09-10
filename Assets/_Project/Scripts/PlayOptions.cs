using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayOptions : MonoBehaviour
{
    private SavedData data;
    private int playableLevel;

    public GameObject mainMenu,
        playMenu;

    void Awake()
    {
        LoadLevel();
    }

    // Re-read progress and re-evaluate the level buttons. Called by GameBridge
    // after a cloud save is merged in — the menu is built at Awake (before the
    // cloud save arrives), so without this the restored progress never shows.
    public void RefreshLevels()
    {
        LoadLevel();
    }

    private void LoadLevel()
    {
        data = SaveSystem.LoadData();

        if (data == null)
        {
            playableLevel = 1; //Initial Playable Level for New Player
            SaveSystem.SaveData(playableLevel);
        }
        else
        {
            playableLevel = data.level;
        }
        Debug.Log(playableLevel);

        //Make Buttons to Enable and Disable
        playMenu.SetActive(true); // GameObject cannot access Tag which is not active !
        GameObject[] levelButtons = GameObject.FindGameObjectsWithTag("LevelButton");
        for (int i = 0; i < levelButtons.Length; i++)
        {
            // Unlock by the level's OWN index, never its position in the array:
            // FindGameObjectsWithTag order is unspecified, so keying off `i` could
            // unlock the wrong buttons. LevelStars.buildIndex is the level number
            // (Level01 = 1 … Level09 = 9).
            LevelStars levelStars = levelButtons[i].GetComponentInChildren<LevelStars>(true);
            int levelIndex = levelStars != null ? levelStars.buildIndex : i + 1;
            if (levelStars == null)
                Debug.LogWarning("PlayOptions: LevelButton '" + levelButtons[i].name
                    + "' has no LevelStars; falling back to array order.");

            levelButtons[i].GetComponent<Button>().interactable = levelIndex <= playableLevel;

            // Reflect the new lock state in the star band right away — it reads
            // button.interactable, and the buttons may already be on screen (e.g.
            // a cloud merge calling RefreshLevels while the level menu is open).
            if (levelStars != null)
                levelStars.Refresh();
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
