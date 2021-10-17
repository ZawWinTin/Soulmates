using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PlayerController : MonoBehaviour
{
    [HideInInspector]
    public bool isPlayerWinning;

    [HideInInspector]
    public bool isMoving;

    [HideInInspector]
    public Vector2 playerNextDirection;

    private Transform winTile;
    private Tilemap groundTilemap;

    private bool isFalling;
    private float timeToMove = 0.35f;
    private Vector3 originalPosition, targetPosition;
    private Vector3Int winTileInCellPosition;
    private float zAngle;

    private Vector3 player1CloneDeclinePosition = new Vector3(0f, -0.125f, 0f);
    private Vector3 player2CloneDeclinePosition = new Vector3(0f, 0.075f, 0f);

    private Vector2 gridMoveUp = new Vector2(-0.5f, 0.25f);
    private Vector2 gridMoveDown = new Vector2(0.5f, -0.25f);
    private Vector2 gridMoveLeft = new Vector2(-0.5f, -0.25f);
    private Vector2 gridMoveRight = new Vector2(0.5f, 0.25f);

    private PlayersMovement controls;

    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rigidBody2D;
    private Animator animator;
    private AudioSource[] audioSources;

    private void Awake()
    {
        controls = new PlayersMovement();   //Get Unity New Input System
        spriteRenderer = GetComponent<SpriteRenderer>();
        rigidBody2D = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        audioSources = GetComponents<AudioSource>();
        isMoving = false;
        isFalling = false;
        
        if (name == "Player1")          //WASD for Player1 and Arrow keys for Player2 & Find Win Tile of Theirs
        {
            controls.Player1.Movement.performed += ctx => CharacterMove(ctx.ReadValue<Vector2>());
            winTile = GameObject.FindGameObjectWithTag("WinTile1").GetComponent<Transform>();
        }
        else
        {
            controls.Player2.Movement.performed += ctx => CharacterMove(ctx.ReadValue<Vector2>());
            winTile = GameObject.FindGameObjectWithTag("WinTile2").GetComponent<Transform>();
        }
        groundTilemap = GameObject.FindGameObjectWithTag("GroundTileMap").GetComponent<Tilemap>();
    }

    private void OnEnable()
    {
        controls.Enable();
    }

   private void OnDisable()
    {
        controls.Disable();
    }

    // Start is called before the first frame update
    void Start()
    {
        rigidBody2D.gravityScale = 0;    //Make Character Not Falling
        isPlayerWinning = false;

        zAngle = (Random.Range(0, 2) == 0) ? 1.0f : -1.0f;  //Randomly Decide Player Rotate Clockwise or AntiClockwise when Fall
             
        winTileInCellPosition = groundTilemap.WorldToCell(winTile.position);    //Get position of Win_Tile in ground tile map
    }

    void Update()
    {
        //If Player's Current Tile has nothing
        if (!groundTilemap.HasTile(groundTilemap.WorldToCell(transform.position)) && isPlayerReal())
        {
            FallPlayer();            
        }
            
    }

    private void CharacterMove(Vector2 direction)
    {        
        Vector2 movePosition;
        if (!isMoving && rigidBody2D.gravityScale == 0 && Time.timeScale==1)
        {
            playerNextDirection = direction;
            //Specify Position to Move
            switch (direction)
            {
                case Vector2 v when v.Equals(Vector2.up):
                    spriteRenderer.flipX = true;                   
                    movePosition = gridMoveUp;
                    break;
                case Vector2 v when v.Equals(Vector2.down):
                    spriteRenderer.flipX = false;                    
                    movePosition = gridMoveDown;
                    break;
                case Vector2 v when v.Equals(Vector2.left):
                    spriteRenderer.flipX = true;                    
                    movePosition = gridMoveLeft;
                    break;
                case Vector2 v when v.Equals(Vector2.right):
                    spriteRenderer.flipX = false;
                    movePosition = gridMoveRight;                    
                    break;
                default:
                    movePosition = Vector2.zero;
                    break;
            }            
            StartCoroutine(GridMovement(movePosition)); //Move as GridBased Movement with smoothness
            Invoke("CheckWinning", timeToMove); //Wait Movement and Check Current Player's Position for Winning or not
        }        
    }

    private IEnumerator GridMovement(Vector2 direction)
    {
        isMoving = true;    //Prevent Other Inputs while Moving
        animator.SetBool("isJumping", true);    //Start Jump Animation
        
        foreach (AudioSource audioSource in audioSources)
        {
            if (audioSource.clip.name == "jump" && isPlayerReal())
            {
                audioSource.Play();
            }
        }

        float elapsedTime = 0;

        originalPosition = transform.position;
        targetPosition = originalPosition + (Vector3)direction;

        //Check Climb Down or Not in Stack Condition
        if (gameObject.tag == "Player1Clone" && playerNextDirection != GameObject.FindGameObjectWithTag("Player2Clone").GetComponent<PlayerController>().playerNextDirection)
        {
            targetPosition += player1CloneDeclinePosition;
        }
        if (gameObject.tag == "Player2Clone" && playerNextDirection != GameObject.FindGameObjectWithTag("Player1Clone").GetComponent<PlayerController>().playerNextDirection)
        {
            targetPosition += player2CloneDeclinePosition;
        }

        while (elapsedTime < timeToMove)
        {
            transform.position = Vector3.Lerp(originalPosition, targetPosition, (elapsedTime / timeToMove));
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        transform.position = targetPosition;    //Make sure to move character to target position

        //Check Players are entering wrong color wintile or not
        if (isPlayerReal())
        {
            Vector3Int playerLastPosition = groundTilemap.WorldToCell(originalPosition);
            GameObject winTile1 = GameObject.FindGameObjectWithTag("WinTile1");
            GameObject winTile2 = GameObject.FindGameObjectWithTag("WinTile2");
            if (playerLastPosition == groundTilemap.WorldToCell(winTile1.transform.position))
            {
                winTile1.GetComponent<SpriteRenderer>().enabled = false;
                winTile1.transform.GetChild(0).gameObject.SetActive(false);             //Turn Light Off
                winTile1.transform.GetChild(1).GetComponent<ParticleSystem>().Stop();   //Stop Particle System
                FindObjectOfType<GameController>().GameOver();
            }
            if (playerLastPosition == groundTilemap.WorldToCell(winTile2.transform.position))
            {
                winTile2.GetComponent<SpriteRenderer>().enabled = false;
                winTile2.transform.GetChild(0).gameObject.SetActive(false);             //Turn Light Off
                winTile2.transform.GetChild(1).GetComponent<ParticleSystem>().Stop();   //Stop Particle System
                FindObjectOfType<GameController>().GameOver();
            }
            groundTilemap.SetTile(groundTilemap.WorldToCell(originalPosition), null);//remove tile of character last position
        }

        animator.SetBool("isJumping", false);    //Stop Jump Animation
        isMoving = false;   //Accept other Input
    }

    private void CheckWinning()
    {
        Vector3Int playerCurrentTileinCellPosition = groundTilemap.WorldToCell(transform.position);
        if (winTileInCellPosition == playerCurrentTileinCellPosition)
        {
            isPlayerWinning = true;
            OnDisable();    // Disable Control of Player
        }            
    }

    private void FallPlayer()
    {
        transform.Rotate(0, 0, zAngle, Space.World); //Rotate gradually while Falling
        if (!isFalling)
        {
            isFalling = true;
            rigidBody2D.gravityScale = 1;
            spriteRenderer.sortingOrder = 0;
            foreach (AudioSource audioSource in audioSources)
            {
                if (audioSource.clip.name == "fall" && isPlayerReal())
                {
                    audioSource.Play();
                }
            }
        }        
    }

    private bool isPlayerReal()
    {
        return (gameObject.tag == "Player1" || gameObject.tag == "Player2");
    }
}
