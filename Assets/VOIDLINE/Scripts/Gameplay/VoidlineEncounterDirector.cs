using System;
using UnityEngine;

/// <summary>
/// Seeded short-horizon pacing director. Emits compact, validated formations and a
/// protected lane rather than scattering independent random asteroids.
/// </summary>
internal sealed class VoidlineEncounterDirector
{
    internal struct Hazard
    {
        public Vector3 Position;
        public float Radius;

        public Hazard(Vector3 position, float radius)
        {
            Position = position;
            Radius = radius;
        }
    }

    internal struct Plan
    {
        public int Index;
        public string PatternName;
        public Vector2 SafeLane;
        public int Count;
        public Hazard First;
        public Hazard Second;
        public Hazard Third;
        public float DelayMultiplier;
        public float Pressure;
    }

    private enum Pattern
    {
        Single = 0,
        OffsetPair = 1,
        CrossWeave = 2,
        VerticalWeave = 3,
        FlankPair = 4,
        Slalom = 5
    }

    private System.Random random;
    private int encounterIndex;
    private Pattern lastPattern = Pattern.Single;
    private bool recoveryDue;

    public void BeginRun(int seed)
    {
        random = new System.Random(unchecked(seed ^ 0x5F3759DF));
        encounterIndex = 0;
        lastPattern = Pattern.Single;
        recoveryDue = false;
    }

    public Plan Next(float elapsed, int passagesCleared, int hull,
        float spawnAheadDistance, float spawnXLimit, float spawnYLimit,
        float spawnZBackJitter, float spawnZForwardJitter, float worldSpeed)
    {
        if (random == null)
            BeginRun(73129);

        int index = encounterIndex++;
        float pressure = Mathf.Clamp01(elapsed / 70f * 0.55f +
            Mathf.Max(0, passagesCleared) * 0.06f);

        Pattern pattern = Pattern.Single;
        bool recovery = recoveryDue || hull <= 1;
        if (recovery)
        {
            pattern = Pattern.Single;
            recoveryDue = false;
        }
        else if (pressure >= 0.36f && passagesCleared >= 2 && index % 11 == 7)
        {
            pattern = Pattern.Slalom;
        }
        else if (pressure >= 0.08f && index % 9 == 2)
        {
            pattern = Pattern.OffsetPair;
        }
        else if (pressure >= 0.14f && index % 9 == 4)
        {
            pattern = Pattern.CrossWeave;
        }
        else if (pressure >= 0.24f && passagesCleared >= 3 && index % 9 == 6)
        {
            pattern = Pattern.VerticalWeave;
        }
        else if (pressure >= 0.30f && passagesCleared >= 1 && index % 9 == 8)
        {
            pattern = Pattern.FlankPair;
        }

        bool peak = pattern != Pattern.Single;
        if (peak && (pattern == lastPattern || recoveryDue))
        {
            pattern = Pattern.Single;
            peak = false;
            recovery = true;
        }

        Vector2 safeLane = ChooseSafeLane(spawnXLimit, spawnYLimit);
        float xLimit = Mathf.Max(0.8f, spawnXLimit - 0.75f);
        float yLimit = Mathf.Max(0.5f, spawnYLimit - 0.4f);
        if (pattern == Pattern.CrossWeave)
        {
            // A clean centre line is deliberately protected by the diagonal pair.
            safeLane = Vector2.zero;
        }
        else if (pattern == Pattern.VerticalWeave)
        {
            float laneX = Mathf.Min(1.65f, xLimit * 0.67f);
            int safeIndex = random.Next(0, 3);
            safeLane = new Vector2(safeIndex == 0 ? -laneX : (safeIndex == 1 ? 0f : laneX), 0f);
        }
        else if (pattern == Pattern.FlankPair)
        {
            // A wide, deliberately open centre lane makes this formation read as a choice,
            // not a wall. Hazards arrive with a small depth stagger to preserve reaction time.
            safeLane = Vector2.zero;
        }
        // Keep adjacent encounter planes ordered. Wide independent depth jitter can put a new
        // formation in front of the second member of the previous pair, especially at boost.
        // Lane and pattern variation carry most of the randomness; depth variation stays subtle.
        float backJitter = Mathf.Min(spawnZBackJitter, 2.5f);
        float forwardJitter = Mathf.Min(spawnZForwardJitter, 2.5f);
        float spawnZ = spawnAheadDistance + NextFloat(-backJitter, forwardJitter);
        // Pair depth is a reaction-time budget, not a fixed decorative spacing. At boost
        // speed a 6 m gap gives barely a quarter-second to read the second obstacle.
        // Keep a useful dodge window across the entire speed range without making pairs
        // drift so far apart that they stop reading as one formation.
        float pairStagger = Mathf.Clamp(worldSpeed * 0.84f, 12f, 21f);
        float radius = Mathf.Lerp(0.68f, 0.96f, Mathf.Clamp01(pressure + NextFloat(0f, 0.24f)));

        Vector2 firstLane = ChooseHazardLane(safeLane, Vector2.positiveInfinity,
            xLimit, yLimit, radius);
        float firstZ = spawnZ;
        Hazard first = new Hazard(new Vector3(firstLane.x, firstLane.y, firstZ), radius);

        Hazard second = default;
        Hazard third = default;
        int count = 1;
        float delayMultiplier = recovery ? 1.2f : 1f;
        if (pattern != Pattern.Single)
        {
            float secondRadius = Mathf.Lerp(0.68f, 0.93f, Mathf.Clamp01(pressure + NextFloat(0f, 0.18f)));
            Vector2 secondLane = ChooseHazardLane(safeLane, firstLane, xLimit, yLimit, secondRadius);
            float stagger = pattern == Pattern.OffsetPair ? pairStagger * 0.94f : pairStagger;
            second = new Hazard(new Vector3(secondLane.x, secondLane.y, spawnZ + stagger), secondRadius);
            count = 2;
            delayMultiplier = 1.08f;
            recoveryDue = peak;
        }

        if (pattern == Pattern.Slalom)
        {
            // Three depth-separated decisions form an S-curve: left, right, then left.
            // Each plane has one broad dodge side; depth grows with actual travel speed so
            // boost remains demanding without collapsing the steering window.
            // Put each boulder close enough to the centre line that holding still is unsafe;
            // alternating sides then requires an actual lateral commitment, not a cosmetic weave.
            float laneX = Mathf.Min(0.92f, xLimit * 0.42f);
            float laneY = Mathf.Min(0.30f, yLimit * 0.16f);
            float slalomStagger = Mathf.Clamp(worldSpeed * 1.03f, 13f, 25f);
            first = new Hazard(new Vector3(-laneX, -laneY, firstZ), first.Radius);
            second = new Hazard(new Vector3(laneX, laneY, spawnZ + slalomStagger), second.Radius);
            third = new Hazard(new Vector3(-laneX, -laneY * 0.55f,
                spawnZ + slalomStagger * 2f), Mathf.Max(0.62f, second.Radius * 0.96f));
            count = 3;
            delayMultiplier = 1.18f;
            recoveryDue = true;
            // Plan.SafeLane denotes the entry-side opening; the next two openings alternate.
            safeLane = new Vector2(laneX + 0.32f, 0f);
        }
        else if (pattern == Pattern.VerticalWeave)
        {
            // A high/low pair stays opposite the protected centre-height lane.
            // The player can read the first obstacle before committing to the second.
            float laneX = Mathf.Min(1.65f, xLimit * 0.67f);
            float x = Mathf.Abs(safeLane.x) < 0.1f
                ? (random.Next(0, 2) == 0 ? laneX : -laneX)
                : (safeLane.x > 0f ? -laneX : laneX);
            float top = Mathf.Min(yLimit * 0.72f, Mathf.Max(0.72f, yLimit * 0.62f));
            first = new Hazard(new Vector3(x, top, firstZ), first.Radius);
            second = new Hazard(new Vector3(
                Mathf.Clamp(x + NextFloat(-0.2f, 0.2f), -xLimit * 0.7f, xLimit * 0.7f),
                -top, spawnZ + pairStagger), second.Radius);
            count = 2;
            delayMultiplier = 1.12f;
        }
        else if (pattern == Pattern.FlankPair)
        {
            float flankX = Mathf.Min(2.35f, xLimit * 0.96f);
            float laneY = Mathf.Min(0.48f, yLimit * 0.28f);
            first = new Hazard(new Vector3(-flankX, -laneY, firstZ), first.Radius);
            second = new Hazard(new Vector3(flankX, laneY, spawnZ + pairStagger * 0.90f), second.Radius);
            count = 2;
            delayMultiplier = 1.10f;
        }
        else if (pattern == Pattern.CrossWeave)
        {
            // A diagonal pair creates a readable central/outer corridor rather than
            // forming a wall across the full flight envelope.
            float laneX = Mathf.Min(1.75f, xLimit * 0.72f);
            float laneY = Mathf.Min(1.15f, yLimit * 0.68f);
            Vector2 leftLow = new Vector2(-laneX, -laneY);
            Vector2 rightHigh = new Vector2(laneX, laneY);
            Vector2 leftHigh = new Vector2(-laneX, laneY);
            Vector2 rightLow = new Vector2(laneX, -laneY);
            bool leftLowSafe = IsSafeClearance(leftLow, safeLane, 1.2f);
            bool rightHighSafe = IsSafeClearance(rightHigh, safeLane, 1.2f);
            if (!leftLowSafe || !rightHighSafe)
            {
                first = new Hazard(new Vector3(leftHigh.x, leftHigh.y, firstZ), first.Radius);
                second = new Hazard(new Vector3(rightLow.x, rightLow.y, second.Position.z), second.Radius);
            }
            else
            {
                first = new Hazard(new Vector3(leftLow.x, leftLow.y, firstZ), first.Radius);
                second = new Hazard(new Vector3(rightHigh.x, rightHigh.y, second.Position.z), second.Radius);
            }
            delayMultiplier = 1.12f;
        }

        lastPattern = pattern;
        return new Plan
        {
            Index = index,
            PatternName = pattern.ToString(),
            SafeLane = safeLane,
            Count = count,
            First = first,
            Second = second,
            Third = third,
            DelayMultiplier = delayMultiplier,
            Pressure = pressure
        };
    }

    private Vector2 ChooseSafeLane(float xLimit, float yLimit)
    {
        float laneX = Mathf.Min(1.65f, xLimit * 0.67f);
        float laneY = Mathf.Min(0.95f, yLimit * 0.56f);
        int xIndex = random.Next(0, 3);
        int yIndex = random.Next(0, 3);
        float x = xIndex == 0 ? -laneX : (xIndex == 1 ? 0f : laneX);
        float y = yIndex == 0 ? -laneY : (yIndex == 1 ? 0f : laneY);
        return new Vector2(x, y);
    }

    private Vector2 ChooseHazardLane(Vector2 safeLane, Vector2 otherHazard,
        float xLimit, float yLimit, float radius)
    {
        const int attempts = 18;
        float safeLaneClearance = 1.05f + Mathf.Clamp(radius, 0.5f, 1.2f) * 0.14f;
        float hazardClearance = 1.02f + Mathf.Clamp(radius, 0.5f, 1.2f) * 0.14f;
        for (int i = 0; i < attempts; i++)
        {
            Vector2 candidate = new Vector2(
                NextFloat(-xLimit, xLimit),
                NextFloat(-yLimit, yLimit));

            if (!IsSafeClearance(candidate, safeLane, safeLaneClearance))
                continue;
            if (otherHazard != Vector2.positiveInfinity &&
                !IsSafeClearance(candidate, otherHazard, hazardClearance))
                continue;
            return candidate;
        }

        float fallbackX = safeLane.x >= 0f ? -xLimit * 0.82f : xLimit * 0.82f;
        float fallbackY = safeLane.y >= 0f ? -yLimit * 0.64f : yLimit * 0.64f;
        return new Vector2(fallbackX, fallbackY);
    }

    private static bool IsSafeClearance(Vector2 a, Vector2 b, float required)
    {
        // Combined player/asteroid footprint with a little extra margin in the
        // narrower vertical axis. This deliberately prefers readable space to density.
        float dx = (a.x - b.x) / 1.75f;
        float dy = (a.y - b.y) / 1.32f;
        return Mathf.Sqrt(dx * dx + dy * dy) >= required;
    }

    private float NextFloat(float minimum, float maximum)
    {
        return minimum + (float)random.NextDouble() * (maximum - minimum);
    }
}
