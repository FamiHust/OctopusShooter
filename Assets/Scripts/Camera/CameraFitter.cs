using UnityEngine;

public class CameraFitter : MonoBehaviour
{
    public Camera cam;

    // kích thước thiết kế gốc (ví dụ 1080x1920)
    public float targetAspect = 9f / 16f;
    public float baseOrthoSize = 5f;

    void Start()
    {
        float screenAspect = (float)Screen.width / Screen.height;

        if (screenAspect >= targetAspect)
        {
            // màn hình rộng → fit chiều cao
            cam.orthographicSize = baseOrthoSize;
        }
        else
        {
            // màn hình hẹp → zoom ra để fit chiều ngang
            float scale = targetAspect / screenAspect;
            cam.orthographicSize = baseOrthoSize * scale;
        }
    }
}
