using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SpriteAnimationLoop : MonoBehaviour
{
    public Sprite[] sprites;
    public float framesPerSecond = 8f;
    private SpriteRenderer spriteRenderer;
    private int currentFrame;
    private float timer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (sprites != null && sprites.Length > 0)
            spriteRenderer.sprite = sprites[0];
    }

    private void Update()
    {
        if (spriteRenderer == null || sprites == null || sprites.Length == 0 ||
            framesPerSecond <= 0f || float.IsNaN(framesPerSecond) || float.IsInfinity(framesPerSecond))
            return;
        if (DungeonGame.Instance != null && !DungeonGame.Instance.IsPlaying)
            return;
        timer += Time.deltaTime;
        float frameTime = 1f / framesPerSecond;
        if (timer < frameTime)
            return;
        int framesToAdvance = Mathf.FloorToInt(timer / frameTime);
        timer %= frameTime;
        currentFrame = (currentFrame + framesToAdvance) % sprites.Length;
        spriteRenderer.sprite = sprites[currentFrame];
    }
}
