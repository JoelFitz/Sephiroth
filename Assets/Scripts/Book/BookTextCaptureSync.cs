using UnityEngine;
using UnityEngine.UI;

public class BookTextCaptureSync : MonoBehaviour
{
    [Header("Sources")]
    [SerializeField] private Image sourceLeftPage;
    [SerializeField] private Image sourceRightPage;

    [Header("Capture Targets")]
    [SerializeField] private Image captureLeftPage;
    [SerializeField] private Image captureRightPage;

    [Header("Sync")]
    [SerializeField] private bool syncEveryFrame = true;
    [SerializeField] private bool syncStyle = true;

    private void OnEnable()
    {
        SyncNow();
    }

    private void LateUpdate()
    {
        if (!syncEveryFrame)
            return;

        SyncNow();
    }

    [ContextMenu("Sync Now")]
    public void SyncNow()
    {
        Copy(sourceLeftPage, captureLeftPage);
        Copy(sourceRightPage, captureRightPage);
    }

    private void Copy(Image source, Image target)
    {
        if (source == null || target == null)
            return;

        if (target.sprite != source.sprite)
            target.sprite = source.sprite;

        if (!syncStyle)
            return;

        target.color = source.color;
        target.material = source.material;
        target.type = source.type;
        target.preserveAspect = source.preserveAspect;
        target.fillCenter = source.fillCenter;
        target.fillMethod = source.fillMethod;
        target.fillAmount = source.fillAmount;
        target.fillClockwise = source.fillClockwise;
        target.fillOrigin = source.fillOrigin;
        target.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        target.useSpriteMesh = source.useSpriteMesh;
    }
}
