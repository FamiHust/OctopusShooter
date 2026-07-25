using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HiddenShooter : BaseShooter
{
    [SerializeField] private GameObject questionMarkTxt;
    [SerializeField] private GameObject questionMarkEffect;

    private Material[] originalMaterials;
    private Material[] hiddenMaterials;
    private bool visualsInitialized;
    private bool isHiddenVisual;

    protected override void OnEnable()
    {
        base.OnEnable();
        EnsureVisualSetup();
        UpdateVisualByCurrentState();
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        TryAutoAssignQuestionMarkRefs();
    }

    private void TryAutoAssignQuestionMarkRefs()
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        if (children == null || children.Length == 0)
        {
            return;
        }

        questionMarkTxt = null;
        questionMarkEffect = null;

        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child == transform)
            {
                continue;
            }

            if (questionMarkTxt == null &&
                child.name.IndexOf("txt_?", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                questionMarkTxt = child.gameObject;
            }

            if (questionMarkEffect == null &&
                child.name.IndexOf("Question", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                questionMarkEffect = child.gameObject;
            }

            if (questionMarkTxt != null && questionMarkEffect != null)
            {
                break;
            }
        }
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();

        UpdateVisualByCurrentState();
    }

    private void UpdateVisualByCurrentState()
    {
        bool revealForPickLockedMode = ShouldRevealForPickLockedMode();
        ShooterState currentState = GetCurrentState();
        bool shouldHide = currentState == ShooterState.Lock && !revealForPickLockedMode;

        if (shouldHide)
        {
            if (!isHiddenVisual)
            {
                ApplyHiddenVisual();
            }
            return;
        }

        if (isHiddenVisual)
        {
            bool playEffect = !revealForPickLockedMode;
            RevealOriginalVisual(playEffect);
        }
    }

    private bool ShouldRevealForPickLockedMode()
    {
        BoosterManager boosterManager = BoosterManager.Instance;
        return boosterManager != null && boosterManager.IsPickLockedShooterModeActive();
    }

    private void EnsureVisualSetup()
    {
        if (visualsInitialized || mesh == null)
        {
            return;
        }

        // Dùng sharedMaterials để lấy trực tiếp Material từ Prefab (không tạo bản sao)
        originalMaterials = mesh.sharedMaterials;

        // Tạo sẵn mảng Material dùng cho trạng thái ẩn
        hiddenMaterials = new Material[originalMaterials.Length];

        // Kiểm tra xem Prefab có gắn đủ 2 Material không (Material 0: Gốc, Material 1: Hidden)
        if (originalMaterials.Length > 1)
        {
            Material hiddenMat = originalMaterials[1];

            // Phủ Material Hidden lên tất cả các SubMesh
            for (int i = 0; i < hiddenMaterials.Length; i++)
            {
                hiddenMaterials[i] = hiddenMat;
            }
        }
        else
        {
            // Fallback an toàn nếu lỡ quên gắn Material thứ 2 trên Inspector
            hiddenMaterials = originalMaterials;
            ;
        }

        visualsInitialized = true;
    }

    private void ApplyHiddenVisual()
    {
        EnsureVisualSetup();

        if (mesh != null && hiddenMaterials != null)
        {
            // Tráo mảng Material thành Hidden
            mesh.sharedMaterials = hiddenMaterials;
        }

        if (questionMarkTxt != null)
        {
            questionMarkTxt.SetActive(true);
        }

        isHiddenVisual = true;
    }

    private void RevealOriginalVisual(bool playRevealEffect = true)
    {
        EnsureVisualSetup();

        if (mesh != null && originalMaterials != null)
        {
            // Trả lại mảng Material gốc ban đầu
            mesh.sharedMaterials = originalMaterials;
        }

        if (questionMarkTxt != null)
        {
            questionMarkTxt.SetActive(false);
        }

        if (playRevealEffect)
        {
            PlayQuestionMarkEffect();
        }

        isHiddenVisual = false;
    }

    private void PlayQuestionMarkEffect()
    {
        if (questionMarkEffect == null)
        {
            return;
        }

        questionMarkEffect.SetActive(true);

        ParticleSystem fxParticle = questionMarkEffect.GetComponent<ParticleSystem>();
        if (fxParticle != null)
        {
            fxParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            fxParticle.Play();
        }
    }
}
