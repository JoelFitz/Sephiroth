using UnityEngine;

[System.Serializable]
public class GrappleZoneData
{
    public string zoneName = "Grapple Zone";
    public bool isActive = true;
    public float requiredFacingAngle = 45f; // How precisely player must face the zone
    public bool requiresSpecificDirection = false;
    public Vector3 requiredFacingDirection = Vector3.forward;
}

public class GrappleZone : MonoBehaviour
{
    [Header("Zone Configuration")]
    [SerializeField] private GrappleZoneData zoneData;

    [Header("Grapple Points")]
    [Tooltip("Point where the tongue will attach (usually above the gap)")]
    public Transform grapplePoint;

    [Tooltip("Where the player will land after grappling")]
    public Transform targetPosition;

    [Header("Detection")]
    [SerializeField] private float detectionRadius = 3f;
    [SerializeField] private LayerMask playerLayer = -1;

    [Header("Visual Feedback")]
    [SerializeField] private GameObject grappleIndicator;
    [SerializeField] private Color gizmoColor = Color.cyan;
    [SerializeField] private bool showGizmos = true;

    // State
    private bool playerInRange = false;
    private bool playerCanGrapple = false;
    private Transform currentPlayer;

    void Start()
    {
        Debug.Log($"GrappleZone {name} initialized - isActive: {zoneData.isActive}");

        // Auto-setup grapple point if not assigned
        if (grapplePoint == null)
        {
            GameObject grappleObj = new GameObject("GrapplePoint");
            grappleObj.transform.SetParent(transform);
            grappleObj.transform.localPosition = Vector3.up * 4f; // Default height
            grapplePoint = grappleObj.transform;
            Debug.Log($"Auto-created grapple point for {name}");
        }

        // Auto-setup target position if not assigned
        if (targetPosition == null)
        {
            GameObject targetObj = new GameObject("TargetPosition");
            targetObj.transform.SetParent(transform);
            targetObj.transform.localPosition = Vector3.forward * 6f; // Default distance
            targetPosition = targetObj.transform;
            Debug.Log($"Auto-created target position for {name}");
        }

        // Setup indicator
        if (grappleIndicator != null)
            grappleIndicator.SetActive(false);
    }

    void Update()
    {
        DetectPlayer();
        UpdateVisualFeedback();
    }

    void DetectPlayer()
    {
        // Find players in range using alternative method (similar to TongueGrappleSystem)
        TongueGrappleSystem[] grapplers = FindObjectsOfType<TongueGrappleSystem>();

        bool foundValidPlayer = false;

        foreach (var grappler in grapplers)
        {
            float distance = Vector3.Distance(transform.position, grappler.transform.position);

            if (distance <= detectionRadius)
            {
                currentPlayer = grappler.transform;
                playerInRange = true;

                // Check if player is facing the right direction
                if (IsPlayerFacingCorrectDirection(currentPlayer))
                {
                    playerCanGrapple = true;
                    foundValidPlayer = true;
                    Debug.Log($"GrappleZone {name}: Player can grapple! Distance: {distance:F2}, Facing: OK");
                    break;
                }
                else
                {
                    Debug.Log($"GrappleZone {name}: Player in range but not facing correctly");
                }
            }
        }

        if (!foundValidPlayer)
        {
            if (playerInRange && !playerCanGrapple)
            {
                Debug.Log($"GrappleZone {name}: Player in range but cannot grapple");
            }

            playerInRange = false;
            playerCanGrapple = false;
            currentPlayer = null;
        }
    }

    bool IsPlayerFacingCorrectDirection(Transform player)
    {
        if (!zoneData.requiresSpecificDirection)
        {
            // Just check if facing generally towards the grapple point
            Vector3 directionToGrapple = (grapplePoint.position - player.position).normalized;
            float dotProduct = Vector3.Dot(player.forward, directionToGrapple);
            float requiredDot = Mathf.Cos(zoneData.requiredFacingAngle * Mathf.Deg2Rad);

            Debug.Log($"GrappleZone {name}: Facing check - DotProduct: {dotProduct:F3}, Required: {requiredDot:F3}, Angle: {zoneData.requiredFacingAngle}°");

            return dotProduct > requiredDot;
        }
        else
        {
            // Check specific direction
            float dotProduct = Vector3.Dot(player.forward, zoneData.requiredFacingDirection.normalized);
            float requiredDot = Mathf.Cos(zoneData.requiredFacingAngle * Mathf.Deg2Rad);

            Debug.Log($"GrappleZone {name}: Specific direction check - DotProduct: {dotProduct:F3}, Required: {requiredDot:F3}");

            return dotProduct > requiredDot;
        }
    }

    void UpdateVisualFeedback()
    {
        if (grappleIndicator != null)
        {
            grappleIndicator.SetActive(playerCanGrapple);
        }
    }

    #region Public Interface

    public bool CanGrapple()
    {
        Debug.Log($"GrappleZone {name}: CanGrapple() - isActive: {zoneData.isActive}, playerCanGrapple: {playerCanGrapple}");
        return zoneData.isActive && playerCanGrapple;
    }

    public Transform GetGrapplePoint()
    {
        return grapplePoint;
    }

    public Vector3 GetTargetPosition()
    {
        return targetPosition.position;
    }

    public bool IsPlayerInRange()
    {
        return playerInRange;
    }

    public bool IsPlayerFacingCorrectly()
    {
        return playerCanGrapple;
    }

    public void SetActive(bool active)
    {
        zoneData.isActive = active;
        Debug.Log($"GrappleZone {name}: SetActive({active})");
    }

    public string GetZoneName()
    {
        return zoneData.zoneName;
    }

    #endregion

    #region Editor Helpers

    public void SetGrapplePoint(Vector3 localPosition)
    {
        if (grapplePoint != null)
            grapplePoint.localPosition = localPosition;
    }

    public void SetTargetPosition(Vector3 localPosition)
    {
        if (targetPosition != null)
            targetPosition.localPosition = localPosition;
    }

    #endregion

    // Visualization
    void OnDrawGizmos()
    {
        if (!showGizmos) return;

        // Draw detection radius
        Gizmos.color = playerInRange ? Color.green : gizmoColor;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // Draw grapple point
        if (grapplePoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(grapplePoint.position, 0.3f);
            Gizmos.DrawLine(transform.position, grapplePoint.position);
        }

        // Draw target position
        if (targetPosition != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(targetPosition.position, Vector3.one * 0.5f);

            if (grapplePoint != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(grapplePoint.position, targetPosition.position);
            }
        }

        // Draw required facing direction
        if (zoneData.requiresSpecificDirection)
        {
            Gizmos.color = Color.blue;
            Vector3 directionEnd = transform.position + zoneData.requiredFacingDirection.normalized * 2f;
            Gizmos.DrawRay(transform.position, zoneData.requiredFacingDirection.normalized * 2f);
        }

        // Draw zone name
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, zoneData.zoneName);
#endif
    }

    void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;

        // Draw facing angle cone
        if (currentPlayer != null || zoneData.requiresSpecificDirection)
        {
            Vector3 centerDirection = zoneData.requiresSpecificDirection
                ? zoneData.requiredFacingDirection.normalized
                : (grapplePoint != null ? (grapplePoint.position - transform.position).normalized : transform.forward);

            // Create cone visualization
            Vector3 rightEdge = Quaternion.AngleAxis(zoneData.requiredFacingAngle * 0.5f, Vector3.up) * centerDirection;
            Vector3 leftEdge = Quaternion.AngleAxis(-zoneData.requiredFacingAngle * 0.5f, Vector3.up) * centerDirection;

            Gizmos.color = Color.white;
            Gizmos.DrawRay(transform.position, rightEdge * detectionRadius);
            Gizmos.DrawRay(transform.position, leftEdge * detectionRadius);
            Gizmos.DrawRay(transform.position, centerDirection * detectionRadius);
        }
    }
}


