// Các thư viện cần thiết
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Colors;
using System;

// TẠO BÍ DANH
using AcColor = Autodesk.AutoCAD.Colors.Color;

namespace MyAutoCAD2026Plugin
{
    /// <summary>
    /// Lớp tiện ích Backend, chứa các hàm để thao tác với Database của AutoCAD.
    /// Lớp này không chứa bất kỳ mã nào tương tác với người dùng (Editor).
    /// </summary>
    public static class GeometryCreator
    {
        #region Layer Utilities

        /// <summary>
        /// (Backend) Kiểm tra xem một Layer có tồn tại trong bản vẽ hay không.
        /// </summary>
        /// <param name="db">Database của bản vẽ cần kiểm tra.</param>
        /// <param name="layerName">Tên Layer cần kiểm tra.</param>
        /// <returns>True nếu Layer tồn tại, False nếu không.</returns>
        public static bool LayerExists(Database db, string layerName)
        {
            bool result = false;
            // Bắt đầu một Transaction chỉ để đọc
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // Mở bảng Layer ở chế độ chỉ đọc
                LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;

                // Dùng phương thức Has() để kiểm tra sự tồn tại
                if (lt.Has(layerName))
                {
                    result = true;
                }
                // Transaction sẽ tự động được Abort khi kết thúc khối using, vì không có Commit()
            }
            return result;
        }

        /// <summary>
        /// (Backend) Tạo một Layer mới trong bản vẽ.
        /// </summary>
        /// <param name="db">Database của bản vẽ cần tạo Layer.</param>
        /// <param name="layerName">Tên của Layer mới.</param>
        /// <returns>ObjectId của Layer vừa được tạo, hoặc ObjectId.Null nếu thất bại.</returns>
        public static ObjectId CreateLayer(Database db, string layerName)
        {
            ObjectId layerId = ObjectId.Null;
            // Bắt đầu một Transaction để ghi
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // Mở bảng Layer ở chế độ để ghi
                LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForWrite) as LayerTable;

                // Kiểm tra lại để chắc chắn Layer chưa tồn tại trước khi tạo
                if (!lt.Has(layerName))
                {
                    // Tạo một đối tượng LayerTableRecord mới trong bộ nhớ
                    using (LayerTableRecord newLtr = new LayerTableRecord())
                    {
                        // Gán tên cho Layer
                        newLtr.Name = layerName;

                        // Thêm Layer mới vào Bảng Layer
                        lt.Add(newLtr);

                        // Đăng ký đối tượng mới với Transaction
                        tr.AddNewlyCreatedDBObject(newLtr, true);

                        // Lấy ObjectId để trả về
                        layerId = newLtr.ObjectId;
                    }
                }
                // Lưu các thay đổi vào Database
                tr.Commit();
            }
            return layerId;
        }

        #endregion

        #region Geometry Creation

        /// <summary>
        /// (Backend) Tạo một đối tượng Line.
        /// </summary>
        /// <returns>ObjectId của Line vừa tạo.</returns>
        public static ObjectId CreateLine(Database db, Point3d startPt, Point3d endPt, string layerName, short colorIndex)
        {
            ObjectId lineId = ObjectId.Null;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTableRecord ms = tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite) as BlockTableRecord;

                using (Line line = new Line(startPt, endPt)) // Thông số hình học
                {
                    line.Layer = layerName;      // Thông số Layer
                    line.Color = AcColor.FromColorIndex(ColorMethod.ByAci, colorIndex); // Thông số màu sắc

                    ms.AppendEntity(line);
                    tr.AddNewlyCreatedDBObject(line, true);
                    lineId = line.ObjectId;
                }
                tr.Commit();
            }
            return lineId;
        }

        /// <summary>
        /// (Backend) Tạo một đối tượng Circle.
        /// </summary>
        /// <returns>ObjectId của Circle vừa tạo.</returns>
        public static ObjectId CreateCircle(Database db, Point3d center, double radius, string layerName, short colorIndex)
        {
            ObjectId circleId = ObjectId.Null;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTableRecord ms = tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite) as BlockTableRecord;

                // Vector pháp tuyến mặc định là trục Z
                using (Circle circle = new Circle(center, Vector3d.ZAxis, radius)) // Thông số hình học
                {
                    circle.Layer = layerName;    // Thông số Layer
                    circle.Color = AcColor.FromColorIndex(ColorMethod.ByAci, colorIndex); // Thông số màu sắc

                    ms.AppendEntity(circle);
                    tr.AddNewlyCreatedDBObject(circle, true);
                    circleId = circle.ObjectId;
                }
                tr.Commit();
            }
            return circleId;
        }

        /// <summary>
        /// (Backend) Tạo một đối tượng Polyline.
        /// </summary>
        /// <returns>ObjectId của Polyline vừa tạo.</returns>
        public static ObjectId CreatePolyline(Database db, Point3dCollection points, string layerName, short colorIndex)
        {
            // Yêu cầu phải có ít nhất 2 điểm để tạo Polyline
            if (points.Count < 2) return ObjectId.Null;

            ObjectId plineId = ObjectId.Null;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTableRecord ms = tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite) as BlockTableRecord;

                using (Polyline pline = new Polyline())
                {
                    // Thêm các đỉnh vào polyline từ Point3dCollection (Thông số hình học)
                    for (int i = 0; i < points.Count; i++)
                    {
                        pline.AddVertexAt(i, new Point2d(points[i].X, points[i].Y), 0, 0, 0);
                    }

                    pline.Layer = layerName;     // Thông số Layer
                    pline.Color = AcColor.FromColorIndex(ColorMethod.ByAci, colorIndex); // Thông số màu sắc

                    ms.AppendEntity(pline);
                    tr.AddNewlyCreatedDBObject(pline, true);
                    plineId = pline.ObjectId;
                }
                tr.Commit();
            }
            return plineId;
        }

        /// <summary>
        /// (Backend) Tạo một đối tượng Arc từ 3 điểm.
        /// </summary>
        /// <returns>ObjectId của Arc vừa tạo, hoặc ObjectId.Null nếu 3 điểm thẳng hàng.</returns>
        public static ObjectId CreateArc(Database db, Point3d startPt, Point3d ptOnArc, Point3d endPt, string layerName, short colorIndex)
        {
            ObjectId arcId = ObjectId.Null;
            try
            {
                // CircularArc3d dùng để tính toán hình học của cung tròn từ 3 điểm.
                // Nó sẽ ném ra exception nếu 3 điểm thẳng hàng.
                CircularArc3d tempArc = new CircularArc3d(startPt, ptOnArc, endPt); // Thông số hình học

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTableRecord ms = tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite) as BlockTableRecord;

                    // Tạo đối tượng Arc thực sự từ các thông số đã tính toán
                    using (Arc arc = new Arc(tempArc.Center, tempArc.Radius, tempArc.StartAngle, tempArc.EndAngle))
                    {
                        arc.Layer = layerName;       // Thông số Layer
                        arc.Color = AcColor.FromColorIndex(ColorMethod.ByAci, colorIndex); // Thông số màu sắc

                        ms.AppendEntity(arc);
                        tr.AddNewlyCreatedDBObject(arc, true);
                        arcId = arc.ObjectId;
                    }
                    tr.Commit();
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
                // Nếu 3 điểm thẳng hàng, không làm gì và trả về ObjectId.Null
                return ObjectId.Null;
            }
            return arcId;
        }

        #endregion
    }
}