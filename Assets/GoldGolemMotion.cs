using System.Collections.Generic;
using UnityEngine;

public class GoldGolemMotion : MonoBehaviour
{
    public float motionSpeed = 1.25f;
    public float armSwing = 18f;
    public float shoulderLift = 10f;
    public float bodyLean = 4f;
    public float headNod = 8f;
    public float stepAngle = 9f;

    private readonly List<BonePose> animatedBones = new List<BonePose>();

    private Quaternion baseLocalRotation;
    private Vector3 baseLocalPosition;
    private float phase;
    private bool initialized;

    private struct BonePose
    {
        public Transform transform;
        public Quaternion baseRotation;
        public int side;
        public BoneKind kind;
    }

    private enum BoneKind
    {
        Head,
        Chest,
        Shoulder,
        UpperArm,
        LowerArm,
        Hand,
        UpperLeg,
        LowerLeg,
        Foot
    }

    private void Awake()
    {
        phase = Random.Range(0f, 10f);
        CollectBones();
    }

    private void Start()
    {
        // SceneÇ÷íºê⁄íuÇ¢ÇΩèÍçáÇÃï€åØ
        if (!initialized)
        {
            CaptureBaseTransform();
        }
    }

    public void CaptureBaseTransform()
    {
        // BulletSpawnerÇ™ê›íËÇµÇΩà íuÅEâÒì]ÇäÓèÄÇ∆ÇµÇƒï€ë∂
        baseLocalPosition = transform.localPosition;
        baseLocalRotation = transform.localRotation;
        initialized = true;
    }

    private void LateUpdate()
    {
        if (!initialized)
        {
            return;
        }

        var time = (Time.time + phase) * motionSpeed;
        var main = Mathf.Sin(time);
        var heavy = Mathf.Sin(time * 0.55f);
        var alternate = Mathf.Sin(time + Mathf.PI * 0.5f);

        transform.localPosition =
            baseLocalPosition +
            Vector3.up * (Mathf.Abs(main) * 0.035f);

        transform.localRotation =
            baseLocalRotation *
            Quaternion.Euler(
                heavy * bodyLean,
                heavy * bodyLean * 0.8f,
                main * bodyLean
            );

        foreach (var bone in animatedBones)
        {
            if (bone.transform == null)
            {
                continue;
            }

            var sideWave = bone.side < 0 ? main : -main;
            var sideAlt = bone.side < 0 ? alternate : -alternate;

            bone.transform.localRotation =
                bone.baseRotation *
                GetPoseRotation(
                    bone.kind,
                    bone.side,
                    sideWave,
                    sideAlt,
                    heavy
                );
        }
    }

    private Quaternion GetPoseRotation(
        BoneKind kind,
        int side,
        float sideWave,
        float sideAlt,
        float heavy)
    {
        switch (kind)
        {
            case BoneKind.Head:
                return Quaternion.Euler(
                    heavy * headNod,
                    sideWave * headNod * 0.6f,
                    0f
                );

            case BoneKind.Chest:
                return Quaternion.Euler(
                    heavy * bodyLean * 0.6f,
                    sideWave * bodyLean,
                    0f
                );

            case BoneKind.Shoulder:
                return Quaternion.Euler(
                    sideAlt * shoulderLift,
                    0f,
                    side * (shoulderLift + Mathf.Abs(sideWave) * shoulderLift)
                );

            case BoneKind.UpperArm:
                return Quaternion.Euler(
                    12f + sideWave * armSwing,
                    side * 6f,
                    side * (-18f + sideAlt * 8f)
                );

            case BoneKind.LowerArm:
                return Quaternion.Euler(
                    -8f + Mathf.Abs(sideWave) * 18f,
                    side * 5f,
                    side * -7f
                );

            case BoneKind.Hand:
                return Quaternion.Euler(
                    sideAlt * 10f,
                    0f,
                    side * (8f + sideWave * 6f)
                );

            case BoneKind.UpperLeg:
                return Quaternion.Euler(
                    sideWave * stepAngle,
                    0f,
                    side * 2f
                );

            case BoneKind.LowerLeg:
                return Quaternion.Euler(
                    Mathf.Abs(sideWave) * stepAngle * 0.8f,
                    0f,
                    0f
                );

            case BoneKind.Foot:
                return Quaternion.Euler(
                    -Mathf.Abs(sideWave) * stepAngle * 0.45f,
                    0f,
                    side * 2f
                );

            default:
                return Quaternion.identity;
        }
    }

    private void CollectBones()
    {
        animatedBones.Clear();

        foreach (var child in GetComponentsInChildren<Transform>(true))
        {
            if (child == transform)
            {
                continue;
            }

            var name = child.name.ToLowerInvariant();
            var side =
                name.StartsWith("l_") || name.Contains("left")
                    ? -1
                    : 1;

            if (TryGetBoneKind(name, out var kind))
            {
                animatedBones.Add(new BonePose
                {
                    transform = child,
                    baseRotation = child.localRotation,
                    side = side,
                    kind = kind
                });
            }
        }
    }

    private bool TryGetBoneKind(string name, out BoneKind kind)
    {
        if (name.Contains("head"))
        {
            kind = BoneKind.Head;
            return true;
        }

        if (name.Contains("chest") || name.Contains("spine"))
        {
            kind = BoneKind.Chest;
            return true;
        }

        if (name.Contains("clavicle") || name.Contains("shoulder"))
        {
            kind = BoneKind.Shoulder;
            return true;
        }

        if (name.Contains("upperarm"))
        {
            kind = BoneKind.UpperArm;
            return true;
        }

        if (name.Contains("lowerarm") || name.Contains("forearm"))
        {
            kind = BoneKind.LowerArm;
            return true;
        }

        if (name.Contains("hand") || name.Contains("wrist"))
        {
            kind = BoneKind.Hand;
            return true;
        }

        if (name.Contains("upperleg") || name.Contains("thigh"))
        {
            kind = BoneKind.UpperLeg;
            return true;
        }

        if (name.Contains("lowerleg") ||
            name.Contains("calf") ||
            name.Contains("shin"))
        {
            kind = BoneKind.LowerLeg;
            return true;
        }

        if (name.Contains("foot") || name.Contains("toebase"))
        {
            kind = BoneKind.Foot;
            return true;
        }

        kind = BoneKind.Head;
        return false;
    }
}