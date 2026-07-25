using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    [SerializeField] private float flyDuration = 0.5f;
    [SerializeField] private float cameraForwardBias = 0.08f;

    [SerializeField] private GameObject muzzlePrefab;
    [SerializeField] private GameObject bulletImpactPrefab;
    [SerializeField] private Vector3 muzzleOffsetFromShootPoint = Vector3.zero;
    [SerializeField] private Vector3 impactOffsetFromShootPoint = Vector3.zero;
    [SerializeField] private Vector3 muzzleRotationOffset = new Vector3(30f, 0f, 0f);
    [SerializeField] private bool enforceVfxFrameBudget = true;
    [SerializeField, Min(1)] private int maxMuzzleVfxPerFrame = 60;
    [SerializeField, Min(1)] private int maxImpactVfxPerFrame = 60;
    private Transform targetTransform;
    private Vector3 targetPosition;
    private GameObject muzzleInstance;
    private GameObject impactInstance;
    private Tween flyTween;
    private int shotToken;
    private static Camera cachedMainCamera;
    private static int vfxBudgetFrame = -1;
    private static int muzzleVfxCountThisFrame;
    private static int impactVfxCountThisFrame;
    // THÊM: Random cuộn tia lửa để nhìn tự nhiên hơn
    [SerializeField] private bool randomMuzzleRoll = true;

    void Start()
    {
    }

    /// <summary>
    /// Khởi tạo đạn và bắn tới target
    /// </summary>
    public void ShootToTarget(Transform shootPoint, Transform target,Action onhit=null)
    {
        if (shootPoint == null || target == null)
        {
            ObjectPoolManager.ReturnObject(gameObject, ObjectPoolManager.PoolType.Bullet);
            return;
        }

        this.targetTransform = target;
        ShootToWorldPosition(shootPoint, target.position, onhit);
    }

    public void ShootToWorldPosition(Transform shootPoint, Vector3 worldTargetPosition, Action onhit = null)
    {
        if (shootPoint == null)
        {
            ObjectPoolManager.ReturnObject(gameObject, ObjectPoolManager.PoolType.Bullet);
            return;
        }

        // Đạn dùng pool nên luôn hủy tween cũ trước khi tái sử dụng.
        flyTween?.Kill();
        DOTween.Kill(transform);
        shotToken++;

        this.targetTransform = null;
        this.targetPosition = worldTargetPosition;

        Camera cam = ResolveMainCamera();
        Vector3 shootOrigin = GetShootOriginWorldPosition(shootPoint);
        Vector3 bulletStartPosition = shootOrigin;
        if (cam != null && Mathf.Abs(cameraForwardBias) > 0.0001f)
        {
            Vector3 bias = -cam.transform.forward * cameraForwardBias;
            this.targetPosition += bias;
            bulletStartPosition += bias;
        }

        // Set vị trí ban đầu của bullet tại shoot origin (đã bao gồm muzzle offset local)
        transform.position = bulletStartPosition;

        Vector3 shootDirection = targetPosition - transform.position;
        if (shootDirection.sqrMagnitude > 0.000001f)
        {
            transform.rotation = Quaternion.LookRotation(shootDirection.normalized);
        }
        else
        {
            transform.rotation = shootPoint.rotation;
        }

        // Spawn muzzle effect tại shootPoint
        SpawnMuzzle(bulletStartPosition, transform.rotation);

        // Bay tới target
        FlyToTarget(onhit);
    }

    private Vector3 GetShootOriginWorldPosition(Transform shootPoint)
    {
        return shootPoint.TransformPoint(muzzleOffsetFromShootPoint);
    }

    /// <summary>
    /// Spawn muzzle effect ở shootPoint
    /// </summary>
    private void SpawnMuzzle(Vector3 spawnPos, Quaternion rotation)
    {
        if (muzzlePrefab == null)
            return;

        if (!TryConsumeMuzzleVfxBudget())
        {
            return;
        }

        // 1. Tạo góc nghiêng tĩnh (tilt) để không bị dẹt
        // Phép nhân Quaternion hoạt động như phép "cộng" góc xoay trong không gian Local
        Quaternion finalRotation = rotation * Quaternion.Euler(muzzleRotationOffset);

        // 2. (Tuỳ chọn) Thêm độ xoay ngẫu nhiên quanh trục Z của Muzzle
        if (randomMuzzleRoll)
        {
            float randomZ = UnityEngine.Random.Range(0f, 360f);
            finalRotation *= Quaternion.Euler(0, 0, randomZ);
        }

        muzzleInstance = ObjectPoolManager.SpawnObject(
            muzzlePrefab,
            spawnPos,
            finalRotation, // Đã thay rotation gốc bằng góc nghiêng mới
            ObjectPoolManager.PoolType.Particle
        );
    }
    /// <summary>
    /// Spawn impact effect khi đạn đến target
    /// </summary>
    private void SpawnImpact()
    {
        if (bulletImpactPrefab == null)
            return;

        if (!TryConsumeImpactVfxBudget())
        {
            return;
        }


        impactInstance = ObjectPoolManager.SpawnObject(
            bulletImpactPrefab,
            targetPosition + impactOffsetFromShootPoint,
            Quaternion.identity, // Thay Quaternion.identity bằng góc nghiêng này
            ObjectPoolManager.PoolType.Particle
        );
    }

    /// <summary>
    /// Di chuyển đạn từ vị trí hiện tại tới target
    /// </summary>
    private void FlyToTarget(Action onhit)
    {
        int currentToken = shotToken;

        if (targetTransform == null)
        {
            flyTween = transform.DOMove(targetPosition, flyDuration)
                .SetEase(Ease.InQuad)
                .OnComplete(() =>
                {
                    if (currentToken != shotToken || !gameObject.activeInHierarchy)
                    {
                        return;
                    }

                    SpawnImpact();
                    ObjectPoolManager.ReturnObject(gameObject, ObjectPoolManager.PoolType.Bullet);
                    onhit?.Invoke();
                });
            return;
        }

        Vector3 startPos = transform.position;
        flyTween = DOVirtual.Float(0f, 1f, flyDuration, t =>
            {
                Vector3 currentTargetPos = targetTransform != null ? targetTransform.position : targetPosition;
                transform.position = Vector3.Lerp(startPos, currentTargetPos, t);

                Vector3 forward = currentTargetPos - transform.position;
                if (forward.sqrMagnitude > 0.000001f)
                {
                    transform.rotation = Quaternion.LookRotation(forward.normalized);
                }
            })
            .SetEase(Ease.InQuad)
            .OnComplete(() =>
            {
                if (currentToken != shotToken || !gameObject.activeInHierarchy)
                {
                    return;
                }

                // Chốt vị trí impact tại điểm mục tiêu cuối cùng khi đạn vừa chạm đích.
                if (targetTransform != null)
                {
                    targetPosition = targetTransform.position;
                }

                // Spawn impact khi tới target
                SpawnImpact();
                // Destroy bullet
                ObjectPoolManager.ReturnObject(gameObject, ObjectPoolManager.PoolType.Bullet);
                onhit?.Invoke();
            });
    }

    private void OnDisable()
    {
        flyTween?.Kill();
        DOTween.Kill(transform);
    }

    private Camera ResolveMainCamera()
    {
        if (cachedMainCamera == null)
        {
            cachedMainCamera = Camera.main;
        }

        return cachedMainCamera;
    }

    /// <summary>
    /// Lấy thời gian bay
    /// </summary>
    public float GetFlyDuration()
    {
        return flyDuration;
    }

    /// <summary>
    /// Set thời gian bay
    /// </summary>
    public void SetFlyDuration(float duration)
    {
        flyDuration = duration;
    }

    private void ResetVfxBudgetIfNeeded()
    {
        int frame = Time.frameCount;
        if (vfxBudgetFrame == frame)
        {
            return;
        }

        vfxBudgetFrame = frame;
        muzzleVfxCountThisFrame = 0;
        impactVfxCountThisFrame = 0;
    }

    private bool TryConsumeMuzzleVfxBudget()
    {
        if (!enforceVfxFrameBudget)
        {
            return true;
        }

        ResetVfxBudgetIfNeeded();
        int budget = Mathf.Max(1, maxMuzzleVfxPerFrame);
        if (muzzleVfxCountThisFrame >= budget)
        {
            return false;
        }

        muzzleVfxCountThisFrame++;
        return true;
    }

    private bool TryConsumeImpactVfxBudget()
    {
        if (!enforceVfxFrameBudget)
        {
            return true;
        }

        ResetVfxBudgetIfNeeded();
        int budget = Mathf.Max(1, maxImpactVfxPerFrame);
        if (impactVfxCountThisFrame >= budget)
        {
            return false;
        }

        impactVfxCountThisFrame++;
        return true;
    }

}
