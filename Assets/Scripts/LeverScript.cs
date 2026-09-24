using UnityEngine;

public class LeverSwitch : MonoBehaviour
{
    public Animator leverAnimator;
    public Animator steelGridAnimator;
    public GameObject steelGrid;

    private Collider steelGridCollider;

    private bool activated = false;

    private void Start()
    {
        steelGridCollider = steelGrid.GetComponent<Collider>();
    }

    private void OnTriggerEnter(Collider other)
    {
        // Nur auf den Player reagieren
        if (activated || !other.CompareTag("Player"))
            return;

        ActivateLever();
    }

    private void ActivateLever()
    {
        activated = true;

        // Lever-Animation abspielen
        leverAnimator.Play("Lever");

        // Steel_Grid öffnen
        steelGridAnimator.Play("Steel_Grid_Open");

        // Collider des Steel_Grid deaktivieren
        if (steelGridCollider != null)
        {
            steelGridCollider.enabled = false;
        }
    }
}