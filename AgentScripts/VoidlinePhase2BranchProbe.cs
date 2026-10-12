using System;
using System.Reflection;
using System.Text;
using UnityEngine;

public static class VoidlinePhase2BranchProbe
{
    private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

    private static string Setup()
    {
        var root = GameObject.Find("CorridorRoot");
        if (root == null) throw new InvalidOperationException("CorridorRoot missing.");
        var route = root.GetComponent<VoidlineRouteDirector>();
        var scroller = root.GetComponent<CorridorScroller>();
        var branches = root.GetComponent<VoidlineRouteBranchController>();
        if (route == null || scroller == null || branches == null)
            throw new InvalidOperationException("Route/scroller/branch controller missing.");
        if (!route.RouteGraph.Validate(out string graphError))
            throw new InvalidOperationException("Invalid graph: " + graphError);

        var builtField = typeof(VoidlineRouteBranchController).GetField("geometryBuilt", InstancePrivate);
        if (builtField != null && !(bool)builtField.GetValue(branches))
        {
            var start = typeof(VoidlineRouteBranchController).GetMethod("Start", InstancePrivate);
            start?.Invoke(branches, null);
        }

        return "OK";
    }

    public static string SetupForkPreview()
    {
        if (!Application.isPlaying)
            throw new InvalidOperationException("Preview probe must run in Play Mode.");
        Setup();

        var root = GameObject.Find("CorridorRoot");
        var route = root.GetComponent<VoidlineRouteDirector>();
        var scroller = root.GetComponent<CorridorScroller>();
        var branches = root.GetComponent<VoidlineRouteBranchController>();
        var junction = route.RouteGraph.Junctions[0];

        float runTimeBefore = Time.time;
        int frameBefore = Time.frameCount;
        float distanceBefore = route.DistanceTravelled;
        float previewDistance = Mathf.Max(0f, junction.CommitStartDistance - 25f);
        typeof(CorridorScroller).GetField("distanceTravelled", InstancePrivate)
            .SetValue(scroller, previewDistance);
        typeof(CorridorScroller).GetMethod("ApplyRouteFrame", InstancePrivate)
            .Invoke(scroller, null);
        typeof(VoidlineRouteBranchController).GetMethod("UpdateGeometryVisibility", InstancePrivate)
            .Invoke(branches, new object[] { previewDistance });

        var player = UnityEngine.Object.FindAnyObjectByType<VoidlinePlayerController>();
        if (player != null)
            typeof(VoidlinePlayerController).GetMethod("UpdateRoutePosition", InstancePrivate)
                .Invoke(player, null);

        Time.timeScale = 0f;

        var geometryRoot = (Transform)typeof(VoidlineRouteBranchController)
            .GetField("geometryRoot", InstancePrivate).GetValue(branches);
        int meshRenderers = geometryRoot != null
            ? geometryRoot.GetComponentsInChildren<MeshRenderer>(true).Length
            : 0;
        int activeMeshes = 0;
        if (geometryRoot != null)
        {
            foreach (var renderer in geometryRoot.GetComponentsInChildren<MeshRenderer>(true))
                if (renderer.enabled && renderer.gameObject.activeInHierarchy) activeMeshes++;
        }

        return "BRANCH_PREVIEW_READY seed=" + route.RunSeed +
            " timeAdvancedBeforePreview=" + runTimeBefore.ToString("0.00") +
            " frame=" + frameBefore +
            " distanceBeforePreview=" + distanceBefore.ToString("0.0") +
            " distanceSet=" + previewDistance.ToString("0.0") +
            " firstCommit=[" + junction.CommitStartDistance.ToString("0.0") + "," +
                junction.CommitEndDistance.ToString("0.0") + "]" +
            " branchInterval=[" + junction.BranchStartDistance.ToString("0.0") + "," +
                junction.BranchEndDistance.ToString("0.0") + "] rejoinEnd=" +
                junction.RejoinEndDistance.ToString("0.0") +
            " choicesInitially=" + branches.SelectedBranchCount +
            " generatedMeshRenderers=" + meshRenderers +
            " activeMeshRenderers=" + activeMeshes +
            " graphValid=true timeScale=" + Time.timeScale;
    }

    public static string TestBothBranchChoices()
    {
        if (!Application.isPlaying)
            throw new InvalidOperationException("Branch test must run in Play Mode.");
        Setup();
        var root = GameObject.Find("CorridorRoot");
        var route = root.GetComponent<VoidlineRouteDirector>();
        var scroller = root.GetComponent<CorridorScroller>();
        var branches = root.GetComponent<VoidlineRouteBranchController>();
        var first = route.RouteGraph.Junctions[0];
        var second = route.RouteGraph.Junctions[1];
        var distanceField = typeof(CorridorScroller).GetField("distanceTravelled", InstancePrivate);
        var applyFrame = typeof(CorridorScroller).GetMethod("ApplyRouteFrame", InstancePrivate);

        float firstDecisionDistance = first.CommitStartDistance + 2f;
        distanceField.SetValue(scroller, firstDecisionDistance);
        applyFrame.Invoke(scroller, null);
        bool choseRisky = branches.TryCommitBranchFromLane(+2f);
        route.TryGetBranchChoice(first.Id, out VoidlineRouteBranchRole firstRole);
        float firstCurrentOffset = route.GetCenterOffsetAtDistance(firstDecisionDistance).x -
            route.GetBaseCenterOffsetAtDistance(firstDecisionDistance).x;
        float firstBranchOffset = branchesSelectedOffset(route, first, firstDecisionDistance, first.BranchStartDistance);
        bool repeatedChoiceRejected = !branches.TryCommitBranchFromLane(-2f);

        float secondDecisionDistance = second.CommitStartDistance + 2f;
        distanceField.SetValue(scroller, secondDecisionDistance);
        applyFrame.Invoke(scroller, null);
        bool choseSafe = branches.TryCommitBranchFromLane(-2f);
        route.TryGetBranchChoice(second.Id, out VoidlineRouteBranchRole secondRole);
        float secondCurrentOffset = route.GetCenterOffsetAtDistance(secondDecisionDistance).x -
            route.GetBaseCenterOffsetAtDistance(secondDecisionDistance).x;
        float secondBranchOffset = branchesSelectedOffset(route, second, secondDecisionDistance, second.BranchStartDistance);

        var player = UnityEngine.Object.FindAnyObjectByType<VoidlinePlayerController>();
        if (player != null)
            typeof(VoidlinePlayerController).GetMethod("UpdateRoutePosition", InstancePrivate)
                .Invoke(player, null);
        float frameError = 0f;
        if (player != null)
        {
            Vector3 expected = route.GetPlayerWorldPosition(player.LanePosition,
                route.DistanceTravelled, player.transform.position.z);
            frameError = Vector3.Distance(expected, player.transform.position);
        }

        bool pass = choseRisky && choseSafe && repeatedChoiceRejected &&
            firstRole == VoidlineRouteBranchRole.RiskyRepair &&
            secondRole == VoidlineRouteBranchRole.SafeBoost &&
            Mathf.Abs(firstCurrentOffset) < 0.001f && Mathf.Abs(secondCurrentOffset) < 0.001f &&
            firstBranchOffset > 3.9f && secondBranchOffset < -3.9f && frameError < 0.001f;

        return "BRANCH_CHOICE_TEST " + (pass ? "PASS" : "FAIL") +
            " first=" + firstRole + " accepted=" + choseRisky +
            " startOffset=" + firstCurrentOffset.ToString("0.000") +
            " selectedPathOffset=" + firstBranchOffset.ToString("0.000") +
            " duplicateRejected=" + repeatedChoiceRejected +
            " second=" + secondRole + " accepted=" + choseSafe +
            " startOffset=" + secondCurrentOffset.ToString("0.000") +
            " selectedPathOffset=" + secondBranchOffset.ToString("0.000") +
            " selectedCount=" + branches.SelectedBranchCount +
            " playerFrameError=" + frameError.ToString("0.000") +
            " gameTime=" + Time.time.ToString("0.00") + " timeScale=" + Time.timeScale;
    }

    private static float branchesSelectedOffset(VoidlineRouteDirector route,
        VoidlineRouteJunction junction, float selectedAt, float sampleAt)
    {
        return route.GetSelectedBranchOffsetAtDistance(junction.Id, sampleAt);
    }

    public static string ResumeClock()
    {
        Time.timeScale = 1f;
        return "Time.timeScale restored to 1.";
    }
}
