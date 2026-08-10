using UnityEngine;
using UnityEngine.Serialization;

namespace QuietStatic.Toolkit.Characters.NPC
{
    /// <summary>
    /// Rotates either the NPC's full body or only its head toward an assigned target.
    /// Humanoid head-only looking can use Animator IK to avoid fighting animated head bones.
    /// </summary>
    [RequireComponent(typeof(NPCController))]
    public class NPCLookAtBehaviour : NPCBehaviour
    {
        [Header("References")]
        [Tooltip("Optional NavMesh motor whose automatic rotation is disabled during full-body looking.")]
        [SerializeField] private NPCNavMeshMotor motor;

        [Tooltip("Animator used for humanoid look-at IK and automatic head-bone lookup.")]
        [SerializeField] private Animator animator;

        [Tooltip("Target this NPC normally looks toward.")]
        [FormerlySerializedAs("explicitTarget")]
        [SerializeField] private Transform target;

        [Header("Look Mode")]
        [Tooltip("When enabled, only the head looks toward the target. Otherwise, the NPC root rotates.")]
        [SerializeField] private bool headOnly;

        [Tooltip("Uses Animator.SetLookAtPosition for humanoid rigs. Enable IK Pass on the Animator layer.")]
        [SerializeField] private bool useHumanoidAnimatorIK = true;

        [Tooltip("Head transform used by the generic-rig fallback. Humanoid rigs can locate this automatically.")]
        [SerializeField] private Transform headTransform;

        [Header("Full Body Rotation")]
        [Tooltip("How quickly the NPC body turns toward the target.")]
        [SerializeField, Min(0f)] private float bodyTurnSpeed = 12f;

        [Tooltip("Prevents full-body rotation from tilting upward or downward.")]
        [SerializeField] private bool keepBodyUpright = true;

        [Header("Head Rotation")]
        [Tooltip("How quickly the head look direction catches up to the target.")]
        [SerializeField, Min(0f)] private float headTurnSpeed = 12f;

        [Tooltip("How quickly the head returns to its animated resting pose after looking stops.")]
        [SerializeField, Min(0f)] private float headReturnSpeed = 8f;

        [Tooltip("Maximum degrees the head may turn left or right.")]
        [SerializeField, Range(0f, 180f)] private float maxHeadYaw = 75f;

        [Tooltip("Maximum degrees the head may look upward.")]
        [SerializeField, Range(0f, 90f)] private float maxHeadPitchUp = 35f;

        [Tooltip("Maximum degrees the head may look downward.")]
        [SerializeField, Range(0f, 90f)] private float maxHeadPitchDown = 45f;

        [Header("Humanoid IK Weights")]
        [Tooltip("Overall Animator IK look-at influence.")]
        [SerializeField, Range(0f, 1f)] private float overallWeight = 1f;
        [Tooltip("How much the character's body rotates toward the target.")]
        [SerializeField, Range(0f, 1f)] private float bodyWeight = 0f;
        [Tooltip("How much the character's head rotates toward the target.")]
        [SerializeField, Range(0f, 1f)] private float headWeight = 1f;
        [Tooltip("How much the character's eyes rotate toward the target.")]
        [SerializeField, Range(0f, 1f)] private float eyesWeight = 0.25f;
        [Tooltip("Restricts the maximum look angle to reduce unnatural twisting.")]
        [SerializeField, Range(0f, 1f)] private float clampWeight = 0.5f;

        private float smoothedYaw;
        private float smoothedPitch;
        private Vector3 smoothedLookPosition;
        private bool hasSmoothedLookPosition;
        private float humanoidLookWeight;
        private Transform temporaryTarget;
        private Vector3 targetOffset;
        private Vector3 temporaryTargetOffset;

        /// <summary>Gets whether this behavior rotates only the head.</summary>
        public bool HeadOnly => headOnly;

        /// <summary>Gets the temporary target when present, otherwise the normal target.</summary>
        public Transform CurrentTarget => GetCurrentTarget();

        protected override void Awake()
        {
            base.Awake();
            CacheReferences();
            EnsureHeadTransform();
        }

        private void Reset()
        {
            motor = GetComponent<NPCNavMeshMotor>();
            animator = GetComponentInChildren<Animator>();
        }

        private void Update()
        {
            if (!IsBehaviourActive || headOnly)
                return;

            if (TryGetLookPosition(out Vector3 lookPosition))
                RotateFullBodyToward(lookPosition);
        }

        /// <summary>
        /// Generic-rig fallback. Humanoid rigs should normally use Animator IK instead.
        /// Smoothing is stored independently from the bone rotation so the Animator resetting
        /// the head pose each frame does not create a reset-and-correct twitch.
        /// </summary>
        private void LateUpdate()
        {
            if (!IsBehaviourActive || !headOnly || ShouldUseAnimatorIK())
                return;

            if (!EnsureHeadTransform())
                return;

            if (TryGetLookPosition(out Vector3 lookPosition))
            {
                RotateHeadTransformToward(lookPosition);
            }
            else
            {
                ReturnHeadTransformToRest();
            }
        }

        /// <summary>
        /// Applies humanoid look-at through the Animator, preventing the animation system
        /// and this component from competing over the head transform.
        /// </summary>
        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null || !animator.isHuman)
                return;

            bool shouldLook =
                IsBehaviourActive &&
                headOnly &&
                useHumanoidAnimatorIK &&
                TryGetLookPosition(out _);

            float blendSpeed = shouldLook ? headTurnSpeed : headReturnSpeed;
            float interpolation = GetExponentialInterpolation(blendSpeed);
            humanoidLookWeight = Mathf.Lerp(
                humanoidLookWeight,
                shouldLook ? 1f : 0f,
                interpolation
            );

            if (shouldLook)
            {
                TryGetLookPosition(out Vector3 targetPosition);

                if (!hasSmoothedLookPosition)
                {
                    smoothedLookPosition = targetPosition;
                    hasSmoothedLookPosition = true;
                }

                smoothedLookPosition = Vector3.Lerp(
                    smoothedLookPosition,
                    targetPosition,
                    interpolation
                );
            }

            animator.SetLookAtWeight(
                overallWeight * humanoidLookWeight,
                bodyWeight,
                headWeight,
                eyesWeight,
                clampWeight
            );

            if (hasSmoothedLookPosition)
                animator.SetLookAtPosition(smoothedLookPosition);
        }

        /// <summary>Sets the normal look target without changing the behavior's active state.</summary>
        /// <param name="target">New normal look target.</param>
        public void SetLookTarget(Transform target)
        {
            this.target = target;
            targetOffset = Vector3.zero;
            ResetLookSmoothing();
        }

        /// <summary>Sets a normal look target and activates looking.</summary>
        /// <param name="target">Target to look toward.</param>
        public void StartLookingAt(Transform target)
        {
            StartLookingAt(target, Vector3.zero);
        }

        /// <summary>Sets a normal look target with a world-space offset and activates looking.</summary>
        /// <param name="target">Target to look toward.</param>
        /// <param name="worldOffset">World-space offset from the target position.</param>
        public void StartLookingAt(Transform target, Vector3 worldOffset)
        {
            this.target = target;
            targetOffset = worldOffset;
            ResetLookSmoothing();
            SetBehaviourActive(target != null);
        }

        /// <summary>
        /// Temporarily overrides the normal look target. Dialogue and cutscenes can use this
        /// without destroying passive player-tracking state.
        /// </summary>
        public void StartTemporaryLookAt(Transform target, Vector3 worldOffset = default)
        {
            temporaryTarget = target;
            temporaryTargetOffset = worldOffset;
            ResetLookSmoothing();
            SetBehaviourActive(target != null);
        }

        /// <summary>Clears the temporary override and returns to the normal look target.</summary>
        public void StopTemporaryLook()
        {
            temporaryTarget = null;
            temporaryTargetOffset = Vector3.zero;
        }

        /// <summary>Activates looking when a normal or temporary target is available.</summary>
        public void StartLooking()
        {
            if (GetCurrentTarget() == null)
                return;

            ResetLookSmoothing();
            SetBehaviourActive(true);
        }

        /// <summary>Clears the normal target while preserving any temporary override.</summary>
        public void ClearLookTarget()
        {
            target = null;
            targetOffset = Vector3.zero;
            ResetLookSmoothing();

            if (temporaryTarget == null)
                SetBehaviourActive(false);
        }

        /// <summary>Clears all targets and deactivates this behavior.</summary>
        public void StopLooking()
        {
            target = null;
            temporaryTarget = null;
            targetOffset = Vector3.zero;
            temporaryTargetOffset = Vector3.zero;
            ResetLookSmoothing();
            SetBehaviourActive(false);
        }

        /// <summary>Changes between head-only and full-body looking.</summary>
        /// <param name="value">True for head-only looking; false for full-body looking.</param>
        public void SetHeadOnly(bool value)
        {
            if (headOnly == value)
                return;

            headOnly = value;
            ResetLookSmoothing();

            if (IsBehaviourActive)
                ApplyMotorRotationState();
        }

        protected override void OnBehaviourActivated()
        {
            CacheReferences();
            EnsureHeadTransform();
            ResetLookSmoothing();
            ApplyMotorRotationState();
        }

        protected override void OnBehaviourDeactivated()
        {
            motor?.SetAutomaticRotation(true);
            ResetLookSmoothing();
        }

        private void CacheReferences()
        {
            if (motor == null)
                motor = GetComponent<NPCNavMeshMotor>();

            if (animator == null)
                animator = GetComponentInChildren<Animator>();
        }

        private Transform GetCurrentTarget()
        {
            return temporaryTarget != null ? temporaryTarget : target;
        }

        private bool TryGetLookPosition(out Vector3 position)
        {
            Transform currentTarget = GetCurrentTarget();
            if (currentTarget == null)
            {
                position = default;
                return false;
            }

            Vector3 offset = temporaryTarget != null
                ? temporaryTargetOffset
                : targetOffset;
            position = currentTarget.position + offset;
            return true;
        }

        private bool ShouldUseAnimatorIK()
        {
            return useHumanoidAnimatorIK &&
                   animator != null &&
                   animator.isHuman;
        }

        private void RotateFullBodyToward(Vector3 targetPosition)
        {
            Vector3 direction = targetPosition - transform.position;

            if (keepBodyUpright)
                direction.y = 0f;

            if (direction.sqrMagnitude < 0.001f)
                return;

            Quaternion desiredRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            float interpolation = 1f - Mathf.Exp(-bodyTurnSpeed * Time.deltaTime);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                desiredRotation,
                interpolation
            );
        }

        private void RotateHeadTransformToward(Vector3 targetPosition)
        {
            Vector3 worldDirection = targetPosition - headTransform.position;
            if (worldDirection.sqrMagnitude < 0.001f)
                return;

            Vector3 localDirection =
                transform.InverseTransformDirection(worldDirection.normalized);

            float targetYaw =
                Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg;

            float horizontalMagnitude =
                new Vector2(localDirection.x, localDirection.z).magnitude;

            float targetPitch =
                -Mathf.Atan2(localDirection.y, horizontalMagnitude) * Mathf.Rad2Deg;

            targetYaw = Mathf.Clamp(targetYaw, -maxHeadYaw, maxHeadYaw);
            targetPitch = Mathf.Clamp(
                targetPitch,
                -maxHeadPitchUp,
                maxHeadPitchDown
            );

            float interpolation = GetExponentialInterpolation(headTurnSpeed);
            smoothedYaw = Mathf.Lerp(smoothedYaw, targetYaw, interpolation);
            smoothedPitch = Mathf.Lerp(smoothedPitch, targetPitch, interpolation);

            ApplyHeadTransformCorrection();
        }

        private void ReturnHeadTransformToRest()
        {
            float interpolation = GetExponentialInterpolation(headReturnSpeed);
            smoothedYaw = Mathf.Lerp(smoothedYaw, 0f, interpolation);
            smoothedPitch = Mathf.Lerp(smoothedPitch, 0f, interpolation);

            ApplyHeadTransformCorrection();
        }

        private void ApplyHeadTransformCorrection()
        {
            Quaternion bodyRelativeRotation =
                Quaternion.Euler(smoothedPitch, smoothedYaw, 0f);

            Vector3 desiredWorldDirection =
                transform.TransformDirection(bodyRelativeRotation * Vector3.forward);

            // The Animator has already evaluated this frame. Apply the complete correction
            // from that animated pose instead of partially slerping from a pose that will
            // be reset again next frame.
            // Imported skeletons do not consistently use the bone's local Z axis as the
            // face direction. Rotate the animated pose by the character-space look delta.
            Quaternion correction = Quaternion.FromToRotation(
                transform.forward,
                desiredWorldDirection
            );

            headTransform.rotation = correction * headTransform.rotation;
        }

        private static float GetExponentialInterpolation(float speed)
        {
            return 1f - Mathf.Exp(-speed * Time.deltaTime);
        }

        private bool EnsureHeadTransform()
        {
            if (headTransform != null)
                return true;

            if (animator != null && animator.isHuman)
                headTransform = animator.GetBoneTransform(HumanBodyBones.Head);

            return headTransform != null;
        }

        private void ResetLookSmoothing()
        {
            smoothedYaw = 0f;
            smoothedPitch = 0f;
            hasSmoothedLookPosition = false;
            humanoidLookWeight = 0f;
        }

        private void ApplyMotorRotationState()
        {
            if (motor == null)
                return;

            // Full-body mode owns root rotation. Head-only mode leaves normal agent turning enabled.
            motor.SetAutomaticRotation(headOnly);
        }
    }
}
