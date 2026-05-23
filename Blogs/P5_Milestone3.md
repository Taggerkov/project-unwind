# Milestone 3: The Playable Core

Milestone 1 built the clock, Milestone 2 built the fighting. Milestone 3 is wrapping both into something you can
actually sit down and play: pick a character, fight a round, have someone win. The headline piece is the one that makes
a single-player session worth anything, an opponent that fights back.

## Picking a Fighter

The character select runs both players through the same screen, navigated by stick and buttons rather than a mouse,
which the arcade panel requires. Each player drives their own cursor, locks in a combatant, a stage gets chosen, and
the session hands those choices to the combat scene. It is the front door to everything the previous two milestones
built.

## An Opponent

`CpuInputProvider` is the main AI implementation. It is a priority state machine that decides one thing per tick, in
order: if it is in hitstun or blockstun it can only hold guard; if it is mid-motion it finishes the input sequence;
otherwise it considers defending, then attacking, then repositioning.

Defence is reactive. It subscribes to the opponent's move-start event, looks the move up in a defence hint sheet to
decide whether it is a real threat, then rolls against a guard-sensitivity stat and waits out a reaction delay before
committing to block. That delay is deliberate: an AI that guards on the exact frame is inhuman and unfun. Offence works
off a move hint sheet where each entry carries a range bracket, a priority, and a cooldown, so the AI picks the
best move that actually reaches at the current spacing, gated by an aggression roll. All of it tunes through a
personality object, so the same code produces a cautious zoner or a reckless rushdown by swapping numbers.

## Rounds

A match is best-of-three. A round ends on a KO or, if the timer runs out, on whoever has more health. The HUD shows the
two health bars and the round timer, updated every tick off the combat clock. The set tracks rounds won on each side
and starts the next one until somebody reaches two.

## Sound

Music is playlist-driven per scene, so the combat scene activates its own loop on entry. Sound effects are triggered
from inside the move scripts themselves, preloaded up front so the first hit does not stall waiting on a load.

## Still Ahead

The session loop has honest holes. When a set ends it currently bounces straight back to the main menu: there is no win
screen and no rematch yet, both of which are next. The HUD is also only half its planned self, the Stamina bar and the
Unique Ability indicator are not in, because neither system exists yet to back them. They land alongside the Stamina
work carried over from Milestone 2.

---
Author: Taggerkov  
Updated: 23/05/26  
Source: [GitHub](https://github.com/Taggerkov/project-unwind)
