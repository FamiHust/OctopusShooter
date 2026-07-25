using UnityEngine;

[DisallowMultipleComponent]
public class OnParticleDestroy : MonoBehaviour
{

    // Được gọi tự động khi Particle kết thúc hoàn toàn
    private void OnParticleSystemStopped()
    {
        ObjectPoolManager.ReturnObject(gameObject, ObjectPoolManager.PoolType.Particle);
    }
}
