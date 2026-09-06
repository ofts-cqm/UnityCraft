using UnityEngine;
using render;
using Render;
using world.blocks;

namespace World.blocks
{
    public struct FluidState
    {
        private byte _amount;
        public byte Amount { get => _amount == 10 ? (byte)8 : _amount; init => _amount = value; }
        internal byte RawAmount => _amount;
        public bool IsEmpty => _amount == 0;
        public bool IsSource => _amount == 8;
        public bool IsFalling => _amount == 10;
        public float OwnHeight => Amount / 9f;
        public static FluidState Source => new() { Amount = 8 };
        public static FluidState FallingState() => new() { _amount = 10 };
    }

    public static class Water
    {
        public const int TickDelay = 12;
        private const float Offset = .001f;
        private static readonly Vector4 StillTexture = new(32, 8, 16, 1);
        private static readonly Vector4 FlowingTexture = new(48, 8, 16, 1);
        private static readonly Vector3Int[] Horizontal = { Vector3Int.left, Vector3Int.right, Vector3Int.forward, Vector3Int.back };

        public static float OwnHeight(FluidState state) => state.Amount / 9f;
        public static float EffectiveHeight(Chunk chunk, Vector3Int p) => chunk.GetFluid(p + Vector3Int.up).IsEmpty ? OwnHeight(chunk.GetFluid(p)) : 1f;

        public static Vector3 GetFlow(Chunk chunk, Vector3Int p)
        {
            FluidState state = chunk.GetFluid(p);
            if (state.IsEmpty) return Vector3.zero;
            float own = EffectiveHeight(chunk, p);
            Vector3 result = Vector3.zero;
            foreach (Vector3Int d in Horizontal)
            {
                Vector3Int q = p + d;
                FluidState neighbor = chunk.GetFluid(q);
                if (!neighbor.IsEmpty) result += new Vector3(d.x, 0, d.z) * (own - EffectiveHeight(chunk, q));
                else if (chunk.GetBlock(q).IsAir && !chunk.GetFluid(q + Vector3Int.down).IsEmpty)
                    result += new Vector3(d.x, 0, d.z) * (own - (EffectiveHeight(chunk, q + Vector3Int.down) - 8f / 9f));
            }
            if (state.IsFalling)
                foreach (Vector3Int d in Horizontal)
                    if (Solid(chunk, p + d, Opposite(Face(d))) || Solid(chunk, p + d + Vector3Int.up, Opposite(Face(d)))) { result += Vector3.down * 6; break; }
            return result.sqrMagnitude < .000001f ? Vector3.zero : result.normalized;
        }

        public static void Tick(Chunk chunk, Vector3Int p)
        {
            FluidState current = chunk.GetFluid(p);
            if (current.IsEmpty) return;
            if (!current.IsSource)
            {
                FluidState next = Recalculate(chunk, p);
                if (next.RawAmount != current.RawAmount)
                {
                    chunk.SetFluid(p, next, false);
                    current = next;
                    if (!current.IsEmpty) chunk.ScheduleFluidTick(p, TickDelay);
                    chunk.ScheduleFluidNeighbors(p);
                    if (current.IsEmpty) return;
                }
            }
            Spread(chunk, p);
        }

        private static FluidState Recalculate(Chunk chunk, Vector3Int p)
        {
            int sources = 0, best = 0;
            foreach (Vector3Int d in Horizontal)
            {
                FluidState n = chunk.GetFluid(p + d);
                if (n.IsEmpty) continue;
                if (n.IsSource) sources++;
                best = Mathf.Max(best, ComputeFlowAmount(chunk, p + d, p, Opposite(Face(d))));
            }
            FluidState below = chunk.GetFluid(p + Vector3Int.down);
            if (sources >= 2 && (below.IsSource || Solid(chunk, p + Vector3Int.down, ChunkRenderObject.TopFace))) return FluidState.Source;
            if (!chunk.GetFluid(p + Vector3Int.up).IsEmpty && ComputeFlowAmount(chunk, p + Vector3Int.up, p, ChunkRenderObject.BottomFace) > 0) return FluidState.FallingState();
            return new FluidState { Amount = (byte)best };
        }

        private static void Spread(Chunk chunk, Vector3Int p)
        {
            Vector3Int down = p + Vector3Int.down;
            int downwardAmount = ComputeFlowAmount(chunk, p, down, ChunkRenderObject.BottomFace);
            bool sideways;
            if (!chunk.GetFluid(down).IsSource && downwardAmount > 0)
            {
                chunk.SetFluid(down, FluidState.FallingState());
                int sources = 0;
                foreach (Vector3Int d in Horizontal) if (chunk.GetFluid(p + d).IsSource) sources++;
                sideways = sources >= 3;
            }
            // Once a flow reaches a floor it continues outward, regardless of whether this cell is a source.
            else sideways = true;
            if (!sideways) return;
            foreach (Vector3Int d in Horizontal)
            {
                Vector3Int target = p + d;
                int amount = ComputeFlowAmount(chunk, p, target, Face(d));
                FluidState old = chunk.GetFluid(target);
                if (amount > 0 && !old.IsSource && old.Amount < amount) chunk.SetFluid(target, new FluidState { Amount = (byte)amount });
            }
        }

        private static int ComputeFlowAmount(Chunk chunk, Vector3Int source, Vector3Int target, int sourceFace)
        {
            FluidState from = chunk.GetFluid(source);
            if (from.IsEmpty) return 0;
            BlockState sourceBlock = chunk.GetBlock(source);
            (int max, int min) = sourceBlock.Block.GetFlowingAmountLimit(sourceBlock, sourceFace);
            int suggested = from.Amount < min ? 0 : from.Amount > max ? max : from.Amount - 1;
            if (suggested <= 0) return 0;
            BlockState targetBlock = chunk.GetBlock(target);
            if (targetBlock.Block.BlockId == Blocks.Void.BlockId) return 0;
            (max, min) = targetBlock.Block.GetFlowingAmountLimit(targetBlock, Opposite(sourceFace));
            return suggested < min ? 0 : Mathf.Min(suggested, max);
        }

        public static void Render(Chunk chunk, MeshBuilder builder, Vector3Int p, Vector3 local)
        {
            if (chunk.GetFluid(p).IsEmpty) return;
            float[] h = CornerHeights(chunk, p);
            if (chunk.GetFluid(p + Vector3Int.up).IsEmpty && !TopOccluded(chunk, p, h))
            {
                Vector3[] top = { new(Offset, h[0], Offset), new(Offset, h[1], 1 - Offset), new(1 - Offset, h[2], Offset), new(1 - Offset, h[3], 1 - Offset) };
                Vector3 flow = GetFlow(chunk, p);
                Vector4 texture = flow == Vector3.zero ? StillTexture : FlowingTexture;
                Add(builder, top, local, TopUvs(flow), texture);
            }
            AddSide(chunk, builder, p, local, Vector3Int.left, h[0], h[1]);
            AddSide(chunk, builder, p, local, Vector3Int.right, h[2], h[3]);
            AddSide(chunk, builder, p, local, Vector3Int.forward, h[1], h[3]);
            AddSide(chunk, builder, p, local, Vector3Int.back, h[0], h[2]);
            BlockState own = chunk.GetBlock(p);
            BlockState below = chunk.GetBlock(p + Vector3Int.down);
            if (chunk.GetFluid(p + Vector3Int.down).IsEmpty &&
                !own.Block.IsSolid(own, ChunkRenderObject.BottomFace) &&
                !below.Block.IsSolid(below, ChunkRenderObject.TopFace))
                Add(builder, new[] { new Vector3(Offset, Offset, Offset), new Vector3(1 - Offset, Offset, Offset), new Vector3(Offset, Offset, 1 - Offset), new Vector3(1 - Offset, Offset, 1 - Offset) }, local, SquareUvs(), FlowingTexture, true);
        }

        private static void AddSide(Chunk c, MeshBuilder b, Vector3Int p, Vector3 local, Vector3Int d, float a, float z)
        {
            Vector3Int q = p + d;
            if (!c.GetFluid(q).IsEmpty || Solid(c, p, Face(d)) || Solid(c, q, Opposite(Face(d)))) return;
            Vector3[] v = d == Vector3Int.left ? new[] { new Vector3(Offset, 0, Offset), new Vector3(Offset, 0, 1 - Offset), new Vector3(Offset, a, Offset), new Vector3(Offset, z, 1 - Offset) } :
                d == Vector3Int.right ? new[] { new Vector3(1 - Offset, 0, 1 - Offset), new Vector3(1 - Offset, 0, Offset), new Vector3(1 - Offset, z, 1 - Offset), new Vector3(1 - Offset, a, Offset) } :
                d == Vector3Int.forward ? new[] { new Vector3(Offset, 0, 1 - Offset), new Vector3(1 - Offset, 0, 1 - Offset), new Vector3(Offset, a, 1 - Offset), new Vector3(1 - Offset, z, 1 - Offset) } :
                new[] { new Vector3(1 - Offset, 0, Offset), new Vector3(Offset, 0, Offset), new Vector3(1 - Offset, z, Offset), new Vector3(Offset, a, Offset) };
            Vector2[] uvs = SideUvs(a, z);
            Add(b, v, local, uvs, FlowingTexture); Add(b, v, local, uvs, FlowingTexture, true);
        }

        private static float[] CornerHeights(Chunk c, Vector3Int p)
        {
            float own = EffectiveHeight(c, p); if (own >= 1) return new[] { 1f, 1f, 1f, 1f };
            return new[] { Corner(c,p,Vector3Int.left,Vector3Int.back,new(-1,0,-1),own), Corner(c,p,Vector3Int.left,Vector3Int.forward,new(-1,0,1),own), Corner(c,p,Vector3Int.right,Vector3Int.back,new(1,0,-1),own), Corner(c,p,Vector3Int.right,Vector3Int.forward,new(1,0,1),own) };
        }
        private static float Corner(Chunk c, Vector3Int p, Vector3Int a, Vector3Int b, Vector3Int diagonal, float own)
        {
            float x = HeightOrSolid(c,p+a), y = HeightOrSolid(c,p+b); if (x >= 1 || y >= 1) return 1;
            float sum = own >= .8f ? own * 10 : own; int weight = own >= .8f ? 10 : 1;
            void Include(float h) { if (h >= 0) { int w = h >= .8f ? 10 : 1; sum += h*w; weight += w; } }
            Include(x); Include(y);
            if (!c.GetFluid(p+a).IsEmpty || !c.GetFluid(p+b).IsEmpty) { float d = HeightOrSolid(c,p+diagonal); if (d >= 1) return 1; Include(d); }
            return sum / weight;
        }
        private static float HeightOrSolid(Chunk c, Vector3Int p) => !c.GetFluid(p).IsEmpty ? EffectiveHeight(c,p) : Solid(c,p,ChunkRenderObject.TopFace) ? -1 : 0;
        private static bool TopOccluded(Chunk c, Vector3Int p, float[] heights)
        {
            // A full-height surface is hidden by an intersecting solid underside. Partial water remains visible below a cube.
            if (heights[0] < 1 || heights[1] < 1 || heights[2] < 1 || heights[3] < 1) return false;
            BlockState above = c.GetBlock(p + Vector3Int.up);
            if (!above.Block.IsSolid(above, ChunkRenderObject.BottomFace)) return false;
            (Vector3 half, Vector3 center) = above.Block.GetBoundingBox(p + Vector3Int.up, above.Data);
            return center.y - half.y <= p.y + 1.001f;
        }
        private static bool Solid(Chunk c, Vector3Int p, int face) { BlockState b=c.GetBlock(p); return b.Block.IsSolid(b,face); }
        private static void Add(MeshBuilder b, Vector3[] vertices, Vector3 local, Vector2[] uvs, Vector4 texture, bool reverse=false) { for (int i=0;i<4;i++) vertices[i]+=local; b.AddTransparentQuad(vertices,uvs,texture,reverse); }
        // Top vertices are ordered x/z as (0,0), (0,1), (1,0), (1,1).
        private static Vector2[] SquareUvs() => new[] { new Vector2(0,0), new Vector2(0,1), new Vector2(1,0), new Vector2(1,1) };
        private static Vector2[] SideUvs(float firstHeight, float secondHeight) => new[] { new Vector2(0,0), new Vector2(1,0), new Vector2(0,firstHeight), new Vector2(1,secondHeight) };
        private static Vector2[] TopUvs(Vector3 flow) { if (flow == Vector3.zero) return SquareUvs(); float angle=Mathf.Atan2(-flow.z,flow.x)-Mathf.PI/2; Vector2 Rotate(Vector2 v) { v-=Vector2.one*.5f; float s=Mathf.Sin(angle),co=Mathf.Cos(angle); return new Vector2(v.x*co-v.y*s,v.x*s+v.y*co)+Vector2.one*.5f; } return new[] {Rotate(new(0,0)),Rotate(new(0,1)),Rotate(new(1,0)),Rotate(new(1,1))}; }
        private static int Face(Vector3Int d) => d==Vector3Int.left?ChunkRenderObject.LeftFace:d==Vector3Int.right?ChunkRenderObject.RightFace:d==Vector3Int.forward?ChunkRenderObject.FrontFace:d==Vector3Int.back?ChunkRenderObject.BackFace:d==Vector3Int.up?ChunkRenderObject.TopFace:ChunkRenderObject.BottomFace;
        private static int Opposite(int f) => f==ChunkRenderObject.LeftFace?ChunkRenderObject.RightFace:f==ChunkRenderObject.RightFace?ChunkRenderObject.LeftFace:f==ChunkRenderObject.FrontFace?ChunkRenderObject.BackFace:f==ChunkRenderObject.BackFace?ChunkRenderObject.FrontFace:f==ChunkRenderObject.TopFace?ChunkRenderObject.BottomFace:ChunkRenderObject.TopFace;
    }
}
