# What is this?

This is my personal practice project. It is a Minecraft game made in Unity. 
  
# What features do we have?

Currently, we have the following features ready:

- Movement (WASD & fly)
- Perlin Noise chunk generation
- Biome generation based on continentality and temperature
- Async chunk loading
- Water rendering
- Glass rendering
- Hotbar rendering

*The following features are planned:*

- Saving & save selection
- Inventory
- More blocks

# What is the architecture?

All rendering is based on Unity. The world is divided into 16x256x16 chunks, which are further divided into 16^3 subchunks. 
After generation, loading, or a player interaction that changes a block, a subchunk is marked dirty and will be updated on the next tick. 
Each dirty subchunk then rebuilds three meshes: the *opaque* mesh for opaque faces, 
the *transparent* mesh for glass and water, and the *collider* mesh used by the mesh collider. 

Inventory icons are baked at runtime before the game starts. A temporary camera bakes each block model into a 1024x1024 sprite. 

# Disclaimer:

NOT AN OFFICIAL MINECRAFT PRODUCT. NOT APPROVED BY OR ASSOCIATED WITH MOJANG OR MICROSOFT

This project uses some assets from Minecraft; however, it is for personal practice only and isn't built for commercial use, nor is it intended to make any money. 

According to the [EULA](https://www.minecraft.net/en-us/eula), the following behaviors are forbidden:

- give copies of our game software or content to anyone else;
- make commercial use of anything we've made;
- try to make money from anything we've made; or
- let other people get access to anything we've made in a way that is unfair or unreasonable.

This game does not include any of the above-mentioned behaviors. 

Notably, this repository does not include the texture folder to avoid sharing Minecraft content. 
