using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.IO;

// Namespace giúp tổ chức code, tránh xung đột tên.
namespace MyAutoCAD2026Plugin.Backend
{
    /// <summary>
    /// (Backend) Lớp tiện ích để tạo và quản lý Layer.
    /// Hoạt động hoàn toàn trên Database.
    /// </summary>
    public static class LayerUtils
    {
        /// <summary>
        /// Đảm bảo một layer tồn tại trong Database. Nếu chưa có, sẽ tạo mới.
        /// Hàm này đã được làm cho "an toàn" trước các đầu vào không hợp lệ.
        /// </summary>
        /// <param name="db">Database đích.</param>
        /// <param name="layerName">Tên layer cần kiểm tra/tạo. Nếu null/rỗng, sẽ mặc định là layer "0".</param>
        /// <returns>ObjectId của LayerTableRecord.</returns>
        public static ObjectId EnsureLayerExists(Database db, string layerName)
        {
            // === REFACTOR: THÊM BƯỚC KIỂM TRA ĐẦU VÀO ===
            // Áp dụng chính xác logic đã học từ GeometryCreator.
            if (string.IsNullOrWhiteSpace(layerName))
            {
                layerName = "0";
            }
            // ===========================================

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;

                if (lt.Has(layerName))
                {
                    // Không cần commit vì đây là thao tác chỉ đọc.
                    return lt[layerName];
                }
                else
                {
                    // Chỉ nâng cấp quyền ghi khi thực sự cần.
                    lt.UpgradeOpen();

                    using (LayerTableRecord ltr = new LayerTableRecord())
                    {
                        ltr.Name = layerName;
                        ObjectId newLayerId = lt.Add(ltr);
                        tr.AddNewlyCreatedDBObject(ltr, true);
                        tr.Commit(); // Commit transaction sau khi đã thay đổi database.
                        return newLayerId;
                    }
                }
            }
        }
    }

    /// <summary>
    /// (Backend) Lớp dùng để đóng gói tất cả các thuộc tính khi chèn một BlockReference.
    /// </summary>
    public class BlockProperties
    {
        public Point3d Position { get; set; } = Point3d.Origin;
        public Scale3d Scale { get; set; } = new Scale3d(1.0);
        public double Rotation { get; set; } = 0.0;
        public Dictionary<string, object> DynamicProperties { get; set; }

        public BlockProperties()
        {
            DynamicProperties = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }
    }


    /// <summary>
    /// (Backend) Lớp tiện ích chính, chứa các hàm để thao tác với Block trong Database của AutoCAD.
    /// </summary>
    public static class BlockUtils
    {
        /// <summary>
        /// (Backend) Nhập khẩu một định nghĩa block từ file DWG bên ngoài vào database đích.
        /// </summary>
        /// <returns>ObjectId của BlockTableRecord, hoặc ObjectId.Null nếu thất bại.</returns>
        public static ObjectId ImportBlockDefinition(Database targetDb, string sourceFilePath, string blockName)
        {
            // === REFACTOR: KIỂM TRA TẤT CẢ CÁC THAM SỐ ĐẦU VÀO ===
            if (string.IsNullOrWhiteSpace(sourceFilePath) || string.IsNullOrWhiteSpace(blockName))
            {
                // Nếu đường dẫn file hoặc tên block không hợp lệ, không thể tiếp tục.
                return ObjectId.Null;
            }
            if (!File.Exists(sourceFilePath))
            {
                // Nếu file không tồn tại, cũng không thể tiếp tục.
                System.Diagnostics.Debug.WriteLine($"Lỗi ImportBlockDefinition: File không tồn tại tại '{sourceFilePath}'");
                return ObjectId.Null;
            }
            // =======================================================

            ObjectId blockIdToCopy = ObjectId.Null;

            using (Database sourceDb = new Database(false, true))
            {
                try
                {
                    sourceDb.ReadDwgFile(sourceFilePath, FileShare.Read, true, "");
                }
                catch (Autodesk.AutoCAD.Runtime.Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Lỗi khi đọc file DWG: {ex.Message}");
                    return ObjectId.Null;
                }

                using (Transaction sourceTrans = sourceDb.TransactionManager.StartTransaction())
                {
                    BlockTable sourceBt = sourceTrans.GetObject(sourceDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                    if (sourceBt == null || !sourceBt.Has(blockName))
                    {
                        return ObjectId.Null;
                    }
                    blockIdToCopy = sourceBt[blockName];
                }
            }

            if (blockIdToCopy.IsNull) return ObjectId.Null;

            using (Transaction targetTrans = targetDb.TransactionManager.StartTransaction())
            {
                BlockTable targetBt = targetTrans.GetObject(targetDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                if (targetBt == null) return ObjectId.Null;

                if (targetBt.Has(blockName))
                {
                    return targetBt[blockName];
                }

                targetBt.UpgradeOpen();

                ObjectIdCollection idsToCopy = new ObjectIdCollection { blockIdToCopy };
                IdMapping idMap = new IdMapping();
                targetDb.WblockCloneObjects(idsToCopy, targetBt.ObjectId, idMap, DuplicateRecordCloning.Replace, false);

                // === REFACTOR: KIỂM TRA KẾT QUẢ ÁNH XẠ ID ===
                // Đảm bảo việc sao chép thực sự thành công trước khi truy cập kết quả.
                if (idMap.Contains(blockIdToCopy) && idMap[blockIdToCopy].IsCloned)
                {
                    targetTrans.Commit();
                    return idMap[blockIdToCopy].Value;
                }
                else
                {
                    // Nếu vì lý do nào đó không clone được, hủy transaction và trả về null.
                    targetTrans.Abort();
                    return ObjectId.Null;
                }
                // ============================================
            }
        }


        /// <summary>
        /// (Backend) Chèn một BlockReference (có thể là Dynamic Block) vào ModelSpace.
        /// </summary>
        public static ObjectId InsertBlockReference(Database db, string layerName, ObjectId blockDefId, BlockProperties props)
        {
            if (blockDefId.IsNull || props == null)
            {
                return ObjectId.Null;
            }

            // === REFACTOR: CHUẨN HÓA DỮ LIỆU LAYER NGAY TỪ ĐẦU ===
            // Kiểm tra và gán giá trị mặc định cho layerName.
            // Biến local 'validLayerName' sẽ được sử dụng an toàn trong suốt hàm.
            string validLayerName = string.IsNullOrWhiteSpace(layerName) ? "0" : layerName;
            // =======================================================

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTableRecord ms = tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite) as BlockTableRecord;

                using (BlockReference br = new BlockReference(props.Position, blockDefId))
                {
                    // Đảm bảo layer tồn tại (sử dụng biến đã được làm sạch).
                    // Mặc dù LayerUtils đã an toàn, việc chuẩn hóa ở đây giúp code rõ ràng hơn.
                    LayerUtils.EnsureLayerExists(db, validLayerName);
                    // Gán layer một cách an toàn.
                    br.Layer = validLayerName;

                    br.ScaleFactors = props.Scale;
                    br.Rotation = props.Rotation;

                    ObjectId blockRefId = ms.AppendEntity(br);
                    tr.AddNewlyCreatedDBObject(br, true);

                    if (br.IsDynamicBlock && props.DynamicProperties != null && props.DynamicProperties.Count > 0)
                    {
                        DynamicBlockReferencePropertyCollection dynProps = br.DynamicBlockReferencePropertyCollection;
                        foreach (KeyValuePair<string, object> entry in props.DynamicProperties)
                        {
                            foreach (DynamicBlockReferenceProperty dbrProp in dynProps)
                            {
                                if (dbrProp.PropertyName.Equals(entry.Key, StringComparison.OrdinalIgnoreCase))
                                {
                                    try
                                    {
                                        dbrProp.Value = entry.Value;
                                        break;
                                    }
                                    catch (Autodesk.AutoCAD.Runtime.Exception) { /* Bỏ qua lỗi gán giá trị không hợp lệ */ }
                                }
                            }
                        }
                    }

                    br.RecordGraphicsModified(true);
                    tr.Commit();
                    return blockRefId;
                }
            }
        }
    }
}