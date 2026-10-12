using System;
using UnityEngine;
using UnityEditor;

public static class VoidlinePhase2RuntimeCheck
{
    public static string Run()
    {
        var root = GameObject.Find("CorridorRoot");
        if (root == null) throw new Exception("CorridorRoot missing");
        var route = root.GetComponent<VoidlineRouteDirector>();
        var branch = root.GetComponent<VoidlineRouteBranchController>();
        var scroller = root.GetComponent<CorridorScroller>();
        var space = root.GetComponent<VoidlineSpaceSequence>();
        if (route == null || branch == null || scroller == null || space == null)
            throw new Exception("Required route/branch/scroller/space components missing");
        if (!route.RouteGraph.Validate(out string validation)) throw new Exception("Graph invalid: " + validation);
        var junction = route.RouteGraph.Junctions[0];
        string choiceBefore = route.TryGetBranchChoice(junction.Id, out var roleBefore) ? roleBefore.ToString() : "none";
        bool committed = false;
        if (route.DistanceTravelled >= junction.CommitStartDistance && route.DistanceTravelled <= junction.CommitEndDistance)
            committed = branch.TryCommitBranchFromLane(-2.2f);
        bool selected = route.TryGetBranchChoice(junction.Id, out var selectedRole);
        float selectedOffset = route.GetCenterOffsetAtDistance(junction.BranchStartDistance).x -
            route.GetBaseCenterOffsetAtDistance(junction.BranchStartDistance).x;
        var sky = RenderSettings.skybox;
        int visibleTunnelRenderers = 0;
        for (int i=0;i<root.transform.childCount;i++)
        {
            var child=root.transform.GetChild(i);
            if(!child.name.StartsWith("Segment_")) continue;
            var rs=child.GetComponentsInChildren<Renderer>(true);
            for(int j=0;j<rs.Length;j++) if(rs[j].enabled) visibleTunnelRenderers++;
        }
        int cueRoots=0;
        var cue= root.transform.Find("VOIDLINE Route Cues — Open Space");
        if(cue!=null) cueRoots=cue.childCount;
        string result = "PHASE2_RUNTIME " +
            "playing=" + Application.isPlaying +
            " time=" + Time.time.ToString("0.0") +
            " distance=" + route.DistanceTravelled.ToString("0.0") +
            " junctions=" + route.RouteGraph.Junctions.Count +
            " graph=valid fingerprint=" + route.RouteGraph.Fingerprint +
            " selectedBefore=" + choiceBefore +
            " commitAttempted=" + committed +
            " selectedNow=" + selected +
            " role=" + (selected ? selectedRole.ToString() : "none") +
            " branchOffsetAtMid=" + selectedOffset.ToString("0.00") +
            " skybox=" + (sky!=null ? sky.name+"/"+sky.shader.name : "none") +
            " tunnelRenderersEnabled=" + visibleTunnelRenderers +
            " cueJunctions=" + cueRoots;
        Debug.Log(result);
        return result;
    }
}