using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public class GameController : MonoBehaviour
{
    private Rigidbody2D player1, player2;
    public GameObject completeLevelUI;

    private GameObject setOfPlayer1Player2;
    private GameObject player1Clone, player2Clone, heart;
    private Tilemap groundTilemap;

    private Vector3 player1CloneInitialPosition = new Vector3(0f, 0.5f, 0f);
    private Vector3 player2CloneInitialPosition = new Vector3(0f, 0.3f, 0f);
    private Vector3 HeartAnimationInitialPosition = new Vector3(0f, 0.4f, 0f);

    private bool isGameOver = false;

    private float heartAnimationDelay = 1f;

    [HideInInspector]
    public bool isLevelCompleted = false;

    void Awake()
    {
        //Get Players Rigidbody2D using Players' Tags
        player1 = GameObject.FindGameObjectWithTag("Player1").GetComponent<Rigidbody2D>();
        player2 = GameObject.FindGameObjectWithTag("Player2").GetComponent<Rigidbody2D>();

        //For Players Stack Condition
        groundTilemap = GameObject.FindGameObjectWithTag("GroundTileMap").GetComponent<Tilemap>();
        setOfPlayer1Player2 = GameObject.FindGameObjectWithTag("Player1+Player2");
        player1Clone = setOfPlayer1Player2.transform.GetChild(0).gameObject;
        player2Clone = setOfPlayer1Player2.transform.GetChild(1).gameObject;
        heart = setOfPlayer1Player2.transform.GetChild(2).gameObject;
        setOfPlayer1Player2.SetActive(false);
    }
    public void GameOver()
    {
        if (!isGameOver)
        {
            isGameOver = true;
            UnStackPlayer1AndPlayer2();
            FindObjectOfType<LevelLoader>().StartLevel(SceneManager.GetActiveScene().buildIndex);
        }
    }

    // Update is called once per frame
    void Update()
    {
        //Game Over
        if (player1.gravityScale > 0 || player2.gravityScale > 0)
        {
            GameOver();
        }
        else
        {
            CheckPlayersAreInSameTile();
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

    private void StackPlayer1AndPlayer2()
    {
        setOfPlayer1Player2.transform.position = groundTilemap.CellToWorld(groundTilemap.WorldToCell(player1.transform.position));
        player1Clone.transform.position = setOfPlayer1Player2.transform.position + player1CloneInitialPosition;
        player2Clone.transform.position = setOfPlayer1Player2.transform.position + player2CloneInitialPosition;
        heart.transform.position = setOfPlayer1Player2.transform.position + HeartAnimationInitialPosition;

        if (!setOfPlayer1Player2.activeInHierarchy)
        {
            //Make invisible to original and visible to clone
            setOfPlayer1Player2.SetActive(true);
            StartCoroutine(HeartAnimationAppear());
            player1Clone.GetComponent<SpriteRenderer>().flipX = player1.GetComponent<SpriteRenderer>().flipX;
            player2Clone.GetComponent<SpriteRenderer>().flipX = player2.GetComponent<SpriteRenderer>().flipX;
            player1.transform.GetChild(0).gameObject.SetActive(false);
            player1.GetComponent<SpriteRenderer>().enabled = false;
            player2.transform.GetChild(0).gameObject.SetActive(false);
            player2.GetComponent<SpriteRenderer>().enabled = false;
        }
    }

    private void UnStackPlayer1AndPlayer2()
    {
        if (setOfPlayer1Player2.activeInHierarchy)
        {
            setOfPlayer1Player2.SetActive(false);

            //Set visible to original players
            player1.transform.GetChild(0).gameObject.SetActive(true);
            player1.GetComponent<SpriteRenderer>().enabled = true;
            player2.transform.GetChild(0).gameObject.SetActive(true);
            player2.GetComponent<SpriteRenderer>().enabled = true;
        }
    }

    private void CheckPlayersAreInSameTile()
    {
        if (!player1.GetComponent<PlayerController>().isMoving && !player2.GetComponent<PlayerController>().isMoving)
        {
            if (groundTilemap.WorldToCell(player1.transform.position) == groundTilemap.WorldToCell(player2.transform.position))
            {
                StackPlayer1AndPlayer2();
            }
            else
            {
                UnStackPlayer1AndPlayer2();
            }
        }
    }

    private IEnumerator HeartAnimationAppear()
    {
        heart.SetActive(true);
        yield return new WaitForSeconds(heartAnimationDelay);
        heart.SetActive(false);
    }
}
