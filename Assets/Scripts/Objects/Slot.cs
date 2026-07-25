using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Slot : MonoBehaviour
{
    [SerializeField] private Transform slotPosition;
    private BaseShooter currentShooter;
    [SerializeField] private ParticleSystem glowDeckVFX;
    private void Awake()
    {
        glowDeckVFX.Stop();
    }

    void OnValidate()
    {
        GetSlotPosition();
        GetGlowDeckVFX();
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
    /// Khởi tạo slot
    /// </summary>
    public void Initialize(int index, Transform position)
    {

        slotPosition = position;
        currentShooter = null;
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
    /// Set slot về trạng thái trống
    /// </summary>


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
