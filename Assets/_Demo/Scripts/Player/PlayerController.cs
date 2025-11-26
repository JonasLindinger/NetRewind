using _Demo.Scripts.State;
using NetRewind.Utils.Input.Data;
using NetRewind.Utils.Player;
using NetRewind.Utils.Simulation;
using NetRewind.Utils.Simulation.State;
using UnityEngine;

namespace _Demo.Scripts.Player
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : NetPlayer
    {
        [Header("Move Settings")]
        [SerializeField] private float walkSpeed;
        [SerializeField] private float sprintSpeed;
        [SerializeField] private float crouchSpeed;
        [SerializeField] private float groundDrag;
        [Space(2)]
        [SerializeField] private float jumpForce; 
        [SerializeField] private float jumpCooldown;
        [SerializeField] private float airMultiplier;
        [Space(10)]
        [Header("References")]
        [SerializeField] private Transform orientation;
        [SerializeField] private LayerMask whatIsGround;
        [SerializeField] private float playerHeight;

        private Rigidbody _rb;
        
        private bool _grounded;
        private float _jumpCooldownTimer;
        
        protected override void NetSpawn()
        {
            PlayerData data = new PlayerData();
            
            _rb = GetComponent<Rigidbody>();
            _rb.freezeRotation = true;
            // _rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private void Move()
        {
            if (_jumpCooldownTimer > 0)
                _jumpCooldownTimer -= Simulation.TimeBetweenTicks;
            
            // Applying movement
            // Setting the drag
            _grounded = Physics.Raycast(transform.position, Vector3.down, playerHeight * 0.5f + 0.2f, whatIsGround);

            if (_grounded)
                _rb.linearDamping = groundDrag;
            else
                _rb.linearDamping = 0;

            // Calculating movement
            Vector2 moveInput = GetVector2(0);

            // _orientation.rotation = Quaternion.Euler(0, input.PlayerRotation, 0);
            Vector3 moveDirection = orientation.forward * moveInput.y + orientation.right * moveInput.x;

            // Applying movement
            float moveSpeed = GetButton(4) ? sprintSpeed : GetButton(5) ? crouchSpeed : walkSpeed;

            // Grounded
            if (_grounded)
                _rb.AddForce(moveDirection.normalized * (moveSpeed * 10), ForceMode.Force);

            // In air
            else
                _rb.AddForce(moveDirection.normalized * (moveSpeed * 10 * airMultiplier), ForceMode.Force);

            // Speed Control
            Vector3 flatVel = new Vector3(_rb.linearVelocity.x, 0, _rb.linearVelocity.z);
            if (flatVel.magnitude > moveSpeed)
            {
                Vector3 limitedVel = flatVel.normalized * moveSpeed;
                _rb.linearVelocity = new Vector3(limitedVel.x, _rb.linearVelocity.y, limitedVel.z);
            }

            if (GetButton(6) && _grounded && _jumpCooldownTimer <= 0)
            {
                // Resetting Y velocity
                _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, 0, _rb.linearVelocity.z);

                // Applying Force
                _rb.AddForce(transform.up * jumpForce, ForceMode.Impulse);

                // Applying Cooldown
                _jumpCooldownTimer = jumpCooldown;
            }
        }
        
        protected override void OnTick(uint tick)
        {
            // GetPlayer data
            PlayerData data = GetData<PlayerData>();
            
            // Rotate player
            Vector3 newRotation = transform.eulerAngles;
            newRotation.y = data.YRotation;
            transform.eulerAngles = newRotation;
            
            // Move player
            Move();
        }

        protected override IData GetAdditionalData()
        {
            return new PlayerData()
            {
                YRotation = transform.eulerAngles.y,
            };
        }

        protected override void UpdateState(IState state)
        {
            PlayerState playerState = (PlayerState) state;
            transform.position = playerState.Position;
            transform.localRotation = Quaternion.Euler(0, playerState.YRotation, 0);
            _rb.linearVelocity = playerState.Velocity;
            _rb.angularVelocity = playerState.AngularVelocity;
        }

        protected override void ApplyState(IState state)
        {
            PlayerState playerState = (PlayerState) state;
            transform.position = playerState.Position;
            transform.localRotation = Quaternion.Euler(0, playerState.YRotation, 0);
            _rb.linearVelocity = playerState.Velocity;
            _rb.angularVelocity = playerState.AngularVelocity;
        }
        
        protected override IState GetCurrentState()
        {
            return new PlayerState()
            {
                Position = transform.position,
                YRotation = transform.localRotation.eulerAngles.y,
                Velocity = _rb.linearVelocity,
                AngularVelocity = _rb.angularVelocity
            };
        }

        protected override void ApplyPartialState(IState state, uint part)
        {
            throw new System.NotImplementedException();
        }
    }
}