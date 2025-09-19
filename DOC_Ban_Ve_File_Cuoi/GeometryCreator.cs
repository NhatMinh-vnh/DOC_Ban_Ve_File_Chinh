// Các thư viện cần thiết
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

// TẠO BÍ DANH ĐỂ CODE GỌN HƠN
using AcColor = Autodesk.AutoCAD.Colors.Color;

namespace MyAutoCAD2026Plugin
{
    /// <summary>
    /// Lớp tiện ích Backend, chứa các hàm để thao tác với Database của AutoCAD.
    /// Lớp này không chứa bất kỳ mã nào tương tác với người dùng (Editor).
    /// </summary>
    public static class GeometryCreator
    {
        #region Private Helper Methods

        /// <summary>
        /// (Backend-Private) Lấy ObjectId của một Layer. Nếu Layer chưa tồn tại, hàm sẽ tạo mới.
        /// Hàm này được gọi nội bộ bởi các hàm tạo hình học khác.
        /// </summary>
        /// <param name="db">Database của bản vẽ hiện hành.</param>
        /// <param name="tr">Transaction đang hoạt động. Hàm này sẽ không tự tạo Transaction mới.</param>
        /// <param name="layerName">Tên của Layer cần lấy hoặc tạo.</param>
        /// <returns>ObjectId của Layer tương ứng.</returns>
        private static ObjectId GetOrCreateLayer(Database db, Transaction tr, string layerName)
        {
            // BẮT BUỘC: Mở bảng Layer (LayerTable) ở chế độ đọc để kiểm tra.
            LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;

            // Dùng phương thức Has() để kiểm tra sự tồn tại của layer.
            if (lt.Has(layerName))
            {
                // Nếu layer đã tồn tại, trả về ObjectId của nó.
                return lt[layerName];
            }
            else
            {
                // Nếu layer chưa tồn tại, ta cần tạo mới.
                // B1: Tạo một đối tượng LayerTableRecord mới trong bộ nhớ.
                using (LayerTableRecord newLtr = new LayerTableRecord())
                {
                    // B2: Gán các thuộc tính cho layer mới.
                    newLtr.Name = layerName;
                    // Bạn có thể gán thêm các thuộc tính khác ở đây, ví dụ:
                    // newLtr.Color = AcColor.FromColorIndex(ColorMethod.ByAci, 1); // Màu đỏ

                    // B3: Nâng cấp quyền của LayerTable lên chế độ ghi để có thể thêm layer mới.
                    // Đây là một kỹ thuật tối ưu, tránh việc mở để ghi ngay từ đầu nếu không cần thiết.
                    tr.GetObject(db.LayerTableId, OpenMode.ForWrite); // Lệnh này chỉ để nâng quyền, không cần gán vào biến

                    // B4: Thêm layer mới (đang ở trong bộ nhớ) vào LayerTable.
                    lt.Add(newLtr);

                    // B5: ĐĂNG KÝ đối tượng vừa tạo với Transaction để nó được lưu vào Database khi Commit.
                    // *** ĐÂY LÀ BƯỚC CỰC KỲ QUAN TRỌNG, NẾU THIẾU, LAYER SẼ KHÔNG ĐƯỢC TẠO RA. ***
                    tr.AddNewlyCreatedDBObject(newLtr, true);

                    // B6: Trả về ObjectId của layer vừa được tạo.
                    return newLtr.ObjectId;
                }
            }
        }

        #endregion

        #region Geometry Creation

        /// <summary>
        /// (Backend) Tạo một đối tượng Line và thêm vào ModelSpace.
        /// Tự động tạo Layer nếu chưa tồn tại.
        /// </summary>
        /// <param name="db">Database của bản vẽ.</param>
        /// <param name="startPt">Điểm bắt đầu.</param>
        /// <param name="endPt">Điểm kết thúc.</param>
        /// <param name="layerName">Tên Layer để đặt đối tượng.</param>
        /// <param name="color">Màu của đối tượng (có thể là ACI hoặc TrueColor).</param>
        /// <returns>ObjectId của Line vừa tạo.</returns>
        public static ObjectId CreateLine(Database db, Point3d startPt, Point3d endPt, string layerName, AcColor color)
        {
            ObjectId lineId = ObjectId.Null;
            // Bắt đầu một Transaction để thực hiện các thay đổi.
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // Lấy về BlockTableRecord của ModelSpace ở chế độ ghi.
                BlockTableRecord ms = tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite) as BlockTableRecord;

                // *** PHẦN XỬ LÝ LAYER (TÍCH HỢP) ***
                // Gọi hàm helper để lấy hoặc tạo layer và nhận về ObjectId của nó.
                ObjectId layerId = GetOrCreateLayer(db, tr, layerName);

                // Tạo đối tượng Line trong bộ nhớ với các thông số hình học.
                // *** THÔNG SỐ HÌNH HỌC CƠ BẢN ***
                using (Line line = new Line(startPt, endPt))
                {
                    // Gán các thuộc tính cho đối tượng.
                    // *** THÔNG SỐ LAYER ***
                    line.LayerId = layerId;

                    // *** THÔNG SỐ MÀU SẮC ***
                    // Đối tượng AcColor có thể chứa cả ACI và TrueColor, ta chỉ cần gán trực tiếp.
                    // Yêu cầu hỗ trợ TrueColor đã được đáp ứng ở đây.
                    line.Color = color;

                    // Thêm đối tượng Line vào ModelSpace.
                    ms.AppendEntity(line);
                    // Đăng ký đối tượng mới với Transaction.
                    tr.AddNewlyCreatedDBObject(line, true);
                    // Lấy ObjectId để trả về.
                    lineId = line.ObjectId;
                }
                // Lưu tất cả các thay đổi vào Database.
                tr.Commit();
            }
            return lineId;
        }

        /// <summary>
        /// (Backend) Tạo một đối tượng Circle và thêm vào ModelSpace.
        /// </summary>
        public static ObjectId CreateCircle(Database db, Point3d center, double radius, string layerName, AcColor color)
        {
            ObjectId circleId = ObjectId.Null;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTableRecord ms = tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite) as BlockTableRecord;

                // *** PHẦN XỬ LÝ LAYER (TÍCH HỢP) ***
                ObjectId layerId = GetOrCreateLayer(db, tr, layerName);

                // *** THÔNG SỐ HÌNH HỌC CƠ BẢN ***
                // Vector pháp tuyến mặc định là trục Z (0,0,1) cho hình tròn trong mặt phẳng XY.
                using (Circle circle = new Circle(center, Vector3d.ZAxis, radius))
                {
                    // *** THÔNG SỐ LAYER ***
                    circle.LayerId = layerId;
                    // *** THÔNG SỐ MÀU SẮC ***
                    circle.Color = color;

                    ms.AppendEntity(circle);
                    tr.AddNewlyCreatedDBObject(circle, true);
                    circleId = circle.ObjectId;
                }
                tr.Commit();
            }
            return circleId;
        }

        /// <summary>
        /// (Backend) Tạo một đối tượng Polyline và thêm vào ModelSpace.
        /// </summary>
        public static ObjectId CreatePolyline(Database db, Point3dCollection points, string layerName, AcColor color)
        {
            // Yêu cầu phải có ít nhất 2 điểm để tạo Polyline.
            if (points.Count < 2) return ObjectId.Null;

            ObjectId plineId = ObjectId.Null;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTableRecord ms = tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite) as BlockTableRecord;

                // *** PHẦN XỬ LÝ LAYER (TÍCH HỢP) ***
                ObjectId layerId = GetOrCreateLayer(db, tr, layerName);

                // *** THÔNG SỐ HÌNH HỌC CƠ BẢN ***
                using (Polyline pline = new Polyline())
                {
                    // Dùng vòng lặp để thêm từng đỉnh vào Polyline.
                    for (int i = 0; i < points.Count; i++)
                    {
                        // Polyline 2D chỉ cần tọa độ X, Y.
                        pline.AddVertexAt(i, new Point2d(points[i].X, points[i].Y), 0, 0, 0);
                    }

                    // *** THÔNG SỐ LAYER ***
                    pline.LayerId = layerId;
                    // *** THÔNG SỐ MÀU SẮC ***
                    pline.Color = color;

                    ms.AppendEntity(pline);
                    tr.AddNewlyCreatedDBObject(pline, true);
                    plineId = pline.ObjectId;
                }
                tr.Commit();
            }
            return plineId;
        }

        /// <summary>
        /// (Backend) Tạo một đối tượng Arc từ 3 điểm và thêm vào ModelSpace.
        /// </summary>
        /// <returns>ObjectId của Arc vừa tạo, hoặc ObjectId.Null nếu 3 điểm thẳng hàng.</returns>
        public static ObjectId CreateArc(Database db, Point3d startPt, Point3d ptOnArc, Point3d endPt, string layerName, AcColor color)
        {
            ObjectId arcId = ObjectId.Null;
            try
            {
                // *** THÔNG SỐ HÌNH HỌC CƠ BẢN (Tính toán) ***
                // CircularArc3d là một lớp hình học thuần túy, không phải là đối tượng trong Database.
                // Nó được dùng để tính toán tâm, bán kính, góc... từ 3 điểm.
                // Nó sẽ ném ra một exception nếu 3 điểm thẳng hàng.
                CircularArc3d tempArc = new CircularArc3d(startPt, ptOnArc, endPt);

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTableRecord ms = tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite) as BlockTableRecord;

                    // *** PHẦN XỬ LÝ LAYER (TÍCH HỢP) ***
                    ObjectId layerId = GetOrCreateLayer(db, tr, layerName);

                    // *** THÔNG SỐ HÌNH HỌC CƠ BẢN (Tạo đối tượng) ***
                    // Tạo đối tượng Arc thực sự từ các thông số đã tính toán được.
                    using (Arc arc = new Arc(tempArc.Center, tempArc.Radius, tempArc.StartAngle, tempArc.EndAngle))
                    {
                        // *** THÔNG SỐ LAYER ***
                        arc.LayerId = layerId;
                        // *** THÔNG SỐ MÀU SẮC ***
                        arc.Color = color;

                        ms.AppendEntity(arc);
                        tr.AddNewlyCreatedDBObject(arc, true);
                        arcId = arc.ObjectId;
                    }
                    tr.Commit();
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                // Bắt lỗi nếu 3 điểm thẳng hàng.
                // Trong môi trường backend, ta không nên hiển thị thông báo.
                // Việc thông báo cho người dùng là của frontend.
                // Ta chỉ cần trả về ObjectId.Null để báo hiệu thao tác thất bại.
                System.Diagnostics.Debug.WriteLine($"Error creating arc: {ex.Message}");
                return ObjectId.Null;
            }
            return arcId;
        }

        #endregion
    }
}