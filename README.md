Hexagon Minecraft in Unity!
Programmed in one week, 1/22/2024-1/29/2024.
All game specific scripts written by Eli Asimow.
FPS Controller Script taken from https://www.sharpcoderblog.com/blog/unity-3d-fps-controller.

Features:

An infinite World! Generated via Perlin World, the map is cut into 8x8 hex 'chunks' for efficient loading and unloading.

Multithreaded World Generation! Using the C# Job system, via Unity's Burst framework, I was able to generate the world in parallel to the main cpu thread. This minimizes the impact of fast traversal on the frames per second.

Efficient Mesh Generation! Only sides of the blocks that are exposed are added to the chunk's mesh and rendered. This includes considerations of block placement in adjacent chunks, and updates with every block placement / deletion. 

Installation:
If you simply want to play Hexacraft, you can download the Windows/Mac build zip files. Then, clicking the executable should start the game.
Screenshots and a direct download can be found at https://easimow.itch.io/hexagon-minecraft
