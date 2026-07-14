using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BallScheduleEntry
{
    [Tooltip("ゲーム開始から、この球を出現させるまでの秒数")]
    public float time = 10f;

    [Tooltip("球が通る横の位置。-8から8の範囲がおすすめ")]
    public float laneX = 0f;

    [Tooltip("この球だけ別の見た目にしたいときに設定。空なら標準の球を使います")]
    public GameObject ballPrefab;

    [Tooltip("この障害物に当たった時に特殊SEを使う")]
    public bool useHitSound2;

    [Tooltip("Life damage when this obstacle hits the player. Range: 0.1 to 3.")]
    [Range(0.1f, 3f)]
    public float damage = 1f;

    [Tooltip("球の大きさ")]
    [Min(0.1f)]
    public float scale = 2.5f;

    [Tooltip("見た目の大きさをXYZ別に調整します。0,0,0のままなら1,1,1として扱います")]
    public Vector3 visualScale = Vector3.one;

    [Tooltip("見た目の向きを調整します")]
    public Vector3 visualRotation = Vector3.zero;

    [Tooltip("出現位置から見た目だけ少しずらしたいときに使います")]
    public Vector3 visualOffset = Vector3.zero;

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
    public float minimumVisibleSpawnDistance = 300f;
    public float spawnHeight = 1.5f;
    public float bulletLifeTime = 12f;
    [Tooltip("一度のフレームで生成できる最大数。設定ミスや例外時の大量生成を防ぎます")]
    [Min(1)]
    public int maxSpawnsPerFrame = 5;
    [Tooltip("プレイヤーが球を追い越して、この距離より後ろに離れたら球を消します")]
    [Min(0f)]
    public float destroyBehindPlayerDistance = 20f;

    [Header("当たり判定")]
    [Tooltip("全モンスターのColliderサイズ倍率")]
    [Min(0.1f)]
    public float colliderScaleMultiplier = 1f;

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
    private CarpetMove carpet;

    private void Start()
    {
        carpet = player != null ? player.GetComponent<CarpetMove>() : null;

        if (mainCircleChallengeBallTimes == null || mainCircleChallengeBallTimes.Count == 0)
        {
            mainCircleChallengeBallTimes = new List<float> { 17f, 38f };
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
        var spawnedThisFrame = 0;

        while (spawnedThisFrame < Mathf.Max(1, maxSpawnsPerFrame) && activeNextBallIndex < activeSchedule.Count && activeElapsedTime >= GetSpawnTime(activeSchedule[activeNextBallIndex], isTutorial))
        {
            var entryIndex = activeNextBallIndex;
            var entry = activeSchedule[entryIndex];
            activeNextBallIndex++;
            spawnedThisFrame++;

            var spawnedBall = SpawnBall(entry, isTutorial);
            if (spawnedBall == null)
            {
                continue;
            }

            var pauseSeconds = 0f;
            if (isTutorial && entryIndex <= tutorialPauseUntilElement)
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
                StartCoroutine(PauseMainCircleChallengeBeforeArrival(spawnedBall));
            }
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

    private IEnumerator PauseMainCircleChallengeBeforeArrival(GameObject spawnedBall)
    {
        float stopBeforeDistance = 8f; // プレイヤーの何m手前で止めるか

        while (spawnedBall != null && player != null)
        {
            float zDistance = spawnedBall.transform.position.z - player.position.z;

            if (zDistance <= stopBeforeDistance)
            {
                break;
            }

            yield return null;
        }

        MagicCarpetGameFlow.PauseCircleChallenge("stekki");
    }

    private GameObject SpawnBall(BallScheduleEntry entry, bool isTutorial)
    {
        var prefab = entry.ballPrefab != null ? entry.ballPrefab : bulletPrefab;
        if (prefab == null)
        {
            return null;
        }

        GameObject ball = null;
        try
        {
            var activeSpawnDistance = GetSpawnDistance(isTutorial);
            var spawnPosition = player.position + player.forward * activeSpawnDistance;
            spawnPosition.x = entry.laneX;
            spawnPosition.y = player.position.y + spawnHeight;

            ball = new GameObject($"{prefab.name}_Obstacle");
            ball.transform.position = spawnPosition;
            ball.transform.rotation = Quaternion.identity;

            var visual = Instantiate(prefab);
            visual.transform.SetParent(ball.transform, false);
            visual.transform.localPosition = entry.visualOffset;
            visual.transform.localRotation = Quaternion.Euler(entry.visualRotation);

            // Scaleを最終値としてそのまま反映
            visual.transform.localScale = Vector3.Scale(
                Vector3.one * entry.scale,
                GetSafeVisualScale(entry.visualScale)
            );

            foreach (var golemMotion in visual.GetComponentsInChildren<GoldGolemMotion>(true))
            {
                golemMotion.CaptureBaseTransform();
            }

            foreach (var idleMotion in visual.GetComponentsInChildren<MonsterIdleMotion>(true))
            {
                idleMotion.CaptureBaseTransform();
            }

            AssignBulletTag(ball);

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

            SetupBallColliders(ball);

            var runtime = ball.GetComponent<BallRuntimeController>();
            if (runtime == null)
            {
                runtime = ball.AddComponent<BallRuntimeController>();
            }

            var soundMarker = ball.GetComponent<ObstacleHitSoundMarker>();
            if (soundMarker == null)
            {
                soundMarker = ball.AddComponent<ObstacleHitSoundMarker>();
            }
            soundMarker.useHitSound2 = entry.useHitSound2;

            runtime.player = player;
            runtime.velocity = ballVelocity;
            runtime.destroyBehindDistance = destroyBehindPlayerDistance;
            runtime.damage = Mathf.Clamp(Mathf.Round(entry.damage * 10f) / 10f, 0.1f, 3f);

            Destroy(ball, Mathf.Max(bulletLifeTime, activeSpawnDistance / entry.speed + 2f));
            return ball;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            if (ball != null)
            {
                Destroy(ball);
            }

            return null;
        }
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

    private void SetupBallColliders(GameObject ball)
    {
        foreach (var collider in ball.GetComponentsInChildren<Collider>(true))
        {
            collider.isTrigger = true;

            if (collider is BoxCollider box)
            {
                box.size *= colliderScaleMultiplier;
            }
            else if (collider is SphereCollider sphere)
            {
                sphere.radius *= colliderScaleMultiplier;
            }
            else if (collider is CapsuleCollider capsule)
            {
                capsule.radius *= colliderScaleMultiplier;
                capsule.height *= colliderScaleMultiplier;
            }
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
        float spawnDistance = GetSpawnDistance(isTutorial);
        float travelTime = spawnDistance / Mathf.Max(0.1f, entry.speed);

        return Mathf.Max(0f, entry.time - travelTime);
    }

    private float GetSpawnDistance(bool isTutorial)
    {
        return isTutorial
            ? spawnDistance
            : Mathf.Max(spawnDistance, minimumVisibleSpawnDistance);
    }
}   // ← BulletSpawnerはここで終了

public class ObstacleHitSoundMarker : MonoBehaviour
{
    public bool useHitSound2;
}








