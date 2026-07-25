using UnityEngine;

public class LevelCameraConfigurator : MonoBehaviour
{
    public static LevelCameraConfigurator Instance { get; private set; }

    [SerializeField] private Transform cameraParent;
    [SerializeField] private LevelCameraConfigSO config;

    private void Reset()
    {
        cameraParent = transform;
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void ApplyForLevel(int level)
    {
        if (config == null)
        {
            return;
        }

        Transform parent = cameraParent != null ? cameraParent : transform;
        Camera[] cameras = parent.GetComponentsInChildren<Camera>(true);
        if (cameras == null || cameras.Length == 0)
        {
            return;
        }

        config.GetValues(level, out float size, out float yPos);

        for (int i = 0; i < cameras.Length; i++)
        {
            Camera cam = cameras[i];
            if (cam == null) continue;

            cam.orthographicSize = size;

            Vector3 pos = cam.transform.localPosition;
            pos.y = yPos;
            cam.transform.localPosition = pos;
        }
    }
}
