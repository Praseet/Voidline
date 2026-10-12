using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Produces a reproducible, gently curved route spine. Track chunks, hazards, pickups,
/// landmarks, ship and camera query the same absolute-distance frame so a bend is
/// gameplay geometry rather than a visual-only trick.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CorridorScroller))]
public sealed class VoidlineRouteDirector : MonoBehaviour
{
    [Header("Seed / Reproduction")]
    [Tooltip("Zero chooses a new run seed. Set a non-zero value to reproduce a route while tuning.")]
    [SerializeField] private int fixedSeed;

    [Header("Route Grammar — first pass")]
    [SerializeField, Min(40f)] private float sectionLength = 74f;
    [SerializeField, Range(0f, 4.5f)] private float maxLateralOffset = 3.1f;
    [SerializeField, Range(0f, 1.8f)] private float maxVerticalOffset = 1.1f;
    [SerializeField, Range(0.25f, 3.5f)] private float maxLateralStep = 2.55f;
    [SerializeField, Range(0f, 1.4f)] private float maxVerticalStep = 0.82f;
    [SerializeField, Range(0f, 1f)] private float returnToCenterChance = 0.18f;

    private readonly List<Vector2> controlPoints = new List<Vector2>(128);
    private CorridorScroller corridorScroller;
    private System.Random routeRandom;
    private int runSeed;
    private bool initialized;

    public int RunSeed => runSeed;
    public float DistanceTravelled => corridorScroller != null ? corridorScroller.DistanceTravelled : 0f;
    public int RouteSection => Mathf.Max(0, Mathf.FloorToInt(DistanceTravelled / Mathf.Max(1f, sectionLength)));

    private void Awake()
    {
        corridorScroller = GetComponent<CorridorScroller>();
        if (!initialized)
            GenerateRoute();
    }

    /// <summary>Starts a new seeded route and resets the matching recycled track distance.</summary>
    public void BeginRun()
    {
        if (corridorScroller == null)
            corridorScroller = GetComponent<CorridorScroller>();

        GenerateRoute();
        if (corridorScroller != null)
            corridorScroller.ResetForRun();

        Debug.Log("VOIDLINE route seed: " + runSeed + ".");
    }

    private void GenerateRoute()
    {
        runSeed = fixedSeed != 0 ? fixedSeed : CreateRunSeed();
        routeRandom = new System.Random(runSeed);
        controlPoints.Clear();
        controlPoints.Add(Vector2.zero);
        initialized = true;
        EnsureControlPoint(3);
    }

    private static int CreateRunSeed()
    {
        long ticks = DateTime.UtcNow.Ticks;
        int seed = unchecked((int)(ticks ^ (ticks >> 32) ^ Environment.TickCount));
        return seed == 0 ? 1 : seed;
    }

    private void EnsureControlPoint(int requestedIndex)
    {
        // With fast Enter Play Mode or an Editor script reload, non-serialized spline
        // caches can be cleared while the scene component survives. Rebuild the cache
        // from its run seed instead of indexing an empty/partial list every frame.
        if (!initialized || routeRandom == null || controlPoints.Count == 0)
        {
            if (runSeed == 0)
                runSeed = fixedSeed != 0 ? fixedSeed : CreateRunSeed();
            routeRandom = new System.Random(runSeed);
            controlPoints.Clear();
            controlPoints.Add(Vector2.zero);
            initialized = true;
        }

        while (controlPoints.Count <= requestedIndex)
        {
            int index = controlPoints.Count;
            if (index == 0)
            {
                controlPoints.Add(Vector2.zero);
                continue;
            }
            Vector2 previous = controlPoints[index - 1];
            Vector2 beforePrevious = index >= 2 ? controlPoints[index - 2] : previous;

            float dx = index < 2
                ? 0f
                : ((float)routeRandom.NextDouble() * 2f - 1f) * maxLateralStep;

            // Prevent a long sequence of same-direction knots. The route may weave,
            // but shouldn't drift to one side indefinitely or manufacture a sharp S.
            if (index >= 3)
            {
                Vector2 beforeBefore = controlPoints[index - 3];
                float priorDelta = beforePrevious.x - beforeBefore.x;
                float lastDelta = previous.x - beforePrevious.x;
                if (Mathf.Abs(priorDelta) > 0.2f &&
                    Mathf.Abs(lastDelta) > 0.2f &&
                    Mathf.Sign(priorDelta) == Mathf.Sign(lastDelta) &&
                    Mathf.Sign(dx) == Mathf.Sign(lastDelta))
                {
                    dx = -Mathf.Abs(dx) * Mathf.Sign(lastDelta);
                }
            }

            if (index >= 3 && routeRandom.NextDouble() < returnToCenterChance)
                dx = -previous.x * 0.82f;

            float nextX = Mathf.Clamp(previous.x + dx, -maxLateralOffset, maxLateralOffset);
            float dy = 0f;
            if (index >= 2 && index % 2 == 0 && maxVerticalStep > 0f)
                dy = ((float)routeRandom.NextDouble() * 2f - 1f) * maxVerticalStep;
            float nextY = Mathf.Clamp(previous.y + dy, -maxVerticalOffset, maxVerticalOffset);

            // Every fifth knot softens vertical offset to add visual breathing room.
            if (index % 5 == 0)
                nextY = Mathf.Lerp(nextY, 0f, 0.65f);

            controlPoints.Add(new Vector2(nextX, nextY));
        }
    }

    /// <summary>
    /// Route centre in world X/Y at an absolute travelled distance. Z is maintained
    /// by the scrolling-world model, so a given obstacle stays on its authored route knot.
    /// </summary>
    public Vector3 GetCenterOffsetAtDistance(float absoluteDistance)
    {
        Vector2 point = EvaluateCenter(absoluteDistance);
        return new Vector3(point.x, point.y, 0f);
    }

    public Vector3 GetPlayerWorldPosition(Vector2 lanePosition, float absoluteDistance, float playerZ = 0f)
    {
        Vector3 center = GetCenterOffsetAtDistance(absoluteDistance);
        return new Vector3(center.x + lanePosition.x, center.y + lanePosition.y, playerZ);
    }

    /// <summary>Approximate the route's forward tangent, with a positive world-Z component.</summary>
    public Vector3 GetTrackTangentAtDistance(float absoluteDistance)
    {
        const float sample = 3f;
        float beforeDistance = Mathf.Max(0f, absoluteDistance - sample);
        float afterDistance = Mathf.Max(beforeDistance + 0.1f, absoluteDistance + sample);
        Vector3 before = GetCenterOffsetAtDistance(beforeDistance);
        Vector3 after = GetCenterOffsetAtDistance(afterDistance);
        Vector3 tangent = new Vector3(
            after.x - before.x,
            after.y - before.y,
            afterDistance - beforeDistance);
        if (tangent.sqrMagnitude < 0.0001f)
            return Vector3.forward;
        return tangent.normalized;
    }

    public Quaternion GetTrackRotationAtDistance(float absoluteDistance)
    {
        Vector3 tangent = GetTrackTangentAtDistance(absoluteDistance);
        return Quaternion.LookRotation(tangent, Vector3.up);
    }

    private Vector2 EvaluateCenter(float absoluteDistance)
    {
        if (absoluteDistance <= 0f || sectionLength <= 0.01f)
            return Vector2.zero;

        float scaled = absoluteDistance / sectionLength;
        int index = Mathf.Max(0, Mathf.FloorToInt(scaled));
        float t = Mathf.Clamp01(scaled - index);
        EnsureControlPoint(index + 2);

        Vector2 p0 = controlPoints[Mathf.Max(0, index - 1)];
        Vector2 p1 = controlPoints[index];
        Vector2 p2 = controlPoints[index + 1];
        Vector2 p3 = controlPoints[index + 2];

        float t2 = t * t;
        float t3 = t2 * t;
        Vector2 value = 0.5f * (
            2f * p1 +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3);

        value.x = Mathf.Clamp(value.x, -maxLateralOffset, maxLateralOffset);
        value.y = Mathf.Clamp(value.y, -maxVerticalOffset, maxVerticalOffset);
        return value;
    }

    private void OnValidate()
    {
        sectionLength = Mathf.Max(40f, sectionLength);
        maxLateralStep = Mathf.Min(maxLateralStep, maxLateralOffset);
        maxVerticalStep = Mathf.Min(maxVerticalStep, maxVerticalOffset);
    }
}
