using UnityEngine;

/// <summary>Designer-facing tuning for a VOIDLINE survival run.</summary>
[CreateAssetMenu(fileName = "VoidlineRunSettings", menuName = "VOIDLINE/Run Settings")]
public sealed class VoidlineRunSettings : ScriptableObject
{
    [Header("Movement and Scoring")]
    [SerializeField, Min(1f)] private float worldSpeed = 12f;
    [SerializeField, Min(1f)] private float spawnAheadDistance = 78f;
    [SerializeField, Min(1)] private int startingHull = 3;

    [Header("Spawn Pacing")]
    [SerializeField, Min(0.35f)] private float initialSpawnInterval = 1.45f;
    [SerializeField, Min(0.35f)] private float minimumSpawnInterval = 0.7f;
    [SerializeField, Min(0f)] private float difficultyRampPerSecond = 0.004f;
    [SerializeField, Min(0.1f)] private float spawnXLimit = 3.2f;
    [SerializeField, Min(0.1f)] private float spawnYLimit = 2.2f;
    [SerializeField, Min(0f)] private float spawnZBackJitter = 8f;
    [SerializeField, Min(4f)] private float pickupInterval = 16f;
    [SerializeField, Min(0)] private int repairScoreBonus = 100;
    [SerializeField, Min(0f)] private float spawnZForwardJitter = 18f;

    public float WorldSpeed => worldSpeed;
    public float SpawnAheadDistance => spawnAheadDistance;
    public int StartingHull => startingHull;
    public float InitialSpawnInterval => initialSpawnInterval;
    public float MinimumSpawnInterval => minimumSpawnInterval;
    public float DifficultyRampPerSecond => difficultyRampPerSecond;
    public float SpawnXLimit => spawnXLimit;
    public float SpawnYLimit => spawnYLimit;
    public float SpawnZBackJitter => spawnZBackJitter;
    public float SpawnZForwardJitter => spawnZForwardJitter;
    public float PickupInterval => pickupInterval;
    public int RepairScoreBonus => repairScoreBonus;
}
