// Thêm các thư viện cần thiết
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
// TẠO BÍ DANH ĐỂ GIẢI QUYẾT XUNG ĐỘT
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MyAutoCAD2026Plugin
{
    public class DrawingAnalyzer
    {
        [CommandMethod("SELECT_BY_TYPE")]
        public static void SelectByType()
        {
            // Lấy về các đối tượng Document và Editor, là điểm khởi đầu của mọi lệnh
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor editor = doc.Editor;

            // Khởi tạo một danh sách rỗng để chứa các điều kiện lọc
            var filterList = new List<TypedValue>();
            var selectedTypes = new List<string>();

            // --- BƯỚC 1: HỎI CHẾ ĐỘ CHỌN ---
            // Tạo một đối tượng để định nghĩa câu hỏi có các từ khóa
            PromptKeywordOptions pkoMode = new PromptKeywordOptions("\nChọn phương thức lựa chọn: ");
            pkoMode.Keywords.Add("All"); // Thêm từ khóa "All"
            pkoMode.Keywords.Add("Interactive"); // Thêm từ khóa "Interactive"
            pkoMode.AllowNone = false; // Bắt buộc người dùng phải chọn một trong hai

            // Hiển thị câu hỏi và lấy kết quả
            PromptResult pkrMode = editor.GetKeywords(pkoMode);
            if (pkrMode.Status != PromptStatus.OK) return; // Nếu người dùng nhấn Esc, hủy lệnh

            // === CODE QUAN TRỌNG 4: LƯU LẠI CHẾ ĐỘ CHỌN ===
            // Dùng một biến boolean để lưu lại lựa chọn của người dùng
            bool selectAllMode = (pkrMode.StringResult == "All");

            // --- BƯỚC 2: HỎI LOẠI ĐỐI TƯỢNG (Cho phép chọn nhiều) ---
            // Bắt đầu một vòng lặp vô tận, sẽ chỉ dừng lại khi người dùng chọn "Done"
            while (true)
            {
                // Xây dựng câu thông báo một cách linh hoạt
                string message = "\nChọn loại đối tượng";
                if (selectedTypes.Count > 0)
                {
                    message += $" (đã chọn: {string.Join(", ", selectedTypes)}). Chọn 'Done' để tiếp tục";
                }
                message += ": ";

                // Hiển thị danh sách các loại đối tượng
                PromptKeywordOptions pko = new PromptKeywordOptions(message);
                pko.Keywords.Add("Line");
                pko.Keywords.Add("Polyline");
                pko.Keywords.Add("Arc");
                pko.Keywords.Add("Circle");
                pko.Keywords.Add("Text");
                if (selectedTypes.Count > 0)
                {
                    pko.Keywords.Add("Done"); // Chỉ hiển thị nút "Done" khi đã có ít nhất 1 lựa chọn
                }
                pko.AllowNone = true;

                PromptResult pkrType = editor.GetKeywords(pko);
                if (pkrType.Status != PromptStatus.OK) return;

                string result = pkrType.StringResult;
                if (result == "Done")
                {
                    break; // Thoát khỏi vòng lặp
                }

                // Thêm lựa chọn vào danh sách nếu nó chưa tồn tại
                if (!selectedTypes.Contains(result))
                {
                    selectedTypes.Add(result);
                }
            }

            if (selectedTypes.Count == 0)
            {
                editor.WriteMessage("\nChưa chọn loại đối tượng nào. Lệnh đã hủy.");
                return;
            }

            // Gom nhóm các lựa chọn thành một chuỗi lọc OR duy nhất
            List<string> dxfNames = new List<string>();
            foreach (var type in selectedTypes)
            {
                switch (type)
                {
                    case "Line": dxfNames.Add(UtilSelection.ALL_LINES); break;
                    case "Polyline": dxfNames.Add(UtilSelection.ALL_POLYLINES); break;
                    case "Arc": dxfNames.Add(UtilSelection.ALL_ARCS); break;
                    case "Circle": dxfNames.Add(UtilSelection.ALL_CIRCLES); break;
                    case "Text": dxfNames.Add(UtilSelection.ALL_TEXTS); break;
                }
            }
            string finalFilterString = string.Join(",", dxfNames.Distinct());
            // Thêm điều kiện lọc theo loại vào danh sách
            filterList.Add(new TypedValue((int)DxfCode.Start, finalFilterString));

            // --- BƯỚC 3: HỎI LAYER VÀ MÀU (Tùy chọn) ---
            // Yêu cầu người dùng nhập tên Layer
            PromptStringOptions psoLayer = new PromptStringOptions("\nNhập tên Layer để lọc (bỏ trống nếu không cần): ");
            psoLayer.AllowSpaces = true;
            PromptResult prLayer = editor.GetString(psoLayer);
            if (prLayer.Status != PromptStatus.OK && prLayer.Status != PromptStatus.None) return;
            // Nếu người dùng có nhập tên, thêm điều kiện lọc Layer
            if (!string.IsNullOrWhiteSpace(prLayer.StringResult))
            {
                filterList.Add(new TypedValue((int)DxfCode.LayerName, prLayer.StringResult));
            }

            // Yêu cầu người dùng nhập màu
            PromptStringOptions psoColor = new PromptStringOptions("\nNhập tên màu (Red, Yellow...) hoặc mã số (1-256) để lọc (bỏ trống nếu không cần): ");
            psoColor.AllowSpaces = false;
            PromptResult prColor = editor.GetString(psoColor);
            if (prColor.Status != PromptStatus.OK && prColor.Status != PromptStatus.None) return;
            // Nếu người dùng có nhập màu
            if (!string.IsNullOrWhiteSpace(prColor.StringResult))
            {
                short colorIndex = UtilSelection.ConvertColorNameToIndex(prColor.StringResult);
                // Nếu màu hợp lệ, thêm điều kiện lọc Màu
                if (colorIndex != -1)
                {
                    filterList.Add(new TypedValue((int)DxfCode.Color, colorIndex));
                }
                else
                {
                    editor.WriteMessage("\nTên hoặc mã màu không hợp lệ, bộ lọc màu sẽ được bỏ qua.");
                }
            }

            // --- BƯỚC 4: GỌI HÀM TIỆN ÍCH PHÙ HỢP VÀ HIỂN THỊ KẾT QUẢ ---
            SelectionSet sset = null;
            // Dựa vào lựa chọn ở Bước 1, gọi hàm tiện ích tương ứng
            if (selectAllMode)
            {
                // Gọi hàm chọn tất cả
                sset = UtilSelection.SelectAllObjects(filterList);
            }
            else
            {
                // Gọi hàm chọn tương tác
                sset = UtilSelection.GetSelection(filterList);
            }

            // Xử lý kết quả cuối cùng
            if (sset != null)
            {
                editor.WriteMessage($"\nĐã chọn được {sset.Count} đối tượng thỏa mãn điều kiện.");
                // Làm nổi bật các đối tượng đã chọn
                editor.SetImpliedSelection(sset.GetObjectIds());
            }
            else
            {
                editor.WriteMessage("\nKhông có đối tượng nào thỏa mãn điều kiện được tìm thấy/lựa chọn.");
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
            acEditor.WriteMessage("\nBẮT ĐẦU ĐẾM ĐỐI TƯỢỢNG VÀ XUẤT BÁO CÁO...");
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
        [CommandMethod("CREATE")]
        public static void CreateObject()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            Editor editor = doc.Editor;

            PromptKeywordOptions pko = new PromptKeywordOptions("\nChọn loại đối tượng muốn tạo: ");
            pko.Keywords.Add("Line");
            pko.Keywords.Add("Polyline");
            pko.Keywords.Add("Arc");
            pko.Keywords.Add("Circle");
            pko.AllowNone = true;

            PromptResult pkr = editor.GetKeywords(pko);
            if (pkr.Status != PromptStatus.OK) return;

            switch (pkr.StringResult)
            {
                case "Line": CreateLineCmd(editor); break;
                case "Polyline": CreatePolylineCmd(editor); break;
                case "Arc": CreateArcCmd(editor); break;
                case "Circle": CreateCircleCmd(editor); break;
            }
        }

        #region Create Command Helpers (ĐÃ NÂNG CẤP)

        private static void CreateLineCmd(Editor editor)
        {
            // Hỏi điểm đầu
            PromptPointResult pprStart = editor.GetPoint("\nChọn điểm bắt đầu cho Line:");
            if (pprStart.Status != PromptStatus.OK) return;

            // Hỏi điểm cuối
            PromptPointOptions ppoEnd = new PromptPointOptions("\nChọn điểm kết thúc cho Line:");
            ppoEnd.UseBasePoint = true;
            ppoEnd.BasePoint = pprStart.Value;
            PromptPointResult pprEnd = editor.GetPoint(ppoEnd);
            if (pprEnd.Status != PromptStatus.OK) return;

            // === THAY ĐỔI: HỎI LAYER VÀ MÀU ===
            string layer = GetLayerFromUser(editor);
            short colorIndex = GetColorFromUser(editor);

            // Gọi hàm tiện ích với thông số người dùng nhập
            GeometryCreator.CreateLine(pprStart.Value, pprEnd.Value, layer, colorIndex);
        }

        private static void CreateCircleCmd(Editor editor)
        {
            // Hỏi điểm tâm
            PromptPointResult pprCenter = editor.GetPoint("\nChọn điểm tâm cho Circle:");
            if (pprCenter.Status != PromptStatus.OK) return;

            // Hỏi bán kính
            PromptDoubleOptions pdoRadius = new PromptDoubleOptions("\nNhập bán kính:");
            pdoRadius.AllowNegative = false;
            pdoRadius.AllowZero = false;
            PromptDoubleResult pdrRadius = editor.GetDouble(pdoRadius);
            if (pdrRadius.Status != PromptStatus.OK) return;

            // === THAY ĐỔI: HỎI LAYER VÀ MÀU ===
            string layer = GetLayerFromUser(editor);
            short colorIndex = GetColorFromUser(editor);

            // Gọi hàm tiện ích với thông số người dùng nhập
            GeometryCreator.CreateCircle(pprCenter.Value, pdrRadius.Value, layer, colorIndex);
        }

        private static void CreatePolylineCmd(Editor editor)
        {
            var points = new Point3dCollection();

            // === THAY ĐỔI: SỬA LỖI VÒNG LẶP VÀ THOÁT LỆNH ===
            while (true)
            {
                string message = (points.Count == 0)
                    ? "\nChọn đỉnh bắt đầu cho Polyline:"
                    : "\nChọn đỉnh tiếp theo (Nhấn Enter để kết thúc):";

                PromptPointOptions ppo = new PromptPointOptions(message);
                if (points.Count > 0)
                {
                    ppo.UseBasePoint = true;
                    ppo.BasePoint = points[points.Count - 1];
                }

                // Cho phép nhấn Enter để kết thúc
                ppo.AllowNone = true;

                PromptPointResult ppr = editor.GetPoint(ppo);

                // Nếu người dùng nhấn Enter (PromptStatus.None) hoặc Esc (PromptStatus.Cancel), thoát vòng lặp
                if (ppr.Status != PromptStatus.OK)
                {
                    break;
                }

                points.Add(ppr.Value);
            }

            if (points.Count > 1)
            {
                // === THAY ĐỔI: HỎI LAYER VÀ MÀU ===
                string layer = GetLayerFromUser(editor);
                short colorIndex = GetColorFromUser(editor);
                GeometryCreator.CreatePolyline(points, layer, colorIndex);
            }
        }

        private static void CreateArcCmd(Editor editor)
        {
            // Hỏi 3 điểm
            PromptPointResult pprStart = editor.GetPoint("\nChọn điểm bắt đầu cho Arc:");
            if (pprStart.Status != PromptStatus.OK) return;

            PromptPointOptions ppoOnArc = new PromptPointOptions("\nChọn một điểm trên cung tròn:");
            ppoOnArc.UseBasePoint = true;
            ppoOnArc.BasePoint = pprStart.Value;
            PromptPointResult pprOnArc = editor.GetPoint(ppoOnArc);
            if (pprOnArc.Status != PromptStatus.OK) return;

            PromptPointOptions ppoEnd = new PromptPointOptions("\nChọn điểm kết thúc cho Arc:");
            ppoEnd.UseBasePoint = true;
            ppoEnd.BasePoint = pprOnArc.Value;
            PromptPointResult pprEnd = editor.GetPoint(ppoEnd);
            if (pprEnd.Status != PromptStatus.OK) return;

            // === THAY ĐỔI: HỎI LAYER VÀ MÀU ===
            string layer = GetLayerFromUser(editor);
            short colorIndex = GetColorFromUser(editor);

            GeometryCreator.CreateArcFrom3Points(pprStart.Value, pprOnArc.Value, pprEnd.Value, layer, colorIndex);
        }

        // --- CÁC HÀM TIỆN ÍCH PHỤ TRỢ CHO VIỆC HỎI NGƯỜI DÙNG ---
        private static string GetLayerFromUser(Editor editor)
        {
            PromptStringOptions pso = new PromptStringOptions("\nNhập tên Layer (bỏ trống để dùng layer '0'): ");
            pso.AllowSpaces = true;
            pso.DefaultValue = "0";
            PromptResult pr = editor.GetString(pso);
            if (pr.Status == PromptStatus.OK)
            {
                return string.IsNullOrWhiteSpace(pr.StringResult) ? "0" : pr.StringResult;
            }
            return "0";
        }

        private static short GetColorFromUser(Editor editor)
        {
            PromptStringOptions pso = new PromptStringOptions("\nNhập tên màu hoặc mã số (bỏ trống để dùng ByLayer): ");
            pso.AllowSpaces = false;
            pso.DefaultValue = "ByLayer";
            PromptResult pr = editor.GetString(pso);
            if (pr.Status == PromptStatus.OK && !string.IsNullOrWhiteSpace(pr.StringResult) && !pr.StringResult.Equals("ByLayer", StringComparison.OrdinalIgnoreCase))
            {
                short colorIndex = UtilSelection.ConvertColorNameToIndex(pr.StringResult);
                if (colorIndex != -1)
                {
                    return colorIndex;
                }
                else
                {
                    editor.WriteMessage("\nMàu không hợp lệ, sẽ dùng màu ByLayer.");
                }
            }
            return 256; // Mã màu cho ByLayer
        }

        #endregion
    }
}