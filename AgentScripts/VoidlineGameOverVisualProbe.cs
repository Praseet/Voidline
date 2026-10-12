using System;
using System.Reflection;
using UnityEngine;

public static class VoidlineGameOverVisualProbe
{
    public static string Run()
    {
        var manager = UnityEngine.Object.FindAnyObjectByType<VoidlineGameManager>();
        if (manager == null)
            return "GAMEOVER_VISUAL_PROBE FAIL: VoidlineGameManager not found";

        var method = typeof(VoidlineGameManager).GetMethod(
            "EndRun", BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
            return "GAMEOVER_VISUAL_PROBE FAIL: EndRun method not found";

        method.Invoke(manager, null);
        return "GAMEOVER_VISUAL_PROBE PASS: EndRun invoked at t=" +
            Time.time.ToString("0.00") + "; overlay active=" +
            manager.IsGameOver + "; console can be checked separately";
    }
}
