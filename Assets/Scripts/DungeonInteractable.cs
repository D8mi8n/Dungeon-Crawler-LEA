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
}
