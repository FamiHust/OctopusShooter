using UnityEngine;
using UnityEngine.Splines;

public class BuildSplineFromBelt : MonoBehaviour
{
    [SerializeField] private Transform beltRoot;
    [SerializeField] private SplineContainer splineContainer;
    [SerializeField] private bool isSecondaryBelt;

    void OnValidate()
    {
        SplineContainer localSplineContainer = GetComponent<SplineContainer>();
        if (localSplineContainer != null)
        {
            splineContainer = localSplineContainer;
        }

        Transform[] allTransforms = transform.root.GetComponentsInChildren<Transform>(true);

        if (name.IndexOf("mainspline", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            for (int i = 0; i < allTransforms.Length; i++)
            {
                Transform tr = allTransforms[i];
                if (tr == null || tr == transform)
                {
                    continue;
                }

                if (tr.name.IndexOf("belt", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    beltRoot = tr;
                    break;
                }
            }
        }

        if (isSecondaryBelt)
        {
            string lowerName = name.ToLowerInvariant();
            bool isSlideRouteL = lowerName.Contains("slidesroutel");
            bool isSlideRoute = lowerName.Contains("slidesroute") && !isSlideRouteL;

            if (isSlideRouteL || isSlideRoute)
            {
                for (int i = 0; i < allTransforms.Length; i++)
                {
                    Transform tr = allTransforms[i];
                    if (tr == null || tr == transform)
                    {
                        continue;
                    }

                    string candidateName = tr.name.ToLowerInvariant();
                    bool matched = false;

                    if (isSlideRouteL)
                    {
                        matched = candidateName.Contains("way_l");
                    }
                    else if (isSlideRoute)
                    {
                        matched = candidateName.Contains("way") && !candidateName.Contains("way_l");
                    }

                    if (matched)
                    {
                        beltRoot = tr;
                        break;
                    }
                }
            }
        }
    }

    void Awake()
    {
        BuildSpline();
    }

    void BuildSpline()
    {
        if (beltRoot == null || splineContainer == null)
        {
            return;
        }

        var spline = splineContainer.Spline;

        spline.Clear();

        for (int i = 0; i < beltRoot.childCount; i++)
        {
            Transform point = beltRoot.GetChild(i);
            Vector3 localPos =
                splineContainer.transform.InverseTransformPoint(point.position);

            BezierKnot knot = new BezierKnot(localPos);

            spline.Add(knot, TangentMode.AutoSmooth); // ⭐ dòng quan trọng
        }

        spline.Closed = !isSecondaryBelt;
    }
}