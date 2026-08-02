using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class Slot : MonoBehaviour
{
    [SerializeField] private Transform slotPosition;
    private BaseShooter currentShooter;
    [SerializeField] private ParticleSystem glowDeckVFX;

    [Header("Ocean Wave Floating Motion")]
    [SerializeField] private bool enableWaveMotion = true;
    [SerializeField, Range(0f, 0.1f)] private float waveAmplitude = 0.02f;
    [SerializeField, Range(0.5f, 5f)] private float waveSpeed = 1.8f;

    [Header("Landing Bounce Settings")]
    [SerializeField] private float landingBounceImpulse = -0.55f;
    [SerializeField] private float springK = 85f;
    [SerializeField] private float springDamping = 8.5f;

    // Runtime wave & bounce variables
    private float baseLocalY = 0f;
    private bool isBaseLocalYSet = false;
    private int slotIndex = -1;
    private float phaseOffset = 0f;
    private float speedMultiplier = 1f;
    private float amplitudeMultiplier = 1f;
    private bool isWaveInitialized = false;

    // Physics spring bounce for landing impact
    private float bounceY = 0f;
    private float bounceVelocity = 0f;

    private void Awake()
    {
        if (glowDeckVFX != null)
        {
            glowDeckVFX.Stop();
        }

        if (!isBaseLocalYSet)
        {
            baseLocalY = transform.localPosition.y;
            isBaseLocalYSet = true;
        }

        InitWaveParameters();
    }

    private void Start()
    {
        InitWaveParameters();
    }

    private void OnValidate()
    {
        GetSlotPosition();
        GetGlowDeckVFX();
    }

    private void InitWaveParameters()
    {
        if (isWaveInitialized) return;
        isWaveInitialized = true;

        int index = slotIndex >= 0 ? slotIndex : transform.GetSiblingIndex();
        int idHash = Mathf.Abs(gameObject.GetInstanceID() % 100);

        // Dynamic unsynchronized phase offset per slot
        phaseOffset = index * 1.45f + idHash * 0.17f;

        // Slight speed variations per slot (0.88x ~ 1.18x)
        speedMultiplier = 0.88f + (index % 4) * 0.09f + (idHash % 5) * 0.02f;

        // Slight amplitude variations per slot (0.85x ~ 1.15x)
        amplitudeMultiplier = 0.85f + ((index * 3) % 5) * 0.07f;
    }

    private void Update()
    {
        UpdateSpringBounce();
        UpdateWaveMotion();
    }

    private void UpdateSpringBounce()
    {
        if (Mathf.Abs(bounceY) < 0.0001f && Mathf.Abs(bounceVelocity) < 0.0001f)
        {
            bounceY = 0f;
            bounceVelocity = 0f;
            return;
        }

        float springForce = -springK * bounceY;
        bounceVelocity += (springForce - springDamping * bounceVelocity) * Time.deltaTime;
        bounceY += bounceVelocity * Time.deltaTime;
    }

    private void UpdateWaveMotion()
    {
        if (!enableWaveMotion) return;

        InitWaveParameters();

        float t = Time.time * waveSpeed * speedMultiplier;

        // Primary wave + secondary out-of-phase wave for realistic organic fluid movement
        float waveY1 = Mathf.Sin(t + phaseOffset);
        float waveY2 = Mathf.Sin(t * 1.37f + phaseOffset * 1.7f) * 0.35f;
        float currentWaveY = (waveY1 + waveY2) * (waveAmplitude * amplitudeMultiplier);

        float finalY = baseLocalY + currentWaveY + bounceY;

        // Maintain local X & Z set by SlotBar while applying wave Y displacement
        Vector3 curLocalPos = transform.localPosition;
        transform.localPosition = new Vector3(curLocalPos.x, finalY, curLocalPos.z);
    }

    private void GetSlotPosition()
    {
        if (transform.childCount == 0) 
            return;
        slotPosition = transform.GetChild(0);
    }

    private void GetGlowDeckVFX()
    {
        glowDeckVFX = GetComponentInChildren<ParticleSystem>(true);
    }

    /// <summary>
    /// Hiệu ứng nhún vị trí Y của Slot khi Shooter nhảy tiếp đất (tạo cảm giác quán tính)
    /// </summary>
    public void PlayLandingBounce()
    {
        // Nhún xuống vị trí Y dạng vật lý spring
        bounceVelocity = landingBounceImpulse;
    }

    /// <summary>
    /// Khởi tạo slot
    /// </summary>
    public void Initialize(int index, Transform position)
    {
        slotIndex = index;
        slotPosition = position;
        currentShooter = null;
        isWaveInitialized = false;
        InitWaveParameters();
    }

    public void SetBaseLocalY(float y)
    {
        baseLocalY = y;
        isBaseLocalYSet = true;
    }

    public void SetWaveEnabled(bool enabled)
    {
        enableWaveMotion = enabled;
    }

    /// <summary>
    /// Đặt shooter vào slot
    /// </summary>
    public void SetShooter(BaseShooter shooter)
    {
        currentShooter = shooter;
    }

    public void PlayGlowVFX()
    {
        if (glowDeckVFX == null) return;
        glowDeckVFX.Play();
    }

    public void StopGlowVFX()
    {
        if (glowDeckVFX == null) return;

        // true: áp dụng cho cả các Particle System con (nếu có)
        // StopEmittingAndClear: Ngừng phát và xóa ngay lập tức
        glowDeckVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    /// <summary>
    /// Xóa shooter khỏi slot
    /// </summary>
    public void ClearShooter()
    {
        currentShooter = null;
    }

    /// <summary>
    /// Lấy vị trí world của slot
    /// </summary>
    public Vector3 GetPosition()
    {
        if (slotPosition != null)
            return slotPosition.position;  // World position
        return transform.position;
    }

    /// <summary>
    /// Lấy transform của slot position
    /// </summary>
    public Transform GetSlotPositionTransform()
    {
        return slotPosition;
    }

    /// <summary>
    /// Lấy shooter trong slot
    /// </summary>
    public BaseShooter GetShooter()
    {
        return currentShooter;
    }

    /// <summary>
    /// Kiểm tra slot có trống không
    /// </summary>
    public bool IsEmpty()
    {
        return currentShooter == null;
    }
}
