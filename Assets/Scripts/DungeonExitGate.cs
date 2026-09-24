using UnityEngine;

/// <summary>Shows whether the seal-locked level exit is ready to use.</summary>
[RequireComponent(typeof(DungeonInteractable))]
public sealed class DungeonExitGate : MonoBehaviour
{
    public SpriteRenderer gateRenderer;
    public Sprite closedSprite;
    public Sprite openSprite;

    public bool IsUnlocked => DungeonGame.Instance != null && DungeonGame.Instance.SealCount >= 3;

    private void OnEnable() => RefreshVisual();
    private void Update() => RefreshVisual();

    public void RefreshVisual()
    {
        if (gateRenderer == null) return;
        Sprite desired = IsUnlocked ? openSprite : closedSprite;
        if (desired != null && gateRenderer.sprite != desired) gateRenderer.sprite = desired;
    }
}
