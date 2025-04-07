# Code Samples for Pac's Revenge

Pac's Revenge is a 2.5D retro-style FPS game that takes inspiration from classic games such as Pac-Man and Doom.

Below are some code samples for some of the systems I designed and created for the game.

I prioritize following best code practices and ensuring readability through the use of clean comments.

---
## Boss Script C#

This is a finite state machine script with direct-pathing towards the player.

The script supports 2 boss phases, with 4 different projectile attacks and 1 melee attack.

The boss randomly chooses a projectile attack to do based on available attacks that dynamically change throughout the fight.

I designed this boss to be the game's final fight and an engaging and immersive experience.


## Map Script C#

This foundational script creates a data representation of the map by procedurally populating a 2D grid array by referencing 3D game objects and converting their 3D coordinates to 2D indices.

Contains many support functions for sampling the grid and converting to and from grid coordinates (2D indices).

Contains the ability to "corrupt" the map using recursion to change a map dynamically as the game progresses. Used in the endless mode.

I designed this script to be used as the basis for many other systems, such as the enemy AI pathfinding.


## Weapon Script C#

This is an abstract class representation of a weapon.

Takes advantage of inheritance to allow for the modular implementation of multiple weapons with the rest of the game systems.

Uses scriptable objects to outline base weapons stats.
