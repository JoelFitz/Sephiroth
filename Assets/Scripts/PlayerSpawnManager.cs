using UnityEngine;

public class PlayerSpawnManager : MonoBehaviour
{
    [Header("Default Spawn Settings")]
    public Transform defaultSpawnPoint;
    public Vector3 defaultSpawnPosition = Vector3.zero;
    public float defaultSpawnRotation = 0f;

    [Header("Scene-Specific Spawns")]
    public DoorSpawnPoint[] doorSpawnPoints;

    void Start()
    {
        SpawnPlayer();
    }

    void SpawnPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("PlayerSpawnManager: No player found with 'Player' tag!");
            return;
        }

        Debug.Log($"PlayerSpawnManager: Found player at {player.transform.position}");

        Vector3 spawnPos = GetSpawnPosition();
        float spawnRot = GetSpawnRotation();

        Debug.Log($"PlayerSpawnManager: Calculated spawn position: {spawnPos}, rotation: {spawnRot}");

        // Position the player
        if (player.GetComponent<CharacterController>() != null)
        {
            // For CharacterController, disable it briefly to move the player
            CharacterController cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            player.transform.position = spawnPos;
            player.transform.rotation = Quaternion.Euler(0, spawnRot, 0);
            cc.enabled = true;
            Debug.Log($"PlayerSpawnManager: Moved player (CharacterController path) to {spawnPos}");
        }
        else
        {
            // For Rigidbody or regular transform
            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.MovePosition(spawnPos);
                rb.rotation = Quaternion.Euler(0, spawnRot, 0);
                Debug.Log($"PlayerSpawnManager: Moved player (Rigidbody path) to {spawnPos}");
            }
            else
            {
                player.transform.position = spawnPos;
                player.transform.rotation = Quaternion.Euler(0, spawnRot, 0);
                Debug.Log($"PlayerSpawnManager: Moved player (Transform path) to {spawnPos}");
            }
        }

        Debug.Log($"Player spawned at {spawnPos} facing {spawnRot} degrees");

        // Clear spawn data
        ClearSpawnData();
    }

    Vector3 GetSpawnPosition()
    {
        // Check if we have stored spawn data from a door transition
        if (PlayerPrefs.HasKey("SpawnPosX"))
        {
            Vector3 pos = new Vector3(
                PlayerPrefs.GetFloat("SpawnPosX"),
                PlayerPrefs.GetFloat("SpawnPosY"),
                PlayerPrefs.GetFloat("SpawnPosZ")
            );
            Debug.Log($"PlayerSpawnManager: Using stored spawn data from PlayerPrefs: {pos}");
            return pos;
        }

        // Check for scene-specific spawn points
        string lastDoor = PlayerPrefs.GetString("LastDoorUsed", "");
        if (!string.IsNullOrEmpty(lastDoor))
        {
            Debug.Log($"PlayerSpawnManager: LastDoorUsed = '{lastDoor}', checking doorSpawnPoints array ({doorSpawnPoints?.Length ?? 0} entries)");
            if (doorSpawnPoints != null)
            {
                foreach (var spawnPoint in doorSpawnPoints)
                {
                    if (spawnPoint.fromDoorName == lastDoor)
                    {
                        Debug.Log($"PlayerSpawnManager: Found matching door spawn point: {spawnPoint.spawnPosition}");
                        return spawnPoint.spawnPosition;
                    }
                }
            }
            Debug.Log($"PlayerSpawnManager: No matching door spawn point found for '{lastDoor}'");
        }
        else
        {
            Debug.Log("PlayerSpawnManager: No spawn data in PlayerPrefs");
        }

        // Use default spawn
        if (defaultSpawnPoint != null)
        {
            Debug.Log($"PlayerSpawnManager: Using defaultSpawnPoint: {defaultSpawnPoint.position}");
            return defaultSpawnPoint.position;
        }

        Debug.Log($"PlayerSpawnManager: Using defaultSpawnPosition: {defaultSpawnPosition}");
        return defaultSpawnPosition;
    }

    float GetSpawnRotation()
    {
        // Check if we have stored rotation data
        if (PlayerPrefs.HasKey("SpawnRotY"))
        {
            return PlayerPrefs.GetFloat("SpawnRotY");
        }

        // Check for scene-specific spawn points
        string lastDoor = PlayerPrefs.GetString("LastDoorUsed", "");
        if (!string.IsNullOrEmpty(lastDoor))
        {
            foreach (var spawnPoint in doorSpawnPoints)
            {
                if (spawnPoint.fromDoorName == lastDoor)
                {
                    return spawnPoint.spawnRotation;
                }
            }
        }

        // Use default rotation
        if (defaultSpawnPoint != null)
            return defaultSpawnPoint.eulerAngles.y;

        return defaultSpawnRotation;
    }

    void ClearSpawnData()
    {
        PlayerPrefs.DeleteKey("SpawnPosX");
        PlayerPrefs.DeleteKey("SpawnPosY");
        PlayerPrefs.DeleteKey("SpawnPosZ");
        PlayerPrefs.DeleteKey("SpawnRotY");
        PlayerPrefs.DeleteKey("LastDoorUsed");
        PlayerPrefs.Save();
    }
}

[System.Serializable]
public class DoorSpawnPoint
{
    public string fromDoorName;
    public Vector3 spawnPosition;
    public float spawnRotation;
}

