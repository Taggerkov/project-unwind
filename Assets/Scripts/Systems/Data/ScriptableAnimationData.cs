using System;
using UnityEngine;

namespace Systems.Data
{
    [Serializable]
    public struct BonePoseData
    {
        public string bonePath;
    
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
    }

    [Serializable]
    public struct FramePose
    {
        public int frameNumber;
        public BonePoseData[] bones;

        public Bounds[] hitBoxes;
    }
    
    public enum CameraAnchor
    {
        Attacker,       // Camera moves relative to the one doing the super
        Victim,         // Camera moves relative to the one getting hit
        StageCenter,    // Camera ignores players and frames the stage 
        Midpoint        // Camera frames the space exactly between the players
    }

    public enum CameraInterpolation
    {
        Smooth,         // Interpolates smoothly from the previous frame
        Linear,         // Moves at a constant speed from the previous frame
        Step            // Snaps instantly to this keyframe (zero interpolation)
    }

    [Serializable]
    public struct CinematicCameraSequence
    {
        [Tooltip("A unique name for this camera sequence, e.g., 'Combatant_GrabCCS'.")]
        public string sequenceName;
        [Tooltip("Total duration of this sequence in 60Hz frames.")]
        public int totalFrames;
    
        [Tooltip("What is the absolute center of this camera sequence?")]
        public CameraAnchor sequenceAnchor;

        [Tooltip("The chronological list of camera keyframes.")]
        public CameraKeyframe[] keyframes;
    }

    [Serializable]
    public struct CameraKeyframe
    {
        [Header("Timing")]
        [Tooltip("The exact frame (e.g., Frame 45 of 254) this keyframe is reached.")]
        public int frameIndex;
    
        [Tooltip("How does the camera move from the last keyframe to this one?")]
        public CameraInterpolation interpolationMode;
    
        [Header("Transform (Relative to Anchor)")]
        public Vector3 localPosition;
        public Vector3 lookAtOffset;
    
        [Header("Lens & Style")]
        public float fieldOfView;
    
        [Tooltip("Z-axis rotation. Crucial for anime 'Dutch angles'.")]
        public float rollAngle; 
    }
}
