using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class HiddenShooter : BaseShooter
{
    [SerializeField] private GameObject questionMarkTxt;
    [SerializeField] private GameObject questionMarkEffect;
    [SerializeField] private Material hiddenMaterial;
    [SerializeField] private float questionMarkOffsetY = 0f;

    private Material[] originalMaterials;
    private Material[] hiddenMaterials;
    private bool visualsInitialized;
    private bool isHiddenVisual;

    public bool IsHidden => isHiddenVisual;

    public override bool IsSelectableForMoveShooter()
    {
        if (GetCurrentState() == ShooterState.Lock || isHiddenVisual)
        {
            return false;
        }
        return base.IsSelectableForMoveShooter();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        EnsureVisualSetup();
        UpdateVisualByCurrentState();
        EnsureQuestionMarkRotationZ();
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        TryAutoAssignQuestionMarkRefs();
        EnsureQuestionMarkRotationZ();
#if UNITY_EDITOR
        TryAutoAssignHiddenMaterial();
#endif
    }

#if UNITY_EDITOR
    private void TryAutoAssignHiddenMaterial()
    {
        if (hiddenMaterial == null)
        {
            hiddenMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/M_BlindShooter.mat");
        }
    }
#endif

    public void EnsureQuestionMarkRotationZ()
    {
        if (questionMarkTxt != null)
        {
            Vector3 currentEuler = questionMarkTxt.transform.localEulerAngles;
            if (!Mathf.Approximately(currentEuler.x, 20f) || !Mathf.Approximately(currentEuler.z, 180f))
            {
                questionMarkTxt.transform.localEulerAngles = new Vector3(20f, currentEuler.y, 180f);
            }

            RectTransform rectTransform = questionMarkTxt.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                Vector2 p = rectTransform.pivot;
                if (!Mathf.Approximately(p.y, 0.65f))
                {
                    rectTransform.pivot = new Vector2(p.x, 0.65f);
                }

                if (questionMarkOffsetY != 0f)
                {
                    Vector2 pos = rectTransform.anchoredPosition;
                    pos.y = questionMarkOffsetY;
                    rectTransform.anchoredPosition = pos;
                }
            }
            else if (questionMarkOffsetY != 0f)
            {
                Vector3 localPos = questionMarkTxt.transform.localPosition;
                localPos.y = questionMarkOffsetY;
                questionMarkTxt.transform.localPosition = localPos;
            }

            DisableQuestionMarkAnimator();
        }
    }

    public void DisableQuestionMarkAnimator()
    {
        if (questionMarkTxt != null)
        {
            Animator[] animators = questionMarkTxt.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] != null && animators[i].enabled)
                {
                    animators[i].enabled = false;
                }
            }

            Animation[] animations = questionMarkTxt.GetComponentsInChildren<Animation>(true);
            for (int i = 0; i < animations.Length; i++)
            {
                if (animations[i] != null && animations[i].enabled)
                {
                    animations[i].enabled = false;
                }
            }
        }
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

        EnsureQuestionMarkRotationZ();
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();

        UpdateVisualByCurrentState();
        EnsureQuestionMarkRotationZ();
    }

    protected override bool ShouldShowBulletCountText()
    {
        if (isHiddenVisual) return false;
        return base.ShouldShowBulletCountText();
    }

    private void UpdateVisualByCurrentState()
    {
        ShooterState currentState = GetCurrentState();
        bool shouldHide = currentState == ShooterState.Lock;

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
            RevealOriginalVisual(true);
        }
    }

    private void EnsureVisualSetup()
    {
        if (visualsInitialized || mesh == null)
        {
            return;
        }

        originalMaterials = mesh.sharedMaterials;
        if (originalMaterials == null || originalMaterials.Length == 0)
        {
            return;
        }

        hiddenMaterials = new Material[originalMaterials.Length];
        for (int i = 0; i < originalMaterials.Length; i++)
        {
            hiddenMaterials[i] = originalMaterials[i];
        }

        if (hiddenMaterial != null)
        {
            for (int i = 0; i < hiddenMaterials.Length; i++)
            {
                hiddenMaterials[i] = hiddenMaterial;
            }
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
            EnsureQuestionMarkRotationZ();
        }

        isHiddenVisual = true;
        UpdateCountTextVisibilityAndAlpha();
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
            AudioManager.Instance?.PlaySFX(Const.shooterDoneSFX);
            PlayUnblockScalePunch(new Vector3(0.2f, 0.2f, 0.2f), 0.35f, 6, 0.5f);
        }

        isHiddenVisual = false;
        UpdateCountTextVisibilityAndAlpha();
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
