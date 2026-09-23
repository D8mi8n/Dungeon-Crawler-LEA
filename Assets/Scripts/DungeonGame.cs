using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Owns a complete run, including menus, rewards and the escape condition.</summary>
public sealed class DungeonGame : MonoBehaviour
{
    public enum GameState { Menu, Playing, Paused, Won, Lost }
    public static DungeonGame Instance { get; private set; }
    private static bool startImmediately;
    public static readonly string[] LevelScenes = { "LEA", "LEA_Zisterne", "LEA_Grabkammern" };
    public static readonly string[] LevelNames = { "Krypta", "Zisterne", "Grabkammern" };
    public int LevelIndex => Mathf.Max(0, System.Array.IndexOf(LevelScenes, SceneManager.GetActiveScene().name));
    public bool HasNextLevel => LevelIndex < LevelScenes.Length - 1;
    public PlayerController player;
    public GameState State { get; private set; } = GameState.Menu;
    public PlayerController Player => player;
    public bool IsPlaying => State == GameState.Playing;
    public int SealCount { get; private set; }
    public int CoinCount { get; private set; }
    public int DefeatedEnemies { get; private set; }
    public int TotalEnemies { get; private set; }
    public float ElapsedTime { get; private set; }
    public string Message { get; private set; } = "";
    public string InteractionPrompt { get; private set; } = "";
    public bool Muted { get; private set; }
    public string Objective => SealCount < 3
        ? "Finde die drei Seelensiegel in den bewachten Truhen."
        : "Alle Siegel gefunden! Kehre zum Nordtor zurück.";

    private readonly HashSet<EnemyController> defeated = new HashSet<EnemyController>();
    private readonly Dictionary<string, AudioClip> sounds = new Dictionary<string, AudioClip>();
    private DungeonInteractable[] interactables;
    private AudioSource audioSource;
    private float messageUntil;

    private void OnEnable()
    {
        // Unity reloads static fields when scripts change during Play mode.
        Instance = this;
    }

    private void Awake()
    {
        Instance = this;
        Time.timeScale = 0;
        Muted = PlayerPrefs.GetInt("LEA.Muted", 0) == 1;
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0;
        audioSource.volume = 0.17f;
    }

    private void Start()
    {
        if (player == null) player = FindFirstObjectByType<PlayerController>();
        TotalEnemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None).Length;
        interactables = FindObjectsByType<DungeonInteractable>(FindObjectsSortMode.None);
        if (startImmediately)
        {
            startImmediately = false;
            StartRun();
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (State == GameState.Playing) Pause();
            else if (State == GameState.Paused) Resume();
        }
        if (Input.GetKeyDown(KeyCode.Return))
        {
            if (State == GameState.Menu) StartRun();
            else if (State == GameState.Won || State == GameState.Lost) ContinueAfterResult();
        }
        if (!IsPlaying || player == null) return;
        ElapsedTime += Time.deltaTime;
        if (Time.time > messageUntil) Message = "";
        InteractionPrompt = "";
        DungeonInteractable closest = null;
        float closestDistance = 1.45f;
        foreach (var item in interactables)
        {
            if (item == null || item.IsConsumed) continue;
            float distance = Vector2.Distance(player.transform.position, item.transform.position);
            if (item.IsAutomatic && distance < 0.65f &&
                Physics2D.Linecast(player.transform.position, item.transform.position,
                    LayerMask.GetMask("DungeonWalls")).collider == null) item.TryUse();
            if (item.IsAutomatic || distance >= closestDistance) continue;
            if (Physics2D.Linecast(player.transform.position, item.transform.position,
                LayerMask.GetMask("DungeonWalls")).collider != null) continue;
            closest = item;
            closestDistance = distance;
        }
        if (closest != null)
        {
            InteractionPrompt = closest.Prompt;
            if (Input.GetKeyDown(KeyCode.E)) closest.TryUse();
        }
    }

    public void StartRun()
    {
        if (State != GameState.Menu) return;
        State = GameState.Playing;
        Time.timeScale = 1;
        ShowMessage("Besiege die Wächter, öffne ihre Truhen und finde den Ausgang.");
        PlaySound("start");
    }

    public void Pause()
    {
        if (!IsPlaying) return;
        State = GameState.Paused;
        Time.timeScale = 0;
    }

    public void Resume()
    {
        if (State != GameState.Paused) return;
        State = GameState.Playing;
        Time.timeScale = 1;
    }

    public void RestartRun()
    {
        startImmediately = true;
        Time.timeScale = 1;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void SelectLevel(int index)
    {
        if (State != GameState.Menu || index < 0 || index >= LevelScenes.Length || index == LevelIndex) return;
        startImmediately = false;
        Time.timeScale = 1;
        SceneManager.LoadScene(LevelScenes[index]);
    }

    public void ContinueAfterResult()
    {
        if (State != GameState.Won && State != GameState.Lost) return;
        if (State == GameState.Won && HasNextLevel)
        {
            startImmediately = true;
            Time.timeScale = 1;
            SceneManager.LoadScene(LevelScenes[LevelIndex + 1]);
        }
        else RestartRun();
    }

    public void ReturnToMenu()
    {
        startImmediately = false;
        Time.timeScale = 1;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void OnPlayerDied()
    {
        if (!IsPlaying) return;
        State = GameState.Lost;
        InteractionPrompt = "";
        PlaySound("death");
        // Keep the death animation running; actors gate their actions on IsPlaying.
    }

    public void OnEnemyDefeated(EnemyController enemy)
    {
        if (enemy == null || !defeated.Add(enemy)) return;
        DefeatedEnemies++;
        CoinCount += enemy.isGuardian ? 15 : 5;
        if (enemy.isGuardian) ShowMessage("Wächter besiegt! Seine Siegeltruhe ist jetzt offen.");
    }

    public void CollectSeal()
    {
        if (!IsPlaying || SealCount >= 3) return;
        SealCount++;
        CoinCount += 20;
        player.Heal(2);
        PlaySound("seal");
        ShowMessage(SealCount == 3 ? "Drei Siegel vereint. Das Nordtor wartet auf dich!"
            : "Seelensiegel " + SealCount + " / 3 gefunden. Zwei Lebenspunkte wiederhergestellt.");
    }

    public void AddCoins(int amount)
    {
        if (!IsPlaying || amount <= 0) return;
        CoinCount += amount;
        PlaySound("coin");
    }

    public bool TryEscape()
    {
        if (!IsPlaying) return false;
        if (SealCount < 3)
        {
            ShowMessage("Das Nordtor benötigt alle drei Seelensiegel.");
            return false;
        }
        State = GameState.Won;
        Time.timeScale = 0;
        InteractionPrompt = "";
        PlaySound("win");
        return true;
    }

    public void ShowMessage(string text)
    {
        Message = text;
        messageUntil = Time.time + 4.5f;
    }

    public void ToggleMute()
    {
        Muted = !Muted;
        PlayerPrefs.SetInt("LEA.Muted", Muted ? 1 : 0);
        PlayerPrefs.Save();
        if (Muted) audioSource.Stop();
        else PlaySound("coin");
    }

    public void PlaySound(string cue)
    {
        if (Muted || audioSource == null) return;
        if (!sounds.TryGetValue(cue, out AudioClip clip))
        {
            float frequency = 280, duration = 0.13f;
            switch (cue)
            {
                case "attack": frequency = 180; break;
                case "enemyAttack": frequency = 120; break;
                case "hurt": frequency = 90; duration = 0.22f; break;
                case "enemyHit": frequency = 140; break;
                case "enemyDeath": frequency = 110; duration = 0.35f; break;
                case "heal": frequency = 520; duration = 0.32f; break;
                case "coin": frequency = 880; break;
                case "seal": frequency = 660; duration = 0.5f; break;
                case "win": frequency = 740; duration = 0.9f; break;
                case "death": frequency = 160; duration = 0.7f; break;
                case "start": frequency = 440; duration = 0.35f; break;
            }
            const int rate = 22050;
            float[] samples = new float[Mathf.CeilToInt(rate * duration)];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate, progress = t / duration;
                float envelope = Mathf.Min(t * 80, 1) * (1 - progress) * (1 - progress);
                samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * t * (1 + progress * 0.22f)) * envelope;
            }
            clip = AudioClip.Create("LEA " + cue, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            sounds.Add(cue, clip);
        }
        audioSource.PlayOneShot(clip);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused && IsPlaying) Pause();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        Time.timeScale = 1;
        foreach (AudioClip clip in sounds.Values) if (clip != null) Destroy(clip);
    }
}
