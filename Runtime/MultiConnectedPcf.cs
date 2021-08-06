using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if PLATFORM_LUMIN
using UnityEngine.XR.MagicLeap;
#endif

public class MultiConnectedPcf : MonoBehaviour
{
#if PLATFORM_LUMIN
    public class PCFReference : MLPersistentCoordinateFrames.PCF.IBinding
    {

        public Transform TargetTransform;
        /// <summary>
        /// The offset from the PCF to the transform.
        /// This is in the Coordinate Space of the PCF.
        /// </summary>
        [SerializeField, HideInInspector]
        private Vector3 OffsetPosition = new Vector3();

        /// <summary>
        /// The orientation offset from the PCF to the transform.
        /// </summary>
        [SerializeField, HideInInspector]
        private Quaternion OffsetOrientation = new Quaternion();

        public PCFReference(Transform target, string id)
        {
            this.Id = id;
            TargetTransform = target;
        }


        public string Id { get; }
        public MLPersistentCoordinateFrames.PCF PCF { get; }
        /// <summary>
        /// Updates the binding information based on the PCF and transform locations.
        /// Checks if the PCF is in a good state before carrying out this operation.
        /// </summary>
        /// <returns>
        /// MLResult.Result will be <c>MLResult.Code.Ok</c> when operation is successful.
        /// MLResult.Result will be <c>MLResult.Code.InvalidParam</c> when the associated transform is not set or when the associated PCF is not set or when the CurrentResult value of the PCF is not Ok.
        /// </returns>
        public bool Update()
        {
            MLResult result;
            bool success = true;

            if (this.PCF == null || this.PCF.CurrentResultCode != MLResult.Code.Ok)
            {
                result = MLResult.Create(MLResult.Code.InvalidParam, "PCF is not in a good state.");
                Debug.LogErrorFormat("Error: TransformBinding failed to update binding information. Reason: {0}", result);
                return false;
            }

            else if (TargetTransform == null)
            {
                result = MLResult.Create(MLResult.Code.InvalidParam, "Transform in binding is null.");
                Debug.LogErrorFormat("Error: TransformBinding failed to update binding information. Reason: {0}", result);
                return false;
            }

            /*
             * Let A = Absolute Transform of PCF,
             *     B = Absolute Transform of Content
             *     C = Relative Transform of Content to PCF, Binding Offset
             *
             *          A * C = B          : Multiply both by A^(-1)
             * A^(-1) * A * C = A^(-1) * B
             *              C = A^(-1) * B
             */

            // Relative Orientation can be computed independent of Position
            // A = PCF.Orientation
            // B = transform.rotation
            // C = OrientationOffset
            Quaternion relOrientation = Quaternion.Inverse(this.PCF.Rotation) * TargetTransform.rotation;
            OffsetOrientation = relOrientation;

            // Relative Position is dependent on Relative Orientation
            // A = pcfCoordinateSpace (Transform of PCF)
            // B = transform.position
            // C = Offset (Position Offset)
            Matrix4x4 pcfCoordinateSpace = Matrix4x4.TRS(this.PCF.Position, this.PCF.Rotation, Vector3.one);
            Vector3 relPosition = Matrix4x4.Inverse(pcfCoordinateSpace).MultiplyPoint3x4(TargetTransform.position);
            OffsetPosition = relPosition;


            return success;
        }

        public bool Regain()
        {
            if (this.PCF == null || this.TargetTransform == null)
            {
                MLResult result = MLResult.Create(MLResult.Code.UnspecifiedFailure, "PCF or Transform is null and must be set.");
                Debug.LogErrorFormat("Error: TransformBinding failed to regain the binding between PCF and transform. Reason: {0}", result);
                return false;
            }
            else
            {
                MLResult result = this.PCF.Update();
                if (result.IsOk)
                {
                    TargetTransform.rotation = this.PCF.Rotation * this.OffsetOrientation;

                    Matrix4x4 pcfCoordinateSpace = new Matrix4x4();
                    pcfCoordinateSpace.SetTRS(this.PCF.Position, this.PCF.Rotation, Vector3.one);

                    TargetTransform.position = pcfCoordinateSpace.MultiplyPoint3x4(this.OffsetPosition);
                }
                else
                {
                    Debug.LogErrorFormat("Error: TransformBinding failed to regain the binding between PCF and transform. Reason: {0}", result);
                    return false;
                }
            }

            return true;
        }

        public bool Lost()
        {
#if PLATFORM_LUMIN
            // Queue this pcf for state updates again since the pcf cache is cleared when maps are lost.
            MLPersistentCoordinateFrames.OnLocalized += HandleOnLocalized;
#endif
            return true;
        }

        /// <summary>
        /// Handles what to do when localizaiton is gained or lost.
        /// Queues the associated PCF for updates again when we are localized.
        /// </summary>
        private void HandleOnLocalized(bool localized)
        {
#if PLATFORM_LUMIN
            if (localized)
            {
                MLPersistentCoordinateFrames.QueueForUpdates(PCF);
                MLPersistentCoordinateFrames.OnLocalized -= HandleOnLocalized;
            }
#endif
        }
    }
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
#endif
}
