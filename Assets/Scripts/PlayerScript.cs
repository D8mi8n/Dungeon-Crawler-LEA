
using UnityEngine;
using System.Collections.Generic;
using System.Text.RegularExpressions;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerController : MonoBehaviour
{
    [Header("Bewegung")]
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float runSpeed = 5f;

    [Header("Lebenspunkte")]
    [SerializeField] private int maxHealth = 3;

    [Header("Animationsgeschwindigkeit")]
    [SerializeField] private float idleFPS = 3f;
    [SerializeField] private float walkFPS = 7f;
    [SerializeField] private float runFPS = 10f;
    [SerializeField] private float attackFPS = 8f;
    [SerializeField] private float hurtFPS = 6f;
    [SerializeField] private float deathFPS = 5f;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;

    private Vector2 movement;
    private int currentHealth;

    // Alle vorhandenen Animationen
    private enum AnimationState
    {
        Idle,
        Walk,
        Run,
        Attack,
        Hurt,
        Death
    }

    // Blickrichtung
    private enum Direction
    {
        D,
        L,
        R,
        U
    }

    private AnimationState currentState = AnimationState.Idle;
    private Direction facing = Direction.D;

    // Hier werden alle Sprites gespeichert
    private Dictionary<string, Sprite[]> animations =
        new Dictionary<string, Sprite[]>();

    private Sprite[] currentFrames;

    private string currentAnimation = "";

    private int currentFrame = 0;
    private float animationTimer = 0f;

    private bool isDead = false;
    private bool isRunning = false;

    public int CurrentHealth => currentHealth;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        currentHealth = maxHealth;

        // Einstellungen für ein Top-Down-Spiel
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        LoadAnimations();

        PlayAnimation(AnimationState.Idle);
    }

    private void Update()
    {
        if (!isDead)
        {
            ReadInput();

            // Angriff starten
            if (Input.GetKeyDown(KeyCode.Space) &&
                currentState != AnimationState.Attack &&
                currentState != AnimationState.Hurt)
            {
                PlayAnimation(AnimationState.Attack, true);
            }
            else if (currentState != AnimationState.Attack &&
                     currentState != AnimationState.Hurt)
            {
                UpdateMovementAnimation();
            }
        }

        UpdateAnimation();
    }

    private void FixedUpdate()
    {
        if (isDead)
            return;

        // Während Angriff oder Schaden nicht bewegen
        if (currentState == AnimationState.Attack ||
            currentState == AnimationState.Hurt)
        {
            return;
        }

        float speed = isRunning ? runSpeed : walkSpeed;

        rb.MovePosition(
            rb.position + movement * speed * Time.fixedDeltaTime
        );
    }

    // Eingabe und Blickrichtung
    private void ReadInput()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        movement = new Vector2(horizontal, vertical).normalized;

        isRunning =
            Input.GetKey(KeyCode.LeftShift) ||
            Input.GetKey(KeyCode.RightShift);

        // Letzte Blickrichtung speichern
        if (movement.sqrMagnitude > 0.01f)
        {
            if (Mathf.Abs(movement.x) > Mathf.Abs(movement.y))
            {
                facing = movement.x > 0
                    ? Direction.R
                    : Direction.L;
            }
            else
            {
                facing = movement.y > 0
                    ? Direction.U
                    : Direction.D;
            }
        }
    }

    // Idle, Walk oder Run auswählen
    private void UpdateMovementAnimation()
    {
        if (movement.sqrMagnitude < 0.01f)
        {
            PlayAnimation(AnimationState.Idle);
        }
        else if (isRunning)
        {
            PlayAnimation(AnimationState.Run);
        }
        else
        {
            PlayAnimation(AnimationState.Walk);
        }
    }

    // Sprites automatisch aus Resources laden
    private void LoadAnimations()
    {
        Sprite[] allSprites = Resources.LoadAll<Sprite>("Player");

        Dictionary<string, List<Sprite>> groups =
            new Dictionary<string, List<Sprite>>();

        foreach (Sprite sprite in allSprites)
        {
            // Beispiel: Player_WalkD_Skeleton_2
            Match match = Regex.Match(
                sprite.name,
                @"^Player_(Attack|Death|Hurt|Idle|Run|Walk)([DLRU])_Skeleton_(\d+)$"
            );

            if (!match.Success)
                continue;

            string key =
                match.Groups[1].Value +
                match.Groups[2].Value;

            if (!groups.ContainsKey(key))
            {
                groups[key] = new List<Sprite>();
            }

            groups[key].Add(sprite);
        }

        // Frames in der richtigen Reihenfolge sortieren
        foreach (var group in groups)
        {
            group.Value.Sort((a, b) =>
                GetFrameNumber(a.name).CompareTo(
                    GetFrameNumber(b.name)
                )
            );

            animations[group.Key] = group.Value.ToArray();
        }

        Debug.Log(
            "Player: " + animations.Count +
            " Animationen geladen."
        );
    }

    private int GetFrameNumber(string spriteName)
    {
        int index = spriteName.LastIndexOf('_');

        if (int.TryParse(
            spriteName.Substring(index + 1),
            out int number))
        {
            return number;
        }

        return 0;
    }

    // Eine Animation starten
    private void PlayAnimation(
        AnimationState newState,
        bool restart = false)
    {
        string animationName =
            newState.ToString() + facing.ToString();

        // Laufende Animation nicht ständig neu starten
        if (!restart && currentAnimation == animationName)
            return;

        if (!animations.TryGetValue(
            animationName,
            out Sprite[] frames))
        {
            Debug.LogWarning(
                "Animation fehlt: " + animationName
            );

            return;
        }

        currentState = newState;
        currentAnimation = animationName;

        currentFrames = frames;

        currentFrame = 0;
        animationTimer = 0f;

        spriteRenderer.sprite = currentFrames[0];
    }

    // Animationsgeschwindigkeit bestimmen
    private float GetAnimationFPS()
    {
        switch (currentState)
        {
            case AnimationState.Idle:
                return idleFPS;

            case AnimationState.Walk:
                return walkFPS;

            case AnimationState.Run:
                return runFPS;

            case AnimationState.Attack:
                return attackFPS;

            case AnimationState.Hurt:
                return hurtFPS;

            case AnimationState.Death:
                return deathFPS;

            default:
                return idleFPS;
        }
    }

    // Einzelne Frames abspielen
    private void UpdateAnimation()
    {
        if (currentFrames == null ||
            currentFrames.Length == 0)
        {
            return;
        }

        float fps = Mathf.Max(0.1f, GetAnimationFPS());

        animationTimer += Time.deltaTime;

        if (animationTimer < 1f / fps)
            return;

        animationTimer -= 1f / fps;

        currentFrame++;

        // Animation ist zu Ende
        if (currentFrame >= currentFrames.Length)
        {
            // Death: Auf dem letzten Frame bleiben
            if (currentState == AnimationState.Death)
            {
                currentFrame = currentFrames.Length - 1;
                return;
            }

            // Nach Attack und Hurt zur Bewegung zurück
            if (currentState == AnimationState.Attack ||
                currentState == AnimationState.Hurt)
            {
                UpdateMovementAnimation();
                return;
            }

            // Idle, Walk und Run wiederholen
            currentFrame = 0;
        }

        spriteRenderer.sprite = currentFrames[currentFrame];
    }

    // Schaden erhalten
    public void TakeDamage(int damage)
    {
        if (isDead || damage <= 0)
            return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            PlayAnimation(AnimationState.Hurt, true);
        }
    }

    // Tod des Spielers
    public void Die()
    {
        if (isDead)
            return;

        isDead = true;
        movement = Vector2.zero;

        PlayAnimation(AnimationState.Death, true);
    }
}
