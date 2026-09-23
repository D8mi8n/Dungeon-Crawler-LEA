using UnityEngine;
using UnityEngine.Tilemaps;
using static Unity.Collections.AllocatorManager;

public class PlayerScript : MonoBehaviour

{

    public bool is_Idle = true;
    public bool is_Skeleton_2 = true;
    public float moveSpeed = 5f;

    private SpriteRenderer spriteRenderer;

    public Tilemap wall;
    public Tilemap block;
    public Tilemap deathblock;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    // Update is called once per frame
    void Update()
    {
        HandleMovement();
    }

    private void HandleMovement()
    {
        if (is_Skeleton_2)
        {
            if (Input.GetKey(KeyCode.A))
            {
                Move(Vector3.left);
            }
        }
    }

    private void Move(Vector3 direction)
    {

        Vector3 newPosition = transform.position + direction;

        if (wall == null)
        {
            transform.position = newPosition;
            return;
        }

        Vector3Int cellPosition = wall.WorldToCell(newPosition);

        bool hasWall = wall.HasTile(cellPosition);
        bool hasBlock = block != null && block.HasTile(cellPosition);
        bool hasDeathBlock = deathblock.HasTile(cellPosition);

        if (hasWall || hasBlock || hasDeathBlock)
            return;

        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        foreach (GameObject player in players)
        {
            if (player != gameObject && Vector3.Distance(player.transform.position, newPosition) < 0.1f)
            {
                return;
            }
        }

        transform.position = newPosition;
    }
}
