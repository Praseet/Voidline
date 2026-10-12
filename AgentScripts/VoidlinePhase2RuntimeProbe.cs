using System;
using System.Reflection;
using System.Text;
using UnityEngine;

public static class VoidlinePhase2RuntimeProbe
{
    public static string Run()
    {
        var sb = new StringBuilder();
        var root = GameObject.Find("CorridorRoot");
        var route = root != null ? root.GetComponent<VoidlineRouteDirector>() : null;
        var scroller = root != null ? root.GetComponent<CorridorScroller>() : null;
        var branches = root != null ? root.GetComponent<VoidlineRouteBranchController>() : null;
        sb.AppendLine("playing=" + Application.isPlaying + " time=" + Time.time.ToString("0.0"));
        if (route == null || scroller == null || branches == null)
            return sb + "MISSING route/scroller/branch component";
        sb.AppendLine("seed=" + route.RunSeed + " junctions=" + route.RouteGraph.Junctions.Count +
            " distance=" + route.DistanceTravelled.ToString("0.0"));
        var env = root.transform.Find("VOIDLINE Route Environmental Cues");
        int meshCount = 0;
        int rendererCount = 0;
        if (env != null)
        {
            var filters = env.GetComponentsInChildren<MeshFilter>(true);
            var renderers = env.GetComponentsInChildren<MeshRenderer>(true);
            meshCount = filters.Length; rendererCount = renderers.Length;
            sb.AppendLine("environmentRoot=present meshFilters=" + meshCount + " meshRenderers=" + rendererCount);
        }
        else sb.AppendLine("environmentRoot=missing");
        var field = typeof(CorridorScroller).GetField("distanceTravelled", BindingFlags.Instance | BindingFlags.NonPublic);
        var junctions = route.RouteGraph.Junctions;
        var first = junctions[0];
        field.SetValue(scroller, first.CommitStartDistance + 2f);
        bool safeCommit = branches.TryCommitBranchFromLane(-2f);
        bool safeSelected = route.TryGetBranchChoice(first.Id, out var safeRole) &&
            safeRole == VoidlineRouteBranchRole.SafeBoost;
        float branchMid = (first.BranchStartDistance + first.BranchEndDistance) * 0.5f;
        float safeOffset = route.GetSelectedBranchOffsetAtDistance(first.Id, branchMid);
        float safeRejoinOffset = route.GetSelectedBranchOffsetAtDistance(first.Id, first.RejoinEndDistance + 1f);
        sb.AppendLine("safeCommit=" + safeCommit + " safeSelected=" + safeSelected +
            " midOffset=" + safeOffset.ToString("0.00") + " rejoinOffset=" + safeRejoinOffset.ToString("0.00"));
        if (junctions.Count > 1)
        {
            var second = junctions[1];
            field.SetValue(scroller, second.CommitStartDistance + 2f);
            bool riskyCommit = branches.TryCommitBranchFromLane(2f);
            bool riskySelected = route.TryGetBranchChoice(second.Id, out var riskyRole) &&
                riskyRole == VoidlineRouteBranchRole.RiskyRepair;
            float riskyOffset = route.GetSelectedBranchOffsetAtDistance(second.Id,
                (second.BranchStartDistance + second.BranchEndDistance) * 0.5f);
            sb.AppendLine("riskyCommit=" + riskyCommit + " riskySelected=" + riskySelected +
                " midOffset=" + riskyOffset.ToString("0.00"));
        }
        int activeSegments = 0;
        for (int i = 0; i < root.transform.childCount; i++)
        {
            var child = root.transform.GetChild(i);
            if (child.name.StartsWith("Segment_") && child.gameObject.activeSelf) activeSegments++;
        }
        sb.AppendLine("activeStartingCorridorSegments=" + activeSegments +
            " selected=" + branches.SelectedBranchCount + " completed=" + branches.CompletedBranchCount);
        return sb.ToString();
    }
}
