// Thêm các thư viện cần thiết
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using MyAutoCAD2026Plugin.Backend;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
// TẠO BÍ DANH ĐỂ GIẢI QUYẾT XUNG ĐỘT
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using AcColor = Autodesk.AutoCAD.Colors.Color;

namespace MyAutoCAD2026Plugin
{
    public class DrawingAnalyzer
    {
        [CommandMethod("SELECT_BY_TYPE")]
        public static void SelectByType()
        {
            Editor editor = AcApp.DocumentManager.MdiActiveDocument.Editor;

            // --- GIAI ĐOẠN 1: THU THẬP THÔNG TIN THÔ TỪ NGƯỜI DÙNG ---
            // Hỏi chế độ chọn
            PromptKeywordOptions pkoMode = new PromptKeywordOptions("\nChọn phương thức lựa chọn: ");
            pkoMode.Keywords.Add("All");
            pkoMode.Keywords.Add("Interactive");
            pkoMode.AllowNone = false;
            PromptResult pkrMode = editor.GetKeywords(pkoMode);
            if (pkrMode.Status != PromptStatus.OK) return;
            bool isInteractive = (pkrMode.StringResult == "Interactive");

            // Hỏi các loại đối tượng
            var selectedTypes = new List<string>();
            while (true)
            {
                string message = "\nChọn loại đối tượng";
                if (selectedTypes.Count > 0)
                {
                    message += $" (đã chọn: {string.Join(", ", selectedTypes)}). Chọn 'Done' để tiếp tục";
                }
                message += ": ";

                PromptKeywordOptions pko = new PromptKeywordOptions(message);
                pko.Keywords.Add("Line");
                pko.Keywords.Add("Polyline");
                pko.Keywords.Add("Arc");
                pko.Keywords.Add("Circle");
                pko.Keywords.Add("Text");
                if (selectedTypes.Count > 0)
                {
                    pko.Keywords.Add("Done");
                }
                pko.AllowNone = true;

                PromptResult pkrType = editor.GetKeywords(pko);
                if (pkrType.Status != PromptStatus.OK) return;

                string result = pkrType.StringResult;
                if (result == "Done") break;

                if (!selectedTypes.Contains(result)) selectedTypes.Add(result);
            }
            if (selectedTypes.Count == 0)
            {
                editor.WriteMessage("\nChưa chọn loại đối tượng. Lệnh đã hủy.");
                return;
            }

            // Hỏi Layer
            PromptStringOptions psoLayer = new PromptStringOptions("\nNhập tên Layer (bỏ trống nếu không cần): ");
            psoLayer.AllowSpaces = true;
            PromptResult prLayer = editor.GetString(psoLayer);
            if (prLayer.Status != PromptStatus.OK && prLayer.Status != PromptStatus.None) return;
            string layerName = prLayer.StringResult;

            // Hỏi Màu
            PromptStringOptions psoColor = new PromptStringOptions("\nNhập tên màu (bỏ trống nếu không cần): ");
            psoColor.AllowSpaces = false;
            PromptResult prColor = editor.GetString(psoColor);
            if (prColor.Status != PromptStatus.OK && prColor.Status != PromptStatus.None) return;
            string colorName = prColor.StringResult;

            // --- GIAI ĐOẠN 2: GỌI "CỖ MÁY" VÀ XỬ LÝ KẾT QUẢ ---
            // Chỉ một dòng lệnh duy nhất để thực hiện toàn bộ công việc phức tạp
            SelectionSet sset = UtilSelection.SelectObjectsByCriteria(selectedTypes, layerName, colorName, isInteractive);

            if (sset != null)
            {
                editor.WriteMessage($"\nĐã chọn được {sset.Count} đối tượng thỏa mãn điều kiện.");
                editor.SetImpliedSelection(sset.GetObjectIds());
            }
            else
            {
                editor.WriteMessage("\nKhông có đối tượng nào được tìm thấy/lựa chọn.");
            }
        }


        #region Previous Commands and Helpers
        [CommandMethod("CountObject_Export")]
        public static void CountObjectAndExport()
        {
            Document acDoc = AcApp.DocumentManager.MdiActiveDocument;
            if (acDoc == null || acDoc.IsReadOnly) return;
            Database acCurDb = acDoc.Database;
            Editor acEditor = acDoc.Editor;
            acEditor.WriteMessage("\nBẮT ĐẦU ĐẾM ĐỐI TƯỢNG VÀ XUẤT BÁO CÁO...");
            var report = new AnalysisReport();
            using (Transaction acTrans = acCurDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var nonBlockCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    var blockInstanceCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    var allBlockContents = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
                    BlockTable bt = acTrans.GetObject(acCurDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord ms = acTrans.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                    foreach (ObjectId objId in ms)
                    {
                        if (objId.IsErased) continue;
                        Entity ent = acTrans.GetObject(objId, OpenMode.ForRead) as Entity;
                        if (ent == null) continue;
                        if (ent is BlockReference br)
                        {
                            string blockName = br.Name;
                            if (br.IsDynamicBlock)
                            {
                                BlockTableRecord blockDef = acTrans.GetObject(br.DynamicBlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                                if (blockDef != null) blockName = blockDef.Name;
                            }
                            if (blockInstanceCounts.ContainsKey(blockName))
                                blockInstanceCounts[blockName]++;
                            else
                                blockInstanceCounts[blockName] = 1;
                            if (!allBlockContents.ContainsKey(blockName))
                            {
                                var newContentDict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                                BlockTableRecord blockDef = acTrans.GetObject(br.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                                CountEntitiesInBtr(blockDef, acTrans, newContentDict);
                                allBlockContents.Add(blockName, newContentDict);
                            }
                        }
                        else
                        {
                            string key = ent.GetType().Name;
                            if (nonBlockCounts.ContainsKey(key))
                                nonBlockCounts[key]++;
                            else
                                nonBlockCounts[key] = 1;
                        }
                    }
                    foreach (var pair in nonBlockCounts.OrderBy(p => p.Key))
                    {
                        report.Entities.Add(new EntityReport { Type = pair.Key, Count = pair.Value });
                    }
                    foreach (var pair in blockInstanceCounts.OrderBy(p => p.Key))
                    {
                        var blockReport = new BlockReport { Name = pair.Key, InstanceCount = pair.Value };
                        if (allBlockContents.ContainsKey(pair.Key))
                        {
                            foreach (var contentPair in allBlockContents[pair.Key].OrderBy(p => p.Key))
                            {
                                blockReport.Contents.Add(new EntityReport { Type = contentPair.Key, Count = contentPair.Value });
                            }
                        }
                        report.Blocks.Add(blockReport);
                    }
                    PrintReportToCommandLine(acEditor, report);
                    acTrans.Commit();
                }
                catch (System.Exception ex)
                {
                    acEditor.WriteMessage($"\nLỖI: {ex.Message}");
                    acTrans.Abort();
                    return;
                }
            }
            PromptAndExportJson(acDoc, acEditor, report);
        }
        private static void PrintReportToCommandLine(Editor editor, AnalysisReport report)
        {
            editor.WriteMessage("\n\n===== BÁO CÁO PHÂN TÍCH BẢN VẼ =====");
            if (report.Blocks.Any())
            {
                editor.WriteMessage($"\n- BlockReference: {report.Blocks.Sum(b => b.InstanceCount)}");
                foreach (var block in report.Blocks)
                {
                    editor.WriteMessage($"\n  + {block.Name}: {block.InstanceCount}");
                    editor.WriteMessage($"\n      trong mỗi Block này bao gồm:");
                    if (block.Contents.Any())
                    {
                        foreach (var content in block.Contents)
                        {
                            editor.WriteMessage($"\n        * {content.Type}: {content.Count}");
                        }
                    }
                    else
                    {
                        editor.WriteMessage($"\n        * (Block rỗng)");
                    }
                }
            }
            foreach (var entity in report.Entities)
            {
                editor.WriteMessage($"\n- {entity.Type}: {entity.Count}");
            }
            editor.WriteMessage("\n\n===== KẾT THÚC BÁO CÁO =====\n");
        }
        private static void PromptAndExportJson(Document doc, Editor editor, AnalysisReport report)
        {
            PromptKeywordOptions pko = new PromptKeywordOptions("\nBạn có muốn xuất báo cáo ra file JSON không? ");
            pko.Keywords.Add("Yes");
            pko.Keywords.Add("No");
            pko.AllowNone = true;
            PromptResult pkr = editor.GetKeywords(pko);
            if (pkr.Status == PromptStatus.OK && pkr.StringResult == "Yes")
            {
                try
                {
                    string jsonString = JsonConvert.SerializeObject(report, Formatting.Indented);
                    string dwgPath = doc.Name;
                    string jsonFileName = Path.ChangeExtension(dwgPath, ".json");
                    File.WriteAllText(jsonFileName, jsonString);
                    editor.WriteMessage($"\nĐã xuất báo cáo thành công ra file: {jsonFileName}");
                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage($"\nLỗi khi xuất file JSON: {ex.Message}");
                }
            }
        }
        private static void CountEntitiesInBtr(BlockTableRecord btr, Transaction tr, Dictionary<string, int> counts)
        {
            if (btr == null || btr.IsErased) return;
            foreach (ObjectId objId in btr)
            {
                if (objId.IsErased) continue;
                Entity ent = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                if (ent == null) continue;
                if (ent is BlockReference br)
                {
                    BlockTableRecord blockDef = tr.GetObject(br.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                    if (blockDef != null)
                    {
                        if (!btr.Name.Equals(blockDef.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            CountEntitiesInBtr(blockDef, tr, counts);
                        }
                    }
                }
                else
                {
                    string typeName = ent.GetType().Name;
                    if (counts.ContainsKey(typeName)) counts[typeName]++;
                    else counts[typeName] = 1;
                }
            }
        }
        #endregion
        #region Unit Test Runner for GeometryCreator

        [CommandMethod("RUN_GEOMETRY_TESTS")]
        public static void RunGeometryCreatorTests()
        {
            Editor ed = AcApp.DocumentManager.MdiActiveDocument.Editor;
            ed.WriteMessage("\n===== BẮT ĐẦU CHẠY UNIT TESTS CHO GEOMETRYCREATOR (PHIÊN BẢN CẢI TIẾN) =====\n");

            // Gọi các hàm test đã được cải tiến và bổ sung
            Test_CreateLine_Should_Create_Line_With_Correct_Properties(ed);
            Test_CreateCircle_Should_Create_Circle_With_Correct_Properties_V2(ed); // Version 2
            Test_CreateArc_Should_Create_Arc_With_Correct_Properties_V2(ed);       // Version 2
            Test_CreatePolyline_Should_Return_Null_For_Insufficient_Points(ed);
            Test_CreateLine_Should_Also_Create_Layer_If_Not_Exists(ed);
            Test_CreateLine_Should_Attach_XData_Correctly(ed); // BÀI TEST MỚI

            ed.WriteMessage("\n===== KẾT THÚC UNIT TESTS CHO GEOMETRYCREATOR =====\n");
        }

        // --- Test cho LINE (giữ nguyên vì đã chi tiết) ---
        private static void Test_CreateLine_Should_Create_Line_With_Correct_Properties(Editor ed)
        {
            // ... code của hàm này giữ nguyên như phản hồi trước ...
            ed.WriteMessage("\n--- Đang chạy Test: CreateLine - Thuộc tính chính xác...");
            using (Database db = new Database(true, false))
            {
                Point3d startPt = new Point3d(10, 20, 0);
                Point3d endPt = new Point3d(110, 120, 0);
                string layerName = "0";
                AcColor color = AcColor.FromColorIndex(ColorMethod.ByAci, 1);
                ObjectId lineId = GeometryCreator.CreateLine(db, startPt, endPt, layerName, color);
                if (lineId.IsNull)
                {
                    ed.WriteMessage("\n  [THẤT BẠI] CreateLine đã trả về ObjectId.Null.");
                    return;
                }
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    Line createdLine = tr.GetObject(lineId, OpenMode.ForRead) as Line;
                    bool isSuccess = true;
                    if (createdLine.StartPoint != startPt) { ed.WriteMessage("\n  [THẤT BẠI] Sai điểm bắt đầu (StartPoint)."); isSuccess = false; }
                    if (createdLine.EndPoint != endPt) { ed.WriteMessage("\n  [THẤT BẠI] Sai điểm kết thúc (EndPoint)."); isSuccess = false; }
                    if (createdLine.Layer != layerName) { ed.WriteMessage($"\n  [THẤT BẠI] Sai Layer. Mong muốn: {layerName}, Thực tế: {createdLine.Layer}"); isSuccess = false; }
                    if (createdLine.Color != color) { ed.WriteMessage($"\n  [THẤT BẠI] Sai Màu. Mong muốn: {color}, Thực tế: {createdLine.Color}"); isSuccess = false; }
                    if (isSuccess) { ed.WriteMessage("\n  [THÀNH CÔNG] Line được tạo với tất cả thuộc tính chính xác."); }
                }
            }
        }

        /// <summary>
        /// KỊCH BẢN TEST 2 (CẢI TIẾN): Kiểm tra hàm CreateCircle, báo lỗi chi tiết.
        /// </summary>
        private static void Test_CreateCircle_Should_Create_Circle_With_Correct_Properties_V2(Editor ed)
        {
            ed.WriteMessage("\n--- Đang chạy Test (V2): CreateCircle - Báo lỗi chi tiết...");
            // ARRANGE
            using (Database db = new Database(true, false))
            {
                Point3d center = new Point3d(50, 50, 0);
                double radius = 25.5;
                string layerName = "CIRCLE_LAYER";
                AcColor color = AcColor.FromColorIndex(ColorMethod.ByAci, 3);

                // ACT
                ObjectId circleId = GeometryCreator.CreateCircle(db, center, radius, layerName, color);

                // ASSERT
                if (circleId.IsNull)
                {
                    ed.WriteMessage("\n  [THẤT BẠI] CreateCircle đã trả về ObjectId.Null.");
                    return;
                }
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    Circle createdCircle = tr.GetObject(circleId, OpenMode.ForRead) as Circle;

                    // NOTE: PHẦN CẢI TIẾN - Kiểm tra từng thuộc tính riêng lẻ
                    bool isSuccess = true;
                    if (createdCircle.Center != center) { ed.WriteMessage("\n  [THẤT BẠI] Sai Tâm (Center)."); isSuccess = false; }
                    // So sánh số thực cần có một sai số nhỏ (Tolerance) để tránh lỗi làm tròn
                    if (Math.Abs(createdCircle.Radius - radius) > 0.0001) { ed.WriteMessage("\n  [THẤT BẠI] Sai Bán kính (Radius)."); isSuccess = false; }
                    if (createdCircle.Layer != layerName) { ed.WriteMessage($"\n  [THẤT BẠI] Sai Layer. Mong muốn: {layerName}, Thực tế: {createdCircle.Layer}"); isSuccess = false; }
                    if (createdCircle.Color != color) { ed.WriteMessage($"\n  [THẤT BẠI] Sai Màu. Mong muốn: {color}, Thực tế: {createdCircle.Color}"); isSuccess = false; }

                    if (isSuccess)
                    {
                        ed.WriteMessage("\n  [THÀNH CÔNG] Circle được tạo với tất cả thuộc tính chính xác.");
                    }
                }
            }
        }

        /// <summary>
        /// KỊCH BẢN TEST 3 (CẢI TIẾN): Kiểm tra hàm CreateArc, báo lỗi chi tiết.
        /// </summary>
        private static void Test_CreateArc_Should_Create_Arc_With_Correct_Properties_V2(Editor ed)
        {
            ed.WriteMessage("\n--- Đang chạy Test (V2): CreateArc - Báo lỗi chi tiết...");
            // ARRANGE
            using (Database db = new Database(true, false))
            {
                Point3d startPt = new Point3d(10, 0, 0);
                Point3d ptOnArc = new Point3d(0, 10, 0);
                Point3d endPt = new Point3d(-10, 0, 0);
                string layerName = "ARC_LAYER";
                AcColor color = AcColor.FromColorIndex(ColorMethod.ByAci, 5); // Blue

                // NOTE: Để kiểm tra Arc chính xác, ta cần tính trước các thuộc tính hình học của nó
                // Dùng CircularArc3d để tính toán trước khi tạo đối tượng thật
                CircularArc3d geoArc = new CircularArc3d(startPt, ptOnArc, endPt);
                Point3d expectedCenter = geoArc.Center;
                double expectedRadius = geoArc.Radius;

                // ACT
                ObjectId arcId = GeometryCreator.CreateArc(db, startPt, ptOnArc, endPt, layerName, color);

                // ASSERT
                if (arcId.IsNull)
                {
                    ed.WriteMessage("\n  [THẤT BẠI] CreateArc đã trả về ObjectId.Null.");
                    return;
                }
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    Arc createdArc = tr.GetObject(arcId, OpenMode.ForRead) as Arc;

                    bool isSuccess = true;
                    if (createdArc.Center.DistanceTo(expectedCenter) > 0.0001) { ed.WriteMessage("\n  [THẤT BẠI] Sai Tâm (Center)."); isSuccess = false; }
                    if (Math.Abs(createdArc.Radius - expectedRadius) > 0.0001) { ed.WriteMessage("\n  [THẤT BẠI] Sai Bán kính (Radius)."); isSuccess = false; }
                    if (createdArc.Layer != layerName) { ed.WriteMessage($"\n  [THẤT BẠI] Sai Layer. Mong muốn: {layerName}, Thực tế: {createdArc.Layer}"); isSuccess = false; }
                    if (createdArc.Color != color) { ed.WriteMessage($"\n  [THẤT BẠI] Sai Màu. Mong muốn: {color}, Thực tế: {createdArc.Color}"); isSuccess = false; }

                    if (isSuccess)
                    {
                        ed.WriteMessage("\n  [THÀNH CÔNG] Arc được tạo với các thuộc tính hình học chính xác.");
                    }
                }
            }
        }

        // --- Test cho Polyline (giữ nguyên, vì nó kiểm tra trường hợp trả về Null) ---
        private static void Test_CreatePolyline_Should_Return_Null_For_Insufficient_Points(Editor ed)
        {
            // ... code của hàm này giữ nguyên như phản hồi trước ...
            ed.WriteMessage("\n--- Đang chạy Test: CreatePolyline - Đầu vào không đủ điểm...");
            using (Database db = new Database(true, false))
            {
                Point3dCollection points = new Point3dCollection();
                points.Add(new Point3d(0, 0, 0));
                ObjectId plineId = GeometryCreator.CreatePolyline(db, points, "0", AcColor.FromColorIndex(ColorMethod.ByAci, 1));
                if (plineId.IsNull) { ed.WriteMessage("\n  [THÀNH CÔNG] Hàm đã trả về Null đúng như mong đợi."); }
                else { ed.WriteMessage("\n  [THẤT BẠI] Hàm đã không trả về Null khi đầu vào không hợp lệ."); }
            }
        }

        // --- Test cho việc tự tạo Layer (giữ nguyên) ---
        private static void Test_CreateLine_Should_Also_Create_Layer_If_Not_Exists(Editor ed)
        {
            // ... code của hàm này giữ nguyên như phản hồi trước ...
            ed.WriteMessage("\n--- Đang chạy Test: CreateLine - Tự động tạo Layer...");
            using (Database db = new Database(true, false))
            {
                string newLayerName = "LAYER_MOI_TINH";
                GeometryCreator.CreateLine(db, Point3d.Origin, new Point3d(1, 1, 0), newLayerName, AcColor.FromColorIndex(ColorMethod.ByAci, 7));
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
                    if (lt.Has(newLayerName)) { ed.WriteMessage($"\n  [THÀNH CÔNG] Layer '{newLayerName}' đã được tự động tạo thành công."); }
                    else { ed.WriteMessage($"\n  [THẤT BẠI] Layer '{newLayerName}' đã không được tạo."); }
                }
            }
        }

        /// <summary>
        /// KỊCH BẢN TEST 6 (BỔ SUNG): Kiểm tra việc đính kèm XData vào đối tượng.
        /// MỤC TIÊU: Đảm bảo dữ liệu XData được ghi và đọc lại một cách chính xác.
        /// </summary>
        private static void Test_CreateLine_Should_Attach_XData_Correctly(Editor ed)
        {
            ed.WriteMessage("\n--- Đang chạy Test (MỚI): CreateLine - Gắn XData chính xác...");
            // ARRANGE
            using (Database db = new Database(true, false))
            {
                // Chuẩn bị dữ liệu XData mẫu
                DoorXData sourceXData = new DoorXData
                {
                    DoorType = "CuaDonCanhTrai",
                    Height = 2200.0,
                    Width = 900.0
                };

                // Chuẩn bị các tham số thông thường
                Point3d startPt = Point3d.Origin;
                Point3d endPt = new Point3d(1, 1, 0);

                // ACT
                // NOTE: Gọi overload của CreateLine có nhận tham số xdata
                ObjectId lineId = GeometryCreator.CreateLine(db, startPt, endPt, "0", AcColor.FromColorIndex(ColorMethod.ByAci, 1), sourceXData);

                // ASSERT
                // NOTE: Sử dụng chính hàm ReadDoorXData trong backend để đọc lại dữ liệu và kiểm tra
                DoorXData readXData = GeometryCreator.ReadDoorXData(db, lineId);

                bool isSuccess = true;
                if (readXData == null)
                {
                    ed.WriteMessage("\n  [THẤT BẠI] Không thể đọc lại XData từ đối tượng.");
                    isSuccess = false;
                }
                else
                {
                    if (readXData.DoorType != sourceXData.DoorType) { ed.WriteMessage("\n  [THẤT BẠI] Sai XData 'DoorType'."); isSuccess = false; }
                    if (Math.Abs(readXData.Height - sourceXData.Height) > 0.0001) { ed.WriteMessage("\n  [THẤT BẠI] Sai XData 'Height'."); isSuccess = false; }
                    if (Math.Abs(readXData.Width - sourceXData.Width) > 0.0001) { ed.WriteMessage("\n  [THẤT BẠI] Sai XData 'Width'."); isSuccess = false; }
                }

                if (isSuccess)
                {
                    ed.WriteMessage("\n  [THÀNH CÔNG] XData đã được gắn và đọc lại chính xác.");
                }
            }
        }
        #endregion
        #region Unit Test Runner for BlockUtils

        /// <summary>
        /// Lớp tiện ích nhỏ để quản lý thông tin về file DWG nguồn dùng cho test.
        /// Giúp code sạch hơn và tránh lặp lại các chuỗi ký tự.
        /// </summary>
        private static class TestFileProvider
        {
            // Tên của block mẫu mà chúng ta sẽ tạo và nhập khẩu.
            public const string TestBlockName = "SAMPLE_BLOCK";
            // Đường dẫn tới file DWG tạm thời. Dùng Path.Combine để đảm bảo nó hoạt động trên mọi hệ điều hành.
            // Path.GetTempPath() sẽ lấy thư mục tạm của hệ thống (vd: C:\Users\YourUser\AppData\Local\Temp).
            public static readonly string SourceDwgPath = Path.Combine(Path.GetTempPath(), "TestSourceBlock.dwg");
        }

        /// <summary>
        /// Hàm helper: Tạo ra một file DWG nguồn chứa một block đơn giản.
        /// Đây là bước chuẩn bị quan trọng cho các bài test import.
        /// </summary>
        private static void CreateTestSourceDwgFile()
        {
            // Tạo một database "ảo" để làm file nguồn.
            using (Database sourceDb = new Database(true, false))
            {
                // Bắt đầu transaction để chỉnh sửa database này.
                using (Transaction tr = sourceDb.TransactionManager.StartTransaction())
                {
                    // Lấy Bảng Block (BlockTable) để thêm định nghĩa block mới.
                    BlockTable bt = tr.GetObject(sourceDb.BlockTableId, OpenMode.ForWrite) as BlockTable;

                    // Tạo một định nghĩa block mới (BlockTableRecord).
                    using (BlockTableRecord btr = new BlockTableRecord())
                    {
                        // Đặt tên cho block.
                        btr.Name = TestFileProvider.TestBlockName;
                        // Đặt điểm chèn gốc của block.
                        btr.Origin = Point3d.Origin;

                        // Thêm một vài đối tượng hình học vào block để nó không bị rỗng.
                        // Ví dụ, một hình tròn ở gốc tọa độ.
                        using (Circle c = new Circle(Point3d.Origin, Vector3d.ZAxis, 10))
                        {
                            btr.AppendEntity(c);
                        }

                        // Thêm định nghĩa block mới vào Bảng Block.
                        bt.Add(btr);
                        // Báo cho transaction biết về đối tượng mới này.
                        tr.AddNewlyCreatedDBObject(btr, true);
                    }
                    // Lưu các thay đổi.
                    tr.Commit();
                }
                // NOTE: Lưu database "ảo" này thành một file DWG thật tại đường dẫn tạm.
                sourceDb.SaveAs(TestFileProvider.SourceDwgPath, DwgVersion.Current);
            }
        }

        /// <summary>
        /// Bộ chạy test cho class BlockUtils.
        /// </summary>
        [CommandMethod("RUN_BLOCKUTILS_TESTS")]
        public static void RunBlockUtilsTests()
        {
            Editor ed = AcApp.DocumentManager.MdiActiveDocument.Editor;
            ed.WriteMessage("\n===== BẮT ĐẦU CHẠY UNIT TESTS CHO BLOCKUTILS =====\n");

            // Dùng try...finally để đảm bảo file tạm luôn được dọn dẹp.
            try
            {
                // Bước chuẩn bị chung: Tạo file DWG nguồn.
                CreateTestSourceDwgFile();

                // Chạy từng bài test.
                Test_ImportBlockDefinition_Success_When_Block_Is_New(ed);
                Test_ImportBlockDefinition_Returns_Existing_Id_If_Block_Exists(ed);
                Test_InsertBlockReference_Success_With_Valid_Properties(ed);
            }
            catch (System.Exception ex)
            {
                // Nếu có lỗi nghiêm trọng xảy ra trong quá trình test, báo cho người dùng biết.
                ed.WriteMessage($"\n!!! ĐÃ XẢY RA LỖI NGHIÊM TRỌNG TRONG BỘ TEST: {ex.Message}");
            }
            finally
            {
                // NOTE: BƯỚC DỌN DẸP QUAN TRỌNG
                // Dù test thành công hay thất bại, luôn xóa file DWG tạm đã tạo.
                if (File.Exists(TestFileProvider.SourceDwgPath))
                {
                    File.Delete(TestFileProvider.SourceDwgPath);
                    ed.WriteMessage($"\n\n(Dọn dẹp) Đã xóa file test tạm: {TestFileProvider.SourceDwgPath}");
                }
            }

            ed.WriteMessage("\n===== KẾT THÚC UNIT TESTS CHO BLOCKUTILS =====\n");
        }

        /// <summary>
        /// KỊCH BẢN TEST 1: Kiểm tra việc nhập khẩu block thành công khi block chưa tồn tại trong bản vẽ đích.
        /// </summary>
        private static void Test_ImportBlockDefinition_Success_When_Block_Is_New(Editor ed)
        {
            ed.WriteMessage($"\n--- Đang chạy Test: ImportBlockDefinition - Nhập khẩu block mới...");
            // ARRANGE
            // Tạo database đích là một bản vẽ "ảo" trống rỗng.
            using (Database targetDb = new Database(true, false))
            {
                // ACT
                // Gọi hàm cần test: nhập khẩu block từ file nguồn đã tạo vào database đích.
                ObjectId blockId = BlockUtils.ImportBlockDefinition(targetDb, TestFileProvider.SourceDwgPath, TestFileProvider.TestBlockName);

                // ASSERT
                // 1. Kiểm tra xem hàm có trả về một ObjectId hợp lệ không.
                if (blockId.IsNull)
                {
                    ed.WriteMessage("\n  [THẤT BẠI] Hàm đã trả về ObjectId.Null.");
                    return;
                }

                // 2. Kiểm tra xem block có thực sự tồn tại trong Bảng Block của database đích hay không.
                using (Transaction tr = targetDb.TransactionManager.StartTransaction())
                {
                    BlockTable bt = tr.GetObject(targetDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                    if (bt.Has(TestFileProvider.TestBlockName))
                    {
                        ed.WriteMessage("\n  [THÀNH CÔNG] Block đã được nhập khẩu thành công vào bản vẽ đích.");
                    }
                    else
                    {
                        ed.WriteMessage("\n  [THẤT BẠI] Block không tồn tại trong BlockTable của bản vẽ đích sau khi nhập khẩu.");
                    }
                }
            }
        }

        /// <summary>
        /// KỊCH BẢN TEST 2: Đảm bảo hàm không nhập khẩu lại nếu block đã tồn tại và trả về đúng ObjectId của block cũ.
        /// </summary>
        private static void Test_ImportBlockDefinition_Returns_Existing_Id_If_Block_Exists(Editor ed)
        {
            ed.WriteMessage($"\n--- Đang chạy Test: ImportBlockDefinition - Xử lý block đã tồn tại...");
            // ARRANGE
            using (Database targetDb = new Database(true, false))
            {
                // Gọi hàm lần thứ nhất để đảm bảo block đã tồn tại trong database đích.
                ObjectId firstImportId = BlockUtils.ImportBlockDefinition(targetDb, TestFileProvider.SourceDwgPath, TestFileProvider.TestBlockName);

                // ACT
                // Gọi hàm lần thứ hai với cùng các tham số.
                ObjectId secondImportId = BlockUtils.ImportBlockDefinition(targetDb, TestFileProvider.SourceDwgPath, TestFileProvider.TestBlockName);

                // ASSERT
                // 1. Cả hai lần gọi đều phải trả về một ObjectId hợp lệ.
                if (firstImportId.IsNull || secondImportId.IsNull)
                {
                    ed.WriteMessage("\n  [THẤT BẠI] Một trong các lần gọi import đã trả về ObjectId.Null.");
                    return;
                }

                // 2. NOTE: Quan trọng nhất: ObjectId trả về từ hai lần gọi phải GIỐNG HỆT nhau.
                if (firstImportId == secondImportId)
                {
                    ed.WriteMessage("\n  [THÀNH CÔNG] Hàm đã trả về đúng ObjectId của block đã tồn tại.");
                }
                else
                {
                    ed.WriteMessage("\n  [THẤT BẠI] Hàm đã tạo một block mới thay vì trả về block đã tồn tại.");
                }
            }
        }

        /// <summary>
        /// KỊCH BẢN TEST 3: Kiểm tra việc chèn một BlockReference vào bản vẽ.
        /// </summary>
        private static void Test_InsertBlockReference_Success_With_Valid_Properties(Editor ed)
        {
            ed.WriteMessage($"\n--- Đang chạy Test: InsertBlockReference - Chèn với thuộc tính hợp lệ...");
            // ARRANGE
            using (Database db = new Database(true, false))
            {
                ObjectId blockDefId;
                // "Dựng cảnh": Tạo một định nghĩa block đơn giản bằng code ngay trong database test.
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;
                    using (BlockTableRecord btr = new BlockTableRecord())
                    {
                        btr.Name = "CHAIR_BLOCK";
                        btr.AppendEntity(new Circle()); // Thêm một thực thể bất kỳ
                        blockDefId = bt.Add(btr); // Lấy ObjectId của định nghĩa block
                        tr.AddNewlyCreatedDBObject(btr, true);
                    }
                    tr.Commit();
                }

                // Chuẩn bị các thuộc tính để chèn block.
                BlockProperties props = new BlockProperties
                {
                    Position = new Point3d(100, 200, 0),
                    Rotation = Math.PI / 2, // 90 độ
                    Scale = new Scale3d(1.5)
                };
                string layerName = "FURNITURE";

                // ACT
                // Gọi hàm cần test.
                ObjectId blockRefId = BlockUtils.InsertBlockReference(db, layerName, blockDefId, props);

                // ASSERT
                if (blockRefId.IsNull)
                {
                    ed.WriteMessage("\n  [THẤT BẠI] InsertBlockReference đã trả về ObjectId.Null.");
                    return;
                }

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockReference br = tr.GetObject(blockRefId, OpenMode.ForRead) as BlockReference;
                    bool isSuccess = true;
                    if (br.Position != props.Position) { ed.WriteMessage("\n  [THẤT BẠI] Sai Vị trí (Position)."); isSuccess = false; }
                    if (Math.Abs(br.Rotation - props.Rotation) > 0.0001) { ed.WriteMessage("\n  [THẤT BẠI] Sai Góc xoay (Rotation)."); isSuccess = false; }
                    if (br.ScaleFactors != props.Scale) { ed.WriteMessage("\n  [THẤT BẠI] Sai Tỉ lệ (Scale)."); isSuccess = false; }
                    if (br.Layer != layerName) { ed.WriteMessage($"\n  [THẤT BẠI] Sai Layer. Mong muốn: {layerName}, Thực tế: {br.Layer}"); isSuccess = false; }

                    if (isSuccess)
                    {
                        ed.WriteMessage("\n  [THÀNH CÔNG] BlockReference được chèn với tất cả thuộc tính chính xác.");
                    }
                }
            }
        }
        #endregion
    }
}