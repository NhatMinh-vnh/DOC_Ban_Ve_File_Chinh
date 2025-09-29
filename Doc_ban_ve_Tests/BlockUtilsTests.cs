using NUnit.Framework;
using MyAutoCAD2026Plugin.Backend;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.ApplicationServices;
using System.IO;

namespace MyAutoCAD2026Plugin.Tests
{
    [TestFixture]
    public class BlockUtilsTests
    {
        private Database _targetDb;
        private string _sourceDwgPath;

        [SetUp]
        public void Setup(Database db)
        {
            _targetDb = db;
            // === XÓA DÒNG NGUY HIỂM ===
            // HostApplicationServices.WorkingDatabase = _targetDb; // Dòng này đã được xóa.

            _sourceDwgPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".dwg");
        }

        [TearDown]
        public void Teardown()
        {
            // === XÓA DÒNG KHÔNG CẦN THIẾT ===
            // HostApplicationServices.WorkingDatabase = null; // Dòng này đã được xóa.

            if (File.Exists(_sourceDwgPath))
            {
                File.Delete(_sourceDwgPath);
            }
        }

        // SỬA LỖI TRONG HÀM HELPER
        private void CreateTestSourceDwgFile(string filePath, string blockName)
        {
            // Vẫn tạo database nguồn trong bộ nhớ.
            using (var sourceDb = new Database(true, true))
            {
                // === XÓA CÁC DÒNG NGUY HIỂM ===
                // Không được thay đổi WorkingDatabase toàn cục ở đây.
                // HostApplicationServices.WorkingDatabase = sourceDb;

                // Các thao tác bên dưới hoạt động trực tiếp trên đối tượng sourceDb
                // mà không cần nó phải là WorkingDatabase.
                using (var tr = sourceDb.TransactionManager.StartTransaction())
                {
                    var bt = tr.GetObject(sourceDb.BlockTableId, OpenMode.ForWrite) as BlockTable;
                    using (var btr = new BlockTableRecord())
                    {
                        btr.Name = blockName;
                        using (var line = new Line(Point3d.Origin, new Point3d(10, 10, 0)))
                        {
                            btr.AppendEntity(line);
                        }
                        bt.Add(btr);
                        tr.AddNewlyCreatedDBObject(btr, true);
                    }
                    tr.Commit();
                }
                sourceDb.SaveAs(filePath, DwgVersion.Current);

                // === XÓA DÒNG NGUY HIỂM ===
                // HostApplicationServices.WorkingDatabase = _targetDb;
            }
        }

        // Các bài test bên dưới không thay đổi.
        [Test] public void EnsureLayerExists_WhenLayerDoesNotExist_ShouldCreateIt() { /* ... */ }
        [Test] public void ImportBlockDefinition_WhenSourceIsValid_ShouldImportBlock() { /* ... */ }
        [Test] public void ImportBlockDefinition_WhenFileDoesNotExist_ShouldReturnNullId() { /* ... */ }
        [Test] public void InsertBlockReference_WithValidDefinition_ShouldInsertCorrectly() { /* ... */ }
    }
}
// Chú thích: Để giữ câu trả lời ngắn gọn, code các bài test không đổi được ẩn đi. 
// Bạn chỉ cần thay thế các phương thức Setup, Teardown, và CreateTestSourceDwgFile là đủ.