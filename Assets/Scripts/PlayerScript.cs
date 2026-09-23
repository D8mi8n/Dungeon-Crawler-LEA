
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    [Header("Bewegung")]
    public float walkSpeed = 3f;
    public float runSpeed = 5f;

    [Header("Charakter")]
    [Range(2, 3)]
    public int skeleton = 2;

    [Header("Lebenspunkte")]
    public int maxHealth = 3;

    private int currentHealth;

    private Rigidbody2D rb;
    private Animator animator;

    private Vector2 movement;

    private bool isRunning;
    private bool isBusy;
    private bool isDead;

    // D = Down, U = Up, L = Left, R = Right
    private string direction = "D";

    private string currentAnimation = "";

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();

        currentHealth = maxHealth;

        // Top-Down-Bewegung
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        PlayAnimation("Idle");
    }

    private void Update()
    {
        if (isDead)
            return;

        // Prüfen, ob Angriff oder Hurt beendet ist
        if (isBusy)
        {
            AnimatorStateInfo state =
                animator.GetCurrentAnimatorStateInfo(0);

            if (state.IsName(currentAnimation) &&
                state.normalizedTime >= 1f)
            {
                isBusy = false;
            }
            else
            {
                return;
            }
        }

        // Bewegung lesen
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        movement = new Vector2(horizontal, vertical).normalized;

        // Rennen mit Shift
        isRunning = Input.GetKey(KeyCode.LeftShift);

        // Blickrichtung bestimmen
        if (movement.sqrMagnitude > 0.01f)
        {
            if (Mathf.Abs(movement.x) > Mathf.Abs(movement.y))
            {
                direction = movement.x > 0 ? "R" : "L";
            }
            else
            {
                direction = movement.y > 0 ? "U" : "D";
            }
        }

        // Angriff mit Leertaste
        if (Input.GetKeyDown(KeyCode.Space))
        {
            movement = Vector2.zero;
            isBusy = true;

            PlayAnimation("Attack", true);

            return;
        }

        // Passende Bewegungsanimation
        if (movement.sqrMagnitude < 0.01f)
        {
            PlayAnimation("Idle");
        }
        else if (isRunning)
        {
            PlayAnimation("Run");
        }
        else
        {
            PlayAnimation("Walk");
        }
    }

    private void FixedUpdate()
    {
        if (isDead || isBusy)
            return;

        float speed = isRunning ? runSpeed : walkSpeed;

        rb.MovePosition(
            rb.position +
            movement * speed * Time.fixedDeltaTime
        );
    }

    // Animation anhand des Namens abspielen
    private void PlayAnimation(
        string animation,
        bool restart = false)
    {
        string stateName =
            "Player_" +
            animation +
            direction +
            "_Skeleton_" +
            skeleton;

        // Gleiche Animation nicht ständig neu starten
        if (!restart && currentAnimation == stateName)
            return;

        currentAnimation = stateName;

        animator.Play(stateName, 0, 0f);
    }

    // Schaden erhalten
    public void TakeDamage(int damage)
    {
        if (isDead || damage <= 0)
            return;

        currentHealth -= damage;

        movement = Vector2.zero;

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            isBusy = true;

            PlayAnimation("Hurt", true);
        }
    }

    // Tod des Spielers
    private void Die()
    {
        isDead = true;
        isBusy = false;

        movement = Vector2.zero;

        PlayAnimation("Death", true);
    }
}
