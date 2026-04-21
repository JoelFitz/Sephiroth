using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class RiverPush : MonoBehaviour
{
    [Header("River Push")]
    [SerializeField] private bool useLocalDirection = true;
    [SerializeField] private Vector3 pushDirection = Vector3.forward;
    [SerializeField] private float pushStrength = 3f;
    [SerializeField] private bool normalizeDirection = true;

    private readonly HashSet<Transform> overlappingTargets = new HashSet<Transform>();
    private BoxCollider riverCollider;

    private void Awake()
    {
        riverCollider = GetComponent<BoxCollider>();
        if (riverCollider != null)
        {
            riverCollider.isTrigger = true;
        }
    }

    private void OnValidate()
    {
        riverCollider = GetComponent<BoxCollider>();
        if (riverCollider != null)
        {
            riverCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Transform target = GetTargetTransform(other);
        if (target != null)
        {
            overlappingTargets.Add(target);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Transform target = GetTargetTransform(other);
        if (target != null)
        {
            overlappingTargets.Remove(target);
        }
    }

    private void FixedUpdate()
    {
        if (overlappingTargets.Count == 0)
        {
            return;
        }

        Vector3 worldDirection = GetWorldDirection();
        if (worldDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        Vector3 movement = worldDirection * (pushStrength * Time.fixedDeltaTime);
        List<Transform> targetsToRemove = null;

        foreach (Transform target in overlappingTargets)
        {
            if (target == null)
            {
                targetsToRemove ??= new List<Transform>();
                targetsToRemove.Add(target);
                continue;
            }

            if (target.TryGetComponent<CharacterController>(out CharacterController characterController))
            {
                characterController.Move(movement);
                continue;
            }

            if (target.TryGetComponent<Rigidbody>(out Rigidbody rigidbody))
            {
                rigidbody.AddForce(worldDirection * pushStrength, ForceMode.Acceleration);
                continue;
            }

            target.position += movement;
        }

        if (targetsToRemove != null)
        {
            foreach (Transform target in targetsToRemove)
            {
                overlappingTargets.Remove(target);
            }
        }
    }

    private Vector3 GetWorldDirection()
    {
        Vector3 direction = pushDirection;

        if (useLocalDirection)
        {
            direction = transform.TransformDirection(direction);
        }

        if (normalizeDirection && direction.sqrMagnitude > Mathf.Epsilon)
        {
            direction.Normalize();
        }

        return direction;
    }

    private Transform GetTargetTransform(Collider other)
    {
        if (other == null)
        {
            return null;
        }

        CharacterController characterController = other.GetComponentInParent<CharacterController>();
        if (characterController != null)
        {
            return characterController.transform;
        }

        Rigidbody rigidbody = other.attachedRigidbody;
        if (rigidbody != null)
        {
            return rigidbody.transform;
        }

        return other.transform.root;
    }
}
