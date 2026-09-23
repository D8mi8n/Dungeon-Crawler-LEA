using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Read-only scene audit. Run with -executeMethod DungeonSceneReport.Export.</summary>
public static class DungeonSceneReport
{
    public static void Export()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Dungeon.unity", OpenSceneMode.Single);
        Physics2D.SyncTransforms();

        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Artifacts"));
        Directory.CreateDirectory(output);
        Camera camera = Camera.main;
        if (camera == null)
            throw new InvalidOperationException("Dungeon has no Main Camera.");

        var report = new StringBuilder();
        report.AppendLine("DUNGEON SCENE AUDIT — world coordinates, unmodified scene");
        report.AppendLine("Camera: " + Format(camera.transform.position) + "; orthographic size " + Number(camera.orthographicSize));
        report.AppendLine("Camera view at 16:9: half width " + Number(camera.orthographicSize * 16f / 9f));
        report.AppendLine();

        var renderers = UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
            .Where(item => item.enabled && item.gameObject.activeInHierarchy)
            .OrderBy(item => Hierarchy(item.transform)).ToArray();
        var colliders = UnityEngine.Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None)
            .Where(item => item.enabled && item.gameObject.activeInHierarchy)
            .OrderBy(item => Hierarchy(item.transform)).ToArray();
        var floors = renderers.Where(IsFloor).ToArray();

        report.AppendLine("SOLID COLLIDERS (trigger colliders are explicitly marked)");
        foreach (var collider in colliders)
            report.AppendLine(Hierarchy(collider.transform) + " | " + collider.GetType().Name
                + " | trigger=" + collider.isTrigger + " | " + Format(collider.bounds));
        report.AppendLine();
        report.AppendLine("FLOOR / BRIDGE SPRITE BOUNDS (visual bounds, not a navigation mesh)");
        foreach (var floor in floors)
            report.AppendLine(Hierarchy(floor.transform) + " | " + Format(floor.bounds));
        report.AppendLine();
        report.AppendLine("ALL SPRITE RENDERERS");
        foreach (var renderer in renderers)
            report.AppendLine(Hierarchy(renderer.transform) + " | position=" + Format(renderer.transform.position)
                + " | order=" + renderer.sortingOrder + " | sprite=" + (renderer.sprite != null ? renderer.sprite.name : "MISSING")
                + " | " + Format(renderer.bounds));

        report.AppendLine();
        report.AppendLine("WALKABILITY PREVIEW: radius 0.28, grid spacing 0.5; # = solid collider, . = floor/bridge bounds, ~ = other.");
        report.AppendLine("Visual sprite bounds can contain transparent pixels; inspect the screenshot before choosing spawn points.");
        report.AppendLine("Columns: x=-10 through 10. Rows: y=6 down through -10.");
        for (float y = 6f; y >= -10f; y -= 0.5f)
        {
            report.Append(y.ToString(" 0.0;-0.0", CultureInfo.InvariantCulture)).Append(" ");
            for (float x = -10f; x <= 10f; x += 0.5f)
            {
                Vector2 point = new Vector2(x, y);
                bool blocked = Physics2D.OverlapCircleAll(point, 0.28f).Any(item => !item.isTrigger);
                bool onFloor = floors.Any(item => InBounds(item.bounds, point));
                report.Append(blocked ? '#' : onFloor ? '.' : '~');
            }
            report.AppendLine();
        }

        report.AppendLine();
        report.AppendLine("CLEAR FLOOR CANDIDATES: one-unit grid, radius 0.4 clear of solid colliders");
        for (float y = 4f; y >= -8f; y -= 1f)
            for (float x = -7f; x <= 7f; x += 1f)
            {
                Vector2 point = new Vector2(x, y);
                if (floors.Any(item => InBounds(item.bounds, point))
                    && !Physics2D.OverlapCircleAll(point, 0.4f).Any(item => !item.isTrigger))
                    report.AppendLine("(" + Number(x) + ", " + Number(y) + ")");
            }

        File.WriteAllText(Path.Combine(output, "Dungeon-scene-report.txt"), report.ToString(), Encoding.UTF8);
        RenderTexture target = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32);
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = camera.targetTexture;
        Texture2D image = null;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(output, "Dungeon-before.png"), image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            if (image != null)
                UnityEngine.Object.DestroyImmediate(image);
            RenderTexture.ReleaseTemporary(target);
        }

        Debug.Log("Dungeon scene audit written to " + output);
    }

    private static bool IsFloor(SpriteRenderer item)
    {
        string objectName = item.name.ToLowerInvariant();
        string spriteName = item.sprite != null ? item.sprite.name.ToLowerInvariant() : "";
        return objectName.Contains("floor") || objectName.Contains("brige") || objectName.Contains("bridge")
            || spriteName.Contains("floor") || spriteName.Contains("brige") || spriteName.Contains("bridge");
    }

    private static bool InBounds(Bounds bounds, Vector2 point)
    {
        return point.x >= bounds.min.x && point.x <= bounds.max.x
            && point.y >= bounds.min.y && point.y <= bounds.max.y;
    }

    private static string Hierarchy(Transform item)
    {
        var names = new List<string>();
        while (item != null)
        {
            names.Add(item.name);
            item = item.parent;
        }
        names.Reverse();
        return string.Join("/", names);
    }

    private static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string Format(Vector3 value) => "(" + Number(value.x) + ", " + Number(value.y) + ", " + Number(value.z) + ")";
    private static string Format(Bounds value) => "bounds min=" + Format(value.min) + " max=" + Format(value.max);
}
