using UnityEngine;

public class MushroomPickup : MonoBehaviour
{
    public MushroomData mushroomData;
    public float pickupRange = 2f;
    public KeyCode pickupKey = KeyCode.E;
    public GameObject pickupEffect;

    private Transform player;
    private bool playerInRange = false;

    void Start()
    {
        // Find player
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;
    }

    void Update()
    {
        if (player == null) return;

        float distance = Vector3.Distance(transform.position, player.position);
        bool wasInRange = playerInRange;
        playerInRange = distance <= pickupRange;

        // Show pickup prompt
        if (playerInRange && !wasInRange)
        {
            // Show "Press F to pick up" UI here
        }
        else if (!playerInRange && wasInRange)
        {
            // Hide pickup prompt
        }

        // Handle pickup
        if (playerInRange && Input.GetKeyDown(pickupKey))
        {
            PickupMushroom();
        }
    }

    void PickupMushroom()
    {
        if (InventorySystem.Instance != null && mushroomData != null)
        {
            bool success = InventorySystem.Instance.AddMushroom(mushroomData);

            if (success)
            {
                // Play effect
                if (pickupEffect != null)
                    Instantiate(pickupEffect, transform.position, Quaternion.identity);

                // Destroy pickup
                Destroy(gameObject);
            }
            else
            {
                Debug.Log("Inventory full!");
                // Show "Inventory Full" message
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, pickupRange);
    }
}
