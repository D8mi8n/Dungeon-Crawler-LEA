using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator), typeof(SpriteRenderer))]
public class EnemyController : MonoBehaviour
{
    [Header("Gegner")]
    public int maxHealth = 3;
    public bool isGuardian;
    public float moveSpeed = 1.55f;
    [Range(2, 3)] public int skeleton = 3;
    public float detectionRadius = 3.5f;
    public float guardianLeash = 3f;
    [Header("Nahkampf")]
    public int attackDamage = 1;
    public float attackRange = 0.8f;
    public float attackWindup = 0.55f;
    public float attackRecovery = 0.65f;
    public float attackCooldown = 1.55f;

    public bool IsDead { get; private set; }
    public int CurrentHealth { get; private set; }

    private Rigidbody2D body;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Collider2D[] bodyColliders;
    private Color normalColor;
    private Vector2 spawnPosition;
    private Vector2 desiredMovement;
    private Vector2 attackDirection;
    private Vector2 knockback;
    private string direction = "D";
    private string currentAnimation;
    private float cooldownRemaining;
    private float windupRemaining;
    private float recoveryRemaining;
    private float hurtRemaining;
    private float hitFlashRemaining;
    private float deathElapsed;
    private bool isWindingUp;
    private int wallMask;

    private bool IsPlaying
    {
        get { return DungeonGame.Instance != null && DungeonGame.Instance.IsPlaying; }
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        bodyColliders = GetComponentsInChildren<Collider2D>();
        spawnPosition = body.position;
        normalColor = spriteRenderer.color;
        wallMask = LayerMask.GetMask("DungeonWalls");
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        CurrentHealth = Mathf.Max(1, maxHealth);
        cooldownRemaining = 0.3f;
        PlayAnimation("Idle");
    }

    private void Update()
    {
        spriteRenderer.sortingOrder = 1000 - Mathf.RoundToInt(transform.position.y * 100f);
        if (!IsPlaying)
        {
            desiredMovement = Vector2.zero;
            body.linearVelocity = Vector2.zero;
            animator.speed = 0f;
            return;
        }
        float elapsed = Time.deltaTime;
        if (IsDead)
        {
            deathElapsed += elapsed;
            animator.speed = 1f;
            Color corpse = normalColor;
            corpse.a *= Mathf.Clamp01(1f - (deathElapsed - 1.5f) / 0.8f);
            spriteRenderer.color = corpse;
            return;
        }

        cooldownRemaining = Mathf.Max(0f, cooldownRemaining - elapsed);
        hurtRemaining = Mathf.Max(0f, hurtRemaining - elapsed);
        recoveryRemaining = Mathf.Max(0f, recoveryRemaining - elapsed);
        hitFlashRemaining = Mathf.Max(0f, hitFlashRemaining - elapsed);
        desiredMovement = Vector2.zero;
        Color tint = normalColor;
        if (isWindingUp)
            tint = Color.Lerp(normalColor, new Color(1f, 0.38f, 0.1f), 0.55f + 0.4f * Mathf.Sin(Time.time * 24f));
        if (hitFlashRemaining > 0f)
            tint = new Color(1f, 0.34f, 0.34f, normalColor.a);
        spriteRenderer.color = tint;

        PlayerController player = DungeonGame.Instance.Player;
        if (player == null || player.IsDead)
        {
            animator.speed = 1f;
            PlayAnimation("Idle");
            return;
        }
        if (isWindingUp)
        {
            animator.speed = isGuardian ? 0.9f : 1.1f;
            windupRemaining -= elapsed;
            if (windupRemaining <= 0f)
                ResolveAttack(player);
            return;
        }
        if (hurtRemaining > 0f)
        {
            animator.speed = 2f;
            return;
        }
        if (recoveryRemaining > 0f)
        {
            animator.speed = 1.1f;
            return;
        }

        Vector2 toPlayer = (Vector2)player.transform.position - body.position;
        float distance = toPlayer.magnitude;
        bool canSeePlayer = distance <= (isGuardian ? Mathf.Min(3f, detectionRadius) : detectionRadius) &&
            Physics2D.Linecast(body.position, player.transform.position, wallMask).collider == null;
        animator.speed = 1f;

        if (isGuardian && Vector2.Distance(player.transform.position, spawnPosition) > guardianLeash)
        {
            Vector2 toHome = spawnPosition - body.position;
            if (toHome.sqrMagnitude > 0.04f && Physics2D.Linecast(body.position, spawnPosition, wallMask).collider == null)
            {
                desiredMovement = toHome.normalized;
                Face(toHome);
                PlayAnimation("Walk");
            }
            else
                PlayAnimation("Idle");
            return;
        }
        if (!canSeePlayer)
        {
            PlayAnimation("Idle");
            return;
        }

        Face(toPlayer);
        if (distance <= attackRange && cooldownRemaining <= 0f)
        {
            isWindingUp = true;
            windupRemaining = attackWindup + (isGuardian ? 0.15f : 0f);
            attackDirection = toPlayer.sqrMagnitude > 0.01f ? toPlayer.normalized : Vector2.down;
            cooldownRemaining = Mathf.Max(attackCooldown, windupRemaining + attackRecovery);
            PlayAnimation("Attack", true);
            DungeonGame.Instance.PlaySound("enemyAttack");
            return;
        }
        if (distance > attackRange * 0.86f)
        {
            desiredMovement = toPlayer.normalized;
            PlayAnimation("Walk");
        }
        else
            PlayAnimation("Idle");
    }

    private void FixedUpdate()
    {
        if (!IsPlaying || IsDead)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }
        Vector2 step = desiredMovement * moveSpeed;
        if (hurtRemaining > 0f && !isGuardian)
            step += knockback;
        body.MovePosition(body.position + step * Time.fixedDeltaTime);
        knockback = Vector2.MoveTowards(knockback, Vector2.zero, 18f * Time.fixedDeltaTime);
    }

    private void ResolveAttack(PlayerController player)
    {
        isWindingUp = false;
        recoveryRemaining = attackRecovery;
        Vector2 toPlayer = (Vector2)player.transform.position - body.position;
        bool inReach = toPlayer.sqrMagnitude <= Mathf.Pow(attackRange + 0.18f, 2f);
        bool inFront = toPlayer.sqrMagnitude < 0.03f || Vector2.Dot(attackDirection, toPlayer.normalized) > 0.25f;
        bool clearPath = Physics2D.Linecast(body.position, player.transform.position, wallMask).collider == null;
        if (inReach && inFront && clearPath)
            player.TakeDamage(attackDamage);
    }

    public void TakeDamage(int damage, Vector2 hitDirection)
    {
        if (!IsPlaying || IsDead || damage <= 0)
            return;
        CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
        hitFlashRemaining = 0.16f;
        if (CurrentHealth == 0)
        {
            IsDead = true;
            desiredMovement = knockback = Vector2.zero;
            body.linearVelocity = Vector2.zero;
            isWindingUp = false;
            deathElapsed = 0f;
            foreach (Collider2D bodyCollider in bodyColliders)
                bodyCollider.enabled = false;
            spriteRenderer.color = normalColor;
            animator.speed = 1f;
            PlayAnimation("Death", true);
            DungeonGame.Instance.PlaySound("enemyDeath");
            DungeonGame.Instance.OnEnemyDefeated(this);
            return;
        }
        DungeonGame.Instance.PlaySound("enemyHit");
        if (!isGuardian)
        {
            isWindingUp = false;
            recoveryRemaining = 0f;
            hurtRemaining = 0.18f;
            cooldownRemaining = Mathf.Max(cooldownRemaining, 0.7f);
            desiredMovement = Vector2.zero;
            knockback = hitDirection.normalized * 2.4f;
            PlayAnimation("Hurt", true);
        }
    }

    public void ResetEnemy()
    {
        CurrentHealth = Mathf.Max(1, maxHealth);
        IsDead = false;
        isWindingUp = false;
        cooldownRemaining = 0.3f;
        windupRemaining = recoveryRemaining = hurtRemaining = hitFlashRemaining = deathElapsed = 0f;
        desiredMovement = knockback = Vector2.zero;
        body.position = spawnPosition;
        transform.position = new Vector3(spawnPosition.x, spawnPosition.y, transform.position.z);
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        foreach (Collider2D bodyCollider in bodyColliders)
            bodyCollider.enabled = true;
        spriteRenderer.color = normalColor;
        direction = "D";
        animator.speed = 1f;
        PlayAnimation("Idle", true);
    }

    private void Face(Vector2 vector)
    {
        if (Mathf.Abs(vector.x) > Mathf.Abs(vector.y))
            direction = vector.x > 0f ? "R" : "L";
        else
            direction = vector.y > 0f ? "U" : "D";
    }

    private void PlayAnimation(string action, bool restart = false)
    {
        string stateName = "Player_" + action + direction + "_Skeleton_" + skeleton;
        if (!restart && currentAnimation == stateName)
            return;
        if (animator.runtimeAnimatorController == null)
            return;
        int state = Animator.StringToHash(stateName);
        if (!animator.HasState(0, state))
            return;
        currentAnimation = stateName;
        animator.Play(state, 0, 0f);
    }

    private void OnGUI()
    {
        if (!IsPlaying || IsDead || (CurrentHealth >= maxHealth && !isGuardian))
            return;
        Camera view = Camera.main;
        if (view == null)
            return;
        Vector3 screen = view.WorldToScreenPoint(transform.position + Vector3.up * 0.58f);
        if (screen.z <= 0f)
            return;
        float width = isGuardian ? 64f : 36f;
        Rect bounds = new Rect(screen.x - width * 0.5f, Screen.height - screen.y, width, 5f);
        Color previousColor = GUI.color;
        GUI.color = new Color(0.1f, 0.06f, 0.08f, 0.95f);
        GUI.DrawTexture(new Rect(bounds.x - 1f, bounds.y - 1f, bounds.width + 2f, bounds.height + 2f), Texture2D.whiteTexture);
        GUI.color = isGuardian ? new Color(1f, 0.65f, 0.19f) : new Color(0.9f, 0.23f, 0.32f);
        bounds.width *= CurrentHealth / (float)Mathf.Max(1, maxHealth);
        GUI.DrawTexture(bounds, Texture2D.whiteTexture);
        GUI.color = previousColor;
    }

    private void OnDisable()
    {
        if (body != null)
            body.linearVelocity = Vector2.zero;
    }
}
