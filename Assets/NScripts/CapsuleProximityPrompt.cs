using UnityEngine;

public class CapsuleProximityPrompt : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform targetTransform;
    [SerializeField] private Transform otherTransform;
    [SerializeField] private GameObject uiRoot;

    [Header("Trigger Mode")]
    [SerializeField] private bool useDistanceCheck = true;
    [SerializeField] private float triggerRadius = 1f;

    [Header("Prompts")]
    [TextArea(2, 4)]
    [SerializeField] private string[] randomPrompts;

    [Header("Debug")]
    [SerializeField] private float currentDistance = -1f;

    private bool wasInside = false;

    private void Update()
    {
        if (!useDistanceCheck)
            return;

        if (targetTransform == null || otherTransform == null)
            return;

        float distance = Vector3.Distance(targetTransform.position, otherTransform.position);
        currentDistance = distance;
        bool isInside = distance <= triggerRadius;

        if (isInside && !wasInside)
        {
            Debug.Log($"[CapsuleProximityPrompt] 들어왔습니다. ({distance:F2}m <= {triggerRadius:F2}m)");
            SetUiVisible(true);
        }
        else if (!isInside && wasInside)
        {
            SetUiVisible(false);
        }

        wasInside = isInside;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (useDistanceCheck)
            return;

        if (!IsOther(other))
            return;

        Debug.Log("[CapsuleProximityPrompt] Trigger enter.");
        SetUiVisible(true);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (useDistanceCheck)
            return;

        if (!IsOther(collision.collider))
            return;

        Debug.Log("[CapsuleProximityPrompt] Collision enter.");
        SetUiVisible(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (useDistanceCheck)
            return;

        if (!IsOther(other))
            return;

        SetUiVisible(false);
    }

    private void OnCollisionExit(Collision collision)
    {
        if (useDistanceCheck)
            return;

        if (!IsOther(collision.collider))
            return;

        SetUiVisible(false);
    }

    private bool IsOther(Collider other)
    {
        if (otherTransform == null)
            return true;

        return other.transform == otherTransform;
    }

    private void SetUiVisible(bool visible)
    {
        if (uiRoot != null && uiRoot.activeSelf != visible)
        {
            uiRoot.SetActive(visible);
        }
    }

    private void OnDrawGizmos()
    {
        if (!useDistanceCheck)
            return;

        if (targetTransform == null)
            return;

        Gizmos.color = new Color(0f, 1f, 1f, 0.35f);
        Gizmos.DrawWireSphere(targetTransform.position, triggerRadius);
    }
}
