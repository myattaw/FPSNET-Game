using System;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using Random = UnityEngine.Random;

namespace UnityStandardAssets.Characters.FirstPerson
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkTransform))]
    [RequireComponent(typeof(AudioSource))]
    public class NetworkFirstPersonController : NetworkBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private bool m_IsWalking = true;
        [SerializeField] private float m_WalkSpeed = 4f;
        [SerializeField] private float m_RunSpeed = 7f;
        [SerializeField, Range(0f, 1f)] private float m_RunstepLenghten = 0.7f;
        [SerializeField] private float m_JumpSpeed = 5f;
        [SerializeField] private float m_StickToGroundForce = 10f;
        [SerializeField] private float m_GravityMultiplier = 2f;
        [SerializeField] public MouseLook m_MouseLook = new MouseLook();
        [SerializeField] private float m_StepInterval = 2f;

        [SerializeField] private NetworkTransform headNetworkTransform;
        [SerializeField] private Transform headTransform;

        private Camera m_Camera;
        private bool m_Jump;
        private Vector2 m_Input;
        private Vector3 m_MoveDir = Vector3.zero;
        private CharacterController m_CharacterController;
        private CollisionFlags m_CollisionFlags;
        private bool m_PreviouslyGrounded;
        private bool m_Jumping;
        private float animSpeedSmooth = 0f;

        // Step cycle for footsteps
        private float m_StepCycle;
        private float m_NextStep;

        [Header("Audio")]
        [SerializeField] private AudioClip[] m_FootstepSounds;    // an array of footstep sounds that will be randomly selected from.
        [SerializeField] private AudioClip m_JumpSound;           // the sound played when character leaves the ground.
        [SerializeField] private AudioClip m_LandSound;           // the sound played when character touches back on ground.

        private AudioSource m_AudioSource;

        private Animator animator;
        private NetworkAnimator networkAnimator;

        [Header("Health")]
        public int maxHealth = 100;
        // Networked health value (clients will see updates)
        public NetworkVariable<int> Health = new NetworkVariable<int>(100);

        public override void OnNetworkSpawn()
        {
            // Assign animator and network animator immediately
            animator = GetComponentInChildren<Animator>();
            networkAnimator = GetComponent<NetworkAnimator>();

            if (m_AudioSource == null)
                m_AudioSource = GetComponent<AudioSource>();

            if (networkAnimator != null && networkAnimator.Animator == null)
            {
                networkAnimator.Animator = animator;
            }

            // Find this player's camera
            m_Camera = GetComponentInChildren<Camera>(true); // include inactive

            if (!IsOwner)
            {
                // Remote players: 3D spatial audio
                if (m_AudioSource != null)
                    m_AudioSource.spatialBlend = 1f;

                // Disable only camera/audio listener, not this script
                if (m_Camera != null)
                    m_Camera.enabled = false;

                var listener = m_Camera != null ? m_Camera.GetComponent<AudioListener>() : null;
                if (listener != null)
                    listener.enabled = false;

                return;
            }

            // Local player setup
            if (m_Camera != null)
            {
                m_Camera.enabled = true;

                var listener = m_Camera.GetComponent<AudioListener>();
                if (listener != null)
                    listener.enabled = true;
            }

            // Initialize health on server and subscribe to changes
            if (IsServer)
            {
                Health.Value = maxHealth;
            }
            Health.OnValueChanged += OnHealthChanged;
        }

        private void OnDestroy()
        {
            Health.OnValueChanged -= OnHealthChanged;
        }

        private void OnHealthChanged(int oldValue, int newValue)
        {
            // Simple client-side notification - replace with UI update later
            Debug.Log($"Player {OwnerClientId} health changed: {oldValue} -> {newValue}");
        }

        // Called by server-side code (e.g. Bullet on server) to apply damage
        public void ApplyDamage(int amount)
        {
            if (!IsServer) return; // must be executed on server

            int newHealth = Mathf.Max(0, Health.Value - amount);
            Health.Value = newHealth;

            if (newHealth <= 0)
            {
                HandleDeath();
            }
        }

        private void HandleDeath()
        {
            Debug.Log($"Player {OwnerClientId} died (server).");

            // Minimal death handling: disable movement for now, etc.
        }

        private void Start()
        {
            m_CharacterController = GetComponent<CharacterController>();
            if (m_Camera == null)
                m_Camera = Camera.main;

            m_Jumping = false;
            m_MouseLook.Init(transform, m_Camera.transform);

            // Ensure animator is cached
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            // Step cycle init
            m_StepCycle = 0f;
            m_NextStep = m_StepInterval * 0.5f;

            if (m_AudioSource == null)
                m_AudioSource = GetComponent<AudioSource>();
        }

        private void Update()
        {
            if (!IsOwner) return;

            RotateView();

            if (!m_Jump)
                m_Jump = Input.GetButtonDown("Jump");

            // Landing detection
            if (!m_PreviouslyGrounded && m_CharacterController.isGrounded)
            {
                m_MoveDir.y = 0f;
                m_Jumping = false;

                // Notify server to play land sound
                if (IsServer)
                    PlayLandClientRpc();
                else
                    PlayLandServerRpc();
            }

            if (!m_CharacterController.isGrounded && !m_Jumping && m_PreviouslyGrounded)
                m_MoveDir.y = 0f;

            m_PreviouslyGrounded = m_CharacterController.isGrounded;
        }

        private void FixedUpdate()
        {
            if (!IsOwner) return;

            // Don't run movement if controller is missing or disabled (prevents Move on inactive controller)
            if (m_CharacterController == null || !m_CharacterController.enabled)
                return;

            float speed;
            GetInput(out speed);

            Vector3 desiredMove = transform.forward * m_Input.y + transform.right * m_Input.x;

            // Move along ground slope
            RaycastHit hitInfo;
            Physics.SphereCast(transform.position, m_CharacterController.radius, Vector3.down,
                out hitInfo, m_CharacterController.height / 2f, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            desiredMove = Vector3.ProjectOnPlane(desiredMove, hitInfo.normal).normalized;

            m_MoveDir.x = desiredMove.x * speed;
            m_MoveDir.z = desiredMove.z * speed;

            if (m_CharacterController.isGrounded)
            {
                m_MoveDir.y = -m_StickToGroundForce;

                if (m_Jump)
                {
                    m_MoveDir.y = m_JumpSpeed;
                    m_Jump = false;
                    m_Jumping = true;

                    // Notify server to play jump sound
                    if (IsServer)
                        PlayJumpClientRpc();
                    else
                        PlayJumpServerRpc();
                }
            }
            else
            {
                m_MoveDir += Physics.gravity * m_GravityMultiplier * Time.fixedDeltaTime;
            }

            UpdateNetworkAnimation();

            m_CollisionFlags = m_CharacterController.Move(m_MoveDir * Time.fixedDeltaTime);

            // Footstep timing (owner calculates, server broadcasts)
            HandleFootsteps(speed);

            m_MouseLook.UpdateCursorLock();

            // Only sync head rotation if both assigned
            if (headTransform != null && headNetworkTransform != null)
                headNetworkTransform.transform.rotation = headTransform.rotation;
        }

        private void UpdateNetworkAnimation()
        {
            if (animator == null)
                return;

            float direction = m_Input.x;
            bool isJumping = !m_CharacterController.isGrounded;

            // Use input magnitude (instant feedback)
            float targetAnimSpeed = m_Input.magnitude * (m_IsWalking ? 0.5f : 1f);

            animator.SetFloat("Speed", targetAnimSpeed);
            animator.SetFloat("Direction", direction);
            animator.SetBool("IsJumping", isJumping);

            // Send correct value to network side
            UpdateAnimationServerRpc(targetAnimSpeed, direction, isJumping);
        }

        [ServerRpc]
        private void UpdateAnimationServerRpc(float speed, float direction, bool isJumping)
        {
            if (animator == null) return;

            animator.SetFloat("Speed", speed);
            animator.SetFloat("Direction", direction);
            animator.SetBool("IsJumping", isJumping);
        }

        // ---------- AUDIO: SERVER / CLIENT RPCS ----------

        // Called from owner client, executed on server, then broadcast
        [ServerRpc]
        private void PlayFootstepServerRpc(int index)
        {
            PlayFootstepClientRpc(index);
        }

        [ServerRpc]
        private void PlayJumpServerRpc()
        {
            PlayJumpClientRpc();
        }

        [ServerRpc]
        private void PlayLandServerRpc()
        {
            PlayLandClientRpc();
        }

        [ClientRpc]
        private void PlayFootstepClientRpc(int index)
        {
            if (m_AudioSource == null || m_FootstepSounds == null || m_FootstepSounds.Length == 0) return;
            if (index < 0 || index >= m_FootstepSounds.Length) return;

            m_AudioSource.PlayOneShot(m_FootstepSounds[index]);
        }

        [ClientRpc]
        private void PlayJumpClientRpc()
        {
            if (m_AudioSource == null || m_JumpSound == null) return;
            m_AudioSource.PlayOneShot(m_JumpSound);
        }

        [ClientRpc]
        private void PlayLandClientRpc()
        {
            if (m_AudioSource == null || m_LandSound == null) return;
            m_AudioSource.PlayOneShot(m_LandSound);
        }

        // ---------- FOOTSTEP TIMING (OWNER-SIDE CALC) ----------

        private void HandleFootsteps(float speed)
        {
            if (!m_CharacterController.isGrounded)
                return;

            if (m_CharacterController.velocity.sqrMagnitude <= 0.1f)
                return;

            if (m_Input.x == 0 && m_Input.y == 0)
                return;

            m_StepCycle += (m_CharacterController.velocity.magnitude +
                            speed * (m_IsWalking ? 1f : m_RunstepLenghten)) *
                           Time.fixedDeltaTime;

            if (!(m_StepCycle > m_NextStep))
                return;

            m_NextStep = m_StepCycle + m_StepInterval;

            if (m_FootstepSounds == null || m_FootstepSounds.Length == 0)
                return;

            int n = Random.Range(0, m_FootstepSounds.Length);

            // Ask server to broadcast the footstep sound
            if (IsServer)
                PlayFootstepClientRpc(n);
            else
                PlayFootstepServerRpc(n);
        }

        // ---------- INPUT / VIEW / COLLISION ----------

        private void GetInput(out float speed)
        {
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");

            bool wasWalking = m_IsWalking;
            m_IsWalking = !Input.GetKey(KeyCode.LeftShift);

            speed = m_IsWalking ? m_WalkSpeed : m_RunSpeed;
            m_Input = new Vector2(horizontal, vertical);

            if (m_Input.sqrMagnitude > 1)
                m_Input.Normalize();
        }

        private void RotateView()
        {
            m_MouseLook.LookRotation(transform, m_Camera.transform);

            //TODO: Head rotation not working properly yet
            if (headTransform != null)
            {
                Quaternion targetRotation = Quaternion.Euler(
                    m_Camera.transform.localEulerAngles.x,
                    0f,
                    0f
                );
                headTransform.localRotation = targetRotation;
            }
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            Rigidbody body = hit.collider.attachedRigidbody;
            if (m_CollisionFlags == CollisionFlags.Below) return;
            if (body == null || body.isKinematic) return;

            body.AddForceAtPosition(m_CharacterController.velocity * 0.1f, hit.point, ForceMode.Impulse);
        }
    }
}
