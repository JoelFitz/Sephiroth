using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class BookPageTextWrapController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Image targetImage;
    [SerializeField] private Shader wrapShader;
    [SerializeField] private Material materialTemplate;
    [SerializeField] private bool createRuntimeMaterialInstance = true;

    [Header("Page Sprites")]
    [SerializeField] private Sprite leftPageSprite;
    [SerializeField] private Sprite rightPageSprite;

    [Header("Depth")]
    [SerializeField] private Sprite bookDepthSprite;

    [Header("Look")]
    [SerializeField] private Color textTint = Color.white;
    [SerializeField, Range(0f, 1f)] private float textTintStrength = 0f;
    [SerializeField, Range(0f, 1f)] private float textBlend = 1f;
    [SerializeField, Range(0f, 1f)] private float depthShadow = 0.2f;
    [SerializeField, Range(-1f, 1f)] private float depthBias = 0f;
    [SerializeField, Range(0.5f, 1.5f)] private float pageImageScale = 1f;
    [SerializeField, Range(0.5f, 1.5f)] private float pageImageScaleX = 1f;
    [SerializeField, Range(-1f, 1f)] private float pageSeparation = 0f;

    [Header("Warp")]
    [SerializeField, Range(-0.25f, 0.25f)] private float leftWarpStrength = 0.03f;
    [SerializeField, Range(-0.25f, 0.25f)] private float rightWarpStrength = 0.03f;
    [SerializeField, Range(0.1f, 0.9f)] private float pageSplit = 0.5f;

    [Header("Depth Isolation")]
    [SerializeField, Range(0.1f, 1f)] private float depthColorSoftness = 0.4f;
    [SerializeField, Range(0f, 1f)] private float depthColorThreshold = 0.35f;

    private Material runtimeMaterial;
    private Sprite runtimeLeftPageSpriteOverride;
    private Sprite runtimeRightPageSpriteOverride;
    private bool runtimePageSpritesOverrideActive;
    private Sprite runtimeDepthSpriteOverride;
    private bool overlayVisible = true;

    private static readonly int LeftTextTexId = Shader.PropertyToID("_LeftTextTex");
    private static readonly int RightTextTexId = Shader.PropertyToID("_RightTextTex");
    private static readonly int BookDepthTexId = Shader.PropertyToID("_BookDepthTex");
    private static readonly int LeftDepthTexId = Shader.PropertyToID("_LeftDepthTex");
    private static readonly int RightDepthTexId = Shader.PropertyToID("_RightDepthTex");
    private static readonly int TextTintId = Shader.PropertyToID("_TextTint");
    private static readonly int TextTintStrengthId = Shader.PropertyToID("_TextTintStrength");
    private static readonly int TextBlendId = Shader.PropertyToID("_TextBlend");
    private static readonly int DepthShadowId = Shader.PropertyToID("_DepthShadow");
    private static readonly int DepthBiasId = Shader.PropertyToID("_DepthBias");
    private static readonly int TextScaleId = Shader.PropertyToID("_TextScale");
    private static readonly int TextScaleXId = Shader.PropertyToID("_TextScaleX");
    private static readonly int PageSeparationId = Shader.PropertyToID("_PageSeparation");
    private static readonly int LeftWarpStrengthId = Shader.PropertyToID("_LeftWarpStrength");
    private static readonly int RightWarpStrengthId = Shader.PropertyToID("_RightWarpStrength");
    private static readonly int PageSplitId = Shader.PropertyToID("_PageSplit");
    private static readonly int DepthColorSoftnessId = Shader.PropertyToID("_DepthColorSoftness");
    private static readonly int DepthColorThresholdId = Shader.PropertyToID("_DepthColorThreshold");

    private void OnEnable()
    {
        if (overlayVisible)
            ApplyWrapMaterial();
    }

    private void LateUpdate()
    {
        if (!overlayVisible || runtimeMaterial == null)
            return;

        PushPropertiesToMaterial(runtimeMaterial);
    }

    private void OnDisable()
    {
        CleanupRuntimeMaterial();
    }

    private void OnDestroy()
    {
        CleanupRuntimeMaterial();
    }

    [ContextMenu("Apply Wrap Material")]
    public void ApplyWrapMaterial()
    {
        if (targetImage == null)
            targetImage = GetComponent<Image>();

        if (targetImage == null)
        {
            Debug.LogWarning("BookPageTextWrapController: target Image is missing.");
            return;
        }

        Material materialToUse = ResolveMaterial();
        if (materialToUse == null)
        {
            Debug.LogWarning("BookPageTextWrapController: Could not create wrap material.");
            return;
        }

        targetImage.material = materialToUse;
        PushPropertiesToMaterial(materialToUse);
    }

    public void SetOverlayVisible(bool isVisible)
    {
        overlayVisible = isVisible;

        if (targetImage == null)
            targetImage = GetComponent<Image>();

        if (targetImage == null)
            return;

        if (isVisible)
        {
            ApplyWrapMaterial();
        }
        else
        {
            targetImage.material = null;
        }
    }

    public void SetRuntimeDepthSprite(Sprite depthSprite)
    {
        runtimeDepthSpriteOverride = depthSprite;

        if (runtimeMaterial != null)
            PushPropertiesToMaterial(runtimeMaterial);
    }

    public void ClearRuntimeDepthSprite()
    {
        runtimeDepthSpriteOverride = null;

        if (runtimeMaterial != null)
            PushPropertiesToMaterial(runtimeMaterial);
    }

    public void SetPageSprites(Sprite leftSprite, Sprite rightSprite)
    {
        runtimeLeftPageSpriteOverride = leftSprite;
        runtimeRightPageSpriteOverride = rightSprite;
        runtimePageSpritesOverrideActive = true;

        if (runtimeMaterial != null)
            PushPropertiesToMaterial(runtimeMaterial);
    }

    public void ClearPageSpritesOverride()
    {
        runtimeLeftPageSpriteOverride = null;
        runtimeRightPageSpriteOverride = null;
        runtimePageSpritesOverrideActive = false;

        if (runtimeMaterial != null)
            PushPropertiesToMaterial(runtimeMaterial);
    }

    [ContextMenu("Clear Wrap Material")]
    public void ClearWrapMaterial()
    {
        if (targetImage == null)
            targetImage = GetComponent<Image>();

        if (targetImage != null)
            targetImage.material = null;

        CleanupRuntimeMaterial();
    }

    private Material ResolveMaterial()
    {
        if (runtimeMaterial != null)
            return runtimeMaterial;

        if (materialTemplate != null)
        {
            if (createRuntimeMaterialInstance)
            {
                runtimeMaterial = new Material(materialTemplate)
                {
                    name = materialTemplate.name + " (Runtime)"
                };
            }
            else
            {
                runtimeMaterial = materialTemplate;
            }

            return runtimeMaterial;
        }

        Shader shaderToUse = wrapShader != null ? wrapShader : Shader.Find("UI/BookPageTextWrap");
        if (shaderToUse == null)
            return null;

        runtimeMaterial = new Material(shaderToUse)
        {
            name = "BookPageTextWrap (Runtime)"
        };

        return runtimeMaterial;
    }

    private void PushPropertiesToMaterial(Material material)
    {
        Sprite activeLeftSprite = runtimePageSpritesOverrideActive ? runtimeLeftPageSpriteOverride : leftPageSprite;
        Sprite activeRightSprite = runtimePageSpritesOverrideActive ? runtimeRightPageSpriteOverride : rightPageSprite;

        Texture leftTexture = activeLeftSprite != null ? activeLeftSprite.texture : null;
        Texture rightTexture = activeRightSprite != null ? activeRightSprite.texture : null;
        Sprite activeDepthSprite = runtimeDepthSpriteOverride != null ? runtimeDepthSpriteOverride : bookDepthSprite;
        Texture depthTexture = activeDepthSprite != null ? activeDepthSprite.texture : null;

        Vector4 leftST = GetSpriteST(activeLeftSprite);
        Vector4 rightST = GetSpriteST(activeRightSprite);
        Vector4 depthST = GetSpriteST(activeDepthSprite);

        SetTextureWithST(material, LeftTextTexId, leftTexture, leftST);
        SetTextureWithST(material, RightTextTexId, rightTexture, rightST);
        SetTextureWithST(material, BookDepthTexId, depthTexture, depthST);

        // Keep legacy shader properties in sync for backward compatibility.
        material.SetTexture(LeftDepthTexId, depthTexture);
        material.SetTexture(RightDepthTexId, depthTexture);

        material.SetColor(TextTintId, textTint);
        material.SetFloat(TextTintStrengthId, textTintStrength);
        material.SetFloat(TextBlendId, textBlend);
        material.SetFloat(DepthShadowId, depthShadow);
        material.SetFloat(DepthBiasId, depthBias);
        material.SetFloat(TextScaleId, pageImageScale);
        material.SetFloat(TextScaleXId, pageImageScaleX);
        material.SetFloat(PageSeparationId, pageSeparation);

        material.SetFloat(LeftWarpStrengthId, leftWarpStrength);
        material.SetFloat(RightWarpStrengthId, rightWarpStrength);
        material.SetFloat(PageSplitId, pageSplit);
        material.SetFloat(DepthColorSoftnessId, depthColorSoftness);
        material.SetFloat(DepthColorThresholdId, depthColorThreshold);
    }

    private static Vector4 GetSpriteST(Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
            return new Vector4(1f, 1f, 0f, 0f);

        Rect rect = sprite.textureRect;
        Texture texture = sprite.texture;

        float invWidth = 1f / texture.width;
        float invHeight = 1f / texture.height;

        return new Vector4(
            rect.width * invWidth,
            rect.height * invHeight,
            rect.x * invWidth,
            rect.y * invHeight
        );
    }

    private static void SetTextureWithST(Material material, int texturePropertyId, Texture texture, Vector4 st)
    {
        material.SetTexture(texturePropertyId, texture);
        material.SetTextureScale(texturePropertyId, new Vector2(st.x, st.y));
        material.SetTextureOffset(texturePropertyId, new Vector2(st.z, st.w));
    }

    private void CleanupRuntimeMaterial()
    {
        if (runtimeMaterial == null)
            return;

        if (Application.isPlaying)
            Destroy(runtimeMaterial);
        else
            DestroyImmediate(runtimeMaterial);

        runtimeMaterial = null;
    }
}
