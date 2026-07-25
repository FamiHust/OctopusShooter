using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ConveyorArrowSystem : MonoBehaviour
{
    [Header("Route")]
    [Tooltip("SplineRoute (Main) cá»§a level nÃ y â€” dÃ¹ng Ä‘á»ƒ láº¥y SplineContainer vÃ  tá»‘c Ä‘á»™ seed.")]
    [SerializeField] private SplineRoute splineRoute;

    [Header("Config")]
    [SerializeField] private ConveyorArrowSystemConfig conveyorArrowConfig;

    [Header("Arrow")]
    [SerializeField] private Transform arrowPrefab;
    [SerializeField] private int arrowCount = 12;

    [Header("Movement")]
    [SerializeField] private float speed = 0.15f;
    [SerializeField] private bool syncWithSeedSpeed = true;

    private SplineContainer spline;
    private List<Transform> arrows = new();
    private List<float> arrowT = new();

    void OnValidate()
    {
        TryAutoAssignMainSplineRoute();
        TryAutoAssignConveyorArrowConfig();
        ApplyConfigValues();
    }

    private void TryAutoAssignMainSplineRoute()
    {
        if (splineRoute != null && splineRoute.GetRouteMode() == SplineRoute.RouteMode.Main)
        {
            return;
        }

        SplineRoute[] routes = GetComponentsInChildren<SplineRoute>(true);
        for (int i = 0; i < routes.Length; i++)
        {
            SplineRoute route = routes[i];
            if (route == null)
            {
                continue;
            }

            if (route.transform == transform)
            {
                continue;
            }

            if (route.GetRouteMode() == SplineRoute.RouteMode.Main)
            {
                splineRoute = route;
#if UNITY_EDITOR
                EditorUtility.SetDirty(this);
#endif
                return;
            }
        }
    }

    void Start()
    {
        ApplyConfigValues();

        if (splineRoute == null)
        {
            ;
            enabled = false;
            return;
        }

        spline = splineRoute.GetSplineContainer();
        if (spline == null)
        {
            ;
            enabled = false;
            return;
        }

        SpawnArrows();
    }

    private void ApplyConfigValues()
    {
        if (conveyorArrowConfig == null)
        {
            arrowCount = Mathf.Max(1, arrowCount);
            speed = Mathf.Max(0f, speed);
            return;
        }

        if (conveyorArrowConfig.arrowPrefab != null)
        {
            arrowPrefab = conveyorArrowConfig.arrowPrefab;
        }

        arrowCount = Mathf.Max(1, conveyorArrowConfig.arrowCount);
        speed = Mathf.Max(0f, conveyorArrowConfig.speed);
        syncWithSeedSpeed = conveyorArrowConfig.syncWithSeedSpeed;
    }

    private void TryAutoAssignConveyorArrowConfig()
    {
        if (conveyorArrowConfig != null)
        {
            return;
        }

#if UNITY_EDITOR
        string[] guids = AssetDatabase.FindAssets("t:ConveyorArrowSystemConfig");
        if (guids != null && guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            ConveyorArrowSystemConfig foundConfig = AssetDatabase.LoadAssetAtPath<ConveyorArrowSystemConfig>(path);
            if (foundConfig != null)
            {
                conveyorArrowConfig = foundConfig;
                EditorUtility.SetDirty(this);
            }
        }
#endif
    }

    void Update()
    {
        if (splineRoute != null && splineRoute.IsMovementPaused())
        {
            return;
        }

        MoveArrows();
    }

    void SpawnArrows()
    {
        for (int i = 0; i < arrowCount; i++)
        {
            Transform arrow = Instantiate(arrowPrefab, transform);
            arrows.Add(arrow);
            arrowT.Add((float)i / arrowCount);
        }
    }

    void MoveArrows()
    {
        float effectiveSpeed = GetEffectiveArrowSpeed();

        for (int i = 0; i < arrows.Count; i++)
        {
            arrowT[i] += effectiveSpeed * Time.deltaTime;

            if (arrowT[i] > 1f)
                arrowT[i] -= 1f;

            Vector3 pos = spline.EvaluatePosition(arrowT[i]);
            arrows[i].position = pos;

            Vector3 dir = spline.EvaluateTangent(arrowT[i]);
            if (dir != Vector3.zero)
                arrows[i].rotation = Quaternion.LookRotation(dir);
        }
    }

    public void DisableArrowsForOutro()
    {
        for (int i = 0; i < arrows.Count; i++)
        {
            if (arrows[i] != null)
            {
                arrows[i].gameObject.SetActive(false);
            }
        }

        enabled = false;
    }

    float GetEffectiveArrowSpeed()
    {
        if (!syncWithSeedSpeed)
            return speed;

        float len = splineRoute.GetSplineLength();
        if (len <= 0f)
            return speed;

        float mul = SpeedMultiplierManager.Instance.GetCurrentMultiplier();
        return (splineRoute.GetMoveSpeed() / len) * mul;
    }
}
