using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// End-to-end smoke test of the authored scene and public gameplay APIs.
/// Run Unity with -batchmode -executeMethod DungeonValidation.Run, without -quit.
/// The editor update loop keeps rendering/physics and scene loads alive between checks.
/// </summary>
public static class DungeonValidation
{
    private enum Phase { Starting, Menu, Paused, PlayingClock, AttackHit, Invulnerability, Restarting, ReturningMenu, FinalMenu, Stopping }

    private static readonly StringBuilder Report = new StringBuilder();
    private static Phase phase;
    private static double startedAt;
    private static double phaseStartedAt;
    private static int phaseFrame;
    private static int passed;
    private static int failed;
    private static int runtimeErrors;
    private static int expectedEnemies;
    private static int previousGameId;
    private static int previousPlayerId;
    private static float pausedElapsed;
    private static float hurtAt;
    private static float attackAt;
    private static EnemyController attackTarget;
    private static Vector3 targetOriginalPosition;
    private static bool targetOriginallyEnabled;
    private static int targetExpectedHealth;
    private static bool running;
    private static bool finishing;
    private static bool savedOptions;
    private static bool previousOptionsEnabled;
    private static EnterPlayModeOptions previousOptions;
    private static string reportPath;

    public static void Run()
    {
        if (running) throw new InvalidOperationException("Dungeon validation is already running.");
        running = true;
        finishing = false;
        savedOptions = false;
        passed = failed = runtimeErrors = 0;
        expectedEnemies = 0;
        Report.Clear();
        Report.AppendLine("LEA PLAYMODE VALIDATION");
        Report.AppendLine("Started: " + DateTime.UtcNow.ToString("O"));
        Report.AppendLine("Unity: " + Application.unityVersion);
        Report.AppendLine("Scene: Assets/Scenes/LEA.unity");
        Report.AppendLine();
        startedAt = EditorApplication.timeSinceStartup;
        reportPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Artifacts/validation-results.txt"));

        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Start validation from edit mode.");

            previousOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            previousOptions = EditorSettings.enterPlayModeOptions;
            savedOptions = true;
            // Retain this runner's callbacks while still reloading the authored scene normally.
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.OpenScene("Assets/Scenes/LEA.unity", OpenSceneMode.Single);
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            Application.logMessageReceived += LogReceived;
            SetPhase(Phase.Starting);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception exception)
        {
            RecordException(exception);
            Finish();
        }
    }

    private static void Tick()
    {
        if (!running) return;
        try
        {
            if (EditorApplication.timeSinceStartup - startedAt > 90 ||
                EditorApplication.timeSinceStartup - phaseStartedAt > 25)
            {
                Check(false, "Timeout in phase " + phase);
                if (finishing) CompleteAndExit();
                else Finish();
                return;
            }
            if (finishing || !EditorApplication.isPlaying) return;

            DungeonGame game = DungeonGame.Instance;
            switch (phase)
            {
                case Phase.Starting:
                    if (game == null || game.Player == null || Time.frameCount < 3) return;
                    CheckReferencesAndSpawns(game);
                    expectedEnemies = game.TotalEnemies;
                    CheckFreshRun(game, DungeonGame.GameState.Menu);
                    pausedElapsed = game.ElapsedTime;
                    SetPhase(Phase.Menu);
                    break;

                case Phase.Menu:
                    if (!Waited(0.15)) return;
                    Check(Mathf.Approximately(game.ElapsedTime, pausedElapsed), "Menu does not advance the run timer");
                    game.StartRun();
                    Check(game.State == DungeonGame.GameState.Playing && Mathf.Approximately(Time.timeScale, 1), "Start enters Playing and resumes time");
                    game.Pause();
                    Check(game.State == DungeonGame.GameState.Paused && Mathf.Approximately(Time.timeScale, 0), "Pause freezes the run");
                    pausedElapsed = game.ElapsedTime;
                    int pausedHealth = game.Player.CurrentHealth;
                    game.Player.TakeDamage(1);
                    Check(game.Player.CurrentHealth == pausedHealth, "Paused player ignores damage");
                    SetPhase(Phase.Paused);
                    break;

                case Phase.Paused:
                    if (!Waited(0.15)) return;
                    Check(Mathf.Approximately(game.ElapsedTime, pausedElapsed), "Paused timer remains unchanged across frames");
                    game.Resume();
                    Check(game.State == DungeonGame.GameState.Playing && Mathf.Approximately(Time.timeScale, 1), "Resume returns to Playing");
                    SetPhase(Phase.PlayingClock);
                    break;

                case Phase.PlayingClock:
                    if (!Waited(0.15)) return;
                    Check(game.ElapsedTime > pausedElapsed, "Playing advances the timer");
                    PrepareAttackTest(game);
                    SetPhase(Phase.AttackHit);
                    break;

                case Phase.AttackHit:
                    if (Time.time - attackAt < 0.35f || !Waited(0.05)) return;
                    Check(attackTarget != null && attackTarget.CurrentHealth == targetExpectedHealth,
                        "Player attack applies one point of damage through its delayed collider hit");
                    RestoreAttackTarget();
                    int maximum = game.Player.MaxHealth;
                    game.Player.TakeDamage(1);
                    Check(game.Player.CurrentHealth == maximum - 1, "One hit removes one health point");
                    hurtAt = Time.time;
                    game.Player.TakeDamage(1);
                    Check(game.Player.CurrentHealth == maximum - 1, "Immediate second hit is blocked by invulnerability");
                    Check(game.Player.Heal(50) && game.Player.CurrentHealth == maximum, "Healing restores health and clamps to the maximum");
                    Check(!game.Player.Heal(1), "Healing at full health returns false");
                    game.Player.TakeDamage(0);
                    game.Player.TakeDamage(-1);
                    Check(game.Player.CurrentHealth == maximum, "Nonpositive damage does not alter health");
                    SetPhase(Phase.Invulnerability);
                    break;

                case Phase.Invulnerability:
                    if (Time.time - hurtAt < 1.05f || !Waited(0.05)) return;
                    game.Player.TakeDamage(1);
                    Check(game.Player.CurrentHealth == game.Player.MaxHealth - 1, "Damage applies again after invulnerability expires");
                    game.Player.Heal(game.Player.MaxHealth);
                    ValidateSealsAndWin(game);
                    previousGameId = game.GetInstanceID();
                    previousPlayerId = game.Player.GetInstanceID();
                    game.RestartRun();
                    SetPhase(Phase.Restarting);
                    break;

                case Phase.Restarting:
                    if (!NewSceneReady(game) || !Waited(0.10)) return;
                    CheckFreshRun(game, DungeonGame.GameState.Playing);
                    CheckReferencesAndSpawns(game);
                    Check(game.Player.GetInstanceID() != previousPlayerId, "Restart creates a new player instance");
                    Check(UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None)
                        .All(enemy => !enemy.IsDead && enemy.CurrentHealth > 0), "Restart restores all enemies alive");
                    Check(UnityEngine.Object.FindObjectsByType<DungeonInteractable>(FindObjectsSortMode.None)
                        .Where(item => item.kind == DungeonInteractable.ItemKind.SealChest).All(item => !item.IsConsumed), "Restart resets every seal chest");
                    game.Player.TakeDamage(game.Player.MaxHealth + 1);
                    Check(game.Player.IsDead && game.Player.CurrentHealth == 0, "Lethal damage kills the player and clamps health to zero");
                    Check(game.State == DungeonGame.GameState.Lost, "Player death enters Lost");
                    game.OnPlayerDied();
                    Check(game.State == DungeonGame.GameState.Lost, "Repeated death notification preserves Lost");
                    previousGameId = game.GetInstanceID();
                    game.ReturnToMenu();
                    SetPhase(Phase.ReturningMenu);
                    break;

                case Phase.ReturningMenu:
                    if (!NewSceneReady(game) || !Waited(0.10)) return;
                    CheckFreshRun(game, DungeonGame.GameState.Menu);
                    pausedElapsed = game.ElapsedTime;
                    SetPhase(Phase.FinalMenu);
                    break;

                case Phase.FinalMenu:
                    if (!Waited(0.15)) return;
                    Check(game.State == DungeonGame.GameState.Menu && Mathf.Approximately(Time.timeScale, 0), "Return to menu stays in Menu with time frozen");
                    Check(Mathf.Approximately(game.ElapsedTime, pausedElapsed), "Returned menu does not advance time");
                    Finish();
                    break;
            }
        }
        catch (Exception exception)
        {
            RecordException(exception);
            Finish();
        }
    }

    private static void ValidateSealsAndWin(DungeonGame game)
    {
        DungeonInteractable[] items = UnityEngine.Object.FindObjectsByType<DungeonInteractable>(FindObjectsSortMode.None);
        DungeonInteractable[] chests = items.Where(item => item.kind == DungeonInteractable.ItemKind.SealChest).ToArray();
        DungeonInteractable exit = items.FirstOrDefault(item => item.kind == DungeonInteractable.ItemKind.Exit);
        Require(chests.Length == 3, "Scene contains exactly three seal chests");
        Require(exit != null, "Scene contains the exit");
        Require(chests.All(chest => chest.guardian != null && !chest.guardian.IsDead), "Each seal chest references a living guardian");
        Check(chests.Select(chest => chest.guardian).Distinct().Count() == 3, "Every chest has its own guardian");

        foreach (DungeonInteractable chest in chests)
            Check(!chest.TryUse() && !chest.IsConsumed && game.SealCount == 0, "Living guardian blocks chest " + chest.name);
        Check(!exit.TryUse() && game.State == DungeonGame.GameState.Playing, "Exit interaction rejects a run without all seals");
        Check(!game.TryEscape(), "Escape API rejects a run without all seals");

        for (int i = 0; i < chests.Length; i++)
        {
            DungeonInteractable chest = chests[i];
            EnemyController guardian = chest.guardian;
            int beforeKills = game.DefeatedEnemies;
            int beforeCoins = game.CoinCount;
            guardian.TakeDamage(10000, game.Player.transform.position);
            Check(guardian.IsDead && guardian.CurrentHealth == 0, "Guardian " + (i + 1) + " dies from lethal damage");
            Check(game.DefeatedEnemies == beforeKills + 1, "Guardian death increments defeated count once");
            Check(game.CoinCount == beforeCoins + 15, "Guardian death grants its coin reward");
            guardian.TakeDamage(10000, game.Player.transform.position);
            game.OnEnemyDefeated(guardian);
            Check(game.DefeatedEnemies == beforeKills + 1 && game.CoinCount == beforeCoins + 15,
                "Repeated death and callback cannot duplicate rewards");

            Check(chest.TryUse() && chest.IsConsumed && game.SealCount == i + 1, "Defeated guardian unlocks seal " + (i + 1));
            int collectedCoins = game.CoinCount;
            Check(!chest.TryUse() && game.SealCount == i + 1 && game.CoinCount == collectedCoins, "Seal chest can only be collected once");
            if (i < 2) Check(!exit.TryUse(), "Exit remains locked with " + (i + 1) + " seals");
        }

        int allSealCoins = game.CoinCount;
        game.CollectSeal();
        Check(game.SealCount == 3 && game.CoinCount == allSealCoins, "Seal count and rewards are capped at three");
        Check(exit.TryUse() && game.State == DungeonGame.GameState.Won, "Three seals allow exit and enter Won");
        Check(Mathf.Approximately(Time.timeScale, 0), "Victory freezes the completed run");
    }

    private static void PrepareAttackTest(DungeonGame game)
    {
        attackTarget = UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None)
            .FirstOrDefault(enemy => !enemy.isGuardian && !enemy.IsDead && enemy.CurrentHealth > 1);
        Require(attackTarget != null, "A normal enemy is available for the real attack test");
        targetOriginalPosition = attackTarget.transform.position;
        targetOriginallyEnabled = attackTarget.enabled;
        targetExpectedHealth = attackTarget.CurrentHealth - 1;
        // Keep this target still so AI decisions cannot make the combat assertion nondeterministic.
        attackTarget.enabled = false;
        Vector2 origin = game.Player.transform.position;
        Vector2 direction = Vector2.zero;
        int wallMask = LayerMask.GetMask("DungeonWalls");
        Vector2[] candidates = { Vector2.up, Vector2.right, Vector2.left, Vector2.down };
        foreach (Vector2 candidate in candidates)
        {
            Vector2 destination = origin + candidate * 0.8f;
            if (Physics2D.OverlapCircle(destination, 0.3f, wallMask) == null &&
                Physics2D.Linecast(origin, destination, wallMask).collider == null)
            {
                direction = candidate;
                break;
            }
        }
        Require(direction != Vector2.zero, "Player spawn has room for an unobstructed melee test");
        attackTarget.transform.position = origin + direction * 0.8f;
        Rigidbody2D body = attackTarget.GetComponent<Rigidbody2D>();
        if (body != null) body.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
        attackAt = Time.time;
        Check(game.Player.TryAttack(direction), "Player accepts a ready melee attack");
        Check(!game.Player.TryAttack(direction), "Immediate second melee attack is blocked by cooldown");
    }

    private static void RestoreAttackTarget()
    {
        if (attackTarget == null) return;
        attackTarget.transform.position = targetOriginalPosition;
        Rigidbody2D body = attackTarget.GetComponent<Rigidbody2D>();
        if (body != null) body.linearVelocity = Vector2.zero;
        attackTarget.enabled = targetOriginallyEnabled;
        attackTarget = null;
        Physics2D.SyncTransforms();
    }

    private static void CheckFreshRun(DungeonGame game, DungeonGame.GameState expectedState)
    {
        Check(game.State == expectedState, "Fresh scene enters " + expectedState);
        Check(game.Player.CurrentHealth == game.Player.MaxHealth && game.Player.MaxHealth == 6, "Fresh player starts with six health points");
        Check(game.SealCount == 0 && game.CoinCount == 0 && game.DefeatedEnemies == 0, "Fresh run resets seals, coins and defeated count");
        Check(game.TotalEnemies == expectedEnemies && expectedEnemies > 0, "Fresh run restores the complete enemy roster");
        Check(game.ElapsedTime >= 0 && game.ElapsedTime < 1, "Fresh run starts with a new timer");
        Check(Mathf.Approximately(Time.timeScale, expectedState == DungeonGame.GameState.Menu ? 0 : 1), "Fresh scene has the correct time scale");
    }

    private static void CheckReferencesAndSpawns(DungeonGame game)
    {
        Require(game.Player != null, "Game has a player reference");
        Check(UnityEngine.Object.FindObjectsByType<DungeonGame>(FindObjectsSortMode.None).Length == 1, "Scene has exactly one game controller");
        Check(UnityEngine.Object.FindFirstObjectByType<DungeonHUD>() != null, "Scene has its HUD");
        Camera camera = Camera.main;
        Check(camera != null && camera.orthographic, "Scene has an orthographic main camera");
        DungeonCamera follow = camera != null ? camera.GetComponent<DungeonCamera>() : null;
        Check(follow != null && follow.target == game.Player.transform, "Camera follows the active player");
        Check(game.GetComponent<AudioSource>() != null, "Game has its runtime audio source");

        List<Component> actors = new List<Component> { game.Player };
        EnemyController[] enemies = UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        actors.AddRange(enemies);
        Check(enemies.Length == game.TotalEnemies && enemies.Length > 0, "Enemy count matches live scene actors");
        int wallMask = LayerMask.GetMask("DungeonWalls");
        Require(wallMask != 0, "DungeonWalls physics layer exists");
        Physics2D.SyncTransforms();
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(wallMask);
        var overlaps = new List<Collider2D>();

        foreach (Component actor in actors)
        {
            Animator animator = actor.GetComponent<Animator>();
            SpriteRenderer sprite = actor.GetComponent<SpriteRenderer>();
            Check(actor.GetComponent<Rigidbody2D>() != null, actor.name + " has a rigidbody");
            Check(animator != null && animator.runtimeAnimatorController != null, actor.name + " has an animation controller");
            Check(sprite != null && sprite.sprite != null, actor.name + " has a visible sprite reference");
            Collider2D[] bodies = actor.GetComponents<Collider2D>().Where(body => body.enabled && !body.isTrigger).ToArray();
            Check(bodies.Length > 0, actor.name + " has a solid body collider");
            bool free = true;
            foreach (Collider2D body in bodies)
            {
                overlaps.Clear();
                body.OverlapCollider(filter, overlaps);
                if (overlaps.Count != 0)
                {
                    free = false;
                    Report.AppendLine("  Wall overlaps: " + string.Join(", ", overlaps.Select(wall => wall.name)));
                }
            }
            Check(free, actor.name + " spawns clear of DungeonWalls");
        }

        bool missingScript = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .Any(transform => transform.GetComponents<MonoBehaviour>().Any(component => component == null));
        Check(!missingScript, "Scene contains no missing script references");
        Check(SceneManager.GetActiveScene().path == "Assets/Scenes/LEA.unity", "Runtime uses the intended LEA scene");
    }

    private static bool NewSceneReady(DungeonGame game)
    {
        return game != null && game.GetInstanceID() != previousGameId && game.Player != null && game.TotalEnemies > 0;
    }

    private static void SetPhase(Phase next)
    {
        phase = next;
        phaseStartedAt = EditorApplication.timeSinceStartup;
        phaseFrame = Time.frameCount;
    }

    private static bool Waited(double seconds)
    {
        return EditorApplication.timeSinceStartup - phaseStartedAt >= seconds && Time.frameCount - phaseFrame >= 2;
    }

    private static void Check(bool condition, string description)
    {
        if (condition) passed++;
        else failed++;
        string line = (condition ? "PASS " : "FAIL ") + description;
        Report.AppendLine(line);
        Debug.Log("[LEA validation] " + line);
    }

    private static void Require(bool condition, string description)
    {
        Check(condition, description);
        if (!condition) throw new InvalidOperationException("Required validation precondition failed: " + description);
    }

    private static void LogReceived(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        runtimeErrors++;
        Report.AppendLine("RUNTIME " + type + ": " + message);
        if (!string.IsNullOrEmpty(stack)) Report.AppendLine(stack);
    }

    private static void RecordException(Exception exception)
    {
        failed++;
        Report.AppendLine("FAIL Exception in phase " + phase + ": " + exception);
    }

    private static void PlayModeChanged(PlayModeStateChange state)
    {
        if (!running) return;
        if (state == PlayModeStateChange.ExitingPlayMode && !finishing)
        {
            Check(false, "Play mode ended before validation completed");
            finishing = true;
            SetPhase(Phase.Stopping);
        }
        if (state == PlayModeStateChange.EnteredEditMode && finishing) CompleteAndExit();
    }

    private static void Finish()
    {
        if (finishing) return;
        finishing = true;
        RestoreAttackTarget();
        SetPhase(Phase.Stopping);
        if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode();
        else CompleteAndExit();
    }

    private static void CompleteAndExit()
    {
        if (!running) return;
        running = false;
        try
        {
            Check(runtimeErrors == 0, "Play mode produced no errors or exceptions (observed: " + runtimeErrors + ")");
            Report.AppendLine();
            Report.AppendLine("RESULT: " + (failed == 0 ? "PASS" : "FAIL"));
            Report.AppendLine("Assertions passed: " + passed + "; failed: " + failed);
            Report.AppendLine("Duration: " + (EditorApplication.timeSinceStartup - startedAt).ToString("F2") + " seconds");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, Report.ToString(), new UTF8Encoding(false));
            Debug.Log("[LEA validation] Report written to " + reportPath);
        }
        catch (Exception exception)
        {
            failed++;
            Debug.LogException(exception);
        }
        finally
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            Application.logMessageReceived -= LogReceived;
            if (savedOptions)
            {
                EditorSettings.enterPlayModeOptions = previousOptions;
                EditorSettings.enterPlayModeOptionsEnabled = previousOptionsEnabled;
            }
            EditorApplication.Exit(failed == 0 ? 0 : 1);
        }
    }
}
