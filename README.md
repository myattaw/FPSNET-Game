# Multiplayer FFA FPS

A minimal free\-for\-all first\-person shooter prototype built in Unity.

## Description
Small FPS with basic movement, aiming and shooting. Designed for quick testing and local/network play prototypes.

## Controls
\- Move: WASD  
\- Look: Mouse  
\- Shoot: Left Mouse Button (`Fire1`)  
\- Reload: R  
\- Jump: Space  
\- Toggle cursor: Esc

## How to run
1. Open the project in Unity Editor.  
2. Open the main scene and press Play.  
3. For builds, use File \> Build Settings and build for your target platform.

## Networking notes
\- Player controllers should run input only for the local player.  
\- Spawn bullets and weapons on the server (or request server spawn) so they replicate to clients.  
\- Ensure authoritative actions (damage, spawn) are validated on the server.
