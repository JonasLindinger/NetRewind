# NetRewind

**Client-side prediction and server reconciliation for Unity** — a tick-based, rollback-capable netcode layer built on top of Unity Netcode for GameObjects (NGO).

NetRewind is meant as a lighter, more transparent alternative to all-in-one networking stacks (FishNet, Photon, and friends) for games where *feel* matters more than raw bandwidth: FPS, racing, sports, and physics-driven titles like Rocket League.

> ⚠️ **Status: work in progress.** The core loop (tick simulation, prediction, reconciliation, rollbacks, snapshots, events) runs in the included demo, but the API is not frozen and the library is not production-ready yet.

---

## Contents

- [Why another netcode library](#why-another-netcode-library)
- [Features](#features)
- [How it works](#how-it-works)
- [Requirements](#requirements)
- [Quick start](#quick-start)
- [Core concepts and API](#core-concepts-and-api)
- [Configuration](#configuration)
- [Build targets and scripting defines](#build-targets-and-scripting-defines)
- [Demo](#demo)
- [Project layout](#project-layout)
- [Known limitations and roadmap](#known-limitations-and-roadmap)
- [Contributing](#contributing)
- [License](#license)

---

## Why another netcode library

Most Unity networking solutions either hide the transport and the tick loop behind a heavy framework, or leave prediction and reconciliation entirely to you. NetRewind does the opposite: it owns the tick loop, the state buffering and the rollback machinery, and leaves *your* gameplay code in charge of what a state actually contains.

You write a state struct and a `Compare` method — the library handles buffering, sending, predicting and correcting.

## Features

- **Fixed tick simulation** — configurable tick rate (60 Hz default); gameplay runs in `Tick(uint tick)` callbacks, not `Update`.
- **Client-side prediction** — objects owned by a client simulate locally and ahead of the server; prediction can be toggled at runtime (`ChangePredictionState`).
- **Server reconciliation** — the authoritative snapshot is applied and locally predicted ticks are re-simulated, so corrections stay invisible when the client guessed right.
- **Rollback support** — server-side rollback to a past tick (`RunCodeInRollback`, `RunAllRollbacks`) for lag-compensated hits and interactions.
- **Snapshot ring buffers** — per-tick states live in fixed-size circular buffers (`SnapshotContainer`, `CircularBuffer`) instead of allocating per tick.
- **Pluggable state and input structs** — any `struct` implementing `IState` / `IData` and marked with `[StateType]` / `[DataType]` is registered by reflection and serialized polymorphically; no manual ID tables.
- **Rate-aware state sending** — per-object `SendingMode` (`Full`, `Half`, `Fourth`) controls how often its state goes out; objects with pending events are always sent.
- **Event replication** — `RegisterEvent` / `OnEvent` for one-shot effects (shots, sounds, impacts) that must not be replayed by reconciliation.
- **Input redundancy** — the last *N* inputs are re-sent with every packet (`inputPackageLoss`), so a single dropped packet does not stall the server simulation.
- **Sync groups** — `RegisterInteraction` couples objects (player ↔ car, player ↔ platform) into a `NetObjectSyncGroup` so they are corrected and sent together.
- **Deferred destruction** — `NetDestroy(tick)` keeps an object alive for a grace period so pending reconciliation can still find it.
- **Explicit build roles** — `Server` / `Client` scripting defines split server and client code paths; host builds compile both.
- **Manual physics hook** — `PhysicsMode.Sync` drives physics in script mode per tick, `Ignore` / `Deactivate` hand control to you, and `Simulation.OnRunPhysics` lets you simulate custom bodies.

## How it works

One tick, executed by `Simulation` at `tickRate`:

1. **Physics** — `Physics.Simulate(TimeBetweenTicks)` when `PhysicsMode = Sync`, plus the `OnRunPhysics` hook.
2. **Snapshot** — `SnapshotContainer.TakeSnapshot(tick)` stores every `NetObject`'s state in its ring buffer.
3. **Server: send state** — objects are grouped by `SendingMode` and sent on `tick % mode == 0`, plus any object with queued events.
4. **Client: collect input** — `InputContainer.Collect(tick)` captures the local input state for this tick.
5. **Local simulation** — `NetObject.RunTick(tick)` calls each object's `Tick(tick)`; the input owner predicts locally, the server consumes the buffered input.
6. **Client: reconciliation** (`LateUpdate`) — an incoming snapshot is applied, the old prediction is compared through `IState.Compare`, and the affected ticks are re-simulated.
7. **Server: rollbacks** (`LateUpdate`) — `NetObject.RunAllRollbacks()` replays code registered for past ticks.

`IState.Compare(local, server)` is the heart of it: `0` means "prediction was correct, keep going", any non-zero value means "correct me" — and that value can be used as a part index for `ApplyPartialState`, so you can correct one field instead of snapping the whole object.

## Requirements

- **Unity 6000.2** (the project is on `6000.2.8f1`)
- **Netcode for GameObjects 2.5.0+**
- **Input System 1.14.2+** (used by the input layer)
- Universal Render Pipeline 17.x — only because the demo project uses URP; the library itself is render-pipeline agnostic
- **Multiplayer Roles enabled** (`Project Settings → Multiplayer → Multiplayer Roles`) for the `Server` / `Client` defines
- Also used by the demo project: `com.eflatun.scenereference`, `com.unity.dedicated-server`, `com.unity.multiplayer.playmode`

## Quick start

**1. Copy the library.** Drop `Assets/NetRewind` into your project — it has no dependency on `Assets/_Demo`. Give it its own assembly definition if you use asmdefs.

**2. Scene setup.** Add a GameObject with `NetRunner` and assign:

- your `NetworkManager`
- the `NetworkTransport` to use
- a **transport layer prefab** containing `InputTransportLayer` and `SnapshotTransportLayer` (the server instantiates one per connected client)

**3. Start a session** from your own menu/bootstrap code:

```csharp
NetRunner.GetInstance().Run(RunType.Host);   // or RunType.Server / RunType.Client
```

**4. Describe your state** — a `struct` implementing `IState`, marked `[StateType]`:

```csharp
using NetRewind.Utils.CustomDataStructures;
using Unity.Netcode;
using UnityEngine;

[StateType]
public struct PlayerState : IState
{
    public Vector3 Position;
    public Vector3 Velocity;
    public bool CanMove;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Position);
        serializer.SerializeValue(ref Velocity);
        serializer.SerializeValue(ref CanMove);
    }

    // 0 = prediction was right. Anything else = correct me (the value can act as a part id).
    public uint Compare(IState localState, IState serverState)
    {
        var local = (PlayerState)localState;
        var server = (PlayerState)serverState;

        if (Vector3.Distance(local.Position, server.Position) > 0.25f) return 1;
        if (local.CanMove != server.CanMove) return 1;
        return 0;
    }
}
```

**5. Describe your input** — a `struct` implementing `IData`, marked `[DataType]`:

```csharp
[DataType]
public struct PlayerInputData : IData
{
    public Vector2 Move;
    public Vector2 Rotation;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Move);
        serializer.SerializeValue(ref Rotation);
    }
}
```

**6. Subclass `NetObject`** instead of `NetworkBehaviour`:

```csharp
using NetRewind;
using NetRewind.Utils.CustomDataStructures;
using NetRewind.Utils.Sync;
using UnityEngine;

public class Player : NetObject
{
    private Rigidbody _rb;
    private Vector2 _move;

    // Local-only input, shipped to the server with every input packet
    protected override IData OnGetInputDataToSend()
        => new PlayerInputData { Move = _move };

    protected override void NetSpawn()
    {
        _rb = GetComponent<Rigidbody>();
        SetInputOwner(OwnerClientId);        // this client drives this object
        ChangePredictionState(IsInputOwner); // predict only what we control
    }

    protected override void NetUpdate()
    {
        if (!IsInputOwner) return;
        _move = /* read your InputActions here */ default;
    }

    protected override void Tick(uint tick)
    {
        if (!HasInputForThisTick(tick)) return;

        var input = GetData<PlayerInputData>(tick);
        // move using input — runs on the client (predicted) and on the server (authoritative)
    }

    protected override IState GetCurrentState()
        => new PlayerState { Position = transform.position, Velocity = _rb.linearVelocity };

    protected override void UpdateState(IState state) { /* called while re-simulating */ }
    protected override void ApplyState(IState state) { /* snap to the authoritative state */ }
    protected override void ApplyPartialState(IState state, uint part) { /* correct a single field */ }

    // Required: the library wants you to decide what "destroyed" means for your object.
    protected override void EnterDestroyedState() { }
    protected override void ExitDestroyedState() { }

    protected override void OnEvent(IData eventData) { /* one-shot effects, never replayed */ }
}
```

**7. Play.** Use the included Multiplayer Play Mode configurations (`Client`, `Client 2`, `Server`) to run a server and two clients from one editor.

## Core concepts and API

| Concept | Type / API |
|---|---|
| Entry point, tick rate, physics mode | `NetRunner` (singleton), `RunType.Server / Client / Host` |
| Tick loop and reconciliation | `Simulation.CurrentTick`, `Simulation.OnRunPhysics`, `Simulation.HandleReconciliation()` |
| Gameplay object | `NetObject` — override `NetSpawn`, `NetDespawn`, `NetUpdate`, `Tick`, `GetCurrentState`, `UpdateState`, `ApplyState`, `ApplyPartialState`, `OnEvent`, `EnterDestroyedState`, `ExitDestroyedState`, `OnGetInputDataToSend` |
| State / input structs | `IState`, `IData`, `[StateType]`, `[DataType]` |
| Prediction control | `ChangePredictionState(bool)`, `IsPredicted`, `IsInputOwner`, `SetInputOwner(id)` |
| Sending rate | `SendingMode` (`Full` / `Half` / `Fourth`), `ChangeStateSendingMode`, `NetObjectSyncGroup` |
| Physics | `PhysicsMode.Sync / Ignore / Deactivate` |
| Spot effects | `RegisterEvent(tick, IData)`, `OnEvent(IData)` |
| Lag compensation | `RunCodeInRollback(tick, action)`, `NetObject.RunAllRollbacks()` |
| Coupling objects | `RegisterInteraction(tick, a, b)`, `EnterSyncGroup`, `LeaveSyncGroup` |
| Deferred removal | `NetDestroy(tick)` |
| Visual separation | `visual` transform, `SetVisualSyncMode`, `SetAllVisualState` |
| Input helpers | `GetButton(tick, name)`, `GetVector2(tick, name)`, `GetData<T>(tick)`, `HasInputForThisTick(tick)` |

## Configuration

`NetRunner` inspector fields:

| Field | Meaning | Default |
|---|---|---|
| `simulationTickRate` | Ticks per second for the whole simulation | `60` |
| `physicsMode` | `Sync` (library drives physics), `Ignore` (you drive it), `Deactivate` | `Sync` |
| `inputSendingMode` | How inputs are batched when sending | `Full` |
| `inputPackageLoss` | Number of past inputs re-sent per packet (packet-loss tolerance) | `4` |
| `eventPackageLossToAccountFor` | How many packets an unacknowledged event is repeated for | `5` |
| `transportLayerPrefab` | Prefab with `InputTransportLayer` + `SnapshotTransportLayer` | — |
| `networkManager` / `transport` | NGO `NetworkManager` and transport instance | — |
| `shouldPredict` *(per `NetObject`)* | Whether this object predicts by default | `true` |
| `initialSendingMode` *(per `NetObject`)* | Starting `SendingMode` for this object | `Full` |

## Build targets and scripting defines

Server-only and client-only code is wrapped in `#if Server` / `#if Client`. Those symbols come from Unity's Multiplayer Roles (or the equivalent scripting defines on a build profile). The project ships ready-made build profiles:

- `Client Windows` / `Client Linux` → define `Client`
- `Server Windows` / `Linux Server` → define `Server`
- `Host Windows` / `Host Linux` → defines `Client` + `Server`

`NetRunner.Run(RunType)` logs a warning instead of failing when the current build lacks the required role, so a client build cannot accidentally start a server.

## Demo

`Assets/_Demo` contains a small but non-trivial scenario that exercises most of the library:

- **Player** — prediction, movement, look and shooting; `RunCodeInRollback` for lag-compensated hits, `ShootEvent` for replicated shot effects.
- **Car** — enter/exit, input ownership transfer, `RegisterInteraction` sync groups between player and car.
- **PatrollingPlatform** — server-driven movement with events, demonstrating `SendingMode` and event replication.
- **Scene flow** — `Bootstrapper` → `Game` through the bundled scene management (`Assets/External/SceneManagement`).

Scenes: `Assets/_Demo/Scenes/Bootstrapper.unity` and `Assets/_Demo/Scenes/Game.unity`.

## Project layout

```
Assets/
├── NetRewind/                      # the library
│   ├── NetRunner.cs                # entry point, session start, RTT helpers
│   ├── Simulation.cs               # tick loop, reconciliation, state broadcasting
│   ├── NetObject.cs                # base class for replicated gameplay objects
│   ├── Event.cs                    # one-shot event replication
│   └── Utils/
│       ├── Sync/                   # snapshots, states, sending modes, sync groups, transports
│       ├── Input/                  # input collection, encoding, transport layer
│       ├── CustomDataStructures/   # IState/IData registries, circular buffers
│       └── Features/               # singleton helper, editor drawers
├── _Demo/                          # playable demo (player, car, platform, scenes)
├── External/                       # bundled scene management + TextMesh Pro
└── Settings/                       # URP assets, Multiplayer Play Mode configs, build profiles
```

## Known limitations and roadmap

- API is still moving — expect breaking changes before a first tagged release.
- No interest management / relevancy beyond `SendingMode` and the per-client transport layers.
- Re-simulation assumes your tick logic is deterministic; Unity `Rigidbody` physics is synchronised but not bit-deterministic across machines.
- `Simulation.CurrentTick` is the highest local tick and is *not* rewound during reconciliation — don't use it inside `Tick`.
- No reconnection, session recovery, matchmaking or lobby layer — pair it with your own.
- Planned: extractable UPM package with asmdefs, automated tests around the tick loop, tooling for inspecting state buffers.

## Contributing

Issues and pull requests are welcome. For anything non-trivial, open an issue first so the design can be discussed — the tick/reconciliation loop is sensitive to small changes.

## License

There is no license file yet, which technically means all rights reserved. If you want others to use this, add one (MIT or Apache-2.0 both fit the goals of the project).

---

*This README was generated by AI. The code it documents was written by hand.*
