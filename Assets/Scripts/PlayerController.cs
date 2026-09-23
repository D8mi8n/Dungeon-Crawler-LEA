using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator), typeof(SpriteRenderer))]
public class PlayerController : MonoBehaviour
{
    [Header("Bewegung")]
    public float walkSpeed = 3.2f;
    public float runSpeed = 4.8f;
    [Header("Charakter")]
    [Range(0, 3)] public int skeleton = 2;
    [Header("Kampf")]
    public int maxHealth = 6;
    public int attackDamage = 1;
    public float attackCooldown = 0.62f;
    public float attackDuration = 0.54f;
    public float attackHitDelay = 0.23f;
    public float attackReach = 0.4f;
    public float attackRadius = 0.5f;
    public float invulnerabilityDuration = 0.95f;

    public int CurrentHealth { get; private set; }
    public int MaxHealth { get { return Mathf.Max(1, maxHealth); } }
    public bool IsDead { get; private set; }
    public Vector2 FacingDirection { get; private set; } = Vector2.down;

    private Rigidbody2D body;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Color normalColor;
    private Vector2 movement;
    private bool isRunning;
    private bool isAttacking;
    private bool attackHitApplied;
    private float attackTime;
    private float cooldownRemaining;
    private float hurtRemaining;
    private float invulnerabilityRemaining;
    private string direction = "D";
    private string currentAnimation;
    private int wallMask;
    private readonly HashSet<EnemyController> struckEnemies = new HashSet<EnemyController>();

    private bool IsPlaying
    {
        get { return DungeonGame.Instance != null && DungeonGame.Instance.IsPlaying; }
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        normalColor = spriteRenderer.color;
        wallMask = LayerMask.GetMask("DungeonWalls");
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        CurrentHealth = MaxHealth;
        PlayAnimation("Idle");
    }

    private void Update()
    {
        spriteRenderer.sortingOrder = 1000 - Mathf.RoundToInt(transform.position.y * 100f);
        if (!IsPlaying)
        {
            movement = Vector2.zero;
            body.linearVelocity = Vector2.zero;
            animator.speed = IsDead && DungeonGame.Instance != null &&
                DungeonGame.Instance.State == DungeonGame.GameState.Lost ? 1f : 0f;
            return;
        }
        if (IsDead)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }

        float elapsed = Time.deltaTime;
        cooldownRemaining = Mathf.Max(0f, cooldownRemaining - elapsed);
        hurtRemaining = Mathf.Max(0f, hurtRemaining - elapsed);
        invulnerabilityRemaining = Mathf.Max(0f, invulnerabilityRemaining - elapsed);
        Color tint = normalColor;
        if (invulnerabilityRemaining > 0f)
            tint.a *= Mathf.FloorToInt(invulnerabilityRemaining * 14f) % 2 == 0 ? 0.38f : 1f;
        spriteRenderer.color = tint;

        if (isAttacking)
        {
            animator.speed = 1.9f;
            movement = Vector2.zero;
            attackTime += elapsed;
            if (!attackHitApplied && attackTime >= attackHitDelay)
            {
                attackHitApplied = true;
                ResolveAttack();
            }
            if (attackTime < attackDuration)
                return;
            isAttacking = false;
        }
        if (hurtRemaining > 0f)
        {
            movement = Vector2.zero;
            animator.speed = 2f;
            return;
        }

        movement = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        isRunning = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (movement.sqrMagnitude > 0.01f)
            Face(movement);
        bool mouseAttack = Input.GetMouseButtonDown(0);
        if (Input.GetKeyDown(KeyCode.Space) || mouseAttack)
        {
            Vector2 aim = FacingDirection;
            Camera view = Camera.main;
            if (mouseAttack && view != null)
            {
                Vector2 towardMouse = (Vector2)view.ScreenToWorldPoint(Input.mousePosition) - body.position;
                if (towardMouse.sqrMagnitude > 0.05f)
                    aim = towardMouse;
            }
            if (TryAttack(aim))
                return;
        }
        animator.speed = 1f;
        PlayAnimation(movement.sqrMagnitude < 0.01f ? "Idle" : isRunning ? "Run" : "Walk");
    }

    private void FixedUpdate()
    {
        if (!IsPlaying || IsDead || isAttacking || hurtRemaining > 0f)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }
        float speed = isRunning ? runSpeed : walkSpeed;
        body.MovePosition(body.position + movement * speed * Time.fixedDeltaTime);
    }

    private void Face(Vector2 vector)
    {
        if (Mathf.Abs(vector.x) > Mathf.Abs(vector.y))
        {
            direction = vector.x > 0f ? "R" : "L";
            FacingDirection = vector.x > 0f ? Vector2.right : Vector2.left;
        }
        else
        {
            direction = vector.y > 0f ? "U" : "D";
            FacingDirection = vector.y > 0f ? Vector2.up : Vector2.down;
        }
    }

    public bool TryAttack(Vector2 aim)
    {
        if (!IsPlaying || IsDead || isAttacking || hurtRemaining > 0f || cooldownRemaining > 0f)
            return false;
        if (aim.sqrMagnitude > 0.01f)
            Face(aim);
        BeginAttack();
        return true;
    }

    private void BeginAttack()
    {
        movement = Vector2.zero;
        body.linearVelocity = Vector2.zero;
        isAttacking = true;
        attackHitApplied = false;
        attackTime = 0f;
        cooldownRemaining = Mathf.Max(attackDuration, attackCooldown);
        animator.speed = 1.9f;
        PlayAnimation("Attack", true);
        DungeonGame.Instance.PlaySound("attack");
    }

    private void ResolveAttack()
    {
        Vector2 origin = body.position;
        Collider2D[] hits = Physics2D.OverlapCircleAll(origin + FacingDirection * attackReach, attackRadius);
        struckEnemies.Clear();
        foreach (Collider2D hit in hits)
        {
            EnemyController enemy = hit.GetComponentInParent<EnemyController>();
            if (enemy == null || enemy.IsDead || !struckEnemies.Add(enemy))
                continue;
            Vector2 toEnemy = (Vector2)enemy.transform.position - origin;
            if (Vector2.Dot(FacingDirection, toEnemy.normalized) < -0.1f)
                continue;
            if (Physics2D.Linecast(origin, enemy.transform.position, wallMask).collider != null)
                continue;
            enemy.TakeDamage(attackDamage, FacingDirection);
        }
    }

    public void TakeDamage(int damage)
    {
        if (!IsPlaying || IsDead || damage <= 0 || invulnerabilityRemaining > 0f)
            return;
        CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
        movement = Vector2.zero;
        body.linearVelocity = Vector2.zero;
        isAttacking = false;
        invulnerabilityRemaining = invulnerabilityDuration;
        DungeonGame.Instance.PlaySound("hurt");
        if (CurrentHealth == 0)
        {
            IsDead = true;
            spriteRenderer.color = normalColor;
            animator.speed = 1f;
            PlayAnimation("Death", true);
            DungeonGame.Instance.OnPlayerDied();
            return;
        }
        hurtRemaining = 0.18f;
        animator.speed = 2f;
        PlayAnimation("Hurt", true);
    }

    public bool Heal(int amount)
    {
        if (IsDead || amount <= 0 || CurrentHealth >= MaxHealth)
            return false;
        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
        if (DungeonGame.Instance != null)
            DungeonGame.Instance.PlaySound("heal");
        return true;
    }

    public void ResetPlayer(Vector2 position)
    {
        CurrentHealth = MaxHealth;
        IsDead = false;
        isAttacking = false;
        attackHitApplied = false;
        movement = Vector2.zero;
        attackTime = cooldownRemaining = hurtRemaining = invulnerabilityRemaining = 0f;
        body.position = position;
        transform.position = new Vector3(position.x, position.y, transform.position.z);
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        spriteRenderer.color = normalColor;
        Face(Vector2.down);
        animator.speed = 1f;
        PlayAnimation("Idle", true);
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

    private void OnDisable()
    {
        if (body != null)
            body.linearVelocity = Vector2.zero;
        if (spriteRenderer != null)
            spriteRenderer.color = normalColor;
    }
}
