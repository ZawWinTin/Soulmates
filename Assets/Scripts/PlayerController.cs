using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PlayerController : MonoBehaviour
{
    [HideInInspector]
    public bool isPlayerWinning;

    private Transform winTile;
    private Tilemap groundTilemap;

    private bool isMoving, isFalling;
    private float timeToMove = 0.35f;
    private Vector3 originalPosition, targetPosition;
    private Vector3Int winTileInCellPosition;
    private float zAngle;

    private PlayersMovement controls;

    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rigidBody2D;
    private Animator animator;

    private void Awake()
    {
        controls = new PlayersMovement();   //Get Unity New Input System
        spriteRenderer = GetComponent<SpriteRenderer>();
        rigidBody2D = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
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
        if (!groundTilemap.HasTile(groundTilemap.WorldToCell(transform.position)))
        {
            FallPlayer();            
        }
            
    }

    private void CharacterMove(Vector2 direction)
    {        
        Vector2 movePosition;
        if (!isMoving && rigidBody2D.gravityScale == 0 && Time.timeScale==1)
        {
            //Specify Position to Move
            switch (direction)
            {
                case Vector2 v when v.Equals(Vector2.up):
                    spriteRenderer.flipX = true;                   
                    movePosition = new Vector2(-0.5f, 0.25f);
                    break;
                case Vector2 v when v.Equals(Vector2.down):
                    spriteRenderer.flipX = false;                    
                    movePosition = new Vector2(0.5f, -0.25f);
                    break;
                case Vector2 v when v.Equals(Vector2.left):
                    spriteRenderer.flipX = true;                    
                    movePosition = new Vector2(-0.5f, -0.25f);
                    break;
                case Vector2 v when v.Equals(Vector2.right):
                    spriteRenderer.flipX = false;
                    movePosition = new Vector2(0.5f, 0.25f);                    
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

        FindObjectOfType<AudioManager>().Play("PlayerJump");

        float elapsedTime = 0;

        originalPosition = transform.position;
        targetPosition = originalPosition + (Vector3)direction;

        while (elapsedTime < timeToMove)
        {
            transform.position = Vector3.Lerp(originalPosition, targetPosition, (elapsedTime / timeToMove));
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        transform.position = targetPosition;    //Make sure to move character to target position

        groundTilemap.SetTile(groundTilemap.WorldToCell(originalPosition), null);   //remove tile of character last position

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
        transform.Rotate(0, 0, zAngle, Space.Self); //Rotate gradually while Falling
        if (!isFalling)
        {
            isFalling = true;
            rigidBody2D.gravityScale = 1;
            spriteRenderer.sortingOrder = -1;            
            FindObjectOfType<AudioManager>().Play("PlayerFall");
        }        
    }
}
