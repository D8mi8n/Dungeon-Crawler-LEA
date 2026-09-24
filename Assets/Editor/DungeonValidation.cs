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
    private enum Phase { Starting, Menu, Paused, PlayingClock, AttackHit, Invulnerability, Restarting, ReturningMenu, FinalMenu, CampaignMenu, CampaignNext, Stopping }

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
    private static RigidbodyInterpolation2D targetInterpolation;
    private static int targetExpectedHealth;
    private static bool running;
    private static bool finishing;
    private static bool savedOptions;
    private static bool previousOptionsEnabled;
    private static EnterPlayModeOptions previousOptions;
    private static string reportPath;
    private static int levelIndex;

    public static void Run()
    {
        if (running) throw new InvalidOperationException("Dungeon validation is already running.");
        running = true;
        finishing = false;
        savedOptions = false;
        passed = failed = runtimeErrors = 0;
        expectedEnemies = 0;
        levelIndex = 0;
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
                    CheckLevelNavigation(game);
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
                    Report.AppendLine("  Attack sample: player=" + game.Player.transform.position
                        + "; target=" + attackTarget.transform.position + "; health=" + attackTarget.CurrentHealth
                        + "; expected=" + targetExpectedHealth + "; facing=" + game.Player.FacingDirection);
                    Report.AppendLine("  Overlaps=" + string.Join(",", Physics2D.OverlapCircleAll(
                        (Vector2)game.Player.transform.position + game.Player.FacingDirection * game.Player.attackReach,
                        game.Player.attackRadius).Select(c => c.name + ":" + c.gameObject.layer)));
                    Report.AppendLine("  Wall=" + Physics2D.Linecast(game.Player.transform.position,
                        attackTarget.transform.position, LayerMask.GetMask("DungeonWalls")).collider);
                    Report.AppendLine("  Target physics=" + attackTarget.GetComponent<Rigidbody2D>().position
                        + "; bounds=" + attackTarget.GetComponent<Collider2D>().bounds
                        + "; colliderEnabled=" + attackTarget.GetComponent<Collider2D>().enabled
                        + "; simulated=" + attackTarget.GetComponent<Rigidbody2D>().simulated);
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
                    if (levelIndex < DungeonGame.LevelScenes.Length - 1)
                    {
                        previousGameId = game.GetInstanceID();
                        game.SelectLevel(++levelIndex);
                        SetPhase(Phase.Starting);
                    }
                    else
                    {
                        previousGameId = game.GetInstanceID();
                        game.SelectLevel(0);
                        SetPhase(Phase.CampaignMenu);
                    }
                    break;

                case Phase.CampaignMenu:
                    if (!NewSceneReady(game) || !Waited(.1)) return;
                    Check(game.LevelIndex == 0 && game.State == DungeonGame.GameState.Menu, "Level selection returns to the first level menu");
                    game.StartRun();
                    ValidateSealsAndWin(game);
                    previousGameId = game.GetInstanceID();
                    game.ContinueAfterResult();
                    SetPhase(Phase.CampaignNext);
                    break;

                case Phase.CampaignNext:
                    if (!NewSceneReady(game) || !Waited(.1)) return;
                    Check(game.State == DungeonGame.GameState.Playing, "Continue starts the next level directly");
                    Check(game.SealCount == 0 && game.Player.CurrentHealth == game.Player.MaxHealth, "Next level resets seals and health");
                    ValidateSealsAndWin(game);
                    if (game.HasNextLevel)
                    {
                        previousGameId = game.GetInstanceID();
                        game.ContinueAfterResult();
                        SetPhase(Phase.CampaignNext);
                    }
                    else Finish();
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
        DungeonInteractable[] exits = items.Where(item => item.kind == DungeonInteractable.ItemKind.Exit).ToArray();
        Require(chests.Length == 3, "Scene contains exactly three seal chests");
        Require(exits.Length == 1, "Scene contains exactly one exit");
        DungeonInteractable exit = exits[0];
        DungeonExitGate[] activeGates = UnityEngine.Object.FindObjectsByType<DungeonExitGate>(FindObjectsSortMode.None)
            .Where(candidate => candidate.isActiveAndEnabled).ToArray();
        Require(activeGates.Length == 1, "Scene contains exactly one active exit gate");
        DungeonExitGate gate = activeGates[0];
        Require(gate.gameObject == exit.gameObject && gate.gateRenderer != null && gate.closedSprite != null
            && gate.openSprite != null && gate.closedSprite != gate.openSprite,
            "Exit owns the gate renderer and distinct closed/open sprites");
        SpriteRenderer renderer = gate.gateRenderer;
        Check(renderer.transform != exit.transform && renderer.transform.IsChildOf(exit.transform)
            && GateVisibleNearExit(Camera.main, exit, renderer),
            "Level " + game.LevelIndex + " has a visible child gate sprite when approaching the exit");
        Check(gate.GetComponentsInChildren<SpriteAnimationLoop>(true).All(loop => !loop.enabled),
            "Gate prefab animation loops are disabled so the seal state controls its appearance");
        gate.RefreshVisual();
        Check(!gate.IsUnlocked && renderer.sprite == gate.closedSprite,
            "Level " + game.LevelIndex + " gate displays its closed appearance with zero seals");
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
            if (i < 2) Check(!exit.TryUse() && game.State == DungeonGame.GameState.Playing,
                "Exit remains locked with " + (i + 1) + " seals");
        }

        int allSealCoins = game.CoinCount;
        game.CollectSeal();
        Check(game.SealCount == 3 && game.CoinCount == allSealCoins, "Seal count and rewards are capped at three");
        gate.RefreshVisual();
        Check(gate.IsUnlocked && gate.gateRenderer.sprite == gate.openSprite,
            "Level " + game.LevelIndex + " gate displays its open appearance with all three seals");
        Check(exit.TryUse() && game.State == DungeonGame.GameState.Won, "Three seals allow exit and enter Won");
        Check(Mathf.Approximately(Time.timeScale, 0), "Victory freezes the completed run");
    }

    private static bool GateVisibleNearExit(Camera camera, DungeonInteractable exit, SpriteRenderer renderer)
    {
        if (camera == null || !camera.isActiveAndEnabled || !renderer.enabled
            || !renderer.gameObject.activeInHierarchy || renderer.sprite == null || renderer.color.a <= 0
            || (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0) return false;

        Vector3 originalPosition = camera.transform.position;
        try
        {
            DungeonCamera follow = camera.GetComponent<DungeonCamera>();
            if (follow != null && follow.isActiveAndEnabled && follow.target != null)
            {
                // Evaluate the settled follow-camera frame near the interaction point. The spawn
                // can be far away in larger levels; no player movement or gameplay frame is needed.
                float halfHeight = camera.orthographicSize;
                float halfWidth = halfHeight * camera.aspect;
                Vector3 nearExit = exit.transform.position;
                nearExit.x = follow.maximum.x - follow.minimum.x > halfWidth * 2
                    ? Mathf.Clamp(nearExit.x, follow.minimum.x + halfWidth, follow.maximum.x - halfWidth)
                    : (follow.minimum.x + follow.maximum.x) / 2;
                nearExit.y = follow.maximum.y - follow.minimum.y > halfHeight * 2
                    ? Mathf.Clamp(nearExit.y, follow.minimum.y + halfHeight, follow.maximum.y - halfHeight)
                    : (follow.minimum.y + follow.maximum.y) / 2;
                nearExit.z = originalPosition.z;
                camera.transform.position = nearExit;
            }
            return GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(camera), renderer.bounds);
        }
        finally
        {
            camera.transform.position = originalPosition;
        }
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
        if (body != null)
        {
            targetInterpolation = body.interpolation;
            body.interpolation = RigidbodyInterpolation2D.None;
            body.position = origin + direction * 0.8f;
            body.linearVelocity = Vector2.zero;
        }
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
        if (body != null)
        {
            body.position = targetOriginalPosition;
            body.linearVelocity = Vector2.zero;
            body.interpolation = targetInterpolation;
        }
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
        Check(follow == null || follow.target == game.Player.transform, "Camera is fixed for the compact dungeon or follows the active player");
        DungeonHUD hud = UnityEngine.Object.FindFirstObjectByType<DungeonHUD>();
        Check(hud != null && hud.panelSprite != null && hud.buttonNormal != null && hud.buttonHover != null
            && hud.buttonPressed != null && hud.characterFrame != null && hud.actionPanel != null
            && hud.sealIcon != null && hud.coinIcon != null && hud.swordIcon != null
            && hud.healthIcon != null && hud.soundOnIcon != null && hud.soundOffIcon != null
            && hud.starIcon != null && hud.defeatIcon != null && hud.movementIcon != null,
            "All fifteen PNG_UI sprite references are assigned");
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
            if (actor is EnemyController enemy)
                Check(enemy.HealthBarWorldPosition.y > sprite.bounds.max.y,
                    actor.name + " health bar sits above the entire animated sprite");
            Collider2D[] bodies = actor.GetComponents<Collider2D>().Where(body => body.enabled && !body.isTrigger).ToArray();
            Check(bodies.Length > 0, actor.name + " has a solid body collider");
            bool free = true;
            foreach (Collider2D body in bodies)
            {
                overlaps.Clear();
                body.Overlap(filter, overlaps);
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
        Check(SceneManager.GetActiveScene().path == DungeonLevelBuild.PathFor(levelIndex), "Runtime uses the selected level scene");
        Check(!DungeonHUD.BlocksWorldPointer(new Vector2(Screen.width*.5f,Screen.height*.06f)), "Removed footer does not block mouse attacks");
    }

    private static void CheckLevelNavigation(DungeonGame game)
    {
        const float playerRadius = 0.27f;
        const float interactionDistance = 1.2f;
        const int maximumPoints = 20000;
        int wallMask = LayerMask.GetMask("DungeonWalls");
        Vector2 spawn = game.Player.transform.position;
        DungeonInteractable[] targets = UnityEngine.Object.FindObjectsByType<DungeonInteractable>(FindObjectsSortMode.None)
            .Where(item => item.kind == DungeonInteractable.ItemKind.SealChest || item.kind == DungeonInteractable.ItemKind.Exit)
            .OrderBy(item => item.name).ToArray();
        var bounds = new Bounds(spawn, Vector3.zero);
        foreach (DungeonInteractable target in targets) bounds.Encapsulate(target.transform.position);
        foreach (Collider2D wall in UnityEngine.Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
            if (wall.enabled && !wall.isTrigger && (wallMask & (1 << wall.gameObject.layer)) != 0)
                bounds.Encapsulate(wall.bounds);
        bounds.Expand(1f);

        // Anchor the lattice to the exact spawn; keep a fine sample without unbounded editor work.
        float step = 0.25f;
        int left, bottom, width, height;
        do
        {
            left = Mathf.FloorToInt((bounds.min.x - spawn.x) / step);
            bottom = Mathf.FloorToInt((bounds.min.y - spawn.y) / step);
            width = Mathf.CeilToInt((bounds.max.x - spawn.x) / step) - left + 1;
            height = Mathf.CeilToInt((bounds.max.y - spawn.y) / step) - bottom + 1;
            if ((long)width * height <= maximumPoints) break;
            step += 0.025f;
        } while (true);

        Vector2 origin = spawn + new Vector2(left * step, bottom * step);
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(wallMask);
        var overlap = new Collider2D[1];
        var sweep = new RaycastHit2D[1];
        var walkable = new bool[width * height];
        var reached = new bool[walkable.Length];
        Physics2D.SyncTransforms();
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            walkable[y * width + x] = Physics2D.OverlapCircle(
                origin + new Vector2(x * step, y * step), playerRadius, filter, overlap) == 0;

        int first = -bottom * width - left;
        bool spawnClear = walkable[first];
        Check(spawnClear, "Level " + game.LevelIndex + " navigation starts with a clear player footprint");
        var frontier = new Queue<int>();
        if (spawnClear)
        {
            reached[first] = true;
            frontier.Enqueue(first);
        }
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        int visited = 0;
        var closest = Enumerable.Repeat(float.PositiveInfinity, targets.Length).ToArray();
        var closestVisible = Enumerable.Repeat(float.PositiveInfinity, targets.Length).ToArray();
        while (frontier.Count != 0)
        {
            int current = frontier.Dequeue();
            int x = current % width, y = current / width;
            Vector2 point = origin + new Vector2(x * step, y * step);
            visited++;
            for (int i = 0; i < targets.Length; i++)
            {
                Vector2 interactionPoint = targets[i].transform.position;
                float distance = Vector2.Distance(point, interactionPoint);
                closest[i] = Mathf.Min(closest[i], distance);
                // Match gameplay: being nearby is insufficient when a wall blocks interaction.
                if (distance <= interactionDistance && distance < closestVisible[i]
                    && Physics2D.Linecast(point, interactionPoint, wallMask).collider == null)
                    closestVisible[i] = distance;
            }

            foreach (Vector2Int direction in directions)
            {
                int nextX = x + direction.x, nextY = y + direction.y;
                if (nextX < 0 || nextX >= width || nextY < 0 || nextY >= height) continue;
                int next = nextY * width + nextX;
                if (reached[next] || !walkable[next]) continue;
                // Sweeps prevent crossing a thin obstacle between two otherwise clear samples.
                if (Physics2D.CircleCast(point, playerRadius, (Vector2)direction, filter, sweep, step) != 0) continue;
                reached[next] = true;
                frontier.Enqueue(next);
            }
        }

        Report.AppendLine("  Navigation: level=" + game.LevelIndex + "; spawn=" + spawn
            + "; raster=" + width + "x" + height + "; step=" + step.ToString("F3")
            + "; reachable=" + visited + "; bounds=" + bounds);
        for (int i = 0; i < targets.Length; i++)
        {
            bool accessible = closestVisible[i] <= interactionDistance;
            Check(accessible, "Level " + game.LevelIndex + " has a continuous walkable route and clear interaction line to " + targets[i].name);
            if (!accessible)
                Report.AppendLine("  Unreachable interaction target: " + targets[i].name + "; position="
                    + targets[i].transform.position + "; nearest reachable distance=" + closest[i].ToString("F2")
                    + "; nearest unobstructed interaction distance=" + closestVisible[i].ToString("F2"));
        }
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
        if (stack != null && stack.Contains("UnityEditor.Search.SearchDatabase") && !stack.Contains("Assets/"))
        {
            Report.AppendLine("EDITOR SEARCH DIAGNOSTIC (not gameplay): " + message);
            Report.AppendLine(stack);
            return;
        }
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
