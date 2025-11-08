using System;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace UnityStandardAssets.Characters.FirstPerson
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkTransform))]
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
        private float animSpeedSmooth = 0f; // Add this field at the top of the class


        private Animator animator;
        private NetworkAnimator networkAnimator;

        public override void OnNetworkSpawn()
        {
            // Assign animator and network animator immediately
            animator = GetComponentInChildren<Animator>();
            networkAnimator = GetComponent<NetworkAnimator>();

            if (networkAnimator != null && networkAnimator.Animator == null)
            {
                networkAnimator.Animator = animator;
            }

            // Find this player's camera
            m_Camera = GetComponentInChildren<Camera>(true); // include inactive

            if (!IsOwner)
            {
                // Disable only camera/audio, not this script yet
                if (m_Camera != null)
                    m_Camera.enabled = false;

                var listener = m_Camera != null ? m_Camera.GetComponent<AudioListener>() : null;
                if (listener != null)
                    listener.enabled = false;

                // Do NOT disable this script — let remote players receive animation updates
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
        }

        private void Update()
        {
            if (!IsOwner) return;

            RotateView();

            if (!m_Jump)
                m_Jump = Input.GetButtonDown("Jump");

            if (!m_PreviouslyGrounded && m_CharacterController.isGrounded)
            {
                m_MoveDir.y = 0f;
                m_Jumping = false;
            }

            if (!m_CharacterController.isGrounded && !m_Jumping && m_PreviouslyGrounded)
                m_MoveDir.y = 0f;

            m_PreviouslyGrounded = m_CharacterController.isGrounded;
        }

        private void FixedUpdate()
        {
            if (!IsOwner) return;

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
                }
            }
            else
            {
                m_MoveDir += Physics.gravity * m_GravityMultiplier * Time.fixedDeltaTime;
            }

            UpdateNetworkAnimation();

            m_CollisionFlags = m_CharacterController.Move(m_MoveDir * Time.fixedDeltaTime);
            m_MouseLook.UpdateCursorLock();
            
            if (headTransform != null)
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

            //TODO: Head rotation not working properly yet`
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
