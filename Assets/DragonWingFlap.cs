using System.Collections.Generic;
using UnityEngine;

public class DragonWingFlap : MonoBehaviour
{
    public float flapSpeed = 9f;
    public float motionTimeScale = 1f;
    public float wingAngle = 24f;
    public float bodyPitchAngle = 3f;
    public float neckAngle = 6f;
    public float neckYawAngle = 10f;
    public float headAngle = 7f;
    public float headLiftAngle = -8f;
    public float mouthRestOpenWeight = 12f;
    public float mouthOpenWeight = 92f;
    public float armAngle = 9f;
    public float handAngle = 8f;
    public float legAngle = 10f;
    public float footAngle = 8f;
    public float tailAngle = 7f;
    public float whiskerAngle = 5f;
    public bool animateBodySegments;
    public float bodySegmentAngle = 10f;
    public float bodySegmentLiftAngle = 5f;

    private readonly List<AnimatedPart> wings = new List<AnimatedPart>();
    private readonly List<AnimatedPart> bodySegments = new List<AnimatedPart>();
    private readonly List<AnimatedPart> necks = new List<AnimatedPart>();
    private readonly List<AnimatedPart> heads = new List<AnimatedPart>();
    private readonly List<AnimatedPart> jaws = new List<AnimatedPart>();
    private readonly List<AnimatedPart> arms = new List<AnimatedPart>();
    private readonly List<AnimatedPart> hands = new List<AnimatedPart>();
    private readonly List<AnimatedPart> legs = new List<AnimatedPart>();
    private readonly List<AnimatedPart> feet = new List<AnimatedPart>();
    private readonly List<AnimatedPart> tails = new List<AnimatedPart>();
    private readonly List<AnimatedPart> whiskers = new List<AnimatedPart>();
    private readonly List<MouthBlendShape> mouthBlendShapes = new List<MouthBlendShape>();
    private Quaternion baseRotation;
    private float phase;
    private bool driveRootMotion;

    private void Awake()
    {
        baseRotation = transform.localRotation;
        phase = Random.Range(0f, 10f);

        foreach (var child in GetComponentsInChildren<Transform>(true))
        {
            if (child == transform)
            {
                continue;
            }

            var lowerName = child.name.ToLowerInvariant();
            if (lowerName.Contains("wing") || lowerName.Contains("hane") || child.name.Contains("羽"))
            {
                wings.Add(new AnimatedPart(child, GetSide(child.name)));
            }
            else if (lowerName.Contains("neck") || child.name.Contains("首"))
            {
                necks.Add(new AnimatedPart(child, 1f));
            }
            else if (lowerName == "head" || lowerName.Contains("_head") || lowerName.Contains("r_head") || child.name.Contains("頭"))
            {
                heads.Add(new AnimatedPart(child, 1f));
            }
            else if (lowerName.Contains("jaw") || lowerName.Contains("mouth") || lowerName.Contains("kuti") || child.name.Contains("口"))
            {
                jaws.Add(new AnimatedPart(child, 1f));
            }
            else if (lowerName.Contains("upper_arm") || lowerName.Contains("lower_arm") || lowerName.Contains("forearm") || lowerName.Contains("arm") || child.name.Contains("腕"))
            {
                arms.Add(new AnimatedPart(child, GetSide(child.name)));
            }
            else if (lowerName.Contains("hand") || lowerName.Contains("claw") || lowerName.Contains("paw") || child.name.Contains("手"))
            {
                hands.Add(new AnimatedPart(child, GetSide(child.name)));
            }
            else if (lowerName.Contains("upper_leg") || lowerName.Contains("lower_leg") || lowerName.Contains("leg") || child.name.Contains("足"))
            {
                legs.Add(new AnimatedPart(child, GetSide(child.name)));
            }
            else if (lowerName.Contains("foot") || lowerName.Contains("toe") || child.name.Contains("爪"))
            {
                feet.Add(new AnimatedPart(child, GetSide(child.name)));
            }
            else if (lowerName.Contains("tail") || lowerName.StartsWith("r_rear") || child.name.Contains("尾") || child.name.Contains("しっぽ"))
            {
                tails.Add(new AnimatedPart(child, 1f));
            }
            else if (lowerName.Contains("hige") || lowerName.Contains("whisker") || child.name.Contains("ひげ"))
            {
                whiskers.Add(new AnimatedPart(child, GetSide(child.name)));
            }

            if (animateBodySegments && LooksLikeBodySegment(lowerName))
            {
                AddPartIfMissing(bodySegments, child, 1f);
            }
        }

        AddHumanoidBones();

        foreach (var skinnedMesh in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var mesh = skinnedMesh.sharedMesh;
            if (mesh == null)
            {
                continue;
            }

            for (var i = 0; i < mesh.blendShapeCount; i++)
            {
                var blendShapeName = mesh.GetBlendShapeName(i).ToLowerInvariant();
                if (blendShapeName.Contains("jaw") || blendShapeName.Contains("mouth") || blendShapeName.Contains("kuti"))
                {
                    mouthBlendShapes.Add(new MouthBlendShape(skinnedMesh, i));
                }
            }
        }
    }

    private void AddHumanoidBones()
    {
        var animator = GetComponentInChildren<Animator>();
        if (animator == null || !animator.isHuman)
        {
            return;
        }

        AddPartIfMissing(necks, animator.GetBoneTransform(HumanBodyBones.Neck), 1f);
        AddPartIfMissing(heads, animator.GetBoneTransform(HumanBodyBones.Head), 1f);
        AddPartIfMissing(arms, animator.GetBoneTransform(HumanBodyBones.LeftUpperArm), -1f);
        AddPartIfMissing(arms, animator.GetBoneTransform(HumanBodyBones.LeftLowerArm), -1f);
        AddPartIfMissing(hands, animator.GetBoneTransform(HumanBodyBones.LeftHand), -1f);
        AddPartIfMissing(arms, animator.GetBoneTransform(HumanBodyBones.RightUpperArm), 1f);
        AddPartIfMissing(arms, animator.GetBoneTransform(HumanBodyBones.RightLowerArm), 1f);
        AddPartIfMissing(hands, animator.GetBoneTransform(HumanBodyBones.RightHand), 1f);
        AddPartIfMissing(legs, animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg), -1f);
        AddPartIfMissing(legs, animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg), -1f);
        AddPartIfMissing(feet, animator.GetBoneTransform(HumanBodyBones.LeftFoot), -1f);
        AddPartIfMissing(feet, animator.GetBoneTransform(HumanBodyBones.LeftToes), -1f);
        AddPartIfMissing(legs, animator.GetBoneTransform(HumanBodyBones.RightUpperLeg), 1f);
        AddPartIfMissing(legs, animator.GetBoneTransform(HumanBodyBones.RightLowerLeg), 1f);
        AddPartIfMissing(feet, animator.GetBoneTransform(HumanBodyBones.RightFoot), 1f);
        AddPartIfMissing(feet, animator.GetBoneTransform(HumanBodyBones.RightToes), 1f);
    }

    private void AddPartIfMissing(List<AnimatedPart> parts, Transform part, float side)
    {
        if (part == null)
        {
            return;
        }

        for (var i = 0; i < parts.Count; i++)
        {
            if (parts[i].Transform == part)
            {
                return;
            }
        }

        parts.Add(new AnimatedPart(part, side));
    }

    private void Start()
    {
        driveRootMotion = GetComponent<MonsterIdleMotion>() == null;
    }

    private void LateUpdate()
    {
        var scaledTime = Time.time * Mathf.Max(0.05f, motionTimeScale);
        var time = scaledTime + phase;
        var flap = Mathf.Sin(time * flapSpeed);
        var glide = Mathf.Sin(time * 1.8f);
        var quick = Mathf.Sin(time * 5.2f);
        var mouth = Mathf.Clamp01((Mathf.Sin(time * 6.5f) + Mathf.Sin(time * 2.3f) * 0.35f) * 0.5f + 0.5f);
        var mouthWeight = Mathf.Lerp(mouthRestOpenWeight, mouthOpenWeight, mouth);

        if (driveRootMotion)
        {
            transform.localRotation = baseRotation * Quaternion.Euler(flap * bodyPitchAngle, glide * 2f, glide * 1.2f);
        }

        Animate(wings, new Vector3(flap * 5f, 0f, flap * wingAngle), true);
        AnimateBodySegments(time);
        Animate(necks, new Vector3(glide * neckAngle, Mathf.Sin(time * 1.3f) * neckYawAngle, Mathf.Sin(time * 0.9f) * neckAngle * 0.25f), false);
        Animate(heads, new Vector3(headLiftAngle + Mathf.Sin(time * 2.2f) * headAngle, Mathf.Sin(time * 1.5f) * headAngle, quick * headAngle * 0.2f), false);
        Animate(jaws, new Vector3(mouthWeight * 0.45f, 0f, 0f), false);
        Animate(arms, new Vector3(Mathf.Sin(time * 3.1f + 1.1f) * armAngle, Mathf.Sin(time * 2.7f) * armAngle * 0.55f, Mathf.Sin(time * 2.6f) * armAngle * 0.45f), true);
        Animate(hands, new Vector3(Mathf.Sin(time * 3.1f + 1.7f) * handAngle, Mathf.Sin(time * 4.1f) * handAngle * 0.4f, Mathf.Sin(time * 4.1f) * handAngle * 0.45f), true);
        Animate(legs, new Vector3(Mathf.Sin(time * 3.4f) * legAngle, Mathf.Sin(time * 2.8f + 0.5f) * legAngle * 0.45f, Mathf.Sin(time * 3.0f) * legAngle * 0.35f), true);
        Animate(feet, new Vector3(Mathf.Sin(time * 3.4f + 0.8f) * footAngle, Mathf.Sin(time * 4.2f) * footAngle * 0.35f, Mathf.Sin(time * 3.8f) * footAngle * 0.3f), true);
        Animate(tails, new Vector3(0f, Mathf.Sin(time * 2.4f) * tailAngle, Mathf.Sin(time * 2.1f) * tailAngle * 0.6f), false, 0.18f);
        Animate(whiskers, new Vector3(quick * whiskerAngle, 0f, quick * whiskerAngle * 0.6f), true);

        foreach (var blendShape in mouthBlendShapes)
        {
            blendShape.Renderer.SetBlendShapeWeight(blendShape.Index, mouthWeight);
        }
    }

    private void Animate(List<AnimatedPart> parts, Vector3 euler, bool mirrorBySide, float stagger = 0f)
    {
        for (var i = 0; i < parts.Count; i++)
        {
            var part = parts[i];
            var side = mirrorBySide ? part.Side : 1f;
            var wave = stagger == 0f ? 1f : Mathf.Sin((Time.time * Mathf.Max(0.05f, motionTimeScale) + phase + i * stagger) * 2.2f);
            part.Transform.localRotation = part.BaseRotation * Quaternion.Euler(euler.x * side * wave, euler.y * wave, euler.z * side * wave);
        }
    }

    private void AnimateBodySegments(float time)
    {
        if (!animateBodySegments)
        {
            return;
        }

        for (var i = 0; i < bodySegments.Count; i++)
        {
            var part = bodySegments[i];
            var wave = Mathf.Sin(time * 1.55f + i * 0.42f);
            var lift = Mathf.Sin(time * 1.1f + i * 0.35f);
            part.Transform.localRotation = part.BaseRotation * Quaternion.Euler(
                lift * bodySegmentLiftAngle,
                wave * bodySegmentAngle,
                Mathf.Cos(time * 1.35f + i * 0.48f) * bodySegmentAngle * 0.35f);
        }
    }

    private bool LooksLikeBodySegment(string lowerName)
    {
        return lowerName.Contains("body")
            || lowerName.Contains("spine")
            || lowerName.Contains("bone")
            || lowerName.Contains("joint")
            || lowerName.Contains("neck")
            || lowerName.Contains("tail")
            || lowerName.Contains("root");
    }

    private float GetSide(string partName)
    {
        var lowerName = partName.ToLowerInvariant();
        if (lowerName.Contains("left") || lowerName.Contains("_l") || lowerName.Contains(".l") || lowerName.EndsWith("l") || lowerName.Contains("左"))
        {
            return -1f;
        }

        return 1f;
    }

    private struct AnimatedPart
    {
        public readonly Transform Transform;
        public readonly Quaternion BaseRotation;
        public readonly float Side;

        public AnimatedPart(Transform transform, float side)
        {
            Transform = transform;
            BaseRotation = transform.localRotation;
            Side = side;
        }
    }

    private struct MouthBlendShape
    {
        public readonly SkinnedMeshRenderer Renderer;
        public readonly int Index;

        public MouthBlendShape(SkinnedMeshRenderer renderer, int index)
        {
            Renderer = renderer;
            Index = index;
        }
    }
}
