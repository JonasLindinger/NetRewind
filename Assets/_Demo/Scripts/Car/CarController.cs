using System;
using System.Collections.Generic;
using _Demo.Scripts.Game;
using _Demo.Scripts.Player;
using NetRewind.Utils.Input.Data;
using NetRewind.Utils.Simulation;
using NetRewind.Utils.Simulation.State;
using UnityEngine;
using UnityEngine.Serialization;

namespace _Demo.Scripts.Car
{
    [RequireComponent(typeof(Rigidbody))]
    public class CarController : NetObject, ITick, IInputListener, IStateHolder, IInteractable
    {
        private static Dictionary<ulong, CarController> _cars = new Dictionary<ulong, CarController>();
        
        [Header("References")]
        [SerializeField] private GameObject seat1;
        [SerializeField] private GameObject seat2;
        [Space(5)]
        [Header("Move Settings")]
        [SerializeField] private float walkSpeed;
        [SerializeField] private float groundDrag;
        [Space(5)]
        [Header("References")]
        [SerializeField] private Transform orientation;
        
        private ushort _playerOnSeat1 = ushort.MaxValue;
        private ushort _playerOnSeat2 = ushort.MaxValue;
        
        public byte[] InputData { get; set; }
        public IData Data { get; set; }
        public uint TickOfTheInput { get; set; }

        private Rigidbody _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        protected override void NetSpawn()
        {
            _cars.Add(NetworkObjectId, this);
            NetworkObject.DontDestroyWithOwner = true;
        
            bool shouldPredict = _playerOnSeat1 == ushort.MaxValue || IsInputOwner;
            ChangePredictionState(shouldPredict);
        }

        protected override void OnOwnershipChanged(ulong previous, ulong current)
        {
            NetInputOwnerUpdate();
        }

        protected override void NetDespawn()
        {
            _cars.Remove(NetworkObjectId);
        }

        public void NetInputOwnerUpdate()
        {
            
        }
        
        public void Tick(uint tick)
        {
            if (!HasInputForThisTick(tick)) return;
            
            Move();
        }
        
        private void Move()
        {
            // Applying movement
            // Setting the drag
            _rb.linearDamping = groundDrag;

            // Calculating movement
            Vector2 moveInput = GetVector2("Move").normalized;

            // _orientation.rotation = Quaternion.Euler(0, input.PlayerRotation, 0);
            Vector3 moveDirection = orientation.forward * moveInput.y + orientation.right * moveInput.x;

            // Applying movement
            float moveSpeed = walkSpeed;

            _rb.AddForce(moveDirection.normalized * (moveSpeed * 10), ForceMode.Force);

            // Speed Control
            Vector3 flatVel = new Vector3(_rb.linearVelocity.x, 0, _rb.linearVelocity.z);
            if (flatVel.magnitude > moveSpeed)
            {
                Vector3 limitedVel = flatVel.normalized * moveSpeed;
                _rb.linearVelocity = new Vector3(limitedVel.x, _rb.linearVelocity.y, limitedVel.z);
            }
        }
        
        public void Interact(PlayerController player)
        {
            if (!player.IsInCar)
            {
                // Check if a seat is available
                if (_playerOnSeat1 == ushort.MaxValue ||
                    _playerOnSeat2 == ushort.MaxValue)
                {
                    // -> Seat available
                    
                    // seatWithAuthority -> main seat -> seat1
                    bool seatWithAuthority = _playerOnSeat1 == ushort.MaxValue;
                    
                    // Get new position
                    Transform seatTransform = _playerOnSeat1 == ushort.MaxValue 
                        ? seat1.transform
                        : seat2.transform;

                    // Get a reference of the seat
                    ref ushort seatOwner = ref _playerOnSeat1 == ushort.MaxValue 
                        ? ref _playerOnSeat1 
                        : ref _playerOnSeat2;
                    
                    seatOwner = player.InputOwnerClientId;
                    player.HopInCar(this, seatTransform);
                    
                    // Now use the input of the seat owner if he is sitting on seat1
                    if (seatWithAuthority)
                        SetInputOwner(seatOwner);
                }
                else
                {
                    // No space
                }
            }
            else
            {
                // Verify
                if (_playerOnSeat1 == player.InputOwnerClientId ||
                    _playerOnSeat2 == player.InputOwnerClientId)
                {
                    // -> Let the player leave the car

                    // seatWithAuthority -> main seat -> seat1
                    bool seatWithAuthority = _playerOnSeat1 == player.InputOwnerClientId;
                    
                    // Get seat of the player
                    ref ushort seatOwner = ref _playerOnSeat1 == player.InputOwnerClientId 
                        ? ref _playerOnSeat1 
                        : ref _playerOnSeat2;
                    
                    // Mark seat as available
                    seatOwner = ushort.MaxValue;
                    
                    player.HopOutCar();
                    
                    // Set the input owner to no one, if the player hopped out of seat1 -> ushort.MaxValue
                    if (seatWithAuthority)
                        SetInputOwner(seatOwner);
                }
                else
                {
                    // Player didn't sit in the car
                }
            }
        }
        
        #region State
        public IState GetCurrentState()
        {
            return new CarState()
            {
                Position = transform.position,
                Rotation = transform.eulerAngles,
                Velocity = _rb.linearVelocity,
                AngularVelocity = _rb.angularVelocity,
                Seat1 = _playerOnSeat1,
                Seat2 = _playerOnSeat2
            };
        }

        public void UpdateState(IState state)
        {
            CarState carState = (CarState) state;
            transform.position = carState.Position;
            transform.eulerAngles = carState.Rotation;
            _rb.linearVelocity = carState.Velocity;
            _rb.angularVelocity = carState.AngularVelocity;
            _playerOnSeat1 = carState.Seat1;
            _playerOnSeat2 = carState.Seat2;
            
            // If occupied and not the driver -> don't predict
            bool shouldPredict = _playerOnSeat1 == ushort.MaxValue || IsInputOwner;
            if (IsPredicted != shouldPredict)
                ChangePredictionState(shouldPredict);
        }

        public void ApplyState(IState state)
        {
            CarState carState = (CarState) state;
            transform.position = carState.Position;
            transform.eulerAngles = carState.Rotation;
            _rb.linearVelocity = carState.Velocity;
            _rb.angularVelocity = carState.AngularVelocity;
            _playerOnSeat1 = carState.Seat1;
            _playerOnSeat2 = carState.Seat2;
            
            // If occupied and not the driver -> don't predict
            bool shouldPredict = _playerOnSeat1 == ushort.MaxValue || IsInputOwner;
            if (IsPredicted != shouldPredict)
                ChangePredictionState(shouldPredict);
        }

        public void ApplyPartialState(IState state, uint part)
        {
            throw new System.NotImplementedException();
        }
        #endregion
        
        public static CarController GetCar(ushort clientId) => _cars[clientId];
        public Transform GetSeatByOwner(ushort ownerClientId)
        {
            if (_playerOnSeat1 == ownerClientId)
                return seat1.transform;
            if (_playerOnSeat2 == ownerClientId)
                return seat2.transform;
            
            return null;
        }
    }
}