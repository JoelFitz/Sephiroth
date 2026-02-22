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

        Vector3 spawnPos = GetSpawnPosition();
        float spawnRot = GetSpawnRotation();

        // Position the player
        if (player.GetComponent<CharacterController>() != null)
        {
            // For CharacterController, disable it briefly to move the player
            CharacterController cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            player.transform.position = spawnPos;
            player.transform.rotation = Quaternion.Euler(0, spawnRot, 0);
            cc.enabled = true;
        }
        else
        {
            // For Rigidbody or regular transform
            player.transform.position = spawnPos;
            player.transform.rotation = Quaternion.Euler(0, spawnRot, 0);
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
            return new Vector3(
                PlayerPrefs.GetFloat("SpawnPosX"),
                PlayerPrefs.GetFloat("SpawnPosY"),
                PlayerPrefs.GetFloat("SpawnPosZ")
            );
        }

        // Check for scene-specific spawn points
        string lastDoor = PlayerPrefs.GetString("LastDoorUsed", "");
        if (!string.IsNullOrEmpty(lastDoor))
        {
            foreach (var spawnPoint in doorSpawnPoints)
            {
                if (spawnPoint.fromDoorName == lastDoor)
                {
                    return spawnPoint.spawnPosition;
                }
            }
        }

        // Use default spawn
        if (defaultSpawnPoint != null)
            return defaultSpawnPoint.position;

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

