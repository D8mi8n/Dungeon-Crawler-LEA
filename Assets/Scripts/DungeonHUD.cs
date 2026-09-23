using UnityEngine;

/// <summary>A small, asset-free interface for the crypt, in a consistent 1280 x 720 canvas.</summary>
public sealed class DungeonHUD : MonoBehaviour
{
    private static readonly Color Ink = new Color(0.025f, 0.043f, 0.063f, 0.95f);
    private static readonly Color PanelColor = new Color(0.046f, 0.075f, 0.094f, 0.94f);
    private static readonly Color Line = new Color(0.27f, 0.37f, 0.40f, 0.55f);
    private static readonly Color Gold = new Color(0.96f, 0.76f, 0.40f);
    private static readonly Color Teal = new Color(0.39f, 0.88f, 0.81f);
    private static readonly Color White = new Color(0.93f, 0.96f, 0.95f);
    private static readonly Color MutedText = new Color(0.61f, 0.70f, 0.72f);
    private static readonly Color Red = new Color(0.97f, 0.38f, 0.35f);

    private GUIStyle titleStyle;
    private GUIStyle headingStyle;
    private GUIStyle bodyStyle;
    private GUIStyle smallStyle;
    private GUIStyle tinyStyle;
    private GUIStyle numberStyle;
    private GUIStyle buttonStyle;
    private GUIStyle keyStyle;

    private void OnGUI()
    {
        DungeonGame game = DungeonGame.Instance;
        if (game == null) return;
        EnsureStyles();

        Matrix4x4 oldMatrix = GUI.matrix;
        Color oldColor = GUI.color;
        int oldDepth = GUI.depth;
        GUI.depth = -100;
        GUI.color = Color.white;
        GUI.matrix = Matrix4x4.identity;

        bool overlay = game.State != DungeonGame.GameState.Playing;
        if (overlay)
        {
            Fill(new Rect(0, 0, Screen.width, Screen.height),
                new Color(0.012f, 0.026f, 0.039f, game.State == DungeonGame.GameState.Menu ? 0.73f : 0.78f));
        }

        float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
        GUI.matrix = Matrix4x4.TRS(
            new Vector3((Screen.width - 1280f * scale) * 0.5f, (Screen.height - 720f * scale) * 0.5f, 0),
            Quaternion.identity, new Vector3(scale, scale, 1));

        switch (game.State)
        {
            case DungeonGame.GameState.Menu:
                DrawMenu(game);
                break;
            case DungeonGame.GameState.Playing:
                DrawGameplay(game);
                break;
            case DungeonGame.GameState.Paused:
                DrawPause(game);
                break;
            case DungeonGame.GameState.Won:
                DrawResult(game, true);
                break;
            case DungeonGame.GameState.Lost:
                DrawResult(game, false);
                break;
        }

        GUI.matrix = oldMatrix;
        GUI.color = oldColor;
        GUI.depth = oldDepth;
    }

    private void EnsureStyles()
    {
        if (bodyStyle != null) return;
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleStyle = MakeStyle(font, 112, White, FontStyle.Bold);
        headingStyle = MakeStyle(font, 34, White, FontStyle.Bold);
        bodyStyle = MakeStyle(font, 20, White);
        bodyStyle.wordWrap = true;
        smallStyle = MakeStyle(font, 16, MutedText);
        smallStyle.wordWrap = true;
        tinyStyle = MakeStyle(font, 13, MutedText);
        tinyStyle.wordWrap = true;
        numberStyle = MakeStyle(font, 30, White, FontStyle.Bold);
        buttonStyle = MakeStyle(font, 19, White, FontStyle.Bold);
        buttonStyle.alignment = TextAnchor.MiddleCenter;
        keyStyle = MakeStyle(font, 15, White, FontStyle.Bold);
        keyStyle.alignment = TextAnchor.MiddleCenter;
    }

    private static GUIStyle MakeStyle(Font font, int size, Color color, FontStyle weight = FontStyle.Normal)
    {
        GUIStyle style = new GUIStyle();
        style.font = font;
        style.fontSize = size;
        style.fontStyle = weight;
        style.normal.textColor = color;
        style.hover.textColor = color;
        style.active.textColor = color;
        style.focused.textColor = color;
        style.richText = false;
        style.clipping = TextClipping.Clip;
        return style;
    }

    private void DrawMenu(DungeonGame game)
    {
        Fill(new Rect(126, 110, 52, 3), Gold);
        Text(new Rect(126, 132, 460, 134), "LEA", titleStyle);
        Text(new Rect(131, 272, 540, 44), "DIE VERGESSENE KRYPTA", headingStyle, Gold, 27);
        Text(new Rect(133, 320, 520, 42), "Ein Tor. Drei Siegel. Dein Weg zurück ins Licht.", smallStyle);

        if (Button(new Rect(132, 394, 434, 58), "Krypta betreten", true)) game.StartRun();
        if (Button(new Rect(132, 468, 211, 47), game.Muted ? "Ton: aus" : "Ton: an")) game.ToggleMute();
        if (Button(new Rect(355, 468, 211, 47), "Beenden")) game.QuitGame();
        Text(new Rect(132, 535, 434, 26), "Auch mit Eingabe starten", tinyStyle);

        Panel(new Rect(740, 157, 412, 425), Teal);
        Text(new Rect(772, 189, 345, 28), "DEIN AUFTRAG", smallStyle, Teal, 15);
        Text(new Rect(772, 233, 342, 118),
            "Sammle die drei Seelensiegel in der Krypta. Erreiche das Nordtor und entkomme.", bodyStyle, White, 23);
        Fill(new Rect(772, 366, 346, 1), Line);
        Instruction(772, 391, "WASD", "Bewegen · auch mit Pfeiltasten", 66);
        Instruction(772, 437, "LEER", "Angreifen · auch mit linker Maus", 66);
        Instruction(772, 483, "E", "Siegel und Tor aktivieren", 66);
        Text(new Rect(772, 539, 344, 25), "SHIFT  Sprinten     ESC  Pause", tinyStyle);

        Text(new Rect(132, 650, 900, 25), "Erkunde die Räume. Halte Abstand. Finde den Ausgang.", tinyStyle);
        Text(new Rect(1072, 650, 80, 25), "LEA / 01", tinyStyle, Gold);
    }

    private void DrawGameplay(DungeonGame game)
    {
        Panel(new Rect(24, 22, 328, 92), Teal);
        Text(new Rect(42, 36, 170, 20), "LEBENSKRAFT", tinyStyle);
        int health = game.Player != null ? game.Player.CurrentHealth : 0;
        int maxHealth = game.Player != null ? Mathf.Max(1, game.Player.MaxHealth) : 6;
        Text(new Rect(254, 33, 79, 27), health + " / " + maxHealth, smallStyle, health <= 2 ? Red : White, 18);
        float segmentWidth = (288f - (maxHealth - 1) * 5f) / maxHealth;
        for (int i = 0; i < maxHealth; i++)
        {
            Rect segment = new Rect(42 + i * (segmentWidth + 5), 74, segmentWidth, 18);
            Fill(segment, i < health ? (health <= 2 ? Red : Teal) : new Color(0.17f, 0.22f, 0.25f));
            if (i < health) Fill(new Rect(segment.x, segment.y, segment.width, 2), new Color(1, 1, 1, 0.23f));
        }

        HudMetric(new Rect(364, 22, 139, 92), "SEELENSIEGEL", game.SealCount + " / 3", Gold);
        HudMetric(new Rect(515, 22, 115, 92), "MÜNZEN", game.CoinCount.ToString(), Gold);
        HudMetric(new Rect(642, 22, 145, 92), "BESIEGT", game.DefeatedEnemies + " / " + game.TotalEnemies, White);
        HudMetric(new Rect(799, 22, 116, 92), "ZEIT", FormatTime(game.ElapsedTime), White);

        Panel(new Rect(927, 22, 329, 92), Gold);
        Text(new Rect(945, 36, 284, 20), "DEIN NÄCHSTES ZIEL", tinyStyle, Gold);
        Text(new Rect(945, 61, 289, 42), game.Objective, smallStyle, White, 16);

        if (!string.IsNullOrEmpty(game.Message))
        {
            Panel(new Rect(315, 134, 650, 52), Gold);
            CenterText(new Rect(333, 146, 614, 29), game.Message, smallStyle, White, 18);
        }

        if (!string.IsNullOrEmpty(game.InteractionPrompt))
        {
            Panel(new Rect(350, 591, 580, 57), Teal);
            CenterText(new Rect(370, 606, 540, 29), game.InteractionPrompt, bodyStyle, White, 19);
        }

        Fill(new Rect(24, 670, 1232, 30), new Color(0.026f, 0.042f, 0.053f, 0.87f));
        CenterText(new Rect(36, 677, 1208, 20),
            "WASD / Pfeile  Bewegen     SHIFT  Sprinten     LEER / Linke Maus  Angreifen     E  Interagieren     ESC  Pause",
            tinyStyle, MutedText, 13);
    }

    private void DrawPause(DungeonGame game)
    {
        Panel(new Rect(412, 125, 456, 470), Teal);
        CenterText(new Rect(445, 161, 390, 45), "ATEMPAUSE", headingStyle, White);
        CenterText(new Rect(449, 220, 382, 28), "Die Krypta wartet auf dich.", smallStyle);
        if (Button(new Rect(452, 278, 376, 55), "Weiterkämpfen", true)) game.Resume();
        if (Button(new Rect(452, 346, 376, 47), "Neu starten")) game.RestartRun();
        if (Button(new Rect(452, 406, 376, 47), "Hauptmenü")) game.ReturnToMenu();
        if (Button(new Rect(452, 466, 376, 41), game.Muted ? "Ton einschalten" : "Ton ausschalten")) game.ToggleMute();
        CenterText(new Rect(452, 544, 376, 26), "ESC  Zurück ins Spiel", tinyStyle);
    }

    private void DrawResult(DungeonGame game, bool won)
    {
        Color accent = won ? Gold : Red;
        Panel(new Rect(256, 105, 768, 510), accent);
        CenterText(new Rect(302, 144, 676, 27), won ? "DREI SIEGEL. EIN NEUER MORGEN." : "DEINE REISE ENDET HIER", smallStyle, accent, 15);
        CenterText(new Rect(295, 191, 690, 53), won ? "DER KRYPTA ENTKOMMEN" : "VON SCHATTEN BESIEGT", headingStyle, White, 34);
        CenterText(new Rect(316, 257, 648, 57),
            won ? "Das Nordtor ist offen. Du hast die Seelensiegel geborgen\nund deinen Weg zurück ins Licht gefunden."
                : "Die Krypta bewahrt ihre Geheimnisse.\nVersuche es erneut und finde alle drei Seelensiegel.",
            bodyStyle, MutedText, 19);

        float firstX = 296;
        ResultMetric(firstX, "SEELENSIEGEL", game.SealCount + " / 3", Gold);
        ResultMetric(firstX + 176, "MÜNZEN", game.CoinCount.ToString(), Gold);
        ResultMetric(firstX + 352, "BESIEGT", game.DefeatedEnemies + " / " + game.TotalEnemies, Teal);
        ResultMetric(firstX + 528, "ZEIT", FormatTime(game.ElapsedTime), White);

        if (Button(new Rect(306, 483, 326, 56), won ? "Noch einmal spielen" : "Erneut versuchen", true)) game.RestartRun();
        if (Button(new Rect(648, 483, 326, 56), "Hauptmenü")) game.ReturnToMenu();
        CenterText(new Rect(306, 564, 668, 25), "EINGABE  Neu starten", tinyStyle);
    }

    private void HudMetric(Rect rect, string label, string value, Color accent)
    {
        Panel(rect, accent);
        Text(new Rect(rect.x + 16, rect.y + 14, rect.width - 22, 20), label, tinyStyle, MutedText, 12);
        Text(new Rect(rect.x + 16, rect.y + 41, rect.width - 23, 36), value, numberStyle, accent, 27);
    }

    private void ResultMetric(float x, string label, string value, Color accent)
    {
        Fill(new Rect(x, 345, 160, 97), new Color(0.10f, 0.15f, 0.17f, 0.7f));
        CenterText(new Rect(x + 8, 359, 144, 20), label, tinyStyle);
        CenterText(new Rect(x + 8, 390, 144, 38), value, numberStyle, accent, 29);
    }

    private void Instruction(float x, float y, string key, string description, float keyWidth)
    {
        Fill(new Rect(x, y, keyWidth, 31), new Color(0.16f, 0.23f, 0.25f));
        Fill(new Rect(x, y + 30, keyWidth, 1), Teal);
        Text(new Rect(x, y, keyWidth, 30), key, keyStyle);
        Text(new Rect(x + keyWidth + 12, y + 5, 270, 40), description, smallStyle, White, 15);
    }

    private bool Button(Rect rect, string label, bool primary = false)
    {
        bool hovered = rect.Contains(Event.current.mousePosition);
        Color background = primary ? Gold : new Color(0.10f, 0.16f, 0.19f, 1);
        if (hovered) background = primary ? new Color(1, 0.85f, 0.56f) : new Color(0.17f, 0.28f, 0.31f);
        Fill(new Rect(rect.x, rect.y + 3, rect.width, rect.height), new Color(0, 0, 0, 0.22f));
        Fill(rect, background);
        Fill(new Rect(rect.x, rect.y, rect.width, 1), primary ? new Color(1, 0.91f, 0.68f) : Line);
        if (hovered && !primary) Fill(new Rect(rect.x, rect.y, 3, rect.height), Teal);
        Color foreground = primary ? Ink : White;
        buttonStyle.normal.textColor = foreground;
        buttonStyle.hover.textColor = foreground;
        buttonStyle.active.textColor = foreground;
        buttonStyle.focused.textColor = foreground;
        return GUI.Button(rect, label, buttonStyle);
    }

    private static void Fill(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    private static void Panel(Rect rect, Color accent)
    {
        Fill(new Rect(rect.x + 2, rect.y + 5, rect.width, rect.height), new Color(0, 0, 0, 0.2f));
        Fill(rect, PanelColor);
        Fill(new Rect(rect.x, rect.y, rect.width, 2), accent);
        Fill(new Rect(rect.x, rect.y + rect.height - 1, rect.width, 1), Line);
    }

    private static void Text(Rect rect, string value, GUIStyle style, Color? color = null, int size = 0)
    {
        Color previousColor = style.normal.textColor;
        int previousSize = style.fontSize;
        if (color.HasValue) style.normal.textColor = color.Value;
        if (size > 0) style.fontSize = size;
        GUI.Label(rect, value ?? string.Empty, style);
        style.normal.textColor = previousColor;
        style.fontSize = previousSize;
    }

    private static void CenterText(Rect rect, string value, GUIStyle style, Color? color = null, int size = 0)
    {
        TextAnchor previous = style.alignment;
        style.alignment = TextAnchor.UpperCenter;
        Text(rect, value, style, color, size);
        style.alignment = previous;
    }

    private static string FormatTime(float elapsed)
    {
        int seconds = Mathf.Max(0, Mathf.FloorToInt(elapsed));
        return (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
    }
}
