using UnityEngine;

public class SkyGemTeleportSpots : MonoBehaviour
{
    [Tooltip("Teleport destinations the Sky Gem can choose from.")]
    public Transform[] teleportSpots;

    public bool TryGetTeleportSpot(out Vector3 position)
    {
        position = transform.position;

        if (teleportSpots == null || teleportSpots.Length == 0)
            return false;

        int startIndex = Random.Range(0, teleportSpots.Length);
        for (int offset = 0; offset < teleportSpots.Length; offset++)
        {
            Transform spot = teleportSpots[(startIndex + offset) % teleportSpots.Length];
            if (spot == null)
                continue;

            position = spot.position;
            return true;
        }

        return false;
    }
}
