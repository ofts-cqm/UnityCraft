using JetBrains.Annotations;
using World.blocks;

namespace world.blocks
{
    public interface IBatchableBlock
    {
        Block CloneAndRegister(int id, BlockProperty block);
        BlockProperty GetProperty();
    }

    public record BatchableBlock : Block, IBatchableBlock
    {
        public BatchableBlock(int blockId, BlockProperty property, [CanBeNull] object defaultState = null) : base(blockId, property, defaultState)
        {
        }

        public Block CloneAndRegister(int id, BlockProperty block)
        {
            return new Block(id, block);
        }

        public BlockProperty GetProperty() => Property;
    }

    public class ColoredBlocks
    {
        public readonly Block White;
        public readonly Block LightGray;
        public readonly Block Gray;
        public readonly Block Black;
        public readonly Block Brown;
        public readonly Block Red;
        public readonly Block Orange;
        public readonly Block Yellow;
        public readonly Block Lime;
        public readonly Block Green;
        public readonly Block Cyan;
        public readonly Block LightBlue;
        public readonly Block Blue;
        public readonly Block Purple;
        public readonly Block Magenta;
        public readonly Block Pink;

        public ColoredBlocks(int id, IBatchableBlock template)
        {
            BlockProperty baseProperty = template.GetProperty();
            int baseTexture = baseProperty.Texture[0];
            White = template as Block;
            LightGray = template.CloneAndRegister(id + 1,  baseProperty.SetTexture(baseTexture + 1));
            Gray      = template.CloneAndRegister(id + 2,  baseProperty.SetTexture(baseTexture + 2));
            Black     = template.CloneAndRegister(id + 3,  baseProperty.SetTexture(baseTexture + 3));
            Brown     = template.CloneAndRegister(id + 4,  baseProperty.SetTexture(baseTexture + 4));
            Red       = template.CloneAndRegister(id + 5,  baseProperty.SetTexture(baseTexture + 5));
            Orange    = template.CloneAndRegister(id + 6,  baseProperty.SetTexture(baseTexture + 6));
            Yellow    = template.CloneAndRegister(id + 7,  baseProperty.SetTexture(baseTexture + 7));
            Lime      = template.CloneAndRegister(id + 8,  baseProperty.SetTexture(baseTexture + 8));
            Green     = template.CloneAndRegister(id + 9,  baseProperty.SetTexture(baseTexture + 9));
            Cyan      = template.CloneAndRegister(id + 10, baseProperty.SetTexture(baseTexture + 10));
            LightBlue = template.CloneAndRegister(id + 11, baseProperty.SetTexture(baseTexture + 11));
            Blue      = template.CloneAndRegister(id + 12, baseProperty.SetTexture(baseTexture + 12));
            Purple    = template.CloneAndRegister(id + 13, baseProperty.SetTexture(baseTexture + 13));
            Magenta   = template.CloneAndRegister(id + 14, baseProperty.SetTexture(baseTexture + 14));
            Pink      = template.CloneAndRegister(id + 15, baseProperty.SetTexture(baseTexture + 15));
        }
    }
    
    public class WoodBlocks
    {
        public readonly Block Acacia;
        public readonly Block Bamboo;
        public readonly Block Birch;
        public readonly Block Cherry;
        public readonly Block DarkOak;
        public readonly Block Jungle;
        public readonly Block Mangrove;
        public readonly Block Oak;
        public readonly Block PaleOak;
        public readonly Block Spruce;

        public WoodBlocks(int id, IBatchableBlock template, int legacyId = -1)
        {
            Acacia = template as Block;
            BlockProperty baseProperty = template.GetProperty();
            Bamboo   = template.CloneAndRegister(++id, baseProperty.OffsetTexture(3));
            Birch    = template.CloneAndRegister(++id, baseProperty.OffsetTexture(6));
            Cherry   = template.CloneAndRegister(++id, baseProperty.OffsetTexture(9));
            DarkOak  = template.CloneAndRegister(++id, baseProperty.OffsetTexture(15));
            Jungle   = template.CloneAndRegister(++id, baseProperty.OffsetTexture(18));
            Mangrove = template.CloneAndRegister(++id, baseProperty.OffsetTexture(21));
            Oak      = template.CloneAndRegister(legacyId == -1 ? ++id : legacyId,  baseProperty.OffsetTexture(24));
            PaleOak  = template.CloneAndRegister(++id, baseProperty.OffsetTexture(27));
            Spruce   = template.CloneAndRegister(++id, baseProperty.OffsetTexture(30));
        }
    }
}