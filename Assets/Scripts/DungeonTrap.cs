using UnityEngine;

/// <summary>A readable warning precedes every short damage window.</summary>
public sealed class DungeonTrap : MonoBehaviour
{
    public float phaseOffset;
    private float nextDamage;
    private SpriteRenderer visual;
    private void Awake() { visual = GetComponent<SpriteRenderer>(); }
    private void Update()
    {
        var game = DungeonGame.Instance;
        if (game == null || !game.IsPlaying || game.Player == null) return;
        float phase = (Time.time + phaseOffset) % 4.5f;
        bool active = phase > 3.6f;
        if (visual != null) visual.color = active ? new Color(1, 0.32f, 0.28f)
            : phase > 2.7f ? new Color(1, 0.78f, 0.3f) : new Color(0.55f, 0.62f, 0.66f);
        if (active && Time.time >= nextDamage && Vector2.Distance(game.Player.transform.position, transform.position) < 0.6f)
        {
            game.Player.TakeDamage(1);
            nextDamage = Time.time + 1;
        }
    }
}
