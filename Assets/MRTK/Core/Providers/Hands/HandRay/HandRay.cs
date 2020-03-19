// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Utilities;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Input
{
    public class HandRay : IHandRay
    {
        /// <inheritdoc />
        public Ray Ray
        {
            get
            {
                ray.origin = stabilizedRay.StabilizedPosition;
                ray.direction = stabilizedRay.StabilizedDirection;
                return ray;
            }
        }

        private readonly float IsInPointingPoseDelayTime = 0.2f;
        private float currentPointingPoseDelayTime = 0f;

        /// <inheritdoc />
        public bool ShouldShowRay
        {
            get
            {
                bool valid = true;

                if (headForward.sqrMagnitude < Mathf.Epsilon)
                {
                    valid = false;
                }

                // Check if palm is facing in the same general direction as the head
                // A palm facing the head does not indicate that the user wishes to point
                if (valid && CursorBeamBackwardTolerance >= 0)
                {
                    Vector3 cameraBackward = -headForward;
                    if (Vector3.Dot(palmNormal.normalized, cameraBackward) > CursorBeamBackwardTolerance)
                    {
                        valid = false;
                    }
                }

                // Check if palm is facing up or down
                // An upwards-facing palm does not indicate the user wishes to point
                if (valid && CursorBeamUpTolerance >= 0)
                {
                    if (Vector3.Dot(palmNormal, Vector3.up) > CursorBeamUpTolerance)
                    {
                        valid = false;
                    }
                }

                // Check if index finger forward is in the same general direction as the palm forward 
                // A fist/curled pointer finger does not indicate that the user wishes to point
                if (valid)
                {
                    Vector3 palmForward = (Quaternion.AngleAxis(-90, Vector3.right) * palmNormal).normalized;
                    if (Vector3.Dot(pointerFingerNormal.normalized, palmForward) < FingerPointedTolerance)
                    {

                        valid = false;
                    }
                }

                // A short time delay preventing false negatives. When the user makes a grabbing gesture, 
                // sometimes the ray turns off via the previous check before the grab is completed
                if (valid)
                {
                    currentPointingPoseDelayTime = Time.time + IsInPointingPoseDelayTime;
                }

                valid = Time.time < currentPointingPoseDelayTime;

                return valid;
            }
        }

        private Ray ray = new Ray();

        // Constants from Shell Implementation of hand ray.
        private const float DynamicPivotBaseY = -0.1f, DynamicPivotMultiplierY = 0.65f, DynamicPivotMinY = -0.6f, DynamicPivotMaxY = -0.2f;
        private const float DynamicPivotBaseX = 0.03f, DynamicPivotMultiplierX = 0.65f, DynamicPivotMinX = 0.08f, DynamicPivotMaxX = 0.15f;
        private const float HeadToPivotOffsetZ = 0.08f;
        private readonly float CursorBeamBackwardTolerance = 0.5f;
        private readonly float CursorBeamUpTolerance = 0.8f;
        private readonly float FingerPointedTolerance = 0.9f;

        // Smoothing factor for ray stabilization.
        private const float StabilizedRayHalfLife = 0.01f;

        private readonly StabilizedRay stabilizedRay = new StabilizedRay(StabilizedRayHalfLife);
        private Vector3 palmNormal;
        private Vector3 headForward;
        private Vector3 pointerFingerNormal;

        #region Public Methods

        /// <inheritdoc />
        public void Update(Vector3 handPosition, Vector3 palmNormal, Vector3 pointerFingerNormal, Transform headTransform, Handedness sourceHandedness)
        {
            Vector3 rayPivotPoint = ComputeRayPivotPosition(handPosition, headTransform, sourceHandedness);
            Vector3 measuredRayPosition = handPosition;
            Vector3 measuredDirection = measuredRayPosition - rayPivotPoint;
            this.palmNormal = palmNormal;
            this.headForward = headTransform.forward;
            this.pointerFingerNormal = pointerFingerNormal;


            stabilizedRay.AddSample(new Ray(measuredRayPosition, measuredDirection));
        }

        #endregion

        private Vector3 ComputeRayPivotPosition(Vector3 handPosition, Transform headTransform, Handedness sourceHandedness)
        {
            Vector3 handPositionHeadSpace = headTransform.InverseTransformPoint(handPosition);
            float relativePivotY = DynamicPivotBaseY + Mathf.Min(DynamicPivotMultiplierY * handPositionHeadSpace.y, 0);
            relativePivotY = Mathf.Clamp(relativePivotY, DynamicPivotMinY, DynamicPivotMaxY);

            float xBase = DynamicPivotBaseX;
            float xMultiplier = DynamicPivotMultiplierX;
            float xMin = DynamicPivotMinX;
            float xMax = DynamicPivotMaxX;
            if (sourceHandedness == Handedness.Left)
            {
                xBase = -xBase;
                float tmp = xMin;
                xMin = -xMax;
                xMax = tmp;
            }
            float relativePivotX = xBase + xMultiplier * handPositionHeadSpace.x;
            relativePivotX = Mathf.Clamp(relativePivotX, xMin, xMax);

            Vector3 relativePivot = new Vector3(
                    relativePivotX,
                    relativePivotY,
                    HeadToPivotOffsetZ
                );

            Quaternion headRotationFlat = Quaternion.Euler(0, headTransform.rotation.eulerAngles.y, 0);
            return headTransform.position + headRotationFlat * relativePivot;
        }
    }
}