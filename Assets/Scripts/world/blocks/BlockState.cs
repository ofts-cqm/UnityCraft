using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using World.blocks;

namespace world.blocks
{
    // Unity compiles this project as C# 9, which predates record structs. Keep the same
    // value/equality behavior explicitly while avoiding a managed allocation per lookup.
    public readonly struct BlockState : IEquatable<BlockState>
    {
        public Vector3Int Position { get; }
        public Block Block { get; }
        public object Data { get; }

        public BlockState(Vector3Int position, Block block, object data)
        {
            Position = position;
            Block = block;
            Data = data;
        }

        public BlockState(int x, int y, int z, Block block, [CanBeNull] object data = null)
        : this(new Vector3Int(x, y, z), block, data ?? block.DefaultState)
        {
        }

        public bool IsAir => Block.IsAir;

        public bool Equals(BlockState other)
        {
            return Position.Equals(other.Position) && EqualityComparer<Block>.Default.Equals(Block, other.Block) &&
                   EqualityComparer<object>.Default.Equals(Data, other.Data);
        }

        public override bool Equals(object obj) => obj is BlockState other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Position, Block, Data);
        public static bool operator ==(BlockState left, BlockState right) => left.Equals(right);
        public static bool operator !=(BlockState left, BlockState right) => !left.Equals(right);

        public void Deconstruct(out Vector3Int position, out Block block, out object data)
        {
            position = Position;
            block = Block;
            data = Data;
        }
    }
}
