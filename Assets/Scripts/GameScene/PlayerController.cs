using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public enum AbilityType {
    None,
    Boost,
    Rocket,
    Freeze,
    Bomb,
    Invincible
}

public class PlayerController : MonoBehaviour {

    [Header("Player Movement")]
    public float moveSpeed = 5f;
    public float desiredJumpHeight = 2f;
    public int maxJumps = 2;

    [SerializeField] private Transform _graphicsObject;
    
    [SerializeField] private float decelerationSpeedThreshold = 15f;
    [SerializeField] private float decelerationStrength = 0.5f;
    private float accelerationProgress = 0f;
    public float accelerationTime = 2f;

    private Vector2 moveInput;
    private int remainingJumps;
    private InputSystem_Actions controls;
    private bool isHorizontalBoosting;
    private bool isVerticalBoosting;
    private float lastHorizontalDirection;
    private float _previousFrameFallVelocity;
    private float jumpVelocity;
    private bool isWalking;

    [Header("Player Stats")]
    public bool canDoubleJump = true;
    [SerializeField] private AbilityType _equippedAbility;
    public int health = 1;

    [Header("Bomb")]
    public GameObject bombPrefab;
    public Transform bombSpawnPoint;
    public float explosionForce = 10f;

    [Header("Invincibility")]
    public Color[] invincibilityColors;
    public float colorChangeSpeed = 0.1f;
    private Coroutine invincibilityCoroutine;

    [Header("Data")]
    [SerializeField] private AbilityData _abilityData;
    [SerializeField] private bool isGrounded;
    [SerializeField] private bool isHeadache;
    [SerializeField] private bool isInvincible;
    [SerializeField] private Vector3 _respawnPoint;
    [SerializeField] private float currentXVelocity;
    [SerializeField] private float currentYVelocity;
    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private EnemyController[] enemies;
    private EnemyGoomba[] enemygoombas;
    private MovingPlatform[] platforms;
    private Crusher[] crushers;
    private Collider2D _myCollider;
    private bool isCollidingWithDanger;
    private Dictionary<AbilityType, float> abilityTimers = new Dictionary<AbilityType, float>();

    void Awake() {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponentInChildren<Animator>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        controls = new InputSystem_Actions();

        controls.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Player.Move.canceled += ctx => moveInput = Vector2.zero;
        controls.Player.Jump.performed += ctx => OnJump();
        controls.Player.Interact.performed += ctx => OnAbilityPressed();
        controls.Player.NextAbility.performed += ctx => CycleAbility(true);
        controls.Player.PreviousAbility.performed += ctx => CycleAbility(false);
        controls.Player.ScrollAbility.performed += ctx => CycleAbility(ctx.ReadValue<float>() > 0);

        enemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        enemygoombas = FindObjectsByType<EnemyGoomba>(FindObjectsSortMode.None);
        platforms = FindObjectsByType<MovingPlatform>(FindObjectsSortMode.None);
        crushers = FindObjectsByType<Crusher>(FindObjectsSortMode.None);

        _myCollider = GetComponent<Collider2D>();

        jumpVelocity = Mathf.Sqrt(2 * Mathf.Abs(Physics2D.gravity.y) * desiredJumpHeight);
    }

    public void SetRigidBodyKinematic(bool trueFalse)
    {
        rb.isKinematic = trueFalse;
        _myCollider.enabled = !trueFalse;

        if (trueFalse)
        {
            transform.SetParent(null);
            rb.velocity = Vector3.zero;
        }
    }
    

    void OnEnable() {
        controls.Player.Enable();
    }

    void OnDisable() {
        controls.Player.Disable();
    }

    void Update() {
        HandleInput();

        if (isGrounded) {
            remainingJumps = maxJumps;
        }

        if (transform.position.y < -10f) {
            Die();
        }

        if (!isHorizontalBoosting) {
            UpdateMovement();
        }

        ApplyDeceleration();

        if (!isVerticalBoosting) {
            rb.velocity = new Vector2(rb.velocity.x, rb.velocity.y);
        }

        currentXVelocity = rb.velocity.x;
        currentYVelocity = rb.velocity.y;

        if (currentXVelocity > 30) {
            StreamerCam.NotifyStreamer(StreamerEvent.HighSpeed);
        }

        if (moveInput.x != 0 && isGrounded) {
            lastHorizontalDirection = Mathf.Sign(moveInput.x);
            isWalking = true;
        } else {
            isWalking = false;
        }

        animator.SetBool("isWalking", isWalking);

        if (rb.velocity.y < 0 && !isGrounded) {
            animator.SetBool("isFalling", true);
        } else {
            animator.SetBool("isFalling", false);
        }

        List<AbilityType> keys = new List<AbilityType>(abilityTimers.Keys);
        foreach (AbilityType ability in keys) {
            abilityTimers[ability] -= Time.deltaTime;
            if (abilityTimers[ability] <= 0) {
                abilityTimers.Remove(ability);
            }
        }
    }

    void HandleInput() {
        if (controls.Player.Jump.triggered) {
            OnJump();
        }

        if (controls.Player.Interact.triggered) {
            OnAbilityPressed();
        }
    }

    void OnJump() {
        if (isGrounded || (canDoubleJump && remainingJumps > 0)) {
            rb.velocity = new Vector2(rb.velocity.x, jumpVelocity);
            remainingJumps--;
            isGrounded = false;
            animator.SetTrigger("hasJumped");
        }
    }

    public void TakeDamage(int damage, Vector2 explosionPosition) {
        if (!isInvincible) {
            health -= damage;

            if (health <= 0) {
                Die();
            }
        } else {
            Vector2 explosionDirection = (transform.position - (Vector3)explosionPosition).normalized;
            rb.AddForce(explosionDirection * explosionForce, ForceMode2D.Impulse);
            StreamerCam.NotifyStreamer(StreamerEvent.BombJumpExecuted);
            Debug.Log("Bomb-jump shenanigans");
        }
    }

    void Die() {
        if (isInvincible) {
            Debug.Log("Player can't die, go wild!");
            return;
        }

        animator.SetTrigger("hasDied");

        Debug.Log("You died :(");
        StreamerCam.NotifyStreamer(StreamerEvent.Death);
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        isInvincible = true;
        controls.Player.Disable();
        _graphicsObject.gameObject.SetActive(false);

        yield return new WaitForSeconds(0.2f);
        FindFirstObjectByType<Porthole>()?.ClosePorthole();
        yield return new WaitForSeconds(1.2f);

        FindFirstObjectByType<Porthole>()?.OpenPorthole();

        _graphicsObject.gameObject.SetActive(true);
        controls.Player.Enable();
        Respawn();
        FindFirstObjectByType<LevelManager>().RespawnSameLevel();
    }

    void Respawn() {
        transform.position = _respawnPoint;
        rb.velocity = Vector2.zero;
        isInvincible = false;
        isCollidingWithDanger = false;
        StartCoroutine(SnapCamRoutine());
    }

    void OnCollisionEnter2D(Collision2D collision) {
        bool isSpike = collision.gameObject.CompareTag("Spike");
        bool isEnemy = collision.gameObject.CompareTag("Enemy");

        if (isSpike || isEnemy) {
            bool fallingOnDanger = collision.contacts[0].normal.y > 0.2f;

            if (fallingOnDanger && isEnemy) {
                collision.gameObject.GetComponent<BaseEnemy>().Die();
                InvertYSpeed();
                return;
            }

            isCollidingWithDanger = true;

            if (!isInvincible) {
                Debug.Log($"going to die because of {collision.gameObject.name}");
                Die();
            }
        }
    }

    void OnCollisionExit2D(Collision2D collision) {
        if (collision.gameObject.CompareTag("Spike") || collision.gameObject.CompareTag("Enemy")) {
            isCollidingWithDanger = false;
        }
    }

    void OnCollisionStay2D(Collision2D other) {
        var contactArray = new ContactPoint2D[16];

        _myCollider.GetContacts(contactArray);

        bool hasContactAbove = false;
        bool hasContactBelow = false;

        foreach (ContactPoint2D contact in contactArray) {
            if (contact.point.magnitude < 0.1f)
                continue;

            if (contact.point.y > transform.position.y + 0.4f)
                hasContactAbove = true;

            if (contact.point.y < transform.position.y - 0.4f)
                hasContactBelow = true;
        }

        isGrounded = hasContactBelow;
        isHeadache = hasContactAbove;

        if (hasContactAbove && hasContactBelow)
        {
            Debug.Log("Get crushed idiot");
            Die();
        }
    }

    private void InvertYSpeed() {
        float yVel = _previousFrameFallVelocity;
        rb.velocity = new Vector2(rb.velocity.x, -yVel);
    }

    public void DebugUseAbility(int ability) {
        _equippedAbility = (AbilityType) ability;
        OnAbilityPressed();
    }

    private void OnAbilityPressed() {
        if (abilityTimers.ContainsKey(_equippedAbility)) {
            Debug.Log($"{_equippedAbility} is on cooldown!");
            return;
        }

        float power = _abilityData.GetPower(_equippedAbility);

        if (_equippedAbility == AbilityType.Boost)
            DoHorizontalBoost(power);
        else if (_equippedAbility == AbilityType.Rocket)
            DoVerticalBoost(power);
        else if (_equippedAbility == AbilityType.Freeze)
            DoFreeze(power);
        else if (_equippedAbility == AbilityType.Bomb)
            DoBomb(power);
        else if (_equippedAbility == AbilityType.Invincible)
            DoInvincibility(power);
    }

    private void DoFreeze(float time) {
        Debug.Log($"Freeze for {time} seconds!");

        foreach (var enemy in enemies) {
            enemy.Freeze(time);
        }

        foreach (var platform in platforms) {
            platform.Freeze(time);
        }

        foreach (var crusher in crushers) {
            crusher.Freeze(time);
        }

        foreach (var enemygoomba in enemygoombas) {
            enemygoomba.Freeze(time);
        }

        StartCooldown(AbilityType.Freeze);
    }

    private void DoBomb(float power) {
        Debug.Log("Bomb!");

        GameObject bombInstance = Instantiate(bombPrefab, bombSpawnPoint.position, bombSpawnPoint.rotation);

        Rigidbody2D bombRb = bombInstance.GetComponent<Rigidbody2D>();

        Vector2 playerVelocity = rb.velocity;

        float additionalVelocity = 5f;
        Vector2 throwForce = new Vector2(
        playerVelocity.x + Mathf.Sign(playerVelocity.x) * additionalVelocity,
        power
    );

        bombRb.velocity = playerVelocity + throwForce;

        StartCooldown(AbilityType.Bomb);
    }

    private void StartCooldown(AbilityType ability) {
        float cooldown = _abilityData.GetCooldown(ability);
        abilityTimers[ability] = cooldown;
    }

    private void ApplyDeceleration() {
        if (Mathf.Abs(rb.velocity.x) > decelerationSpeedThreshold) {
            float deceleration = decelerationStrength * Time.deltaTime * Mathf.Sign(rb.velocity.x);
            float newVelocityX = rb.velocity.x - deceleration;

            if (Mathf.Abs(newVelocityX) < decelerationSpeedThreshold) {
                newVelocityX = decelerationSpeedThreshold * Mathf.Sign(rb.velocity.x);
            }

            rb.velocity = new Vector2(newVelocityX, rb.velocity.y);
        }
    }

    private void DoHorizontalBoost(float power) {
        Debug.Log("Boost!");

        animator.SetTrigger("hasBoosted");

        float dashDirection = moveInput.x == 0 ? lastHorizontalDirection : Mathf.Sign(moveInput.x);

        rb.velocity = new Vector2(dashDirection * power, rb.velocity.y);

        StartCooldown(AbilityType.Boost);

        StartCoroutine(HorizontalBoostRoutine(0.5f));
    }

    private IEnumerator HorizontalBoostRoutine(float duration) {
        isHorizontalBoosting = true;
        rb.gravityScale = 0;
        rb.velocity = new Vector3(rb.velocity.x, 0, 0);
        yield return new WaitForSeconds(duration - 0.1f);
        rb.gravityScale = 1;
        isHorizontalBoosting = false;
    }

    private void DoVerticalBoost(float power) {
        Debug.Log("Rocket!");

        rb.velocity = new Vector2(rb.velocity.x, power);

        StartCooldown(AbilityType.Rocket);

        isVerticalBoosting = true;
        Invoke(nameof(EndVerticalBoost), 0.5f);
    }

    private void EndVerticalBoost() {
        isVerticalBoosting = false;
    }

    public void SetSpawnPosition(Vector3 position) {
        _respawnPoint = position;
        transform.position = position;
    }

    private void DoInvincibility(float time) {
        Debug.Log($"Invincible for {time} seconds!");

        isInvincible = true;
        // StreamerCam.NotifyStreamer(StreamerEvent.Invincibility);

        invincibilityCoroutine = StartCoroutine(InvincibilityDuration(time));

        StartCooldown(AbilityType.Invincible);
    }

    private IEnumerator InvincibilityDuration(float time) {
        float elapsedTime = 0f;
        int colorIndex = 0;

        while (elapsedTime < time) {
            spriteRenderer.color = invincibilityColors[colorIndex];
            colorIndex = (colorIndex + 1) % invincibilityColors.Length;
            yield return new WaitForSeconds(colorChangeSpeed);
            elapsedTime += colorChangeSpeed;
        }

        spriteRenderer.color = Color.white;
        isInvincible = false;

        if (isCollidingWithDanger) {
            Die();
        }
    }

    private void UpdateMovement() {
        if (moveInput.x != 0) {
            accelerationProgress += Time.deltaTime / accelerationTime;
            /* TODO fix me
            if (isGrounded) {
                accelerationProgress += Time.deltaTime / accelerationTime;
            } else {
                accelerationProgress = Mathf.Max(accelerationProgress - Time.deltaTime / accelerationTime, 0f);
            }
            */
            float easedProgress = ExponentialEaseOut(accelerationProgress);
            rb.velocity = new Vector2(Mathf.Sign(moveInput.x) * easedProgress * moveSpeed, rb.velocity.y);

            spriteRenderer.flipX = moveInput.x < 0;
        } else {
            accelerationProgress = 0f;
            // if (isGrounded) {
                rb.velocity = new Vector2(0, rb.velocity.y);
            // }
        }
    }

    private float ExponentialEaseOut(float t) {
        return t == 1f ? 1f : 1 - Mathf.Pow(2, -10 * t);
    }

    private void CycleAbility(bool next) {
        var abilities = System.Enum.GetValues(typeof(AbilityType));
        int currentIndex = System.Array.IndexOf(abilities, _equippedAbility);
        if (next) {
            currentIndex = (currentIndex + 1) % abilities.Length;
        } else {
            currentIndex = (currentIndex - 1 + abilities.Length) % abilities.Length;
        }
        _equippedAbility = (AbilityType) abilities.GetValue(currentIndex);
        Debug.Log($"Equipped ability: {_equippedAbility}");
        FindFirstObjectByType<AbilitySelectUI>().SelectAbility(_equippedAbility);
    }

    private void LateUpdate() {
        _previousFrameFallVelocity = rb.velocity.y;
    }
    
    private IEnumerator SnapCamRoutine()
    {
        var cam = FindFirstObjectByType<CinemachinePositionComposer>();
        Vector3 damping = cam.Damping;
        cam.Damping = Vector3.zero;
        cam.Lookahead.Enabled = false;
        yield return null;
        cam.Lookahead.Enabled = true;
        cam.Damping = damping;
    }
}