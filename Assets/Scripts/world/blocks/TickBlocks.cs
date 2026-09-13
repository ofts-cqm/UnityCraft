using UnityEngine;
using BlockState = world.blocks.BlockState;
using Blocks = world.blocks.Blocks;

namespace World.blocks
{
    public sealed record GravityBlock(int Id, world.blocks.BlockProperty Properties) : Block(Id, Properties)
    {
        public const int UpdateDelay = 2;

        public override int? BlockUpdateDelayTicks => UpdateDelay;

        public override void OnBlockUpdate(World world, BlockState state)
        {
            world.TryStartFallingBlock(state);
        }
    }

    public sealed record GrassBlock(int Id, world.blocks.BlockProperty Properties) : Block(Id, Properties)
    {
        public override bool ReceivesRandomTicks => true;

        public override void OnRandomTick(World world, BlockState state)
        {
            Vector3Int above = state.Position + Vector3Int.up;
            if (world.IsGrassBlocked(above)) world.SetBlock(state.Position, Blocks.Dirt);
        }
    }

    public sealed record DirtBlock(int Id, world.blocks.BlockProperty Properties) : Block(Id, Properties)
    {
        public override bool ReceivesRandomTicks => true;

        public override void OnRandomTick(World world, BlockState state)
        {
            if (world.IsGrassBlocked(state.Position + Vector3Int.up)) return;

            for (int y = -1; y <= 1; y++)
            for (int z = -1; z <= 1; z++)
            for (int x = -1; x <= 1; x++)
            {
                if (x == 0 && y == 0 && z == 0) continue;
                Vector3Int candidate = state.Position + new Vector3Int(x, y, z);
                BlockState neighbor = world.GetBlock(candidate);
                if (neighbor.Block.BlockId != Blocks.GrassBlock.BlockId ||
                    world.IsGrassBlocked(candidate + Vector3Int.up)) continue;
                world.SetBlock(state.Position, Blocks.GrassBlock);
                return;
            }
        }
    }

    public sealed record LeavesBlock(int Id, world.blocks.BlockProperty Properties) : Block(Id, Properties)
    {
        public override bool ReceivesRandomTicks => true;

        public override void OnRandomTick(World world, BlockState state)
        {
            if (world.ResolveLeafDistance(state.Position) == LeafDistanceCache.Disconnected)
                world.SetBlock(state.Position, Blocks.Air);
        }
    }
}
