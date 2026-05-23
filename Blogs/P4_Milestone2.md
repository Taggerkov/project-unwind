# Milestone 2: The Combat System

Milestone 1 built the clock. Milestone 2 is everything that happens on it. The goal: author moves that read as exact
frame data to the engine but write like a short script, plus the cancel rules and hit detection that turn isolated
moves into real interactions.

## Moves as Scripts

Every move is a `Script()` coroutine with exactly one blocking primitive: `yield return Pose(id, ticks)`, which holds a
pose for N ticks. Everything else (opening hitboxes, adding velocity, opening cancel windows, hit reactions) is an
instant DSL call that runs at the transition between poses. `MoveRunner` drives it: on start it runs everything before
the first yield, then advances one pose each time the tick counter hits zero. So the move reads top to bottom like a
timeline, but the engine only ever sees discrete tick-aligned state changes. `BeginActiveState()` flips the hitbox
phase on exactly the tick its pose begins, which keeps startup and active frames honest.

## The Cancel Ladder

The interesting part of any fighting game is not the moves, it is when you are allowed to leave one. Neutral-commit
moves (walking, idle) sit on their own track: they never lock you out, so any real move preempts them outright.
Committed attacks are the real case. There `TryCancel` runs every active tick and checks four rules in strict priority
order, taking the first that wins the input contest:

1. **Kara-cancel**: a 3-tick startup window into the same tier or higher, so a grab input where the second button
   lands a frame late still comes out as the grab and not the lone normal.
2. **IASA**: a window the move opened itself, cancellable into anything.
3. **Gatling**: only on the tick a hit or guard connects, into an explicit whitelist plus the tier ladder
   (Normal into Special into Overdrive).
4. **Whiff cancel**: recovery only, no hit confirmed, into a hand-picked list.

Ordering them this way means a hit-confirm never loses to a whiff route, and kara resolves messy multi-button inputs
first.

## Hitboxes and Trades

Each tick, every combatant registers its hitboxes and hurtboxes with `CombatOverlapSolver`. It walks the pairs, keeps
the ones that overlap, and dedupes through a per-attack `HitId` registry so one move lands once on a target however
long its hitbox is live. The surviving list goes to `CombatManager`, which is the only thing that sees
both sides, so it resolves each into hit, block, knockback and hitstop. Reciprocal overlaps on the same tick are not a
special case: both connect, and a trade falls straight out of the determinism.

## Still Ahead

One scoped piece is not in yet: Stamina and the Stamina Break state. It is the signature mechanic meant to replace
combo scaling, so it is deliberately not something I wanted to rush into the same milestone as the engine plumbing. HP,
cancels, and hit resolution are all live; the stamina economy sits on top of them and is next.

---
Author: Taggerkov  
Updated: 23/05/26  
Source: [GitHub](https://github.com/Taggerkov/project-unwind)
