using UnityEngine;

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

        [Tooltip("Optional target that overrides the target stored by NPCController.")]
        [SerializeField] private Transform explicitTarget;

        [Tooltip("Uses NPCController.Target when no explicit target has been assigned.")]
        [SerializeField] private bool useControllerTarget = true;

        [Header("Activation")]
        [Tooltip("Automatically activates this behaviour when SetLookTarget receives a valid target.")]
        [SerializeField] private bool activateWhenTargetAssigned = true;

        [Tooltip("Automatically deactivates this behaviour when its explicit target is cleared.")]
        [SerializeField] private bool deactivateWhenTargetCleared = true;

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

        [Tooltip("Maximum degrees the head may turn left or right.")]
        [SerializeField, Range(0f, 180f)] private float maxHeadYaw = 75f;

        [Tooltip("Maximum degrees the head may look upward.")]
        [SerializeField, Range(0f, 90f)] private float maxHeadPitchUp = 35f;

        [Tooltip("Maximum degrees the head may look downward.")]
        [SerializeField, Range(0f, 90f)] private float maxHeadPitchDown = 45f;

        [Header("Humanoid IK Weights")]
        [SerializeField, Range(0f, 1f)] private float overallWeight = 1f;
        [SerializeField, Range(0f, 1f)] private float bodyWeight = 0f;
        [SerializeField, Range(0f, 1f)] private float headWeight = 1f;
        [SerializeField, Range(0f, 1f)] private float eyesWeight = 0.25f;
        [SerializeField, Range(0f, 1f)] private float clampWeight = 0.5f;

        private float smoothedYaw;
        private float smoothedPitch;
        private Vector3 smoothedLookPosition;
        private bool hasSmoothedLookPosition;

        public bool HeadOnly => headOnly;
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

            Transform target = GetCurrentTarget();
            if (target != null)
                RotateFullBodyToward(target.position);
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

            Transform target = GetCurrentTarget();
            if (target == null || !EnsureHeadTransform())
                return;

            RotateHeadTransformToward(target.position);
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
                GetCurrentTarget() != null;

            if (!shouldLook)
            {
                animator.SetLookAtWeight(0f);
                return;
            }

            Transform target = GetCurrentTarget();
            Vector3 targetPosition = target.position;

            if (!hasSmoothedLookPosition)
            {
                smoothedLookPosition = targetPosition;
                hasSmoothedLookPosition = true;
            }

            float interpolation = 1f - Mathf.Exp(-headTurnSpeed * Time.deltaTime);
            smoothedLookPosition = Vector3.Lerp(
                smoothedLookPosition,
                targetPosition,
                interpolation
            );

            animator.SetLookAtWeight(
                overallWeight,
                bodyWeight,
                headWeight,
                eyesWeight,
                clampWeight
            );
            animator.SetLookAtPosition(smoothedLookPosition);
        }

        public void SetLookTarget(Transform target)
        {
            explicitTarget = target;
            ResetLookSmoothing();

            if (target != null && activateWhenTargetAssigned)
                SetBehaviourActive(true);
            else if (target == null && deactivateWhenTargetCleared)
                SetBehaviourActive(false);
        }

        public void StartLookingAt(Transform target)
        {
            explicitTarget = target;
            ResetLookSmoothing();
            SetBehaviourActive(target != null);
        }

        public void StartLooking()
        {
            if (GetCurrentTarget() == null)
                return;

            ResetLookSmoothing();
            SetBehaviourActive(true);
        }

        public void ClearLookTarget()
        {
            explicitTarget = null;
            ResetLookSmoothing();

            if (deactivateWhenTargetCleared)
                SetBehaviourActive(false);
        }

        public void StopLooking()
        {
            explicitTarget = null;
            ResetLookSmoothing();
            SetBehaviourActive(false);
        }

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
            if (explicitTarget != null)
                return explicitTarget;

            return useControllerTarget && Controller != null
                ? Controller.Target
                : null;
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

            float interpolation = 1f - Mathf.Exp(-headTurnSpeed * Time.deltaTime);
            smoothedYaw = Mathf.Lerp(smoothedYaw, targetYaw, interpolation);
            smoothedPitch = Mathf.Lerp(smoothedPitch, targetPitch, interpolation);

            Quaternion bodyRelativeRotation =
                Quaternion.Euler(smoothedPitch, smoothedYaw, 0f);

            Vector3 desiredWorldDirection =
                transform.TransformDirection(bodyRelativeRotation * Vector3.forward);

            // The Animator has already evaluated this frame. Apply the complete correction
            // from that animated pose instead of partially slerping from a pose that will
            // be reset again next frame.
            Quaternion correction = Quaternion.FromToRotation(
                headTransform.forward,
                desiredWorldDirection
            );

            headTransform.rotation = correction * headTransform.rotation;
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