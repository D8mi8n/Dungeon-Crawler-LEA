using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Configures the shared seal-locked gate without rebuilding level layouts.</summary>
public static class DungeonGateBuild
{
    public static void Configure(Transform gameplay)
    {
        var exits = gameplay.GetComponentsInChildren<DungeonInteractable>()
            .Where(item => item.kind == DungeonInteractable.ItemKind.Exit).ToArray();
        if (exits.Length > 1) throw new InvalidOperationException("LEA enthält mehrere Ausgänge.");
        var exit = exits.SingleOrDefault();
        if (exit == null)
        {
            var root = new GameObject("Nordtor");
            root.transform.SetParent(gameplay);
            exit = root.AddComponent<DungeonInteractable>();
            exit.kind = DungeonInteractable.ItemKind.Exit;
        }

        ConfigureExit(exit, new Vector2(1.95f, 3.05f), new Vector2(1.95f, 4.05f), 650);

        // Keep the north-wall gate below the existing HUD, with the southern edge still visible.
        var camera = Camera.main;
        if (camera != null)
        {
            camera.transform.position = new Vector3(0f, 0.5f, -10f);
            camera.orthographicSize = 6.8f;
        }
    }

    public static void ConfigureExit(DungeonInteractable exit, Vector2 interactionPoint,
        Vector2 visualCenter, int sortingOrder, SpriteRenderer existingVisual = null)
    {
        // Interaction stays on the walkable floor, separately from the wall-mounted artwork.
        exit.transform.position = interactionPoint;
        exit.transform.localScale = Vector3.one;
        exit.transform.localRotation = Quaternion.identity;
        var oldRenderer = exit.GetComponent<SpriteRenderer>();
        if (oldRenderer != null) UnityEngine.Object.DestroyImmediate(oldRenderer);

        var gate = exit.GetComponent<DungeonExitGate>() ?? exit.gameObject.AddComponent<DungeonExitGate>();
        if (existingVisual != null) gate.gateRenderer = existingVisual;
        if (gate.gateRenderer == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Door.prefab");
            if (prefab == null) throw new InvalidOperationException("Door.prefab fehlt.");
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, exit.transform);
            visual.name = "Nordtor · Door Prefab";
            gate.gateRenderer = visual.GetComponent<SpriteRenderer>();
        }

        var renderer = gate.gateRenderer;
        renderer.transform.SetParent(exit.transform, true);
        renderer.transform.localRotation = Quaternion.identity;
        var animation = renderer.GetComponent<SpriteAnimationLoop>();
        // The prefab's continuous open/close loop must not override the seal state.
        if (animation != null)
        {
            animation.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(animation);
        }
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/PNG/doors.png").OfType<Sprite>().ToArray();
        gate.closedSprite = sprites.Single(sprite => sprite.name == "doors_0");
        gate.openSprite = sprites.Single(sprite => sprite.name == "doors_2");
        renderer.sprite = gate.closedSprite;
        renderer.color = Color.white;
        renderer.sortingOrder = sortingOrder;
        renderer.transform.localScale = Vector3.one * 0.65f;
        renderer.transform.position += (Vector3)visualCenter - renderer.bounds.center;
        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer.transform);
        EditorUtility.SetDirty(gate);
    }

    // Batch entry point: patch a validation copy, render it and exercise all level exits.
    public static void InstallAndValidate()
    {
        var scene = EditorSceneManager.OpenScene(DungeonBuild.ScenePath, OpenSceneMode.Single);
        var gameplay = GameObject.Find("LEA Gameplay");
        if (gameplay == null) throw new InvalidOperationException("LEA Gameplay fehlt.");
        Configure(gameplay.transform);
        Physics2D.SyncTransforms();
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("LEA konnte nicht gespeichert werden.");
        AssetDatabase.SaveAssets();
        RenderPreview("LEA-gate");
        DungeonValidation.Run();
    }

    public static void InstallAdditionalAndValidate()
    {
        for (int index = 1; index < DungeonGame.LevelScenes.Length; index++)
        {
            var scene = EditorSceneManager.OpenScene(DungeonLevelBuild.PathFor(index), OpenSceneMode.Single);
            var gameplay = UnityEngine.Object.FindFirstObjectByType<DungeonGame>().transform.root;
            var exits = UnityEngine.Object.FindObjectsByType<DungeonInteractable>(FindObjectsSortMode.None)
                .Where(item => item.kind == DungeonInteractable.ItemKind.Exit).ToArray();
            // Prefer an already copied LEA gate over its obsolete, separate interaction object.
            var exit = exits.OrderByDescending(item => item.GetComponent<DungeonExitGate>() != null).First();
            var gate = exit.GetComponent<DungeonExitGate>();
            var visual = gate != null ? gate.gateRenderer : null;
            if (visual == null)
                visual = UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
                    .Single(renderer => renderer.name.StartsWith("Nordtor", StringComparison.Ordinal)
                        && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(renderer.gameObject) == "Assets/Prefabs/Door.prefab");

            // Preserve each level's authored doorway position; bring interaction in front of its wall.
            Vector2 center = visual.bounds.center;
            Vector2 interactionPoint = index == 1 ? center + Vector2.down * 1.3f : (Vector2)exit.transform.position;
            visual.transform.SetParent(exit.transform, true);
            foreach (var duplicate in exits.Where(item => item != exit))
                UnityEngine.Object.DestroyImmediate(duplicate.gameObject);
            exit.name = "Nordtor";
            exit.transform.SetParent(gameplay, true);
            ConfigureExit(exit, interactionPoint, center, 200, visual);
            Physics2D.SyncTransforms();
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException(scene.name + " konnte nicht gespeichert werden.");

            // Preview from the north room without changing the saved follow-camera setup.
            Camera.main.transform.position = new Vector3(center.x, 5.2f, -10f);
            RenderPreview(scene.name + "-gate");
        }
        AssetDatabase.SaveAssets();
        DungeonValidation.Run();
    }

    private static void RenderPreview(string name)
    {
        var camera = Camera.main;
        var target = RenderTexture.GetTemporary(1280, 720, 24);
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            texture.Apply();
            Directory.CreateDirectory("Artifacts");
            File.WriteAllBytes("Artifacts/" + name + ".png", texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
