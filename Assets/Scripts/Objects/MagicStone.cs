using UnityEngine;
using DG.Tweening;
using UnityEngine.Serialization;

public class MagicStone : MonoBehaviour
{
	[Header("Fake Physics")]
	[SerializeField] private bool autoPlayOnEnable = true;
	[SerializeField, Min(0.05f)] private float riseDuration = 0.16f;
	[SerializeField, Min(0.05f)] private float fallDuration = 0.24f;
	[SerializeField, Min(0f)] private float launchHeight = 0.8f;
	[FormerlySerializedAs("forwardDistance")]
	[SerializeField, Min(0f)] private float sideLaunchDistance = 0.45f;
	[SerializeField, Min(0f)] private float sideRandomOffset = 0.12f;
	[SerializeField, Min(0.2f)] private float groundProbeDistance = 6f;
	[SerializeField] private LayerMask groundLayerMask = ~0;
	[SerializeField, Min(0f)] private float groundOffset = 0.02f;
	[SerializeField, Min(0f)] private float firstBounceHeight = 0.22f;
	[SerializeField, Min(0)] private int bounceCount = 2;
	[SerializeField, Min(0.05f)] private float totalBounceDuration = 0.22f;
	[SerializeField] private Ease riseEase = Ease.OutCubic;
	[SerializeField] private Ease fallEase = Ease.InQuad;
	[SerializeField] private Ease bounceUpEase = Ease.OutQuad;
	[SerializeField] private Ease bounceDownEase = Ease.InQuad;
	[SerializeField] private bool allowTapToDisappearEarly = true;
	[SerializeField] private bool autoAddTapCollider = true;
	[SerializeField, Min(0.05f)] private float tapColliderMinRadius = 0.2f;

	[Header("Disappear")]
	[SerializeField] private GameObject disappearVfxPrefab;
	[SerializeField, Min(0f)] private float disappearDelay = 0.04f;
	[SerializeField, Min(0.1f)] private float fallbackDisappearVfxLifetime = 1.2f;
	[SerializeField] private ObjectPoolManager.PoolType selfPoolType = ObjectPoolManager.PoolType.Particle;

	private Sequence activeSequence;
	private BaseShooter ownerShooter;
	private bool isDisappearing;
	private SphereCollider cachedTapCollider;
	private bool hasCachedTapColliderLayout;
	private Vector3 cachedTapColliderCenter;
	private float cachedTapColliderRadius;

	private void OnEnable()
	{
		isDisappearing = false;
		EnsureTapCollider();

		if (autoPlayOnEnable)
		{
			PlayMotion();
		}
	}

	private void OnDisable()
	{
		KillMotionTweens();
		ownerShooter = null;
		isDisappearing = false;
	}

	private void OnMouseDown()
	{
		if (!allowTapToDisappearEarly)
		{
			return;
		}

		TryDisappearEarlyByTap();
	}

	public void BindOwnerShooter(BaseShooter shooter)
	{
		ownerShooter = shooter;
	}

	public void PlayMotion()
	{
		KillMotionTweens();

		Vector3 spawnPos = transform.position;
		Vector3 sideDirection = GetSideDirection();
		float sideSign = Random.value < 0.5f ? -1f : 1f;
		float sideDistance = Mathf.Max(0f, sideLaunchDistance) + Random.Range(0f, Mathf.Max(0f, sideRandomOffset));
		Vector3 lateral = sideDirection * (sideSign * sideDistance);

		Vector3 apexPos = spawnPos + (lateral * 0.45f) + (Vector3.up * Mathf.Max(0f, launchHeight));
		Vector3 landingPos = spawnPos + lateral;
		landingPos.y = ResolveGroundY(spawnPos) + Mathf.Max(0f, groundOffset);

		activeSequence = DOTween.Sequence().SetUpdate(true);
		activeSequence.Append(transform.DOMove(apexPos, Mathf.Max(0.05f, riseDuration)).SetEase(riseEase).SetUpdate(true));
		activeSequence.Append(transform.DOMove(landingPos, Mathf.Max(0.05f, fallDuration)).SetEase(fallEase).SetUpdate(true)
			.OnComplete(PlayBounceLandingSfx));

		int safeBounceCount = Mathf.Max(0, bounceCount);
		float perBounceDuration = safeBounceCount > 0 ? Mathf.Max(0.03f, totalBounceDuration / safeBounceCount) : 0f;
		for (int i = 0; i < safeBounceCount; i++)
		{
			float bounceHeight = Mathf.Max(0f, firstBounceHeight) * Mathf.Pow(0.55f, i);
			if (bounceHeight <= 0.001f)
			{
				break;
			}

			float upDuration = perBounceDuration * 0.45f;
			float downDuration = perBounceDuration - upDuration;

			activeSequence.Append(transform.DOMoveY(landingPos.y + bounceHeight, upDuration).SetEase(bounceUpEase).SetUpdate(true));
			activeSequence.Append(transform.DOMoveY(landingPos.y, downDuration).SetEase(bounceDownEase).SetUpdate(true)
				.OnComplete(PlayBounceLandingSfx));
		}

		if (disappearDelay > 0f)
		{
			activeSequence.AppendInterval(disappearDelay);
		}

		activeSequence.AppendCallback(PlayDisappearAndReturn);
	}

	private Vector3 GetSideDirection()
	{
		if (ownerShooter != null)
		{
			Vector3 ownerRight = ownerShooter.transform.right;
			if (ownerRight.sqrMagnitude > 0.0001f)
			{
				return ownerRight.normalized;
			}
		}

		return Vector3.right;
	}

	private float ResolveGroundY(Vector3 spawnPos)
	{
		Vector3 rayOrigin = spawnPos + Vector3.up * 0.2f;
		if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, Mathf.Max(0.2f, groundProbeDistance), groundLayerMask, QueryTriggerInteraction.Ignore))
		{
			return hit.point.y;
		}

		return spawnPos.y - Mathf.Max(0.25f, launchHeight * 0.7f);
	}

	private void PlayDisappearAndReturn()
	{
		if (isDisappearing)
		{
			return;
		}

		isDisappearing = true;
		AudioManager.Instance?.PlaySFX(Const.clickMagicStoneSFX);
		KillMotionTweens();

		if (disappearVfxPrefab != null)
		{
			if (ownerShooter != null)
			{
				ownerShooter.SpawnOneShotShooterVfx(
					disappearVfxPrefab,
					transform.position,
					Quaternion.identity,
					fallbackDisappearVfxLifetime
				);
			}
			else
			{
				GameObject vfx = ObjectPoolManager.SpawnObject(
					disappearVfxPrefab,
					transform.position,
					Quaternion.identity,
					ObjectPoolManager.PoolType.Particle
				);

				float lifetime = EstimateVfxLifetime(vfx);
				DOVirtual.DelayedCall(lifetime, () =>
				{
					if (vfx != null)
					{
						ObjectPoolManager.ReturnObject(vfx, ObjectPoolManager.PoolType.Particle);
					}
				}).SetUpdate(true);
			}
		}

		ObjectPoolManager.ReturnObject(gameObject, selfPoolType);
	}

	private void TryDisappearEarlyByTap()
	{
		if (!isActiveAndEnabled || isDisappearing)
		{
			return;
		}

		PlayDisappearAndReturn();
	}

	private void PlayBounceLandingSfx()
	{
		if (!isActiveAndEnabled || isDisappearing)
		{
			return;
		}

		AudioManager.Instance?.PlaySFX(Const.bounceMagicStoneSFX);
	}

	private void EnsureTapCollider()
	{
		if (!autoAddTapCollider)
		{
			return;
		}

		if (cachedTapCollider == null)
		{
			cachedTapCollider = GetComponent<SphereCollider>();
			if (cachedTapCollider == null)
			{
				cachedTapCollider = gameObject.AddComponent<SphereCollider>();
			}
		}

		cachedTapCollider.isTrigger = true;

		if (!hasCachedTapColliderLayout)
		{
			float radius = Mathf.Max(0.05f, tapColliderMinRadius);
			Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
			if (renderers != null && renderers.Length > 0)
			{
				Bounds combinedBounds = renderers[0].bounds;
				for (int i = 1; i < renderers.Length; i++)
				{
					Renderer renderer = renderers[i];
					if (renderer == null)
					{
						continue;
					}

					combinedBounds.Encapsulate(renderer.bounds);
				}

				cachedTapColliderCenter = transform.InverseTransformPoint(combinedBounds.center);
				radius = Mathf.Max(radius, combinedBounds.extents.magnitude * 0.75f);
			}

			cachedTapColliderRadius = radius;
			hasCachedTapColliderLayout = true;
		}

		cachedTapCollider.center = cachedTapColliderCenter;
		cachedTapCollider.radius = cachedTapColliderRadius;
	}

	private float EstimateVfxLifetime(GameObject vfxObject)
	{
		if (vfxObject == null)
		{
			return Mathf.Max(0.1f, fallbackDisappearVfxLifetime);
		}

		float maxLifetime = 0f;
		ParticleSystem[] systems = vfxObject.GetComponentsInChildren<ParticleSystem>(true);
		for (int i = 0; i < systems.Length; i++)
		{
			ParticleSystem ps = systems[i];
			if (ps == null)
			{
				continue;
			}

			ParticleSystem.MainModule main = ps.main;
			float duration = main.duration;
			if (main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants)
			{
				duration += Mathf.Max(main.startLifetime.constantMin, main.startLifetime.constantMax);
			}
			else
			{
				duration += main.startLifetime.constantMax;
			}

			maxLifetime = Mathf.Max(maxLifetime, duration);
		}

		if (maxLifetime <= 0f)
		{
			maxLifetime = Mathf.Max(0.1f, fallbackDisappearVfxLifetime);
		}

		return maxLifetime;
	}

	private void KillMotionTweens()
	{
		activeSequence?.Kill(false);
		activeSequence = null;
		transform.DOKill(false);
	}
}
