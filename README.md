# What is this?

This is my personal practice project. It is a Minecraft game made in Unity. 
  
# What features do we have?

Currently, we have the following features ready:

- Movement (WASD & fly)
- Perlin Noise chunk generation
- Seeded world creation and world selection
- Persistent world, chunk, player, and inventory saves
- Biome generation based on continentality and temperature
- Async chunk loading
- Water rendering
- Glass rendering
- Hotbar rendering

*The following features are planned:*

- Complete Inventory
- More blocks (more trees, stairs, glasses and colored blocks)
- More biomes
- Lightings
- Settings screen

# What is the architecture?

All rendering is based on Unity build from scratch. The world is divided into 16x256x16 chunks, which are further divided into 16^3 subchunks. 
After generation, loading, or a player interaction that changes a block, a subchunk is marked dirty and will be updated on the next tick. 
Each dirty subchunk then rebuilds three meshes: the *opaque* mesh for opaque faces, 
the *transparent* mesh for glass and water, and the *collider* mesh used by the mesh collider. 

Inventory icons are baked at runtime before the game starts. A temporary camera bakes each block model into a 1024x1024 sprite. 

# Disclaimer:

NOT AN OFFICIAL MINECRAFT PRODUCT. NOT APPROVED BY OR ASSOCIATED WITH MOJANG OR MICROSOFT

This project does not contain any Minecraft assets. All textures used are recreated. 
