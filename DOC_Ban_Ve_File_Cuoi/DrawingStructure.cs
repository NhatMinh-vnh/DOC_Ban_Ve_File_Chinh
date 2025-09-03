using System.Collections.Generic;

namespace MyAutoCAD2026Plugin
{
    public class AnalysisReport
    {
        public List<EntityReport> Entities { get; set; }
        public List<BlockReport> Blocks { get; set; }

        public AnalysisReport()
        {
            Entities = new List<EntityReport>();
            Blocks = new List<BlockReport>();
        }
    }

    public class EntityReport
    {
        public string Type { get; set; }
        public int Count { get; set; }
    }

    public class BlockReport
    {
        public string Name { get; set; }
        public int InstanceCount { get; set; }
        public List<EntityReport> Contents { get; set; }

        public BlockReport()
        {
            Contents = new List<EntityReport>();
        }
    }
}