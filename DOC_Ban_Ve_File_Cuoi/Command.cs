// Thêm các thư viện cần thiết
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;

// Thêm thư viện hệ thống
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// Thêm thư viện JSON đã cài đặt
using Newtonsoft.Json;

// TẠO BÍ DANH ĐỂ GIẢI QUYẾT XUNG ĐỘT
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MyAutoCAD2026Plugin
{
    public class DrawingAnalyzer
    {
        // === LỆNH 1: ĐẾM VÀ XUẤT JSON (Không thay đổi) ===
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

        // === LỆNH 2: CHỌN ĐỐI TƯỢNG THEO LOẠI (Bản nâng cấp, tìm sâu trong block) ===
        [CommandMethod("SELECT_BY_TYPE")]
        public static void SelectByType()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Database db = doc.Database;
            Editor editor = doc.Editor;

            // Bước 1: Hỏi người dùng muốn lọc loại đối tượng nào
            PromptKeywordOptions pko = new PromptKeywordOptions("\nChọn loại đối tượng muốn lọc (kể cả trong block): ");
            pko.Keywords.Add("Line");
            pko.Keywords.Add("Polyline");
            pko.Keywords.Add("Arc");
            pko.Keywords.Add("Circle");
            pko.Keywords.Add("Text");
            pko.AllowNone = true;
            PromptResult pkr = editor.GetKeywords(pko);
            if (pkr.Status != PromptStatus.OK) return;

            string selectedType = pkr.StringResult;
            string dxfName = selectedType.ToUpper();
            if (dxfName == "TEXT") dxfName = "MTEXT,TEXT";

            // Bước 2: Yêu cầu người dùng chọn tất cả đối tượng trong một vùng
            PromptSelectionResult psr = editor.GetSelection();
            if (psr.Status != PromptStatus.OK) return;

            List<ObjectId> finalResultIds = new List<ObjectId>();

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // Bước 3: Duyệt qua các đối tượng người dùng đã chọn
                foreach (ObjectId selectedId in psr.Value.GetObjectIds())
                {
                    Entity ent = tr.GetObject(selectedId, OpenMode.ForRead) as Entity;
                    if (ent == null) continue;

                    // Trường hợp 1: Đối tượng là BlockReference
                    if (ent is BlockReference br)
                    {
                        // Mở định nghĩa của block đó ra
                        BlockTableRecord btr = tr.GetObject(br.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                        if (btr == null) continue;

                        // Duyệt qua từng đối tượng con bên trong định nghĩa block
                        foreach (ObjectId idInBlock in btr)
                        {
                            Entity subEnt = tr.GetObject(idInBlock, OpenMode.ForRead) as Entity;
                            if (subEnt == null) continue;

                            // Kiểm tra xem đối tượng con có khớp với loại người dùng chọn không
                            string typeName = subEnt.GetType().Name.ToUpper();
                            if (dxfName.Contains(typeName))
                            {
                                // QUAN TRỌNG: Chúng ta không thể chọn trực tiếp đối tượng con.
                                // Chúng ta phải chọn BlockReference chứa nó.
                                if (!finalResultIds.Contains(br.ObjectId))
                                {
                                    finalResultIds.Add(br.ObjectId);
                                }
                            }
                        }
                    }
                    // Trường hợp 2: Đối tượng là đối tượng thường, nằm bên ngoài
                    else
                    {
                        // Kiểm tra xem đối tượng này có khớp với loại người dùng chọn không
                        string typeName = ent.GetType().Name.ToUpper();
                        if (dxfName.Contains(typeName))
                        {
                            if (!finalResultIds.Contains(ent.ObjectId))
                            {
                                finalResultIds.Add(ent.ObjectId);
                            }
                        }
                    }
                }
                tr.Commit();
            }

            // Bước 4: Hiển thị kết quả
            if (finalResultIds.Count > 0)
            {
                editor.WriteMessage($"\nĐã tìm thấy {finalResultIds.Count} đối tượng (hoặc block chứa đối tượng) thỏa mãn điều kiện.");
                editor.SetImpliedSelection(finalResultIds.ToArray());
            }
            else
            {
                editor.WriteMessage("\nKhông có đối tượng nào thỏa mãn điều kiện được tìm thấy.");
            }
        }


        #region Helper Functions (Không thay đổi)
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
