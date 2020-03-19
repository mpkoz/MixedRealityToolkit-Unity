// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.MixedReality.Toolkit.Utilities;
using System.Collections.Generic;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Input
{
    /// <summary>
    /// Defines the interactions and data that an articulated hand can provide.
    /// </summary>
    public class ArticulatedHandDefinition
    {
        public ArticulatedHandDefinition(IMixedRealityInputSource source, Handedness handedness)
        {
            inputSource = source;
            this.handedness = handedness;
        }

        protected readonly IMixedRealityInputSource inputSource;
        protected readonly Handedness handedness;

        private readonly float cursorBeamBackwardTolerance = 0.5f;
        private readonly float cursorBeamUpTolerance = 0.8f;
        private readonly float fingerPointedTolerance = 0.4f;

        private readonly float isInPointingPoseDelayTime = 0.06f;
        private float currentPointingPoseDelayTime = 0f;

        private Dictionary<TrackedHandJoint, MixedRealityPose> unityJointPoses = new Dictionary<TrackedHandJoint, MixedRealityPose>();
        private MixedRealityPose currentIndexPose = MixedRealityPose.ZeroIdentity;

        /// <summary>
        /// The articulated hands default interactions.
        /// </summary>
        /// <remarks>A single interaction mapping works for both left and right articulated hands.</remarks>
        public MixedRealityInteractionMapping[] DefaultInteractions => new[]
        {
            new MixedRealityInteractionMapping(0, "Spatial Pointer", AxisType.SixDof, DeviceInputType.SpatialPointer),
            new MixedRealityInteractionMapping(1, "Spatial Grip", AxisType.SixDof, DeviceInputType.SpatialGrip),
            new MixedRealityInteractionMapping(2, "Select", AxisType.Digital, DeviceInputType.Select),
            new MixedRealityInteractionMapping(3, "Grab", AxisType.SingleAxis, DeviceInputType.TriggerPress),
            new MixedRealityInteractionMapping(4, "Index Finger Pose", AxisType.SixDof, DeviceInputType.IndexFinger)
        };

        /// <summary>
        /// Calculates whether the current pose allows for pointing/distant interactions.
        /// </summary>
        public bool IsInPointingPose
        {
            get
            {
                bool valid = true;

                MixedRealityPose palmJoint;
                if (unityJointPoses.TryGetValue(TrackedHandJoint.Palm, out palmJoint))
                {
                    Vector3 palmNormal = palmJoint.Rotation * (-1 * Vector3.up);

                    // Check if palm is facing in the same general direction as the head
                    // A palm facing the head does not indicate that the user wishes to point
                    if (cursorBeamBackwardTolerance >= 0)
                    {
                        Vector3 cameraBackward = -CameraCache.Main.transform.forward;
                        if (Vector3.Dot(palmNormal.normalized, cameraBackward.normalized) > cursorBeamBackwardTolerance)
                        {
                            valid = false;
                        }
                    }

                    // Check if palm is facing up or down
                    // An upwards-facing palm dowes not indicate the user wishes to point
                    if (valid && cursorBeamUpTolerance >= 0)
                    {
                        if (Vector3.Dot(palmNormal.normalized, Vector3.up) > cursorBeamUpTolerance)
                        {
                            valid = false;
                        }
                    }

                    MixedRealityPose indexMiddleJoint;
                    if (valid && unityJointPoses.TryGetValue(TrackedHandJoint.IndexMiddleJoint, out indexMiddleJoint))
                    {
                        // Check if index finger forward is in the same general direction as the palm forward 
                        // A fist/curled pointer finger does not indicate that the user wishes to point

                        // Vector pointing forward from palm, towards the fingers
                        Vector3 palmForward = (palmJoint.Rotation * Vector3.forward).normalized;
                        Vector3 indexMiddleForward = (indexMiddleJoint.Rotation * Vector3.forward).normalized;
                        if (Vector3.Dot(indexMiddleForward, palmForward) < fingerPointedTolerance)
                        {
                            valid = false;
                        }
                    }

                    // A short time delay preventing false negatives. When the user makes a grabbing gesture, 
                    // sometimes the ray turns off via the previous check before the grab is completed
                    if (valid)
                    {
                        currentPointingPoseDelayTime = Time.time + isInPointingPoseDelayTime;
                    }

                    valid = Time.time < currentPointingPoseDelayTime;
                }

                return valid;
            }
        }

        /// <summary>
        /// Updates the current hand joints with new data.
        /// </summary>
        /// <param name="jointPoses">The new joint poses.</param>
        public void UpdateHandJoints(Dictionary<TrackedHandJoint, MixedRealityPose> jointPoses)
        {
            unityJointPoses = jointPoses;
            CoreServices.InputSystem?.RaiseHandJointsUpdated(inputSource, handedness, unityJointPoses);
        }

        /// <summary>
        /// Updates the MixedRealityInteractionMapping with the latest index pose and fires a corresponding pose event.
        /// </summary>
        /// <param name="interactionMapping">The index finger's interaction mapping.</param>
        public void UpdateCurrentIndexPose(MixedRealityInteractionMapping interactionMapping)
        {
            if (unityJointPoses.TryGetValue(TrackedHandJoint.IndexTip, out currentIndexPose))
            {
                // Update the interaction data source
                interactionMapping.PoseData = currentIndexPose;

                // If our value changed raise it
                if (interactionMapping.Changed)
                {
                    // Raise input system event if it's enabled
                    CoreServices.InputSystem?.RaisePoseInputChanged(inputSource, handedness, interactionMapping.MixedRealityInputAction, currentIndexPose);
                }
            }
        }
    }
}
