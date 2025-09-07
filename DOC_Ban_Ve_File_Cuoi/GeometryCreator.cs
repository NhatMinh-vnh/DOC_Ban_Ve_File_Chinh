using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Colors;
using System.Collections.Generic;

// TẠO BÍ DANH ĐỂ TRÁNH XUNG ĐỘT
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using AcColor = Autodesk.AutoCAD.Colors.Color;

namespace MyAutoCAD2026Plugin
{
    /// <summary>
    /// Lớp tiện ích tĩnh, chuyên dùng để TẠO RA các đối tượng hình học cơ bản.
    /// Đây là "hộp công cụ" vẽ, giúp trừu tượng hóa các quy trình phức tạp của AutoCAD API.
    /// </summary>
    public static class GeometryCreator
    {
        /// <summary>
        /// Tạo một đối tượng Line và thêm vào ModelSpace.
        /// </summary>
        public static ObjectId CreateLine(Point3d startPoint, Point3d endPoint, string layer, short colorIndex)
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            ObjectId lineId = ObjectId.Null;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord ms = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                using (Line line = new Line(startPoint, endPoint))
                {
                    line.Layer = layer;
                    line.Color = AcColor.FromColorIndex(ColorMethod.ByAci, colorIndex);
                    lineId = ms.AppendEntity(line);
                    tr.AddNewlyCreatedDBObject(line, true);
                }
                tr.Commit();
            }
            return lineId;
        }

        /// <summary>
        /// Tạo một đối tượng Circle và thêm vào ModelSpace.
        /// </summary>
        public static ObjectId CreateCircle(Point3d center, double radius, string layer, short colorIndex)
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            ObjectId circleId = ObjectId.Null;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord ms = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                using (Circle circle = new Circle(center, Vector3d.ZAxis, radius))
                {
                    circle.Layer = layer;
                    circle.Color = AcColor.FromColorIndex(ColorMethod.ByAci, colorIndex);
                    circleId = ms.AppendEntity(circle);
                    tr.AddNewlyCreatedDBObject(circle, true);
                }
                tr.Commit();
            }
            return circleId;
        }

        /// <summary>
        /// Tạo một đối tượng Polyline và thêm vào ModelSpace từ một danh sách các điểm.
        /// </summary>
        public static ObjectId CreatePolyline(Point3dCollection points, string layer, short colorIndex)
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            ObjectId polylineId = ObjectId.Null;

            if (points.Count < 2) return ObjectId.Null;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord ms = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                using (Polyline pline = new Polyline())
                {
                    for (int i = 0; i < points.Count; i++)
                    {
                        pline.AddVertexAt(i, new Point2d(points[i].X, points[i].Y), 0, 0, 0);
                    }

                    pline.Layer = layer;
                    pline.Color = AcColor.FromColorIndex(ColorMethod.ByAci, colorIndex);

                    polylineId = ms.AppendEntity(pline);
                    tr.AddNewlyCreatedDBObject(pline, true);
                }
                tr.Commit();
            }
            return polylineId;
        }

        /// <summary>
        /// Tạo một đối tượng Arc và thêm vào ModelSpace từ 3 điểm.
        /// </summary>
        public static ObjectId CreateArcFrom3Points(Point3d startPoint, Point3d pointOnArc, Point3d endPoint, string layer, short colorIndex)
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            ObjectId arcId = ObjectId.Null;

            try
            {
                CircularArc3d tempArc = new CircularArc3d(startPoint, pointOnArc, endPoint);

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord ms = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                    using (Arc arc = new Arc(tempArc.Center, tempArc.Radius, tempArc.StartAngle, tempArc.EndAngle))
                    {
                        arc.Layer = layer;
                        arc.Color = AcColor.FromColorIndex(ColorMethod.ByAci, colorIndex);

                        arcId = ms.AppendEntity(arc);
                        tr.AddNewlyCreatedDBObject(arc, true);
                    }
                    tr.Commit();
                }
            }
            catch (System.Exception)
            {
                // Bắt lỗi nếu 3 điểm thẳng hàng và không thể tạo cung tròn
                return ObjectId.Null;
            }
            return arcId;
        }
    }
}