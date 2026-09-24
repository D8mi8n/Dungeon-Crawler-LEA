using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Reproducibly assembles the playable LEA scene from the preserved Dungeon artwork.
/// Prepare intentionally rebuilds LEA.unity; edit Dungeon.unity or this recipe for lasting changes.
/// </summary>
public static class DungeonBuild
{
    public const string ScenePath = "Assets/Scenes/LEA.unity";
    private const string PlayerControllerPath = "Assets/Animations/Player/LEA_Player.controller";
    private const string EnemyControllerPath = "Assets/Animations/Player/LEA_Enemy.controller";
    private const string SpriteFolder = "Assets/Sprites/PNG/";
    private static readonly string[] Actions = { "Idle", "Walk", "Run", "Attack", "Hurt", "Death" };
    private static readonly string[] Directions = { "D", "U", "L", "R" };

    [MenuItem("LEA/Spielszene vorbereiten")]
    public static void Prepare()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Die Spielszene kann nur außerhalb des Spielmodus vorbereitet werden.");

        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Dungeon.unity", OpenSceneMode.Single);
        // Keep source scene and author-added test actors intact in the original asset.
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new InvalidOperationException("Die Spielszene konnte nicht als LEA gespeichert werden.");
        foreach (PlayerController sourcePlayer in UnityEngine.Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            sourcePlayer.gameObject.SetActive(false);
        int wallLayer = EnsureWallLayer();
        foreach (Collider2D collider in UnityEngine.Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
            collider.gameObject.layer = wallLayer;

        Transform gameplay = new GameObject("LEA Gameplay").transform;
        var southWall = new GameObject("Südlicher Kartenabschluss");
        southWall.transform.SetParent(gameplay);
        southWall.transform.position = new Vector3(0f, -6.05f, 0f);
        southWall.layer = wallLayer;
        southWall.AddComponent<BoxCollider2D>().size = new Vector2(3f, 0.4f);

        AnimatorController playerAnimations = CreateController(PlayerControllerPath, 2);
        AnimatorController enemyAnimations = CreateController(EnemyControllerPath, 3);
        Material spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
        if (spriteMaterial == null)
            throw new InvalidOperationException("Das URP-Sprite-Material fehlt.");

        // The source art uses lit sprite materials, but the original scene has no 2D light.
        var light = new GameObject("LEA Globales Licht").AddComponent<Light2D>();
        light.transform.SetParent(gameplay);
        light.lightType = Light2D.LightType.Global;
        light.color = new Color(0.91f, 0.96f, 1f);
        light.intensity = 1f;

        Camera camera = Camera.main;
        if (camera == null) throw new InvalidOperationException("Dungeon benötigt eine Main Camera.");
        if (camera.GetComponent<AudioListener>() == null) camera.gameObject.AddComponent<AudioListener>();
        camera.transform.position = new Vector3(0f, -0.7f, -10f);
        camera.orthographic = true;
        camera.orthographicSize = 6.4f;
        camera.backgroundColor = new Color(0.02f, 0.04f, 0.055f);
        var cameraFollow = camera.GetComponent<DungeonCamera>();
        if (cameraFollow != null) UnityEngine.Object.DestroyImmediate(cameraFollow);

        var playerObject = CreateActor("Player", new Vector2(0f, -4.8f), 2, playerAnimations, spriteMaterial, gameplay);
        playerObject.tag = "Player";
        var player = playerObject.AddComponent<PlayerController>();
        player.maxHealth = 6;
        player.walkSpeed = 2.8f;
        player.runSpeed = 4.5f;
        player.skeleton = 2;

        EnemyController leftGuardian = CreateEnemy("Wächter West", new Vector2(-4.8f, 0.2f), true, enemyAnimations, spriteMaterial, gameplay);
        EnemyController rightGuardian = CreateEnemy("Wächter Ost", new Vector2(4.8f, 0.2f), true, enemyAnimations, spriteMaterial, gameplay);
        EnemyController northGuardian = CreateEnemy("Wächter Nord", new Vector2(-1.9f, 2.5f), true, enemyAnimations, spriteMaterial, gameplay);
        CreateEnemy("Kryptenwache West", new Vector2(-2f, 0f), false, enemyAnimations, spriteMaterial, gameplay);
        CreateEnemy("Kryptenwache Ost", new Vector2(2f, 0f), false, enemyAnimations, spriteMaterial, gameplay);

        Sprite closedChest = LoadSprite("chest_lever.png", "chest_lever_0");
        Sprite openedChest = LoadSprite("chest_lever.png", "chest_lever_6");
        CreateChest("Siegeltruhe West", new Vector2(-5.8f, 0.3f), leftGuardian, closedChest, openedChest, spriteMaterial, gameplay);
        CreateChest("Siegeltruhe Ost", new Vector2(5.8f, 0.3f), rightGuardian, closedChest, openedChest, spriteMaterial, gameplay);
        CreateChest("Siegeltruhe Nord", new Vector2(-2f, 3.5f), northGuardian, closedChest, openedChest, spriteMaterial, gameplay);

        DungeonGateBuild.Configure(gameplay);

        Sprite potion = CreatePotionSprite();
        foreach (float y in new[] { -2.5f, -4f })
        {
            var pickup = CreateSprite("Heiltrank", new Vector2(0f, y), potion, 0.65f, 12, spriteMaterial, gameplay);
            var interactable = pickup.AddComponent<DungeonInteractable>();
            interactable.kind = DungeonInteractable.ItemKind.Potion;
            interactable.amount = 2;
        }

        Sprite trapSprite = LoadSprite("plate_trap.png", "plate_trap_0");
        for (int i = 0; i < 2; i++)
        {
            var trap = CreateSprite(i == 0 ? "Bodenfalle West" : "Bodenfalle Ost", new Vector2(i == 0 ? -3f : 3f, 0f),
                trapSprite, 0.42f, 8, spriteMaterial, gameplay);
            trap.AddComponent<DungeonTrap>().phaseOffset = i * 2.25f;
        }

        var systems = new GameObject("LEA Spielsteuerung");
        systems.transform.SetParent(gameplay);
        systems.AddComponent<DungeonGame>().player = player;
        ConfigureHud(systems.AddComponent<DungeonHUD>());

        Physics2D.SyncTransforms();
        ValidateSpawn(player.transform.position, wallLayer, "Player");
        foreach (EnemyController enemy in UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            ValidateSpawn(enemy.transform.position, wallLayer, enemy.name);

        DungeonLevelBuild.UpdateBuildSettings();
        PlayerSettings.companyName = "LEA";
        PlayerSettings.productName = "LEA – Die vergessene Krypta";
        PlayerSettings.bundleVersion = "1.0";
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = false;
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new InvalidOperationException("LEA.unity konnte nicht gespeichert werden.");
        Debug.Log("LEA ist spielbereit: 3 Siegeltruhen, 5 Gegner, 2 Heiltränke, 2 ausweichbare Fallen und Nordtor. Dungeon.unity blieb unverändert.");
    }

    [MenuItem("LEA/Windows-Version bauen")]
    public static void BuildWindows()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) Prepare();
        var preparedScene = SceneManager.GetSceneByPath(ScenePath);
        if (preparedScene.IsValid() && preparedScene.isLoaded && preparedScene.isDirty)
            EditorSceneManager.SaveScene(preparedScene);
        DungeonLevelBuild.EnsureLevels();
        DungeonLevelBuild.UpdateBuildSettings();
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Windows/LEA.exe"));
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = output,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("LEA Windows-Build fehlgeschlagen: " + report.summary.result
                + " (" + report.summary.totalErrors + " Fehler).");
        Debug.Log("LEA Windows-Version erstellt: " + output);
    }

    [MenuItem("LEA/GUI-Sprites zuweisen")]
    public static void RefreshHud()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Die GUI bitte außerhalb des Spielmodus aktualisieren.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) { Prepare(); return; }
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var hud = UnityEngine.Object.FindFirstObjectByType<DungeonHUD>();
        if (hud == null) throw new InvalidOperationException("Die Spielszene enthält kein DungeonHUD.");
        ConfigureHud(hud);
        EditorUtility.SetDirty(hud);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    private static void ConfigureHud(DungeonHUD hud)
    {
        hud.panelSprite = UiSprite("Main_tiles", 54);
        hud.buttonNormal = UiSprite("Main_menu", 2);
        hud.buttonHover = UiSprite("Main_menu", 4);
        hud.buttonPressed = UiSprite("Main_menu", 3);
        hud.characterFrame = UiSprite("character_panel", 1);
        hud.playerPortrait = FirstSprite(2);
        hud.actionPanel = UiSprite("Action_panel", 0);
        hud.sealIcon = UiSprite("Icons", 102);
        hud.coinIcon = UiSprite("Icons", 69);
        hud.swordIcon = UiSprite("Icons", 85);
        hud.movementIcon = UiSprite("Icons", 64);
        hud.healthIcon = UiSprite("Icons", 97);
        hud.soundOnIcon = UiSprite("Buttons", 172);
        hud.soundOffIcon = UiSprite("Buttons", 166);
        hud.starIcon = UiSprite("Win_loose", 52);
        hud.defeatIcon = UiSprite("Win_loose", 56);
    }

    private static Sprite UiSprite(string atlas, int index)
    {
        string path = "Assets/Sprites/PNG_UI/" + atlas + ".png";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("GUI-Atlas fehlt: " + path);
        if (importer.filterMode != FilterMode.Point || importer.textureCompression != TextureImporterCompression.Uncompressed || importer.mipmapEnabled)
        {
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        var sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault(s => s.name == atlas + "_" + index);
        if (sprite == null) throw new InvalidOperationException("GUI-Sprite fehlt: " + atlas + "_" + index);
        return sprite;
    }

    private static int EnsureWallLayer()
    {
        int existing = LayerMask.NameToLayer("DungeonWalls");
        if (existing >= 0) return existing;
        var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = settings.FindProperty("layers");
        int index = 8;
        while (index < 32 && !string.IsNullOrEmpty(layers.GetArrayElementAtIndex(index).stringValue)) index++;
        if (index >= 32) throw new InvalidOperationException("Kein freier User-Layer für DungeonWalls.");
        layers.GetArrayElementAtIndex(index).stringValue = "DungeonWalls";
        settings.ApplyModifiedPropertiesWithoutUndo();
        return index;
    }

    private static AnimatorController CreateController(string path, int skeleton)
    {
        // These are generated assets. Rebuilding avoids retaining obsolete transitions/subassets.
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null)
            AssetDatabase.DeleteAsset(path);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        for (int actionIndex = 0; actionIndex < Actions.Length; actionIndex++)
            for (int directionIndex = 0; directionIndex < Directions.Length; directionIndex++)
            {
                string action = Actions[actionIndex];
                string stateName = "Player_" + action + Directions[directionIndex] + "_Skeleton_" + skeleton;
                AnimationClip source = LoadClip(stateName);
                // Preserve the author's source clips; normalize names and loop settings on copies.
                AnimationClip clip = UnityEngine.Object.Instantiate(source);
                clip.name = stateName;
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = action == "Idle" || action == "Walk" || action == "Run";
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                AssetDatabase.AddObjectToAsset(clip, controller);
                AnimatorState state = machine.AddState(stateName, new Vector3(actionIndex * 270f, directionIndex * 75f, 0));
                state.motion = clip;
                state.writeDefaultValues = false;
                if (actionIndex == 0 && directionIndex == 0) machine.defaultState = state;
            }
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static AnimationClip LoadClip(string canonicalName)
    {
        string filename = canonicalName;
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Player/" + filename + ".anim");
        if (clip == null && canonicalName == "Player_WalkD_Skeleton_2")
            clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Player/Player_WalkD_Skeleton2.anim");
        if (clip == null) throw new InvalidOperationException("Animationsclip fehlt: " + filename);
        return clip;
    }

    private static Sprite FirstSprite(int skeleton)
    {
        AnimationClip clip = LoadClip("Player_IdleD_Skeleton_" + skeleton);
        foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            if (binding.type != typeof(SpriteRenderer) || binding.propertyName != "m_Sprite") continue;
            foreach (var key in AnimationUtility.GetObjectReferenceCurve(clip, binding))
                if (key.value is Sprite sprite) return sprite;
        }
        throw new InvalidOperationException("Idle-Animation enthält kein Sprite für Skeleton " + skeleton);
    }

    private static GameObject CreateActor(string name, Vector2 position, int skeleton, AnimatorController controller,
        Material material, Transform parent)
    {
        GameObject actor = CreateSprite(name, position, FirstSprite(skeleton), 0.65f, 20, material, parent);
        var body = actor.AddComponent<Rigidbody2D>();
        body.gravityScale = 0;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        var collider = actor.AddComponent<CircleCollider2D>();
        collider.radius = 0.42f;
        // The sprite pivots mark the feet. Unity's automatic visual-center offset
        // otherwise makes melee reach and wall collisions direction-dependent.
        collider.offset = Vector2.zero;
        actor.AddComponent<Animator>().runtimeAnimatorController = controller;
        return actor;
    }

    private static EnemyController CreateEnemy(string name, Vector2 position, bool guardian, AnimatorController controller,
        Material material, Transform parent)
    {
        GameObject actor = CreateActor(name, position, 3, controller, material, parent);
        var enemy = actor.AddComponent<EnemyController>();
        enemy.maxHealth = guardian ? 4 : 2;
        enemy.moveSpeed = guardian ? 1.15f : 1.35f;
        enemy.skeleton = 3;
        enemy.isGuardian = guardian;
        actor.GetComponent<SpriteRenderer>().color = guardian ? new Color(1f, 0.83f, 0.62f) : new Color(0.80f, 0.90f, 1f);
        return enemy;
    }

    private static void CreateChest(string name, Vector2 position, EnemyController guardian, Sprite closed, Sprite opened,
        Material material, Transform parent)
    {
        var chest = CreateSprite(name, position, closed, 0.60f, 12, material, parent);
        var item = chest.AddComponent<DungeonInteractable>();
        item.kind = DungeonInteractable.ItemKind.SealChest;
        item.guardian = guardian;
        item.openedSprite = opened;
    }

    private static GameObject CreateSprite(string name, Vector2 position, Sprite sprite, float scale, int order,
        Material material, Transform parent)
    {
        var item = new GameObject(name);
        item.transform.SetParent(parent);
        item.transform.position = new Vector3(position.x, position.y, 0);
        item.transform.localScale = Vector3.one * scale;
        var renderer = item.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = material;
        renderer.sortingOrder = order >= 12 ? 1000 - Mathf.RoundToInt(position.y * 100f) : order;
        return item;
    }

    private static Sprite LoadSprite(string file, string name)
    {
        Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(SpriteFolder + file).OfType<Sprite>().FirstOrDefault(item => item.name == name);
        if (sprite == null) throw new InvalidOperationException("Sprite fehlt: " + file + " / " + name);
        return sprite;
    }

    private static Sprite CreatePotionSprite()
    {
        const string folder = "Assets/Generated";
        const string path = folder + "/LEA_Potion.png";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "Generated");
        // A small, crisp bottle with turquoise glass, magenta potion and a gold stopper.
        string[] pixels =
        {
            "................", ".....GGGGGG.....", ".....GGGGGG.....", "......TTTT......",
            "......TWWT......", "......TWWT......", ".....TTWWTT.....", "....TWWWWWWT....",
            "...TWWWWWWWWT...", "...TWMMMMMMWT...", "...TWMMMMMMWT...", "...TWMMMMMMMT...",
            "...TWMMMMMMMT...", "...TWMMMMMMMT...", "...TMMMMMMMMT...", "....TMMMMMMT....",
            ".....TTTTTT.....", "................", "................", "................"
        };
        var texture = new Texture2D(16, 20, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        for (int y = 0; y < pixels.Length; y++)
            for (int x = 0; x < 16; x++)
            {
                Color color = Color.clear;
                switch (pixels[y][x])
                {
                    case 'G': color = new Color(0.97f, 0.77f, 0.42f); break;
                    case 'T': color = new Color(0.26f, 0.88f, 0.82f); break;
                    case 'W': color = new Color(0.78f, 1f, 0.95f); break;
                    case 'M': color = new Color(0.92f, 0.30f, 0.70f); break;
                }
                texture.SetPixel(x, pixels.Length - y - 1, color);
            }
        texture.Apply();
        File.WriteAllBytes(Path.Combine(Application.dataPath, "Generated/LEA_Potion.png"), texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 20;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void ValidateSpawn(Vector2 point, int wallLayer, string name)
    {
        if (Physics2D.OverlapCircle(point, 0.29f, 1 << wallLayer) != null)
            throw new InvalidOperationException(name + " startet in einer Wand bei " + point + ".");
    }
}
