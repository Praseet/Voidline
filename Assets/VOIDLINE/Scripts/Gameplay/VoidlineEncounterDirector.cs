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
        public float DelayMultiplier;
        public float Pressure;
    }

    private enum Pattern
    {
        Single = 0,
        OffsetPair = 1,
        CrossWeave = 2,
        VerticalWeave = 3
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
        float spawnZBackJitter, float spawnZForwardJitter)
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
        else if (pressure >= 0.08f && index % 7 == 2)
        {
            pattern = Pattern.OffsetPair;
        }
        else if (pressure >= 0.16f && index % 7 == 4)
        {
            pattern = Pattern.CrossWeave;
        }
        else if (pressure >= 0.32f && passagesCleared >= 3 && index % 7 == 6)
        {
            pattern = Pattern.VerticalWeave;
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
        float spawnZ = spawnAheadDistance + NextFloat(-spawnZBackJitter, spawnZForwardJitter);
        float radius = Mathf.Lerp(0.68f, 0.96f, Mathf.Clamp01(pressure + NextFloat(0f, 0.24f)));

        Vector2 firstLane = ChooseHazardLane(safeLane, Vector2.positiveInfinity,
            xLimit, yLimit, radius);
        float firstZ = spawnZ;
        Hazard first = new Hazard(new Vector3(firstLane.x, firstLane.y, firstZ), radius);

        Hazard second = default;
        int count = 1;
        float delayMultiplier = recovery ? 1.2f : 1f;
        if (pattern != Pattern.Single)
        {
            float secondRadius = Mathf.Lerp(0.68f, 0.93f, Mathf.Clamp01(pressure + NextFloat(0f, 0.18f)));
            Vector2 secondLane = ChooseHazardLane(safeLane, firstLane, xLimit, yLimit, secondRadius);
            float stagger = pattern == Pattern.OffsetPair ? 4.8f : NextFloat(6f, 9f);
            second = new Hazard(new Vector3(secondLane.x, secondLane.y, spawnZ + stagger), secondRadius);
            count = 2;
            delayMultiplier = 1.08f;
            recoveryDue = peak;
        }

        if (pattern == Pattern.VerticalWeave)
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
                -top, spawnZ + NextFloat(6f, 9f)), second.Radius);
            count = 2;
            delayMultiplier = 1.12f;
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
        for (int i = 0; i < attempts; i++)
        {
            Vector2 candidate = new Vector2(
                NextFloat(-xLimit, xLimit),
                NextFloat(-yLimit, yLimit));

            if (!IsSafeClearance(candidate, safeLane, 1.18f))
                continue;
            if (otherHazard != Vector2.positiveInfinity &&
                !IsSafeClearance(candidate, otherHazard, 1.15f))
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
