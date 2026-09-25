using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Matches the fire hitbox to the displayed frame without changing its animation.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class DragonTrapDamage : MonoBehaviour
{
    [Serializable]
    public struct FrameHitbox
    {
        public Sprite sprite;
        public Vector2 offset;
        public Vector2 size;
    }

    [Min(1)] public int damage = 1;
    [Tooltip("Lokale Flammenbereiche je Sprite. Größe 0 bedeutet: kein Feuer, kein Schaden.")]
    public FrameHitbox[] frames = Array.Empty<FrameHitbox>();

    public BoxCollider2D DamageHitbox { get; private set; }
    private SpriteRenderer visual;
    private readonly List<Collider2D> overlaps = new List<Collider2D>(4);
    private ContactFilter2D playerFilter;

    private void Awake()
    {
        visual = GetComponent<SpriteRenderer>();
        // The existing solid head collider stays untouched; fire is a separate trigger.
        var area = new GameObject("Flammen-Hitbox");
        area.transform.SetParent(transform, false);
        area.layer = gameObject.layer;
        DamageHitbox = area.AddComponent<BoxCollider2D>();
        DamageHitbox.isTrigger = true;
        DamageHitbox.enabled = false;
        playerFilter = new ContactFilter2D { useTriggers = false };
    }

    private void LateUpdate()
    {
        var game = DungeonGame.Instance;
        if (game == null || !game.IsPlaying || game.Player == null || game.Player.IsDead
            || !visual.enabled || !TryGetHitbox(visual.sprite, out FrameHitbox frame))
        {
            DamageHitbox.enabled = false;
            return;
        }

        // SpriteAnimationLoop runs in Update, so this always uses the visible frame.
        Vector2 offset = frame.offset;
        if (visual.flipX) offset.x = -offset.x;
        if (visual.flipY) offset.y = -offset.y;
        DamageHitbox.offset = offset;
        DamageHitbox.size = frame.size;
        DamageHitbox.enabled = true;
        Physics2D.SyncTransforms();
        overlaps.Clear();
        DamageHitbox.Overlap(playerFilter, overlaps);
        foreach (Collider2D hit in overlaps)
        {
            if (hit.GetComponentInParent<PlayerController>() != game.Player) continue;
            // Includes players already standing where the growing flame reaches them.
            // TakeDamage preserves the player's existing invulnerability and death rules.
            game.Player.TakeDamage(damage);
            break;
        }
    }

    private bool TryGetHitbox(Sprite sprite, out FrameHitbox frame)
    {
        if (sprite != null && frames != null)
            foreach (FrameHitbox candidate in frames)
                if (candidate.sprite == sprite)
                {
                    frame = candidate;
                    return frame.size.x > 0f && frame.size.y > 0f;
                }
        frame = default;
        return false;
    }

    private void OnDisable()
    {
        if (DamageHitbox != null) DamageHitbox.enabled = false;
    }

    private void OnDestroy()
    {
        if (DamageHitbox != null) Destroy(DamageHitbox.gameObject);
    }
}
