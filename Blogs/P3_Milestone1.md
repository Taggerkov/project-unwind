# Milestone 1: The Tick System

The first milestone for Project Unwind had no direct visual feedback, yet it is the system with the most impact on the game.
A fighting game is deeply rooted in frame data, and frame data only means something if every frame is the same
length. Unity's `Update` is not, so before any combat code existed I built the clock everything else would run on.

## Why?

`Update` fires once per rendered frame, and that interval moves with the framerate. Gating logic behind `Time.deltaTime`
smooths movement but gives no discrete, countable frames, and "7 frame startup" has to be 7 of something fixed. So
`TickManager` keeps its own clock at a constant 60 Hz and treats Unity's frame loop only as a time source.

**The accumulator**: each `Update` I add scaled `Time.deltaTime` to an accumulator and drain it in fixed `TickInterval` slices.
If a frame runs long, two ticks fire to catch up; if it runs short, none fire and the leftover carries over. Ticks are
never partial; the simulation advances in whole 60ths of a second regardless of what the display is doing.

## Phase Ordering

Each tick runs three phases in order: `InputTick`, `LogicTick`, `UITick`. The catch is that every phase runs across
*all* tickables before the next begins, not one object at a time. Input is sampled for everyone, then logic resolves
for everyone, then UI reacts, so no combatant ever reads another's half-updated state inside the same tick. Resolution
stays deterministic, not order-dependent.

Only the `CombatManager` implements `ITickable<TickManager>` directly. It relays the same three phases down to its own
`ITickable<CombatManager>` objects (combatants, camera, combat UI), so the whole tree ticks in lockstep. The milestone
rule I held to: nothing simulates outside the tick. If it touches game state, it ticks.

## Time Scale & Interpolation

Because timescale feeds straight into the accumulator, `SetTimeScale` just multiplies what goes in. Halving it halves
how fast ticks arrive, which is global slow-motion; I drive it from a dev console while testing.
(Per-hit hitstop is a separate frame freeze inside the combat loop, not a timescale trick, so it is not counted here.)

The clock also hands out an `alpha` (how far we are between the last two ticks) to anything implementing
`IInterpolatable`, the hook for decoupling render rate from the fixed 60 Hz sim. Nothing consumes it yet (KCC runs its
own interpolation for now), so wiring visuals to it is still ahead. What works today is the manual path:
flip `SetAutoTick` off and call `ForceTickAndInterpolate` to step one tick at a time and read frame data while debugging.

## Benefits

Invisible, but the spine. Deterministic trades, exact startup and recovery counts, hitstop, frame stepping: all of it
falls out of getting this one piece right early, and the same inputs land the same way every run. Milestone 2, the
combat system, plugs straight into `ITickable` and stops worrying about wall-clock time.

---
Author: Taggerkov  
Updated: 23/05/26  
Source: [GitHub](https://github.com/Taggerkov/project-unwind)