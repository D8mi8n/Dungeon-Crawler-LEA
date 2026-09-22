using UnityEngine;

public class SpriteAnimationLoop : MonoBehaviour
{
    public Sprite[] sprites;
    public float framesPerSecond = 8f;

    private SpriteRenderer spriteRenderer;
    private int currentFrame = 0;
    private float timer = 0f;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        if (sprites == null || sprites.Length == 0)
            return;

        timer += Time.deltaTime;

        float frameTime = 1f / framesPerSecond;

        if (timer >= frameTime)
        {
            timer -= frameTime;

            currentFrame++;

            if (currentFrame >= sprites.Length)
                currentFrame = 0;

            spriteRenderer.sprite = sprites[currentFrame];
        }
    }
}