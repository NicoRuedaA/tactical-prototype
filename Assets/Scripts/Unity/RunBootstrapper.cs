using System.Collections;
using UnityEngine;

/// <summary>
/// Scene entry point for local playtesting.
/// It starts a new run through the configured RunManager when the scene begins.
/// </summary>
public sealed class RunBootstrapper : MonoBehaviour
{
    public bool AutoStart = true;
    public float StartDelaySeconds = 0f;
    public RunManager RunManager;

    private void Awake()
    {
        if (RunManager == null)
            RunManager = GetComponent<RunManager>();

        if (RunManager == null)
            RunManager = FindObjectOfType<RunManager>();
    }

    private void Start()
    {
        if (!AutoStart)
            return;

        if (RunManager == null)
        {
            Debug.LogError("RunBootstrapper: No RunManager found. Add one to this scene or assign it in the inspector.");
            return;
        }

        if (StartDelaySeconds > 0f)
            StartCoroutine(StartRunAfterDelay());
        else
            RunManager.StartNewRun();
    }

    private IEnumerator StartRunAfterDelay()
    {
        yield return new WaitForSeconds(StartDelaySeconds);
        RunManager.StartNewRun();
    }
}
