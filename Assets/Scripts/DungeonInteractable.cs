using UnityEngine;

public sealed class DungeonInteractable : MonoBehaviour
{
    public enum ItemKind { SealChest, Potion, Coin, Exit }
    public ItemKind kind;
    public EnemyController guardian;
    public Sprite openedSprite;
    public int amount = 2;
    public bool IsConsumed { get; private set; }
    public bool IsAutomatic => kind == ItemKind.Potion || kind == ItemKind.Coin;
    public string Prompt
    {
        get
        {
            if (kind == ItemKind.Exit) return DungeonGame.Instance.SealCount >= 3
                ? "[E] Durch das Nordtor entkommen" : "Nordtor versiegelt · Drei Seelensiegel benötigt";
            if (guardian != null && !guardian.IsDead) return "Siegeltruhe · Besiege zuerst den Wächter";
            return "[E] Siegeltruhe öffnen · +2 Leben";
        }
    }

    public bool TryUse()
    {
        var game = DungeonGame.Instance;
        if (IsConsumed || game == null || !game.IsPlaying) return false;
        switch (kind)
        {
            case ItemKind.SealChest:
                if (guardian != null && !guardian.IsDead)
                {
                    game.ShowMessage("Der Wächter schützt dieses Seelensiegel.");
                    return false;
                }
                game.CollectSeal();
                break;
            case ItemKind.Potion:
                if (!game.Player.Heal(amount)) return false;
                game.ShowMessage("Heiltrank gefunden · +" + amount + " Leben");
                break;
            case ItemKind.Coin: game.AddCoins(amount); break;
            case ItemKind.Exit: return game.TryEscape();
        }
        IsConsumed = true;
        if (kind == ItemKind.SealChest && openedSprite != null)
            GetComponent<SpriteRenderer>().sprite = openedSprite;
        else if (kind != ItemKind.SealChest) gameObject.SetActive(false);
        return true;
    }

    private void OnGUI()
    {
        var game = DungeonGame.Instance;
        if (IsConsumed || game == null || !game.IsPlaying || game.Player == null || Camera.main == null) return;
        if (kind != ItemKind.SealChest && kind != ItemKind.Exit) return;
        Vector3 screen = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 0.85f);
        if (screen.z < 0 || screen.y < 0 || screen.y > Screen.height) return;
        string label = kind == ItemKind.Exit ? "NORDTOR" : "SEELENSIEGEL";
        float scale = Mathf.Max(0.65f, Screen.height / 720f);
        var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(11 * scale) };
        style.normal.textColor = kind == ItemKind.Exit ? new Color(0.45f, 0.95f, 0.86f) : new Color(1, 0.8f, 0.4f);
        float labelY = Mathf.Max(122 * scale, Screen.height - screen.y);
        GUI.Label(new Rect(screen.x - 90 * scale, labelY, 180 * scale, 22 * scale), label, style);
    }
}
