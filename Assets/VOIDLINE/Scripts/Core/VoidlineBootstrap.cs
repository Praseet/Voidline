using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Starts the VOIDLINE runtime systems when the playable scene is entered.
/// This keeps the prototype usable without fragile hand-wired scene references.
/// </summary>
internal static class VoidlineBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureGameManager()
    {
                Application.runInBackground = true;
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.name != "main")
            return;

        if (Object.FindAnyObjectByType<VoidlineGameManager>() != null)
            return;

        GameObject managerObject = GameObject.Find("GameManager");
        if (managerObject == null)
            managerObject = new GameObject("GameManager");

        managerObject.AddComponent<VoidlineGameManager>();
    }
}



