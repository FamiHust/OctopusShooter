using UnityEngine;
using System.Collections;

public class BoosterRunner : MonoBehaviour
{
    public static BoosterRunner Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public Coroutine Run(IEnumerator routine)
    {
        return StartCoroutine(routine);
    }
}
