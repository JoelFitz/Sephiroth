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

    [Header("Text Textures")]
    [SerializeField] private Texture leftTextTexture;
    [SerializeField] private Texture rightTextTexture;

    [Header("Depth Textures")]
    [SerializeField] private Texture leftDepthTexture;
    [SerializeField] private Texture rightDepthTexture;

    [Header("Look")]
    [SerializeField] private Color textTint = new Color(0.07f, 0.06f, 0.05f, 1f);
    [SerializeField, Range(0f, 1f)] private float textBlend = 1f;
    [SerializeField, Range(0f, 1f)] private float depthShadow = 0.2f;
    [SerializeField, Range(-1f, 1f)] private float depthBias = 0f;

    [Header("Warp")]
    [SerializeField, Range(-0.25f, 0.25f)] private float leftWarpStrength = 0.03f;
    [SerializeField, Range(-0.25f, 0.25f)] private float rightWarpStrength = 0.03f;
    [SerializeField, Range(0.1f, 0.9f)] private float pageSplit = 0.5f;

    private Material runtimeMaterial;

    private static readonly int LeftTextTexId = Shader.PropertyToID("_LeftTextTex");
    private static readonly int RightTextTexId = Shader.PropertyToID("_RightTextTex");
    private static readonly int LeftDepthTexId = Shader.PropertyToID("_LeftDepthTex");
    private static readonly int RightDepthTexId = Shader.PropertyToID("_RightDepthTex");
    private static readonly int TextTintId = Shader.PropertyToID("_TextTint");
    private static readonly int TextBlendId = Shader.PropertyToID("_TextBlend");
    private static readonly int DepthShadowId = Shader.PropertyToID("_DepthShadow");
    private static readonly int DepthBiasId = Shader.PropertyToID("_DepthBias");
    private static readonly int LeftWarpStrengthId = Shader.PropertyToID("_LeftWarpStrength");
    private static readonly int RightWarpStrengthId = Shader.PropertyToID("_RightWarpStrength");
    private static readonly int PageSplitId = Shader.PropertyToID("_PageSplit");

    private void OnEnable()
    {
        ApplyWrapMaterial();
    }

    private void LateUpdate()
    {
        if (runtimeMaterial == null)
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
        material.SetTexture(LeftTextTexId, leftTextTexture);
        material.SetTexture(RightTextTexId, rightTextTexture);
        material.SetTexture(LeftDepthTexId, leftDepthTexture);
        material.SetTexture(RightDepthTexId, rightDepthTexture);

        material.SetColor(TextTintId, textTint);
        material.SetFloat(TextBlendId, textBlend);
        material.SetFloat(DepthShadowId, depthShadow);
        material.SetFloat(DepthBiasId, depthBias);

        material.SetFloat(LeftWarpStrengthId, leftWarpStrength);
        material.SetFloat(RightWarpStrengthId, rightWarpStrength);
        material.SetFloat(PageSplitId, pageSplit);
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
