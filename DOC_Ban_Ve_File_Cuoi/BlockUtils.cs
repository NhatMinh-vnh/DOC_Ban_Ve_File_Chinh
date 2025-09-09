using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.IO; // Cần cho FileShare

// TẠO BÍ DANH
using AcColor = Autodesk.AutoCAD.Colors.Color;

namespace MyAutoCAD2026Plugin
{
    /// <summary>
    /// Lớp tiện ích Backend, chứa các hàm để thao tác với Block trong Database của AutoCAD.
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
            ObjectId blockDefId = ObjectId.Null;

            // Mở một database tạm thời để đọc file DWG nguồn
            using (Database sourceDb = new Database(false, true))
            {
                try
                {
                    // Đọc file DWG từ đường dẫn. FileShare.Read cho phép các tiến trình khác đọc file này.
                    sourceDb.ReadDwgFile(sourceFilePath, FileShare.Read, true, "");
                }
                catch (System.Exception) // Bắt lỗi chung như file không tồn tại, file lỗi...
                {
                    return ObjectId.Null; // Trả về Null nếu không đọc được file
                }

                // Bắt đầu transaction trên cả hai database
                using (Transaction sourceTrans = sourceDb.TransactionManager.StartTransaction())
                using (Transaction targetTrans = targetDb.TransactionManager.StartTransaction())
                {
                    BlockTable sourceBt = sourceTrans.GetObject(sourceDb.BlockTableId, OpenMode.ForRead) as BlockTable;

                    // Kiểm tra xem block có tồn tại trong file nguồn không
                    if (!sourceBt.Has(blockName))
                    {
                        return ObjectId.Null; // Trả về Null nếu block không có trong file nguồn
                    }

                    BlockTable targetBt = targetTrans.GetObject(targetDb.BlockTableId, OpenMode.ForRead) as BlockTable;

                    // Nếu block đã có trong bản vẽ đích, chỉ lấy ObjectId của nó
                    if (targetBt.Has(blockName))
                    {
                        blockDefId = targetBt[blockName];
                    }
                    else // Nếu chưa có, tiến hành nhập khẩu
                    {
                        // Nâng cấp quyền để có thể ghi vào BlockTable đích
                        targetBt.UpgradeOpen();

                        ObjectIdCollection idsToCopy = new ObjectIdCollection();
                        idsToCopy.Add(sourceBt[blockName]);

                        // Sao chép định nghĩa block từ sourceDb sang targetDb
                        IdMapping idMap = new IdMapping();
                        targetDb.WblockCloneObjects(idsToCopy, targetBt.ObjectId, idMap, DuplicateRecordCloning.Replace, false);

                        blockDefId = idMap[sourceBt[blockName]].Value;
                    }

                    targetTrans.Commit();
                }
            }
            return blockDefId;
        }


        /// <summary>
        /// (Backend) Chèn một BlockReference (có thể là Dynamic Block) vào ModelSpace.
        /// </summary>
        /// <param name="db">Database của bản vẽ cần chèn block.</param>
        /// <param name="blockDefId">ObjectId của BlockTableRecord (định nghĩa block).</param>
        /// <param name="insertionPoint">Điểm chèn.</param>
        /// <param name="layerName">Tên layer để chèn block vào.</param>
        /// <param name="scale">Tỉ lệ chèn.</param>
        /// <param name="rotationAngle">Góc xoay (radian).</param>
        /// <param name="dynamicProperties">Từ điển chứa các thuộc tính động cần thay đổi.</param>
        /// <returns>ObjectId của BlockReference vừa được chèn, hoặc ObjectId.Null nếu thất bại.</returns>
        public static ObjectId InsertBlockReference(Database db, ObjectId blockDefId, Point3d insertionPoint,
            string layerName, Scale3d scale, double rotationAngle, Dictionary<string, object> dynamicProperties)
        {
            if (blockDefId.IsNull) return ObjectId.Null;

            ObjectId blockRefId = ObjectId.Null;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTableRecord ms = tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite) as BlockTableRecord;

                // Tạo đối tượng BlockReference trong bộ nhớ
                using (BlockReference br = new BlockReference(insertionPoint, blockDefId))
                {
                    // === GÁN CÁC THUỘC TÍNH CƠ BẢN ===
                    br.Layer = layerName;
                    br.ScaleFactors = scale;
                    br.Rotation = rotationAngle;

                    // === XỬ LÝ THUỘC TÍNH ĐỘNG (DYNAMIC PROPERTIES) ===
                    if (br.IsDynamicBlock && dynamicProperties != null)
                    {
                        // Duyệt qua từng thuộc tính mà người dùng muốn thay đổi
                        foreach (var prop in dynamicProperties)
                        {
                            // Duyệt qua tất cả các thuộc tính động có sẵn của block
                            foreach (DynamicBlockReferenceProperty dbrProp in br.DynamicBlockReferencePropertyCollection)
                            {
                                // Nếu tìm thấy thuộc tính có tên khớp (không phân biệt hoa thường)
                                if (dbrProp.PropertyName.Equals(prop.Key, StringComparison.OrdinalIgnoreCase))
                                {
                                    try
                                    {
                                        // Gán giá trị mới. Đây là nơi "phép màu" xảy ra.
                                        dbrProp.Value = prop.Value;
                                        break; // Thoát vòng lặp bên trong khi đã tìm thấy và gán
                                    }
                                    catch (System.Exception)
                                    {
                                        // Ghi nhận lỗi có thể xảy ra (ví dụ: gán giá trị sai kiểu)
                                        // Trong backend, ta không hiển thị message, có thể log lỗi hoặc bỏ qua
                                    }
                                }
                            }
                        }
                    }

                    blockRefId = ms.AppendEntity(br);
                    tr.AddNewlyCreatedDBObject(br, true);
                }
                tr.Commit();
            }
            return blockRefId;
        }
    }
}