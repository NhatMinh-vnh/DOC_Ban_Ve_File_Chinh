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
    }
}