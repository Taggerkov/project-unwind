# Roll-a-Ball: Building My First Unity Scene

Roll-a-Ball was the first real hands-on project for this course, and the starting point was Unity's own official Roll-a-Ball tutorial. Rather than following it step by step, I used it more as a loose guide, implementing the basics while swapping things out or adding on top whenever something felt like a better learning opportunity.

## Extras

Once the basics were working, the project became more of a playground. A few things worth mentioning:

**New Input System**: Unity projects now come built-in with Input settings, I built it into a script file and used it for my own scripts instead of the course recommendation. Made this choice as Editor complained of being deprecated and it would be a more modern way.

**Animated character**: the enemy (Warden) uses a Mixamo Paladin model with a walking animation, which involved importing an `.fbx`, setting up an Animator Controller, and blending states. Nothing complex but good first contact with how models and animations work.

**Lava shader**: the area surrounding the play field (aka DeathBox) uses a custom HLSL shader ported and adapted from a GLSL Shadertoy shader by nimitz. It uses layered noise octaves with displacement fields to simulate flowing lava. Getting it to work in Unity's URP required rewriting it for HLSL and adding a URP-compatible SubShader structure. This was completely unnecessary but I always had curiosity with what a shader was and it was worth.

**Scene decoration**: added many assets from the Asset Store to give the scene more immersion. Played around by combining assets into Prefabs and treating them as unitary while making most decorative ones Static.

**Replayability**: made the scene replayable without a true scene reload. Instead every time the game start states are reset, this means coins are redisplayed, score reset and moving entities destroyed before respawning on their respective point.

## What Actually Taught Me the Most

The most valuable thing was implementing a proper game reset without reloading the scene. This forced me to think about state management programmatically: spawning and destroying entities through spawners, re-enabling coins with `FindObjectsByType` including inactive ones, resetting obstacle positions, and managing all of it through a central `GameManager` singleton. Hardcoding references in the scene would have been easier, but going through the `GameManager` made it clear how to query and manipulate assets at runtime. And of course, special mention to the shader, though I had personal help with the process.

## Screenshots

### Look overview
![Player spawning.](Resources/P1_overview.png)
### Action feel
![Player stunt jump.](Resources/P1_action.png)

---
Author: Taggerkov  
Date: 02/03/26  
Source: [GitHub](https://github.com/Taggerkov/Roll-a-Ball)  
Host: [Pages](https://taggerkov.github.io/Roll-a-Ball/)