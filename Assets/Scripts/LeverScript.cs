using UnityEngine;

public class LeverSwitch : MonoBehaviour
{
    [Header("Lever")]
    public Animator leverAnimator;

    [Header("Steel Grid")]
    public GameObject steelGrid;

    private Animator steelGridAnimator;
    private Collider2D steelGridCollider;

    private bool activated = false;

    private void Awake()
    {
        if (steelGrid == null)
        {
            Debug.LogError("Steel_Grid wurde im Inspector nicht zugewiesen!");
            return;
        }

        // Animator des Steel_Grid suchen
        steelGridAnimator = steelGrid.GetComponent<Animator>();

        // Collider2D des Steel_Grid suchen
        steelGridCollider = steelGrid.GetComponent<Collider2D>();

        if (steelGridAnimator == null)
        {
            Debug.LogError("Kein Animator auf Steel_Grid gefunden!");
        }

        if (steelGridCollider == null)
        {
            Debug.LogError("Kein Collider2D auf Steel_Grid gefunden!");
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Nur auf den Player reagieren
        if (!collision.gameObject.CompareTag("Player"))
            return;

        ActivateLever();
    }

    private void ActivateLever()
    {
        if (activated)
            return;

        activated = true;

        Debug.Log("Lever wurde aktiviert!");

        // Lever Animation
        if (leverAnimator != null)
        {
            leverAnimator.Play("Lever_Offen");
        }

        // Steel Grid Animation
        if (steelGridAnimator != null)
        {
            steelGridAnimator.Play("Steel_Grid_Open");
        }

        // Collider des Steel Grid deaktivieren
        if (steelGridCollider != null)
        {
            steelGridCollider.enabled = false;
        }
    }
}
