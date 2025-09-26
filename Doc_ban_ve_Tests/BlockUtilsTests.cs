using NUnit.Framework;
using MyAutoCAD2026Plugin.Backend; // Namespace chứa BlockUtils và LayerUtils
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.ApplicationServices;
using System.IO;

namespace MyAutoCAD2026Plugin.Tests
{
    [TestFixture]
    public class BlockUtilsTests
    {
        // Database đích cho các bài test, sẽ được tạo mới mỗi lần.
        private Database _targetDb;
        // Đường dẫn đến file DWG nguồn tạm thời, sẽ được tạo và xóa tự động.
        private string _sourceDwgPath;

        // *** SETUP VÀ TEARDOWN ***

        [SetUp]
        public void Setup()
        {
            // Tạo database đích trong bộ nhớ cho mỗi bài test.
            _targetDb = new Database(false, true);
            HostApplicationServices.WorkingDatabase = _targetDb;

            // Tạo một đường dẫn file tạm ngẫu nhiên và duy nhất.
            _sourceDwgPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".dwg");
        }

        [TearDown]
        public void Teardown()
        {
            // Dọn dẹp database và file tạm sau mỗi bài test.
            _targetDb.Dispose();
            HostApplicationServices.WorkingDatabase = null;
            if (File.Exists(_sourceDwgPath))
            {
                File.Delete(_sourceDwgPath);
            }
        }

        // *** HÀM HELPER ĐỂ TẠO FILE DWG NGUỒN ***

        /// <summary>
        /// (Helper) Tạo một file DWG tạm chứa một định nghĩa block đơn giản.
        /// </summary>
        private void CreateTestSourceDwgFile(string filePath, string blockName)
        {
            using (var sourceDb = new Database(true, true))
            {
                // Phải set WorkingDatabase cho cả database nguồn khi thao tác.
                HostApplicationServices.WorkingDatabase = sourceDb;

                using (var tr = sourceDb.TransactionManager.StartTransaction())
                {
                    var bt = tr.GetObject(sourceDb.BlockTableId, OpenMode.ForWrite) as BlockTable;
                    using (var btr = new BlockTableRecord())
                    {
                        btr.Name = blockName;
                        // Thêm một đối tượng Line vào định nghĩa block để nó không bị rỗng.
                        using (var line = new Line(Point3d.Origin, new Point3d(10, 10, 0)))
                        {
                            btr.AppendEntity(line);
                        }
                        bt.Add(btr);
                        tr.AddNewlyCreatedDBObject(btr, true);
                    }
                    tr.Commit();
                }
                // Lưu database trong bộ nhớ này ra file vật lý.
                sourceDb.SaveAs(filePath, DwgVersion.Current);

                // Trả WorkingDatabase về lại database đích của bài test.
                HostApplicationServices.WorkingDatabase = _targetDb;
            }
        }

        // *** BÀI TEST CHO LAYERUTILS ***

        [Test]
        public void EnsureLayerExists_WhenLayerDoesNotExist_ShouldCreateIt()
        {
            // --- ARRANGE ---
            string newLayerName = "LAYER_SHOULD_BE_CREATED";

            // --- ACT ---
            ObjectId layerId = LayerUtils.EnsureLayerExists(_targetDb, newLayerName);

            // --- ASSERT ---
            Assert.That(layerId.IsNull, Is.False);
            using (var tr = _targetDb.TransactionManager.StartTransaction())
            {
                var lt = tr.GetObject(_targetDb.LayerTableId, OpenMode.ForRead) as LayerTable;
                Assert.That(lt.Has(newLayerName), Is.True, "Layer was not created in the LayerTable.");
            }
        }

        // *** BÀI TEST CHO BLOCKUTILS.IMPORTBLOCKDEFINITION ***

        [Test]
        public void ImportBlockDefinition_WhenSourceIsValid_ShouldImportBlock()
        {
            // --- ARRANGE ---
            string blockName = "TEST_BLOCK";
            CreateTestSourceDwgFile(_sourceDwgPath, blockName);

            // --- ACT ---
            ObjectId importedBlockId = BlockUtils.ImportBlockDefinition(_targetDb, _sourceDwgPath, blockName);

            // --- ASSERT ---
            Assert.That(importedBlockId.IsNull, Is.False, "Imported block ObjectId should not be null.");
            using (var tr = _targetDb.TransactionManager.StartTransaction())
            {
                var bt = tr.GetObject(_targetDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                Assert.That(bt.Has(blockName), Is.True, "Block definition was not found in the target database.");
            }
        }

        [Test]
        public void ImportBlockDefinition_WhenFileDoesNotExist_ShouldReturnNullId()
        {
            // --- ARRANGE ---
            string invalidPath = @"C:\non_existent_directory\fakefile.dwg";

            // --- ACT ---
            ObjectId resultId = BlockUtils.ImportBlockDefinition(_targetDb, invalidPath, "ANY_BLOCK");

            // --- ASSERT ---
            Assert.That(resultId.IsNull, Is.True, "Should return null ObjectId for invalid file path.");
        }

        // *** BÀI TEST CHO BLOCKUTILS.INSERTBLOCKREFERENCE ***

        [Test]
        public void InsertBlockReference_WithValidDefinition_ShouldInsertCorrectly()
        {
            // --- ARRANGE ---
            string blockName = "MY_INSERT_BLOCK";
            string layerName = "MY_BLOCK_LAYER";

            // Bước 1: Tạo một định nghĩa block trực tiếp trong database đích.
            ObjectId blockDefId;
            using (var tr = _targetDb.TransactionManager.StartTransaction())
            {
                var bt = tr.GetObject(_targetDb.BlockTableId, OpenMode.ForWrite) as BlockTable;
                using (var btr = new BlockTableRecord { Name = blockName })
                {
                    bt.Add(btr);
                    tr.AddNewlyCreatedDBObject(btr, true);
                    blockDefId = btr.ObjectId;
                }
                tr.Commit();
            }

            // Bước 2: Chuẩn bị các thuộc tính để chèn.
            var props = new BlockProperties
            {
                Position = new Point3d(100, 200, 0),
                Rotation = System.Math.PI / 4, // 45 độ
                Scale = new Scale3d(2.0, 2.0, 2.0)
            };

            // --- ACT ---
            ObjectId blockRefId = BlockUtils.InsertBlockReference(_targetDb, layerName, blockDefId, props);

            // --- ASSERT ---
            Assert.That(blockRefId.IsNull, Is.False, "Block reference ObjectId should not be null.");
            using (var tr = _targetDb.TransactionManager.StartTransaction())
            {
                var br = tr.GetObject(blockRefId, OpenMode.ForRead) as BlockReference;
                Assert.That(br, Is.Not.Null, "Object should be a BlockReference.");

                // Kiểm tra từng thuộc tính đã được gán chính xác chưa.
                Assert.That(br.Position, Is.EqualTo(props.Position), "Position is incorrect.");
                Assert.That(br.Rotation, Is.EqualTo(props.Rotation).Within(0.0001), "Rotation is incorrect.");
                Assert.That(br.ScaleFactors, Is.EqualTo(props.Scale), "Scale is incorrect.");
                Assert.That(br.Layer, Is.EqualTo(layerName), "Layer is incorrect.");
            }
        }
    }
}