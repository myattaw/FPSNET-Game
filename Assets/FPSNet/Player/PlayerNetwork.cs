using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using Random = UnityEngine.Random;
using System.Collections;
using FPSNet.Network;
using FPSNet.Network.KillFeed;

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

        // New: track whether player is currently dead (server authoritative)
        public NetworkVariable<bool> IsDead = new NetworkVariable<bool>(false);

        private float respawnDelay = 0f; // set to 0 for instant-ish respawn (one frame to sync)

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
           
            
            var mainMenu = GameObject.Find("MainMenu");
            if (mainMenu != null) 
                mainMenu.SetActive(false);
            
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

            if (IsServer)
            {
                Health.Value = maxHealth;
                IsDead.Value = false;
            }

            // subscribe safely
            if (Health != null)
                Health.OnValueChanged += OnHealthChanged;

            // immediately update owner UI so it shows correct initial value
            if (IsOwner && UIManager.instance != null)
            {
                UIManager.instance.UpdateHealthBar(Health != null ? Health.Value : maxHealth);
            }
        }

        private void OnDestroy()
        {
            if (Health != null)
                Health.OnValueChanged -= OnHealthChanged;
        }

        private void OnHealthChanged(int oldValue, int newValue)
        {
            // Simple client-side notification - replace with UI update later
            Debug.Log($"Player {OwnerClientId} health changed: {oldValue} -> {newValue}");

            // Only the owning client should update its HUD
            if (IsOwner && UIManager.instance != null)
            {
                UIManager.instance.UpdateHealthBar(newValue);
                if (newValue < oldValue)
                    UIManager.instance.InstantiateHitUI();
            }
        }

        // Called by server-side code (e.g. Bullet on server) to apply damage
        // Returns true if this call caused the player's death (server only)
        public bool ApplyDamage(int amount, ulong attackerClientId = ulong.MaxValue)
        {
            if (!IsServer) return false; // must be executed on server

            // If already dead, ignore further damage
            if (IsDead.Value) return false;

            int newHealth = Mathf.Max(0, Health.Value - amount);
            Health.Value = newHealth;

            if (newHealth <= 0)
            {
                // mark dead immediately so subsequent collisions don't re-enter death logic
                IsDead.Value = true;

                // Update stats exactly once here (server)
                var victimStats = GetComponent<PlayerStats>();
                if (victimStats != null)
                    victimStats.Deaths.Value++;

                if (attackerClientId != ulong.MaxValue)
                {
                    var attackerStats = FPSNet.Network.PlayerStats.AllPlayers.Find(p => p.OwnerClientId == attackerClientId);
                    if (attackerStats != null)
                        attackerStats.Kills.Value++;
                }

                HandleDeath(attackerClientId);
                return true;
            }
            return false;
        }

        private void HandleDeath(ulong attackerClientId)
        {
            // Log once (IsDead already set in ApplyDamage)
            Debug.Log($"Player {OwnerClientId} died (server).");

            // Resolve attacker name (world or player)
            string attackerName = "World";
            if (attackerClientId != ulong.MaxValue)
            {
                var attackerStats = FPSNet.Network.PlayerStats.AllPlayers.Find(p => p.OwnerClientId == attackerClientId);
                if (attackerStats != null)
                    attackerName = attackerStats.PlayerName.Value.ToString();
                else
                    attackerName = "Player " + attackerClientId;
            }

            // Resolve victim name
            var victimStats = GetComponent<FPSNet.Network.PlayerStats>();
            string victimName = victimStats != null ? victimStats.PlayerName.Value.ToString() : "Player " + OwnerClientId;

            // Broadcast to all clients via KillFeedManager (server-only)
            if (KillFeedManager.Instance != null && IsServer)
            {
                KillFeedManager.Instance.BroadcastKill(attackerName, victimName);
            }

            // Continue with respawn routine
            StartCoroutine(RespawnCoroutine(attackerClientId));
        }
        
        private IEnumerator RespawnCoroutine(ulong attacker)
        {
            if (m_CharacterController == null)
                m_CharacterController = GetComponent<CharacterController>();

            m_CharacterController.enabled = false;

            yield return null; // allow sync

            // Pick spawn point
            GameObject[] spawns = GameObject.FindGameObjectsWithTag("SpawnPoint");
            Vector3 pos = Vector3.up * 2f;
            Quaternion rot = Quaternion.identity;

            if (spawns.Length > 0)
            {
                var chosen = spawns[Random.Range(0, spawns.Length)];
                pos = chosen.transform.position;
                rot = chosen.transform.rotation;
            }

            // Tell ONLY the owner client to teleport itself
            TeleportClientRpc(pos, rot, OwnerClientId);

            // Reset server-side stats
            Health.Value = maxHealth;
            IsDead.Value = false;

            yield return null;
            m_CharacterController.enabled = true;
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

        // TELEPORT RPC → only owner applies it
        [ClientRpc]
        private void TeleportClientRpc(Vector3 pos, Quaternion rot, ulong targetClientId)
        {
            if (!IsOwner) return;

            StartCoroutine(DoTeleport(pos, rot));
        }

        private IEnumerator DoTeleport(Vector3 pos, Quaternion rot)
        {
            m_CharacterController.enabled = false;
            yield return null;

            transform.SetPositionAndRotation(pos, rot);

            yield return null;
            m_CharacterController.enabled = true;
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
