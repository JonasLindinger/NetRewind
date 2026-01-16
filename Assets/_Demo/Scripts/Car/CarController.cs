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
        
        private ulong _playerOnSeat1 = ulong.MaxValue;
        private ulong _playerOnSeat2 = ulong.MaxValue;
        
        
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
        
            // Not really necessary, because the default value is true. But just in case the value changed in the inspector, set it to true.
            ChangePredictionState(true);
        }

        protected override void OnOwnershipChanged(ulong previous, ulong current)
        {
            NetOwnerUpdate();
        }

        protected override void NetDespawn()
        {
            _cars.Remove(NetworkObjectId);
        }

        public void NetOwnerUpdate()
        {
            
        }
        
        public void Tick(uint tick)
        {
            Move();
        }
        
        private void Move()
        {
            // Applying movement
            // Setting the drag
            _rb.linearDamping = groundDrag;

            // Calculating movement
            Vector2 moveInput = GetVector2(0).normalized;

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
                if (_playerOnSeat1 == ulong.MaxValue ||
                    _playerOnSeat2 == ulong.MaxValue)
                {
                    // -> Seat available
                    
                    bool seatWithAuthority = _playerOnSeat1 == ulong.MaxValue;
                    
                    // Get new position
                    Transform seat = _playerOnSeat1 == ulong.MaxValue 
                        ? seat1.transform
                        : seat2.transform;

                    // Get a reference of the seat
                    ref ulong seatOwner = ref _playerOnSeat1 == ulong.MaxValue 
                        ? ref _playerOnSeat1 
                        : ref _playerOnSeat2;
                    
                    seatOwner = player.OwnerClientId;
                    player.HopInCar(this, seat);
                    
                    #if Server
                    if (IsServer && seatWithAuthority)
                    {
                        // Seat 1 -> authority over the car.
                        // -> give ownership.
                        NetworkObject.ChangeOwnership(player.OwnerClientId);
                    }
                    #endif
                }
                else
                {
                    // No space
                }
            }
            else
            {
                // Verify
                if (_playerOnSeat1 == player.OwnerClientId ||
                    _playerOnSeat2 == player.OwnerClientId)
                {
                    // -> Let the player leave the car
                    
                    bool seatWithAuthority = _playerOnSeat1 == player.OwnerClientId;
                    
                    //Get seat of the player
                    ref ulong seat = ref _playerOnSeat1 == player.OwnerClientId 
                        ? ref _playerOnSeat1 
                        : ref _playerOnSeat2;
                    
                    // Mark seat as available
                    seat = ulong.MaxValue;
                    
                    player.HopOutCar();
                    
                    #if Server
                    if (IsServer && seatWithAuthority)
                    {
                        // Seat 1 -> authority over the car.
                        // -> remove ownership.
                        NetworkObject.RemoveOwnership();
                    }
                    #endif
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
        }

        public void ApplyPartialState(IState state, uint part)
        {
            throw new System.NotImplementedException();
        }
        #endregion
        
        public static CarController GetCar(ulong clientId) => _cars[clientId];
        public Transform GetSeatByOwner(ulong ownerClientId)
        {
            if (_playerOnSeat1 == ownerClientId)
                return seat1.transform;
            if (_playerOnSeat2 == ownerClientId)
                return seat2.transform;
            
            return null;
        }
    }
}