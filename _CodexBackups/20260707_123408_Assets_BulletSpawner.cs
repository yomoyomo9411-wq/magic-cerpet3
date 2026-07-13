using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BallScheduleEntry
{
    [Tooltip("ゲーム開始から、球がプレイヤーに届くまでの秒数")]
    public float time = 10f;

    [Tooltip("球が通る横の位置。-8から8の範囲がおすすめ")]
    public float laneX = 0f;

    [Tooltip("この球だけ別の見た目にしたいときに設定。空なら標準の球を使います")]
    public GameObject ballPrefab;

    [Tooltip("球の大きさ")]
    [Min(0.1f)]
    public float scale = 2.5f;

    [Tooltip("見た目の大きさをXYZ別に調整します。0,0,0のままなら1,1,1として扱います")]
    public Vector3 visualScale = Vector3.one;

    [Tooltip("見た目の向きを調整します")]
    public Vector3 visualRotation = Vector3.zero;

    [Tooltip("出現位置から見た目だけ少しずらしたいときに使います")]
    public Vector3 visualOffset = Vector3.zero;

    [Tooltip("当たり判定の倍率。1なら見た目と同じ比率で大きくなります")]
    [Min(0.1f)]
    public float colliderScale = 1f;

    [Tooltip("球の速さ")]
    [Min(0.1f)]
    public float speed = 4f;

    [Tooltip("この球が見えた瞬間に画面を止める秒数。0なら止めません")]
    [Min(0f)]
    public float pauseSecondsOnSpawn;
}

public class BulletSpawner : MonoBehaviour
{
    public Transform player;
    public GameObject bulletPrefab;

    [Header("球が出現する位置")]
    public float spawnDistance = 35f;
    [Tooltip("球が急に現れないように、最低でもこの距離から出します")]
    [Min(1f)]
    public float minimumVisibleSpawnDistance = 60f;
    public float spawnHeight = 1.5f;
    public float bulletLifeTime = 12f;
    [Tooltip("プレイヤーが球を追い越して、この距離より後ろに離れたら球を消します")]
    [Min(0f)]
    public float destroyBehindPlayerDistance = 20f;

    [Header("当たり判定")]
    [Tooltip("モンスターの当たり判定を見た目より少し小さくします。0.65なら約65%です")]
    [Range(0.2f, 1.2f)]
    public float monsterHitboxTightness = 0.65f;

    [Tooltip("色が付いていないモンスターに、実行時だけ薄く色を付けます")]
    public bool colorPlainMonsters = true;

    [Header("モンスター待機モーション")]
    public bool animateSpawnedMonsters = true;
    public float idleBobHeight = 0.18f;
    public float idleBobSpeed = 2.2f;
    public float idleYawDegrees = 8f;
    public float idleRollDegrees = 3f;
    public float idleScalePulse = 0.04f;

    [Header("ドラゴン演出")]
    public Color leftDragonColor = new Color(0.95f, 0.15f, 0.1f, 1f);
    public Color rightDragonColor = new Color(0.15f, 0.85f, 0.2f, 1f);

    [Header("チュートリアルコース")]
    public bool repeatTutorialSchedule;
    [Tooltip("チュートリアルの球が出た瞬間に一時停止する秒数")]
    [Min(0f)]
    public float tutorialPauseSecondsOnSpawn = 4f;
    [Tooltip("Element 0から何番目まで一時停止するか。3ならElement 0, 1, 2, 3で止まります")]
    [Min(0)]
    public int tutorialPauseUntilElement = 3;
    [Tooltip("Timeがこの秒数のチュートリアル球は、見えた瞬間に追加で止めます")]
    public float tutorialSpecialPauseBallTime = 22f;
    [Tooltip("Timeがこの秒数のチュートリアル球はMiddleを表示します")]
    public float tutorialMiddlePromptBallTime = 17f;
    [Tooltip("Timeが一致したチュートリアル球で止める秒数")]
    [Min(0f)]
    public float tutorialSpecialPauseSeconds = 5f;
    public List<BallScheduleEntry> tutorialSchedule = new List<BallScheduleEntry>
    {
        new BallScheduleEntry { time = 6f, laneX = -3f, scale = 2.5f, speed = 3f },
        new BallScheduleEntry { time = 12f, laneX = 3f, scale = 2.5f, speed = 3f },
        new BallScheduleEntry { time = 18f, laneX = 0f, scale = 2.8f, speed = 3f },
    };

    [Header("子ども向けコース")]
    public bool repeatSchedule;
    [Tooltip("本番中に、球が見えた瞬間に時間を止めて〇判定に入る球のTime")]
    public List<float> mainCircleChallengeBallTimes = new List<float> { 20f, 38f };
    [Tooltip("本番の〇判定対象の球がプレイヤーに届く何秒前に時間を止めるか")]
    [Min(0f)]
    public float mainCircleChallengeDelayAfterSpawn = 2f;
    [Tooltip("大きい球は見た目より早く当たるので、球の半径を考慮して停止タイミングを前に出します")]
    public bool mainCircleChallengeUseBallRadius = true;
    public List<BallScheduleEntry> schedule = new List<BallScheduleEntry>
    {
        new BallScheduleEntry { time = 10f, laneX = -4f },
        new BallScheduleEntry { time = 17f, laneX = 4f },
        new BallScheduleEntry { time = 24f, laneX = 0f },
        new BallScheduleEntry { time = 31f, laneX = -5f },
        new BallScheduleEntry { time = 38f, laneX = 5f },
    };

    private float elapsedTime;
    private int nextBallIndex;
    private float tutorialElapsedTime;
    private int nextTutorialBallIndex;
    private bool mainScheduleStarted;

    private void Start()
    {
        if (mainCircleChallengeBallTimes == null || mainCircleChallengeBallTimes.Count == 0)
        {
            mainCircleChallengeBallTimes = new List<float> { 20f, 38f };
        }

        tutorialSchedule.Sort((a, b) => a.time.CompareTo(b.time));
        schedule.Sort((a, b) => a.time.CompareTo(b.time));
    }

    private void Update()
    {
        if (Time.timeScale == 0f || player == null)
        {
            return;
        }

        if (!MagicCarpetGameFlow.IsMainGameStarted)
        {
            ProcessSchedule(tutorialSchedule, repeatTutorialSchedule, ref tutorialElapsedTime, ref nextTutorialBallIndex, true);
            return;
        }

        if (!mainScheduleStarted)
        {
            elapsedTime = 0f;
            nextBallIndex = 0;
            mainScheduleStarted = true;
        }

        ProcessSchedule(schedule, repeatSchedule, ref elapsedTime, ref nextBallIndex, false);
    }

    private void ProcessSchedule(List<BallScheduleEntry> activeSchedule, bool repeat, ref float activeElapsedTime, ref int activeNextBallIndex, bool isTutorial)
    {
        if (activeSchedule == null || activeSchedule.Count == 0)
        {
            return;
        }

        activeElapsedTime += Time.deltaTime;

        while (activeNextBallIndex < activeSchedule.Count && activeElapsedTime >= GetSpawnTime(activeSchedule[activeNextBallIndex], isTutorial))
        {
            var entry = activeSchedule[activeNextBallIndex];
            SpawnBall(entry, isTutorial);
            var pauseSeconds = 0f;
            if (isTutorial && activeNextBallIndex <= tutorialPauseUntilElement)
            {
                pauseSeconds = tutorialPauseSecondsOnSpawn;
            }

            var ballPauseSeconds = isTutorial ? GetPauseSecondsForTutorialBall(entry) : 0f;
            if (ballPauseSeconds > 0f)
            {
                pauseSeconds = Mathf.Max(pauseSeconds, ballPauseSeconds);
            }

            if (isTutorial && pauseSeconds > 0f)
            {
                MagicCarpetGameFlow.PauseTutorialForSeconds(pauseSeconds, GetDodgePromptForBall(entry, activeSchedule));
            }
            else if (!isTutorial && IsMainCircleChallengeBall(entry))
            {
                StartCoroutine(PauseMainCircleChallengeBeforeArrival(entry, activeElapsedTime));
            }

            activeNextBallIndex++;
        }

        if (repeat && activeNextBallIndex >= activeSchedule.Count)
        {
            activeElapsedTime = 0f;
            activeNextBallIndex = 0;
        }
    }

    private float GetPauseSecondsForTutorialBall(BallScheduleEntry entry)
    {
        if (entry.pauseSecondsOnSpawn > 0f)
        {
            return entry.pauseSecondsOnSpawn;
        }

        if (IsSpecialPauseBall(entry))
        {
            return tutorialSpecialPauseSeconds;
        }

        return 0f;
    }

    private string GetDodgePromptForBall(BallScheduleEntry entry, List<BallScheduleEntry> activeSchedule)
    {
        if (Mathf.Abs(entry.time - tutorialSpecialPauseBallTime) <= 0.25f)
        {
            return "stekki";
        }

        if (Mathf.Abs(entry.time - tutorialMiddlePromptBallTime) <= 0.25f)
        {
            return "Middle";
        }

        if (entry.laneX < 0f)
        {
            return "Right";
        }

        if (entry.laneX > 0f)
        {
            return "Left";
        }
        if (Mathf.Abs(entry.laneX) <= 0.01f)
        {
            return "Middle";
        }

        return "Practice";
    }

    private bool IsSpecialPauseBall(BallScheduleEntry entry)
    {
        return Mathf.Abs(entry.time - tutorialSpecialPauseBallTime) <= 0.25f;
    }

    private bool IsMainCircleChallengeBall(BallScheduleEntry entry)
    {
        if (mainCircleChallengeBallTimes == null)
        {
            return false;
        }

        foreach (var challengeTime in mainCircleChallengeBallTimes)
        {
            if (Mathf.Abs(entry.time - challengeTime) <= 0.25f)
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerator PauseMainCircleChallengeBeforeArrival(BallScheduleEntry entry, float scheduleElapsedAtSpawn)
    {
        var frontArrivalOffset = mainCircleChallengeUseBallRadius ? GetEstimatedBallRadius(entry) / Mathf.Max(0.1f, entry.speed) : 0f;
        var targetTime = Mathf.Max(0f, entry.time - frontArrivalOffset - mainCircleChallengeDelayAfterSpawn);
        var waitSeconds = Mathf.Max(0f, targetTime - scheduleElapsedAtSpawn);
        if (waitSeconds > 0f)
        {
            yield return new WaitForSeconds(waitSeconds);
        }

        MagicCarpetGameFlow.PauseCircleChallenge("stekki");
    }

    private float GetEstimatedBallRadius(BallScheduleEntry entry)
    {
        return Mathf.Max(0f, entry.scale * entry.colliderScale * 0.5f);
    }

    private void SpawnBall(BallScheduleEntry entry, bool isTutorial)
    {
        var prefab = entry.ballPrefab != null ? entry.ballPrefab : bulletPrefab;
        if (prefab == null)
        {
            return;
        }

        var activeSpawnDistance = GetSpawnDistance(isTutorial);
        var spawnPosition = player.position + player.forward * activeSpawnDistance;
        spawnPosition.x = entry.laneX;
        spawnPosition.y = player.position.y + spawnHeight;

        var ball = new GameObject("Ball");
        ball.transform.position = spawnPosition;
        ball.transform.rotation = Quaternion.identity;

        var visual = Instantiate(prefab, ball.transform);
        visual.name = prefab.name;
        visual.transform.localPosition = entry.visualOffset;
        visual.transform.localRotation = Quaternion.Euler(entry.visualRotation);
        visual.transform.localScale = Vector3.Scale(visual.transform.localScale * entry.scale, GetSafeVisualScale(entry.visualScale));
        ApplyMonsterColorIfNeeded(visual, prefab);
        ApplyDragonStyleIfNeeded(visual, prefab, entry);
        ApplyIdleMotionIfNeeded(visual);
        AssignBulletTag(ball);
        EnsureFallbackCollider(ball, entry.colliderScale);
        ApplyColliderScale(ball, entry.colliderScale);

        if (isTutorial)
        {
            var resultReporter = ball.AddComponent<TutorialBallResultReporter>();
            resultReporter.player = player;
            resultReporter.ignorePassSuccess = IsSpecialPauseBall(entry);
        }
        else if (IsMainCircleChallengeBall(entry))
        {
            ball.AddComponent<CircleChallengeObstacle>();
        }

        var carpet = player.GetComponent<CarpetMove>();
        var carpetSpeed = carpet != null ? carpet.forwardSpeed : 0f;
        var ballVelocity = Vector3.forward * (carpetSpeed - entry.speed);
        var rootRigidbody = ball.GetComponent<Rigidbody>();
        if (rootRigidbody == null)
        {
            rootRigidbody = ball.AddComponent<Rigidbody>();
        }

        foreach (var rigidbody in ball.GetComponentsInChildren<Rigidbody>())
        {
            rigidbody.useGravity = false;
            rigidbody.isKinematic = true;
            rigidbody.detectCollisions = true;
        }

        MakeBallCollidersTriggers(ball);

        var runtime = ball.GetComponent<BallRuntimeController>();
        if (runtime == null)
        {
            runtime = ball.AddComponent<BallRuntimeController>();
        }

        runtime.player = player;
        runtime.velocity = ballVelocity;
        runtime.destroyBehindDistance = destroyBehindPlayerDistance;

        Destroy(ball, Mathf.Max(bulletLifeTime, activeSpawnDistance / entry.speed + 2f));
    }

    private void AssignBulletTag(GameObject ball)
    {
        foreach (var transform in ball.GetComponentsInChildren<Transform>(true))
        {
            try
            {
                transform.gameObject.tag = "Bullet";
            }
            catch (UnityException)
            {
                return;
            }
        }
    }

    private void MakeBallCollidersTriggers(GameObject ball)
    {
        foreach (var collider in ball.GetComponentsInChildren<Collider>())
        {
            collider.isTrigger = true;
        }
    }

    private void ApplyColliderScale(GameObject ball, float colliderScale)
    {
        var safeColliderScale = Mathf.Max(0.1f, colliderScale) * Mathf.Clamp(monsterHitboxTightness, 0.2f, 1.2f);

        foreach (var sphere in ball.GetComponentsInChildren<SphereCollider>())
        {
            sphere.radius *= safeColliderScale;
        }

        foreach (var box in ball.GetComponentsInChildren<BoxCollider>())
        {
            box.size *= safeColliderScale;
        }

        foreach (var capsule in ball.GetComponentsInChildren<CapsuleCollider>())
        {
            capsule.radius *= safeColliderScale;
            capsule.height *= safeColliderScale;
        }
    }

    private void EnsureFallbackCollider(GameObject ball, float colliderScale)
    {
        var hasPrimitiveCollider = false;
        foreach (var collider in ball.GetComponentsInChildren<Collider>(true))
        {
            if (collider is MeshCollider)
            {
                collider.enabled = false;
                continue;
            }

            hasPrimitiveCollider = true;
        }

        if (hasPrimitiveCollider)
        {
            return;
        }

        var bounds = GetRendererBounds(ball);
        if (!bounds.HasValue)
        {
            return;
        }

        var box = ball.AddComponent<BoxCollider>();
        box.center = ball.transform.InverseTransformPoint(bounds.Value.center);
        box.size = new Vector3(
            Mathf.Max(0.1f, bounds.Value.size.x),
            Mathf.Max(0.1f, bounds.Value.size.y),
            Mathf.Max(0.1f, bounds.Value.size.z));
        box.isTrigger = true;
    }

    private Bounds? GetRendererBounds(GameObject target)
    {
        Bounds? bounds = null;
        foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null)
            {
                continue;
            }

            if (!bounds.HasValue)
            {
                bounds = renderer.bounds;
            }
            else
            {
                var current = bounds.Value;
                current.Encapsulate(renderer.bounds);
                bounds = current;
            }
        }

        return bounds;
    }

    private void ApplyMonsterColorIfNeeded(GameObject visual, GameObject prefab)
    {
        if (!colorPlainMonsters || visual == null)
        {
            return;
        }

        var palette = new[]
        {
            new Color(0.9f, 0.35f, 0.3f, 1f),
            new Color(0.35f, 0.75f, 1f, 1f),
            new Color(0.75f, 0.45f, 1f, 1f),
            new Color(0.45f, 0.95f, 0.55f, 1f),
            new Color(1f, 0.75f, 0.25f, 1f)
        };
        var paletteColor = palette[Mathf.Abs((prefab != null ? prefab.name : visual.name).GetHashCode()) % palette.Length];

        foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.materials;
            foreach (var material in materials)
            {
                if (material == null || HasMainTexture(material))
                {
                    continue;
                }

                var color = GetMaterialColor(material);
                Color.RGBToHSV(color, out _, out var saturation, out var value);
                if (saturation > 0.18f || value < 0.18f)
                {
                    continue;
                }

                SetMaterialColor(material, paletteColor);
            }
        }
    }

    private void ApplyIdleMotionIfNeeded(GameObject visual)
    {
        if (!animateSpawnedMonsters || visual == null)
        {
            return;
        }

        var motion = visual.GetComponent<MonsterIdleMotion>();
        if (motion == null)
        {
            motion = visual.AddComponent<MonsterIdleMotion>();
        }

        motion.bobHeight = idleBobHeight;
        motion.bobSpeed = idleBobSpeed;
        motion.yawDegrees = idleYawDegrees;
        motion.rollDegrees = idleRollDegrees;
        motion.scalePulse = idleScalePulse;
    }

    private void ApplyDragonStyleIfNeeded(GameObject visual, GameObject prefab, BallScheduleEntry entry)
    {
        if (visual == null || !IsDragonPrefab(prefab, visual))
        {
            return;
        }

        if (Mathf.Abs(entry.time - 20f) <= 0.25f)
        {
            var flap = visual.GetComponent<DragonWingFlap>();
            if (flap == null)
            {
                flap = visual.AddComponent<DragonWingFlap>();
            }
        }

        if (Mathf.Abs(entry.time - 38f) <= 0.25f)
        {
            if (entry.laneX < -0.1f)
            {
                TintMonster(visual, leftDragonColor);
            }
            else if (entry.laneX > 0.1f)
            {
                TintMonster(visual, rightDragonColor);
            }
        }
    }

    private bool IsDragonPrefab(GameObject prefab, GameObject visual)
    {
        var sourceName = ((prefab != null ? prefab.name : string.Empty) + " " + (visual != null ? visual.name : string.Empty)).ToLowerInvariant();
        return sourceName.Contains("dragon") || sourceName.Contains("doragon") || sourceName.Contains("ドラゴン");
    }

    private void TintMonster(GameObject visual, Color tint)
    {
        foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
        {
            foreach (var material in renderer.materials)
            {
                if (material == null)
                {
                    continue;
                }

                SetMaterialColor(material, tint);
            }
        }
    }

    private bool HasMainTexture(Material material)
    {
        return (material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null)
            || (material.HasProperty("_MainTex") && material.GetTexture("_MainTex") != null);
    }

    private Color GetMaterialColor(Material material)
    {
        if (material.HasProperty("_BaseColor"))
        {
            return material.GetColor("_BaseColor");
        }

        if (material.HasProperty("_Color"))
        {
            return material.GetColor("_Color");
        }

        return Color.white;
    }

    private void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    public void ResetSchedules()
    {
        elapsedTime = 0f;
        nextBallIndex = 0;
        tutorialElapsedTime = 0f;
        nextTutorialBallIndex = 0;
        mainScheduleStarted = false;
    }

    private Vector3 GetSafeVisualScale(Vector3 visualScale)
    {
        if (Mathf.Approximately(visualScale.x, 0f) &&
            Mathf.Approximately(visualScale.y, 0f) &&
            Mathf.Approximately(visualScale.z, 0f))
        {
            return Vector3.one;
        }

        return new Vector3(
            Mathf.Approximately(visualScale.x, 0f) ? 1f : visualScale.x,
            Mathf.Approximately(visualScale.y, 0f) ? 1f : visualScale.y,
            Mathf.Approximately(visualScale.z, 0f) ? 1f : visualScale.z);
    }

    private float GetSpawnTime(BallScheduleEntry entry, bool isTutorial)
    {
        // The carpet moves forward too, so schedule the ball early enough to arrive at entry.time.
        return Mathf.Max(0f, entry.time - GetSpawnDistance(isTutorial) / entry.speed);
    }

    private float GetSpawnDistance(bool isTutorial)
    {
        return isTutorial
            ? spawnDistance
            : Mathf.Max(spawnDistance, minimumVisibleSpawnDistance);
    }
}
