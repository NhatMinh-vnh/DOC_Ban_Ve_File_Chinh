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
        /// </summary>
        /// <param name="db">Database đích.</param>
        /// <param name="layerName">Tên layer cần kiểm tra/tạo.</param>
        /// <returns>ObjectId của LayerTableRecord.</returns>
        public static ObjectId EnsureLayerExists(Database db, string layerName)
        {
            // Bắt đầu một Transaction để tương tác với Database.
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // Lấy LayerTable từ Database để quản lý các layer.
                // Mở ở chế độ đọc (ForRead) trước cho hiệu năng.
                LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;

                // Kiểm tra xem layer đã tồn tại chưa.
                if (lt.Has(layerName))
                {
                    // Nếu đã có, chỉ cần trả về ObjectId của nó.
                    return lt[layerName];
                }
                else
                {
                    // Nếu chưa có, chúng ta cần tạo mới.
                    // Phải nâng cấp quyền của LayerTable lên chế độ ghi (ForWrite).
                    lt.UpgradeOpen();

                    // Tạo một đối tượng LayerTableRecord mới trong bộ nhớ.
                    using (LayerTableRecord ltr = new LayerTableRecord())
                    {
                        // Gán tên cho layer. Đây là thuộc tính quan trọng nhất.
                        ltr.Name = layerName;
                        // Bạn có thể gán các thuộc tính khác ở đây, ví dụ: màu sắc.
                        // ltr.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(ColorMethod.ByAci, 1); // Màu đỏ

                        // Thêm layer mới vào LayerTable.
                        ObjectId newLayerId = lt.Add(ltr);
                        // Báo cho Transaction biết về đối tượng mới được tạo này.
                        tr.AddNewlyCreatedDBObject(ltr, true);

                        // Lưu lại tất cả các thay đổi vào Database.
                        tr.Commit();

                        // Trả về ObjectId của layer vừa tạo.
                        return newLayerId;
                    }
                }
            }
        }
    }

    /// <summary>
    /// (Backend) Lớp dùng để đóng gói tất cả các thuộc tính khi chèn một BlockReference.
    /// Giúp cho chữ ký của hàm InsertBlockReference gọn gàng hơn.
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
    /// Lớp này không chứa bất kỳ mã nào tương tác với người dùng (Editor).
    /// </summary>
    public static class BlockUtils
    {
        /// <summary>
        /// (Backend) Nhập khẩu một định nghĩa block từ file DWG bên ngoài vào database đích.
        /// Hàm này sẽ kiểm tra, nếu block đã tồn tại thì không làm gì và trả về ObjectId của nó.
        /// </summary>
        /// <param name="targetDb">Database của bản vẽ đích cần nhập khẩu block vào.</param>
        /// <param name="sourceFilePath">Đường dẫn đầy đủ đến file DWG nguồn.</param>
        /// <param name="blockName">Tên của block cần nhập khẩu.</param>
        /// <returns>ObjectId của BlockTableRecord đã được nhập khẩu, hoặc ObjectId.Null nếu thất bại.</returns>
        public static ObjectId ImportBlockDefinition(Database targetDb, string sourceFilePath, string blockName)
        {
            // === PHẦN SỬA LỖI BẮT ĐẦU TỪ ĐÂY ===

            ObjectId blockIdToCopy = ObjectId.Null;

            // BƯỚC 1: Mở database nguồn, tìm block cần sao chép và chỉ lấy về ObjectId của nó.
            // Hoàn thành tất cả công việc trên database nguồn trong một giao dịch duy nhất.
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

                // Bắt đầu giao dịch CHỈ trên database nguồn.
                using (Transaction sourceTrans = sourceDb.TransactionManager.StartTransaction())
                {
                    BlockTable sourceBt = sourceTrans.GetObject(sourceDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                    if (!sourceBt.Has(blockName))
                    {
                        return ObjectId.Null; // Không tìm thấy block trong file nguồn.
                    }
                    // Lấy ObjectId và lưu vào một biến.
                    blockIdToCopy = sourceBt[blockName];
                } // Giao dịch trên sourceDb được đóng lại ngay tại đây.
            } // Database nguồn cũng được hủy ngay tại đây. Công việc với nó đã xong.

            // Nếu không tìm thấy block, kết thúc sớm.
            if (blockIdToCopy.IsNull) return ObjectId.Null;


            // BƯỚC 2: Mở một giao dịch mới trên database đích để thực hiện việc sao chép.
            // Giao dịch này hoàn toàn độc lập với giao dịch ở trên.
            using (Transaction targetTrans = targetDb.TransactionManager.StartTransaction())
            {
                // Lấy BlockTable của database đích.
                BlockTable targetBt = targetTrans.GetObject(targetDb.BlockTableId, OpenMode.ForRead) as BlockTable;

                // Kiểm tra xem block đã tồn tại trong bản vẽ đích chưa.
                if (targetBt.Has(blockName))
                {
                    return targetBt[blockName]; // Nếu có, trả về ObjectId của nó và không làm gì thêm.
                }

                // Nâng cấp quyền để có thể ghi vào BlockTable đích.
                targetBt.UpgradeOpen();

                // Chuẩn bị một collection để chứa ObjectId cần sao chép.
                ObjectIdCollection idsToCopy = new ObjectIdCollection();
                idsToCopy.Add(blockIdToCopy);

                // Thực hiện sao chép.
                IdMapping idMap = new IdMapping();
                // NOTE: Dòng lệnh quan trọng WblockCloneObjects giờ được gọi trong một bối cảnh an toàn hơn nhiều.
                targetDb.WblockCloneObjects(idsToCopy, targetBt.ObjectId, idMap, DuplicateRecordCloning.Replace, false);

                // Lưu các thay đổi vào database đích.
                targetTrans.Commit();

                // Lấy ObjectId của block mới được tạo ra trong database đích.
                // idMap chứa ánh xạ từ ObjectId cũ (trong file nguồn) sang ObjectId mới (trong file đích).
                return idMap[blockIdToCopy].Value;
            }
            // === KẾT THÚC PHẦN SỬA LỖI ===
        }


        /// <summary>
        /// (Backend) Chèn một BlockReference (có thể là Dynamic Block) vào ModelSpace.
        /// </summary>
        /// <param name="db">Database của bản vẽ cần chèn block.</param>
        /// <param name="layerName">Tên layer để chèn block vào. Layer sẽ được tạo nếu chưa có.</param>
        /// <param name="blockDefId">ObjectId của BlockTableRecord (định nghĩa block).</param>
        /// <param name="props">Đối tượng chứa tất cả thuộc tính chèn (vị trí, scale, xoay, dynamic props).</param>
        /// <returns>ObjectId của BlockReference vừa được chèn, hoặc ObjectId.Null nếu thất bại.</returns>
        public static ObjectId InsertBlockReference(Database db, string layerName, ObjectId blockDefId, BlockProperties props)
        {
            // Kiểm tra đầu vào quan trọng. Nếu định nghĩa block không hợp lệ, không thể tiếp tục.
            if (blockDefId.IsNull || props == null)
            {
                return ObjectId.Null;
            }

            // Bắt đầu transaction để thêm đối tượng mới vào database.
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // Lấy đối tượng BlockTableRecord của ModelSpace để có thể thêm đối tượng vào đó.
                // Luôn mở ở chế độ ForWrite vì ta sắp thêm entity.
                BlockTableRecord ms = tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite) as BlockTableRecord;

                // --- PHẦN CODE QUAN TRỌNG: TẠO VÀ CẤU HÌNH BLOCKREFERENCE ---
                // Tạo đối tượng BlockReference trong bộ nhớ, liên kết nó với định nghĩa block.
                using (BlockReference br = new BlockReference(props.Position, blockDefId))
                {
                    // --- CODE LIÊN QUAN ĐẾN THÔNG SỐ LAYER ---
                    // Đảm bảo layer tồn tại trước khi gán.
                    LayerUtils.EnsureLayerExists(db, layerName);
                    br.Layer = layerName;

                    // --- CODE LIÊN QUAN ĐẾN THÔNG SỐ CƠ BẢN CỦA ĐỐI TƯỢNG ---
                    br.ScaleFactors = props.Scale; // Gán tỉ lệ
                    br.Rotation = props.Rotation;   // Gán góc xoay (radian)
                    // Màu sắc thường được điều khiển bởi layer (ByLayer),
                    // nhưng nếu muốn gán trực tiếp, dùng:
                    // br.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(ColorMethod.ByAci, 2); // Màu vàng

                    // Thêm thực thể vào ModelSpace và vào transaction.
                    ObjectId blockRefId = ms.AppendEntity(br);
                    tr.AddNewlyCreatedDBObject(br, true);

                    // --- PHẦN CODE QUAN TRỌNG: XỬ LÝ THUỘC TÍNH ĐỘNG (DYNAMIC PROPERTIES) ---
                    // Chỉ xử lý khi block này thực sự là Dynamic Block và có thuộc tính cần gán.
                    if (br.IsDynamicBlock && props.DynamicProperties != null && props.DynamicProperties.Count > 0)
                    {
                        // Lấy collection chứa các thuộc tính động.
                        DynamicBlockReferencePropertyCollection dynProps = br.DynamicBlockReferencePropertyCollection;

                        // Duyệt qua từng cặp (Tên, Giá trị) mà người dùng muốn thay đổi.
                        foreach (KeyValuePair<string, object> entry in props.DynamicProperties)
                        {
                            // Duyệt qua các thuộc tính có sẵn trong block.
                            foreach (DynamicBlockReferenceProperty dbrProp in dynProps)
                            {
                                // So sánh tên thuộc tính (không phân biệt hoa thường) để tìm thuộc tính tương ứng.
                                if (dbrProp.PropertyName.Equals(entry.Key, StringComparison.OrdinalIgnoreCase))
                                {
                                    try
                                    {
                                        // Gán giá trị mới. AutoCAD sẽ tự động cập nhật hình học của block.
                                        dbrProp.Value = entry.Value;
                                        // Khi đã tìm thấy và gán xong, thoát vòng lặp bên trong để tối ưu.
                                        break;
                                    }
                                    catch (Autodesk.AutoCAD.Runtime.Exception)
                                    {
                                        // Bỏ qua nếu giá trị gán không hợp lệ (ví dụ: gán chuỗi cho thuộc tính khoảng cách).
                                        // Có thể thêm log lỗi ở đây.
                                    }
                                }
                            }
                        }
                    }

                    // --- PHẦN CODE QUAN TRỌNG: XỬ LÝ HATCH ---
                    // Sau khi tất cả các thuộc tính đã được thay đổi, cần yêu cầu AutoCAD
                    // vẽ lại đối tượng này để đảm bảo các đối tượng phụ thuộc như Hatch được cập nhật.
                    br.RecordGraphicsModified(true);

                    // Lưu tất cả thay đổi.
                    tr.Commit();

                    // Trả về ObjectId của BlockReference vừa được tạo.
                    return blockRefId;
                }
            }
        }
    }
}