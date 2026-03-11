using UnityEngine;

/// <summary>
/// World-space marker for a swing grapple point placed above a gap.
///
/// Setup:
///   - Place this component on a GameObject positioned above the gap.
///   - Assign (or let the component auto-create) an AnchorPoint child transform
///     high above the gap – this is where the rope attaches.
///   - Assign two approach transforms (PositionA / PositionB) on either side of the gap.
///     The player must be within activationRadius of one of them to activate the grapple.
///   - When using the auto-create path (adding the component in Edit mode), default
///     children are generated; move them in the Scene view to fit your level geometry.
/// </summary>
public class SwingGrappleZone : MonoBehaviour
{
    [Header("Swing Points")]
    [Tooltip("The overhead point the rope attaches to. Should be well above the gap.")]
    public Transform anchorPoint;

    [Tooltip("Approach / landing zone on side A.")]
    public Transform positionA;

    [Tooltip("Approach / landing zone on side B.")]
    public Transform positionB;

    [Header("Detection")]
    [Tooltip("Radius within which the player can activate the grapple from either position.")]
    public float activationRadius = 4f;

    [Header("Display Name")]
    [Tooltip("Name shown in the UI indicator.")]
    public string zoneName = "Grapple";

    [Header("Gizmos")]
    public bool showGizmos = true;

    // ── Editor auto-setup ──────────────────────────────────────────────────────

    void Reset()
    {
        if (anchorPoint == null)
        {
            var go = new GameObject("AnchorPoint");
            go.transform.SetParent(transform);
            go.transform.localPosition = new Vector3(0f, 7f, 0f);
            anchorPoint = go.transform;
        }

        if (positionA == null)
        {
            var go = new GameObject("PositionA");
            go.transform.SetParent(transform);
            go.transform.localPosition = new Vector3(-5f, 0f, 0f);
            positionA = go.transform;
        }

        if (positionB == null)
        {
            var go = new GameObject("PositionB");
            go.transform.SetParent(transform);
            go.transform.localPosition = new Vector3(5f, 0f, 0f);
            positionB = go.transform;
        }
    }

    // ── Public query API ───────────────────────────────────────────────────────

    /// <summary>True if the player is close enough to PositionA or PositionB to activate.</summary>
    public bool IsPlayerInRange(Vector3 playerWorldPos)
    {
        if (positionA != null && Vector3.Distance(playerWorldPos, positionA.position) <= activationRadius)
            return true;
        if (positionB != null && Vector3.Distance(playerWorldPos, positionB.position) <= activationRadius)
            return true;
        return false;
    }

    /// <summary>
    /// Returns whichever activation point (A or B) is closest to the player and within
    /// activationRadius, or null if neither qualifies.
    /// </summary>
    public Transform GetNearestActivationPoint(Vector3 playerWorldPos)
    {
        float distA = positionA != null ? Vector3.Distance(playerWorldPos, positionA.position) : float.MaxValue;
        float distB = positionB != null ? Vector3.Distance(playerWorldPos, positionB.position) : float.MaxValue;

        if (distA <= activationRadius && distA <= distB) return positionA;
        if (distB <= activationRadius)                   return positionB;
        return null;
    }

    /// <summary>Returns the "other" side: A → B, B → A.</summary>
    public Transform GetOppositeSide(Transform fromSide)
    {
        if (fromSide == positionA) return positionB;
        if (fromSide == positionB) return positionA;
        return null;
    }

    // ── Editor visualization ───────────────────────────────────────────────────

    void OnDrawGizmos()
    {
        if (!showGizmos) return;

        // Anchor
        if (anchorPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(anchorPoint.position, 0.35f);
        }

        // Position A
        if (positionA != null)
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f);
            Gizmos.DrawWireSphere(positionA.position, activationRadius);
            Gizmos.DrawSphere(positionA.position, 0.25f);
            if (anchorPoint != null)
                Gizmos.DrawLine(positionA.position, anchorPoint.position);
        }

        // Position B
        if (positionB != null)
        {
            Gizmos.color = new Color(1f, 0.4f, 0.8f);
            Gizmos.DrawWireSphere(positionB.position, activationRadius);
            Gizmos.DrawSphere(positionB.position, 0.25f);
            if (anchorPoint != null)
                Gizmos.DrawLine(positionB.position, anchorPoint.position);
        }

        // A ↔ B connector
        if (positionA != null && positionB != null)
        {
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.6f);
            Gizmos.DrawLine(positionA.position, positionB.position);
        }

#if UNITY_EDITOR
        if (anchorPoint != null)
            UnityEditor.Handles.Label(anchorPoint.position + Vector3.up * 0.5f, zoneName);
#endif
    }
}
