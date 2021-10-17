using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelLoader : MonoBehaviour
{
    public Animator transition;

    private float delay = 0.5f;
    private float transitionTime = 1f;

    public void StartLevel(int levelIndex)
    {
        StartCoroutine(LoadLevel(levelIndex));
    }

    IEnumerator LoadLevel(int levelIndex)
    {
        //Wait Before Play Animation
        yield return new WaitForSeconds(delay);

        //Play Animation
        transition.SetTrigger("Start");

        //Wait Animation
        yield return new WaitForSeconds(transitionTime);

        //Load Scene
        SceneManager.LoadScene(levelIndex);
    }
}
