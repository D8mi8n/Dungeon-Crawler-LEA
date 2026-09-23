using UnityEngine;

/// <summary>Optional bounded follow camera for larger variants of the dungeon.</summary>
[RequireComponent(typeof(Camera))]
public sealed class DungeonCamera : MonoBehaviour
{
    public Transform target;
    public Vector2 minimum = new Vector2(-14, -10);
    public Vector2 maximum = new Vector2(14, 10);
    private Camera view;
    private void Awake() { view = GetComponent<Camera>(); }
    private void LateUpdate()
    {
        if (target == null) return;
        float height = view.orthographicSize, width = height * view.aspect;
        Vector3 desired = target.position;
        desired.x = maximum.x - minimum.x > width * 2
            ? Mathf.Clamp(desired.x, minimum.x + width, maximum.x - width) : (minimum.x + maximum.x) / 2;
        desired.y = maximum.y - minimum.y > height * 2
            ? Mathf.Clamp(desired.y, minimum.y + height, maximum.y - height) : (minimum.y + maximum.y) / 2;
        desired.z = -10;
        transform.position = Vector3.Lerp(transform.position, desired, 1 - Mathf.Exp(-8 * Time.unscaledDeltaTime));
    }
}
