using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
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
    private Vector3 originalPosition,
        targetPosition;
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

    [Header("Character facing (new slimes)")]
    public Sprite frontSprite; // facing the camera — used moving toward (S / A)
    public Sprite backSprite; // facing away — used moving away (W / D)
    public Sprite blinkSprite; // front view with closed eyes — for the idle blink
    private Vector3 baseScale = Vector3.one;
    private float idlePhase;
    private float blinkTimer;
    private float nextBlink = 3f;
    private float funTimer;
    private float nextFun = 10f;
    private bool isFlipping;
    private bool isCelebrating; // happy victory hops while sitting on the goal tile

    // Current hop/back-flip height above the ground (world units). Read by CharacterShadow so the
    // shadow stays planted on the floor while the slime is airborne. 0 when grounded.
    [HideInInspector]
    public float currentLift;

    private float dissolveDuration = 1f;

    private void Awake()
    {
        controls = new PlayersMovement(); //Get Unity New Input System
        spriteRenderer = GetComponent<SpriteRenderer>();
        rigidBody2D = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        isMoving = false;
        isFalling = false;
        baseScale = transform.localScale;
        idlePhase = Random.Range(0f, 6.2831853f); // desync each slime's breathing
        nextFun = Random.Range(8f, 16f);

        // New slimes are single sprites driven by code (squash/stretch + front/back),
        // so disable the old frame-based blob Animator if a front sprite is assigned.
        if (frontSprite != null)
        {
            if (animator != null)
                animator.enabled = false;
            spriteRenderer.sprite = frontSprite;
        }

        if (name == "Player1") //WASD for Player1 and Arrow keys for Player2 & Find Win Tile of Theirs
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
        rigidBody2D.gravityScale = 0; //Make Character Not Falling
        isPlayerWinning = false;

        zAngle = (Random.Range(0, 2) == 0) ? 1.0f : -1.0f; //Randomly Decide Player Rotate Clockwise or AntiClockwise when Fall

        winTileInCellPosition = groundTilemap.WorldToCell(winTile.position); //Get position of Win_Tile in ground tile map
    }

    void FixedUpdate()
    {
        //If Player's Current Tile has nothing (skip mid-hop AND mid-backflip so the arc that lifts
        //the slime off its cell doesn't read the empty cell above as a fall)
        if (
            !isMoving
            && !isFlipping
            && !isCelebrating // victory hops lift off the cell; don't read that as a fall
            && !groundTilemap.HasTile(groundTilemap.WorldToCell(transform.position))
            && isPlayerReal()
        )
        {
            FallPlayer();
        }
    }

    void Update()
    {
        // Idle animations for the new slimes (skip while hopping, flipping, falling, or while this
        // slime is hidden inside the stacked couple — its SpriteRenderer is disabled when together,
        // so no idle/backflip plays during the stacked state).
        if (
            frontSprite == null
            || isMoving
            || isFlipping
            || isCelebrating
            || rigidBody2D.gravityScale != 0
            || !spriteRenderer.enabled
        )
            return;

        // Gentle breathing
        float breathe = Mathf.Sin(Time.time * 2.6f + idlePhase) * 0.08f;
        transform.localScale = new Vector3(
            baseScale.x * (1f - breathe),
            baseScale.y * (1f + breathe),
            baseScale.z
        );

        // Occasional eye blink (only while the front face is showing)
        if (blinkSprite != null && spriteRenderer.sprite == frontSprite)
        {
            blinkTimer += Time.deltaTime;
            if (blinkTimer >= nextBlink)
            {
                blinkTimer = 0f;
                nextBlink = Random.Range(2.5f, 5f);
                StartCoroutine(Blink());
            }
        }
        // Every so often, a fun little backflip — only in solo play. It can't fire while stacked
        // because the early-return above bails when this slime is hidden inside the couple.
        funTimer += Time.deltaTime;
        if (funTimer >= nextFun)
        {
            funTimer = 0f;
            nextFun = Random.Range(8f, 16f);
            StartCoroutine(BackFlip());
        }
    }

    private IEnumerator Blink()
    {
        spriteRenderer.sprite = blinkSprite;
        yield return new WaitForSeconds(0.12f);
        if (!isMoving && spriteRenderer.sprite == blinkSprite)
            spriteRenderer.sprite = frontSprite;
    }

    private IEnumerator BackFlip()
    {
        isFlipping = true;
        if (frontSprite != null)
            spriteRenderer.sprite = frontSprite; // flip facing the camera
        float dur = 0.6f,
            e = 0f;
        Vector3 startPos = transform.position;
        while (e < dur)
        {
            float t = e / dur;
            float arc = Mathf.Sin(t * Mathf.PI);
            currentLift = arc * 0.55f; // shadow stays grounded while the slime is up
            transform.position = startPos + Vector3.up * currentLift;
            transform.localScale = new Vector3(
                baseScale.x * (1f - 0.1f * arc),
                baseScale.y * (1f + 0.15f * arc),
                baseScale.z
            );
            transform.localRotation = Quaternion.Euler(0f, 0f, -360f * t); // backflip spin
            e += Time.deltaTime;
            yield return null;
        }
        transform.position = startPos;
        transform.localScale = baseScale;
        transform.localRotation = Quaternion.identity;
        currentLift = 0f;
        isFlipping = false;
    }

    private void CharacterMove(Vector2 direction)
    {
        Vector2 movePosition;
        if (!isMoving && !isFlipping && rigidBody2D.gravityScale == 0 && Time.timeScale == 1)
        {
            //Specify Position to Move
            switch (direction)
            {
                case Vector2 v when v.Equals(Vector2.up):
                    SetFacing(backSprite, false); // moving away → back
                    movePosition = gridMoveUp;
                    break;
                case Vector2 v when v.Equals(Vector2.down):
                    SetFacing(frontSprite, true); // toward, down-right → face right
                    movePosition = gridMoveDown;
                    break;
                case Vector2 v when v.Equals(Vector2.left):
                    SetFacing(frontSprite, false); // toward, down-left → face left
                    movePosition = gridMoveLeft;
                    break;
                case Vector2 v when v.Equals(Vector2.right):
                    SetFacing(backSprite, true); // away, up-right → back facing right (flipped, like down)
                    movePosition = gridMoveRight;
                    break;
                default:
                    // Non-cardinal / zero input — e.g. a diagonal from a touch
                    // joystick or two arrow keys pressed together. A zero-distance
                    // move still runs GridMovement, which destroys the tile under
                    // the (stationary) player. Reject it: no move, no destruction.
                    return;
            }
            // Accepted a real cardinal move — now it counts as activity: restart the
            // idle countdown (backflip only plays after a quiet spell) and record the
            // direction the clones compare for climb-down offsets.
            funTimer = 0f;
            playerNextDirection = direction;
            StartCoroutine(GridMovement(movePosition)); //Move as GridBased Movement with smoothness
            Invoke("CheckWinning", timeToMove); //Wait Movement and Check Current Player's Position for Winning or not
        }
    }

    // Flip left/right, and swap front/back sprite (no-op for the old blob with no sprites set).
    private void SetFacing(Sprite sprite, bool flip)
    {
        spriteRenderer.flipX = flip;
        if (sprite != null)
            spriteRenderer.sprite = sprite;
    }

    private IEnumerator GridMovement(Vector2 direction)
    {
        isMoving = true; //Prevent Other Inputs while Moving
        if (animator != null && animator.enabled)
            animator.SetBool("isJumping", true); //Start Jump Animation (old blob)

        // Per-character hop sound: Player1 is the boy (blue), Player2 the girl (pink).
        if (isPlayerReal())
            AudioManager.instance?.Play(name == "Player1" ? "JumpBoy" : "JumpGirl");

        float elapsedTime = 0;

        originalPosition = transform.position;
        targetPosition = originalPosition + (Vector3)direction;

        //Check Climb Down or Not in Stack Condition
        if (
            gameObject.tag == "Player1Clone"
            && playerNextDirection
                != GameObject
                    .FindGameObjectWithTag("Player2Clone")
                    .GetComponent<PlayerController>()
                    .playerNextDirection
        )
        {
            targetPosition += player1CloneDeclinePosition;
        }
        if (
            gameObject.tag == "Player2Clone"
            && playerNextDirection
                != GameObject
                    .FindGameObjectWithTag("Player1Clone")
                    .GetComponent<PlayerController>()
                    .playerNextDirection
        )
        {
            targetPosition += player2CloneDeclinePosition;
        }

        while (elapsedTime < timeToMove)
        {
            float t = elapsedTime / timeToMove;
            Vector3 pos = Vector3.Lerp(originalPosition, targetPosition, t);
            if (frontSprite != null) // bouncy jump for the new slimes: stretch + vertical arc
            {
                float arc = Mathf.Sin(t * Mathf.PI); // 0 → 1 → 0
                transform.localScale = new Vector3(
                    baseScale.x * (1f - 0.2f * arc),
                    baseScale.y * (1f + 0.3f * arc),
                    baseScale.z
                );
                currentLift = arc * 0.4f; // shadow stays grounded while the slime is up
                pos += Vector3.up * currentLift; // higher, bouncier jump when switching tiles
            }
            transform.position = pos;
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        transform.position = targetPosition; //Make sure to move character to target position
        currentLift = 0f;
        if (frontSprite != null)
            transform.localScale = baseScale;

        //Check Players are entering wrong color wintile or not
        if (isPlayerReal())
        {
            Vector3Int playerLastPosition = groundTilemap.WorldToCell(originalPosition);
            GameObject winTile1 = GameObject.FindGameObjectWithTag("WinTile1");
            GameObject winTile2 = GameObject.FindGameObjectWithTag("WinTile2");
            if (playerLastPosition == groundTilemap.WorldToCell(winTile1.transform.position))
            {
                StartCoroutine(DissolveWinTile(winTile1));
                StopWinTileParticles(winTile1); // safe: no crash if the child layout differs
                AudioManager.instance?.Play("WrongTile"); // wrong-colour tile crumbles → mistake cue
                FindObjectOfType<GameController>().GameOver();
            }
            if (playerLastPosition == groundTilemap.WorldToCell(winTile2.transform.position))
            {
                StartCoroutine(DissolveWinTile(winTile2));
                StopWinTileParticles(winTile2);
                AudioManager.instance?.Play("WrongTile"); // wrong-colour tile crumbles → mistake cue
                FindObjectOfType<GameController>().GameOver();
            }
            StartCoroutine(
                DestroyTile(
                    groundTilemap.GetTile(groundTilemap.WorldToCell(originalPosition)),
                    playerLastPosition
                )
            );
        }

        if (animator != null && animator.enabled)
            animator.SetBool("isJumping", false); //Stop Jump Animation (old blob)
        isMoving = false; //Accept other Input
    }

    // Stop a win tile's glow particles without assuming a fixed child index (the old code used
    // GetChild(1).GetComponent<ParticleSystem>(), which crashed if the hierarchy changed).
    private void StopWinTileParticles(GameObject winTile)
    {
        if (winTile == null)
            return;
        var ps = winTile.GetComponentInChildren<ParticleSystem>();
        if (ps != null)
            ps.Stop();
    }

    private IEnumerator DissolveWinTile(GameObject winTile)
    {
        Material winTileMaterial = winTile.GetComponent<SpriteRenderer>().material;
        winTileMaterial.renderQueue = 3000;

        GameObject light = winTile.transform.GetChild(0).gameObject;
        float initialLightIntensity = light.GetComponent<Light2D>().intensity;

        winTile.AddComponent<Rigidbody2D>();
        winTile.GetComponent<Rigidbody2D>().gravityScale = -0.5f;
        // Dissolve effect
        float fade = 1f;
        while (fade > 0f)
        {
            fade -= Time.deltaTime / dissolveDuration;
            fade = Mathf.Clamp01(fade);

            winTileMaterial.SetFloat("_Fade", fade);
            light.GetComponent<Light2D>().intensity = Mathf.Lerp(
                initialLightIntensity,
                0f,
                1 - fade
            ); //Light Dissolve Effect
            yield return null;
        }
        winTile.GetComponent<SpriteRenderer>().enabled = false;

        light.SetActive(false); //Turn Light Off
    }

    private IEnumerator DestroyTile(TileBase tileBase, Vector3Int cellPosition)
    {
        Material tileMaterial = groundTilemap.GetComponent<TilemapRenderer>().material;
        if (tileBase == null)
            yield break;

        // Create a temporary tile renderer for this specific tile
        GameObject tempTileObject = new GameObject("DissolvingTile");
        SpriteRenderer spriteRenderer = tempTileObject.AddComponent<SpriteRenderer>();

        // Set the sprite to match the tile
        Sprite tileSprite = groundTilemap.GetSprite(cellPosition);
        spriteRenderer.sprite = tileSprite;

        // Position the temporary object exactly where the tile is
        tempTileObject.transform.SetParent(groundTilemap.transform.parent, false);

        tempTileObject.transform.position = groundTilemap.GetCellCenterWorld(cellPosition);

        // Preserve the tile's random horizontal flip so the falling tile matches the one that was there
        // (the per-cell transform matrix carries the mirror set by "Flip Random Ground Tiles").
        Matrix4x4 cellMatrix = groundTilemap.GetTransformMatrix(cellPosition);
        tempTileObject.transform.localScale = new Vector3(cellMatrix.m00, cellMatrix.m11, 1f);

        TilemapRenderer tilemapRenderer = groundTilemap.GetComponent<TilemapRenderer>();
        // Keep the falling tile at the GROUND's sorting order so it depth-sorts against the other tiles
        // via the project's iso transparency-sort axis: tiles BEHIND it stay behind, tiles in FRONT stay
        // in front, and as it drops/forward it naturally passes the lower tiles. (No fixed layer, no
        // 0.3s "pop" to -1 — that pop was the old bug.)
        spriteRenderer.sortingOrder = tilemapRenderer.sortingOrder;
        // Create a material instance for dissolving
        Material dissolveMaterial = new Material(tileMaterial);
        spriteRenderer.material = dissolveMaterial;
        tempTileObject.AddComponent<Rigidbody2D>();
        tempTileObject.GetComponent<Rigidbody2D>().gravityScale = 0.8f;

        // Dissolve effect
        float fade = 1f;

        // Depth-freeze: hold the falling tile at the DEPTH of the cell it left, on the project's iso
        // transparency-sort axis (0,1,-0.26). As it visually falls in Y, we compensate Z so its sort
        // value stays = the cell's — so tiles in FRONT of its column keep occluding it (it sinks behind
        // them) instead of drifting in front. Clamp Z so it can't fly past the camera as it fades out.
        Vector3 startP = tempTileObject.transform.position;
        float sortMetric = startP.y - 0.26f * startP.z;

        // Remove the tile from the tilemap
        groundTilemap.SetTile(cellPosition, null);
        while (fade > 0f)
        {
            fade -= Time.deltaTime / dissolveDuration;
            fade = Mathf.Clamp01(fade);

            dissolveMaterial.SetFloat("_Fade", fade);

            Vector3 p = tempTileObject.transform.position;
            p.z = Mathf.Clamp((p.y - sortMetric) / 0.26f, startP.z - 6f, startP.z); // hold cell depth
            tempTileObject.transform.position = p;
            yield return null;
        }

        // Destroy the temporary tile object
        Destroy(tempTileObject);
    }

    private void CheckWinning()
    {
        Vector3Int playerCurrentTileinCellPosition = groundTilemap.WorldToCell(transform.position);
        if (winTileInCellPosition == playerCurrentTileinCellPosition)
        {
            isPlayerWinning = true;
            // Per-character "reached my tile" chime (boy = Player1, girl = Player2).
            if (isPlayerReal())
                AudioManager.instance?.Play(name == "Player1" ? "WinBoy" : "WinGirl");
            // On the win tile the slime's own glow stacks with the tile's glow (same colour) and gets
            // too bright — switch off the slime's light so only the win-tile glow shows.
            foreach (var l in GetComponentsInChildren<Light2D>(true))
                l.enabled = false;
            OnDisable(); // Disable Control of Player
            if (frontSprite != null)
                StartCoroutine(CelebrateWin()); // joyful hops while waiting for the partner
        }
    }

    // Happy victory hops once this slime reaches its own goal tile — repeats until the level ends
    // (the object is destroyed on scene change). Squash-stretch bounce with a little wiggle.
    private IEnumerator CelebrateWin()
    {
        isCelebrating = true;
        if (frontSprite != null)
            spriteRenderer.sprite = frontSprite; // face the camera, smiling
        Vector3 home = transform.position;
        int hops = 0;
        while (isPlayerWinning && spriteRenderer != null && spriteRenderer.enabled)
        {
            float dur = 0.42f,
                e = 0f;
            while (e < dur)
            {
                float t = e / dur;
                float arc = Mathf.Sin(t * Mathf.PI); // 0 → 1 → 0
                currentLift = arc * 0.28f; // shadow stays grounded (CharacterShadow reads this)
                transform.position = home + Vector3.up * currentLift;
                transform.localScale = new Vector3(
                    baseScale.x * (1f - 0.14f * arc),
                    baseScale.y * (1f + 0.2f * arc),
                    baseScale.z
                );
                transform.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    Mathf.Sin(t * Mathf.PI * 2f) * 10f
                ); // wiggle
                e += Time.deltaTime;
                yield return null;
            }
            transform.position = home;
            transform.localScale = baseScale;
            transform.localRotation = Quaternion.identity;
            currentLift = 0f;

            // happy blink between hops (the idle blink is gated off while celebrating, so do it here)
            hops++;
            if (blinkSprite != null && hops % 2 == 0)
            {
                spriteRenderer.sprite = blinkSprite;
                yield return new WaitForSeconds(0.12f);
                if (spriteRenderer != null && spriteRenderer.sprite == blinkSprite)
                    spriteRenderer.sprite = frontSprite;
            }

            float wait = 0.18f,
                w = 0f;
            while (w < wait && isPlayerWinning)
            {
                w += Time.deltaTime;
                yield return null;
            }
        }
        currentLift = 0f;
        isCelebrating = false;
    }

    private void FallPlayer()
    {
        transform.Rotate(0, 0, zAngle, Space.World); //Rotate gradually while Falling
        if (!isFalling)
        {
            isFalling = true;
            OnDisable();
            rigidBody2D.gravityScale = 0.6f;
            // Per-character fall sound: Player1 is the boy (shared "Fall"), Player2 the girl.
            if (isPlayerReal())
                AudioManager.instance?.Play(name == "Player1" ? "Fall" : "FallGirl");
            StartCoroutine(UpdateDroppingSortingOrder(spriteRenderer));
        }
    }

    private IEnumerator UpdateDroppingSortingOrder(SpriteRenderer spriteRenderer)
    {
        float delay = 300f / 1000f;
        yield return new WaitForSeconds(delay);
        spriteRenderer.sortingOrder = -1;
    }

    private bool isPlayerReal()
    {
        return (gameObject.tag == "Player1" || gameObject.tag == "Player2");
    }
}
