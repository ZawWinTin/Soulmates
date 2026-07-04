using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public class GameController : MonoBehaviour
{
    private Rigidbody2D player1,
        player2;
    public GameObject completeLevelUI;

    private GameObject setOfPlayer1Player2;
    private GameObject player1Clone,
        player2Clone,
        heart;
    private Tilemap groundTilemap;

    // The single hugging-couple sprite shown when both slimes meet on one tile (replaces the two
    // stacked clones). Front/back chosen from the slimes' facing. Wired by the editor tool.
    public Sprite hugFront,
        hugBack;
    private GameObject hug;
    private HugLife hugLife;
    private Vector3 playerBaseScale = Vector3.one; // a slime's resting scale, for matching its squash
    private Vector3 hugInitialPosition = new Vector3(0f, 0.2f, 0f);

    // Brief window for the partner to join a same-direction move so the couple hops together
    // (the two control schemes rarely fire on the exact same frame).
    private float coupleGrace = 0f;
    private const float coupleGraceWindow = 0.1f;

    // Decided in Update, applied in LateUpdate so the hug follows the slime's CURRENT (post-move-
    // coroutine) position — without this one-frame lag the couple jump looked off vs a solo slime.
    private Rigidbody2D coupleHopMover;
    private Vector2 coupleHopDir;

    private Vector3 player1CloneInitialPosition = new Vector3(0f, 0.5f, 0f);
    private Vector3 player2CloneInitialPosition = new Vector3(0f, 0.3f, 0f);
    private Vector3 HeartAnimationInitialPosition = new Vector3(0f, 0.15f, 0f); // over the slimes/hug, to mask the swap

    private bool isGameOver = false;

    private float heartAnimationDelay = 2.2f; // long enough for the love-smoke puff to rise + fade

    [HideInInspector]
    public bool isLevelCompleted = false;

    [Header("Star rating")]
    [Tooltip("Finish at or under this many seconds to earn the TIME star.")]
    public float timeParSeconds = 45f;

    [HideInInspector]
    public bool starCollected; // set true by a StarPickup when a slime grabs the map star

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
        var hugT = setOfPlayer1Player2.transform.Find("Hug");
        hug = hugT != null ? hugT.gameObject : null; // null until the editor tool adds it
        hugLife = hug != null ? hug.GetComponent<HugLife>() : null;
        playerBaseScale = player1.transform.localScale; // slimes rest at this scale (~0.1)
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
        if (
            player1.GetComponent<PlayerController>().isPlayerWinning
            && player2.GetComponent<PlayerController>().isPlayerWinning
        )
        {
            CompleteLevel();
        }
    }

    // Apply the couple hop AFTER the slimes' move coroutines have updated their transforms this
    // frame, so the hug tracks them exactly (no one-frame lag → smooth like a solo jump).
    void LateUpdate()
    {
        if (coupleHopMover != null && setOfPlayer1Player2.activeInHierarchy)
            RideCoupleHop(coupleHopMover, coupleHopDir);
    }

    void CompleteLevel()
    {
        if (!isLevelCompleted)
        {
            isLevelCompleted = true;

            // Star rating: 1 for completing, +1 for time, +1 for grabbing the map star. Set it on the
            // win screen BEFORE activating it (WinStars reveals `earned` stars in OnEnable).
            int earned = 1;
            if (Time.timeSinceLevelLoad <= timeParSeconds)
                earned++;
            if (starCollected)
                earned++;
            var stars = completeLevelUI.GetComponentInChildren<WinStars>(true);
            if (stars != null)
                stars.earned = earned;

            completeLevelUI.SetActive(true);
            AudioManager.instance?.Play("LevelComplete"); // both soulmates home → celebration

            SavedData data = SaveSystem.LoadData();
            int savedLevel = data != null ? data.level : 0;
            int nextLevel = SceneManager.GetActiveScene().buildIndex + 1;

            if (savedLevel < nextLevel) //Save level when playableLevel become greater
            {
                SaveSystem.SaveData(nextLevel);
            }
        }
    }

    private void StackPlayer1AndPlayer2()
    {
        setOfPlayer1Player2.transform.position = groundTilemap.CellToWorld(
            groundTilemap.WorldToCell(player1.transform.position)
        );
        heart.transform.position =
            setOfPlayer1Player2.transform.position + HeartAnimationInitialPosition;

        if (!setOfPlayer1Player2.activeInHierarchy)
        {
            //Make invisible to original and visible to the hug couple
            setOfPlayer1Player2.SetActive(true);
            StartCoroutine(HeartAnimationAppear());

            if (hug != null)
            {
                // single hugging-couple sprite replaces the two old clones
                player1Clone.SetActive(false);
                player2Clone.SetActive(false);

                // back view only when BOTH slimes are facing away, else the cute front hug
                var p1c = player1.GetComponent<PlayerController>();
                var p2c = player2.GetComponent<PlayerController>();
                bool bothFacingBack =
                    player1.GetComponent<SpriteRenderer>().sprite == p1c.backSprite
                    && player2.GetComponent<SpriteRenderer>().sprite == p2c.backSprite;
                hug.GetComponent<SpriteRenderer>().sprite =
                    bothFacingBack && hugBack != null ? hugBack : hugFront;

                // set the local offset ONCE, then activate so HugLife (bob + pop) owns the motion
                hug.transform.localPosition = hugInitialPosition;
                hug.SetActive(true);
            }
            else
            {
                // fallback to the old two-clone stack if the hug hasn't been wired yet
                player1Clone.transform.position =
                    setOfPlayer1Player2.transform.position + player1CloneInitialPosition;
                player2Clone.transform.position =
                    setOfPlayer1Player2.transform.position + player2CloneInitialPosition;
                player1Clone.GetComponent<SpriteRenderer>().flipX = player1
                    .GetComponent<SpriteRenderer>()
                    .flipX;
                player2Clone.GetComponent<SpriteRenderer>().flipX = player2
                    .GetComponent<SpriteRenderer>()
                    .flipX;
            }

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
        var p1c = player1.GetComponent<PlayerController>();
        var p2c = player2.GetComponent<PlayerController>();
        bool p1Moving = p1c.isMoving;
        bool p2Moving = p2c.isMoving;
        coupleHopMover = null; // cleared unless a couple-hop is decided below (applied in LateUpdate)

        if (!p1Moving && !p2Moving)
        {
            coupleGrace = 0f;
            if (hugLife != null)
                hugLife.moving = false; // at rest → let the idle breathing resume
            // both settled — stack if they share a tile, else split
            if (
                groundTilemap.WorldToCell(player1.transform.position)
                == groundTilemap.WorldToCell(player2.transform.position)
            )
                StackPlayer1AndPlayer2();
            else
                UnStackPlayer1AndPlayer2();
        }
        else if (setOfPlayer1Player2.activeInHierarchy)
        {
            // already a couple while someone is moving
            bool bothMoving = p1Moving && p2Moving;
            bool sameDir = bothMoving && p1c.playerNextDirection == p2c.playerNextDirection;
            bool diffDir = bothMoving && p1c.playerNextDirection != p2c.playerNextDirection;

            if (sameDir)
            {
                // both pressed the same way → the hug JUMPS as one (ridden in LateUpdate, no split)
                coupleGrace = 0f;
                coupleHopMover = player1;
                coupleHopDir = p1c.playerNextDirection;
            }
            else if (diffDir)
            {
                // clearly going different ways → split into the two slimes
                coupleGrace = 0f;
                UnStackPlayer1AndPlayer2();
            }
            else
            {
                // exactly one moving so far — hold the couple briefly in case the partner is just a
                // frame or two behind with the SAME direction; if it never joins, split.
                coupleGrace += Time.deltaTime;
                if (coupleGrace <= coupleGraceWindow)
                {
                    coupleHopMover = p1Moving ? player1 : player2;
                    coupleHopDir = (p1Moving ? p1c : p2c).playerNextDirection;
                }
                else
                {
                    coupleGrace = 0f;
                    UnStackPlayer1AndPlayer2();
                }
            }
        }
    }

    // Make the hug follow the mover's hop AND copy its squash/stretch, so the couple jump is as
    // smooth/juicy as a single slime's. HugLife.moving stops the idle bob fighting it.
    private void RideCoupleHop(Rigidbody2D mover, Vector2 dir)
    {
        setOfPlayer1Player2.transform.position = mover.transform.position;
        if (hug == null)
            return;
        if (hugLife != null)
            hugLife.moving = true;

        Vector3 baseS = hugLife != null ? hugLife.BaseScale : hug.transform.localScale;
        float rx = playerBaseScale.x != 0f ? mover.transform.localScale.x / playerBaseScale.x : 1f;
        float ry = playerBaseScale.y != 0f ? mover.transform.localScale.y / playerBaseScale.y : 1f;
        hug.transform.localPosition = hugInitialPosition; // no idle bob during the hop
        hug.transform.localScale = new Vector3(baseS.x * rx, baseS.y * ry, baseS.z);
        FaceHug(dir);
    }

    // Swap the hug to its back view when the couple hops away (up/right), else the front view.
    private void FaceHug(Vector2 dir)
    {
        if (hug == null)
            return;
        var sr = hug.GetComponent<SpriteRenderer>();
        bool back = dir == Vector2.up || dir == Vector2.right; // moving away → back view
        sr.sprite = back && hugBack != null ? hugBack : hugFront;
        // Flip the right-side iso dirs on BOTH front and back, matching the slimes: front flips on
        // down (SE), back flips on right (NE); the left-side dirs (left=SW, up=NW) stay unflipped.
        sr.flipX = dir == Vector2.down || dir == Vector2.right;
    }

    private IEnumerator HeartAnimationAppear()
    {
        heart.SetActive(true);
        yield return new WaitForSeconds(heartAnimationDelay);
        heart.SetActive(false);
    }
}
