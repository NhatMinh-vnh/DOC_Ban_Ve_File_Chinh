using System.Collections.Generic;

namespace MyAutoCAD2026Plugin
{
    /// <summary>
    /// Lớp chính đại diện cho toàn bộ báo cáo phân tích.
    /// Đây sẽ là gốc của cây JSON.
    /// </summary>
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

    /// <summary>
    /// Lớp đại diện cho báo cáo của một loại đối tượng đơn lẻ.
    /// Ví dụ: { "Type": "Line", "Count": 50 }
    /// </summary>
    public class EntityReport
    {
        public string Type { get; set; }
        public int Count { get; set; }
    }

    /// <summary>
    /// Lớp đại diện cho báo cáo của một loại block.
    /// Chứa thông tin về số lần chèn và danh sách các đối tượng con bên trong.
    /// </summary>
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