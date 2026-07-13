using UnityEngine;

[CreateAssetMenu(fileName = "BrightPointTrackingSettings", menuName = "Magic Carpet/Bright Point Tracking Settings")]
public class BrightPointTrackingSettings : ScriptableObject
{
    [Header("Brightness Threshold")]
    public bool useAutomaticThreshold = true;
    [Range(0, 255)] public int valueThreshold = 245;
    [Range(0, 255)] public int minimumAutomaticValue = 220;
    [Range(0, 80)] public int automaticValueMargin = 12;

    [Header("Bright Point Shape")]
    [Range(1, 8)] public int sampleStep = 4;
    [Min(1)] public int minimumRegionPixels = 3;
    [Range(0.001f, 0.2f)] public float maximumRegionFraction = 0.02f;
    [Range(0.05f, 1f)] public float maximumTrackingJump = 0.35f;
    [Range(1, 10)] public int framesBetweenSamples = 3;
}
