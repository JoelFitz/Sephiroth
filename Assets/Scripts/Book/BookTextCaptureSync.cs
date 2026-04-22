using TMPro;
using UnityEngine;

public class BookTextCaptureSync : MonoBehaviour
{
    [Header("Sources")]
    [SerializeField] private TMP_Text sourceLeftTitle;
    [SerializeField] private TMP_Text sourceLeftContent;
    [SerializeField] private TMP_Text sourceRightTitle;
    [SerializeField] private TMP_Text sourceRightContent;

    [Header("Capture Targets")]
    [SerializeField] private TMP_Text captureLeftTitle;
    [SerializeField] private TMP_Text captureLeftContent;
    [SerializeField] private TMP_Text captureRightTitle;
    [SerializeField] private TMP_Text captureRightContent;

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
        Copy(sourceLeftTitle, captureLeftTitle);
        Copy(sourceLeftContent, captureLeftContent);
        Copy(sourceRightTitle, captureRightTitle);
        Copy(sourceRightContent, captureRightContent);
    }

    private void Copy(TMP_Text source, TMP_Text target)
    {
        if (source == null || target == null)
            return;

        if (target.text != source.text)
            target.text = source.text;

        if (!syncStyle)
            return;

        target.font = source.font;
        target.fontSharedMaterial = source.fontSharedMaterial;
        target.fontSize = source.fontSize;
        target.enableWordWrapping = source.enableWordWrapping;
        target.overflowMode = source.overflowMode;
        target.alignment = source.alignment;
        target.margin = source.margin;
        target.color = source.color;
        target.lineSpacing = source.lineSpacing;
        target.characterSpacing = source.characterSpacing;
        target.wordSpacing = source.wordSpacing;
        target.enableAutoSizing = source.enableAutoSizing;
        target.fontSizeMin = source.fontSizeMin;
        target.fontSizeMax = source.fontSizeMax;
        target.richText = source.richText;
    }
}
