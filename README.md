# Memory Path Game

**Memory Path Game** is a puzzle game developed in Unity that challenges players to remember and navigate hidden routes.

## Purpose

The primary goal of this project is to create an engaging cognitive exercise that tests and improves the player's short-term memory and spatial awareness. It is designed to be a fun, interactive way to train the brain while offering a progressively challenging gameplay experience.

## How It Works

The game system operates on a simple but effective core loop:
1. **Observation Phase:** At the start of each level, a specific safe path is briefly highlighted on a grid.
2. **Memory Phase:** After a few seconds, the path is completely hidden from view.
3. **Execution Phase:** The player must rely entirely on their memory to navigate across the correct tiles. Stepping on an incorrect tile will reset the level, while successfully reaching the end point unlocks the next, more complex stage.

Under the hood, the system is powered by C# scripts in Unity that handle grid generation, path randomization, player input detection, and level progression management.

## Gameplay

**Memory Path Game** offers an intuitive yet challenging experience. The core mechanic revolves around memorization and precise movement. Players navigate through a grid-based environment trying to recall the hidden path. 
- **Level Progression:** Each level presents a more complex path or larger grid than the last, testing the limits of your short-term memory.
- **Precision and Consequence:** Stepping off the correct path results in failure, requiring the player to restart the level and try again.

## Camera Tracking System

To enhance the player experience, the game features a dynamic **Camera Tracking System**. This system ensures that the player's focus remains on the action without needing manual camera adjustments:
- **Smooth Follow:** The camera smoothly tracks the player's movement across the grid, preventing abrupt visual changes and maintaining a seamless flow.
- **Optimal Framing:** It adjusts to keep both the player character and the immediate surrounding tiles within the camera's view. This ensures that the player always has the necessary visual context to make their next move, which is especially important on larger and more complex levels.
