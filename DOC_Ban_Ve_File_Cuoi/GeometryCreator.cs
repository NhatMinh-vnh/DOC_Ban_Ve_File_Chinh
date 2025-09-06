using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System.Collections.Generic; // Cần cho Polyline
// TẠO BÍ DANH CHO LỚP APPLICATION CỦA AUTOCAD
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
// === THAY ĐỔI QUAN TRỌNG NHẤT NẰM Ở ĐÂY ===
// TẠO BÍ DANH CHO LỚP COLOR CỦA AUTOCAD
using AcColor = Autodesk.AutoCAD.Colors.Color;

namespace MyAutoCAD2026Plugin
{
    public static class GeometryCreator
    {
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
                    // SỬ DỤNG BÍ DANH "AcColor" ĐỂ CHỈ ĐỊNH RÕ RÀNG
                    line.Color = AcColor.FromColorIndex(ColorMethod.ByAci, colorIndex);
                    lineId = ms.AppendEntity(line);
                    tr.AddNewlyCreatedDBObject(line, true);
                }
                tr.Commit();
            }
            return lineId;
        }

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
                    // SỬ DỤNG BÍ DANH "AcColor"
                    circle.Color = AcColor.FromColorIndex(ColorMethod.ByAci, colorIndex);
                    circleId = ms.AppendEntity(circle);
                    tr.AddNewlyCreatedDBObject(circle, true);
                }
                tr.Commit();
            }
            return circleId;
        }

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
                    // SỬ DỤNG BÍ DANH "AcColor"
                    pline.Color = AcColor.FromColorIndex(ColorMethod.ByAci, colorIndex);

                    polylineId = ms.AppendEntity(pline);
                    tr.AddNewlyCreatedDBObject(pline, true);
                }
                tr.Commit();
            }
            return polylineId;
        }

        public static ObjectId CreateArcFrom3Points(Point3d startPoint, Point3d pointOnArc, Point3d endPoint, string layer, short colorIndex)
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            ObjectId arcId = ObjectId.Null;

            CircularArc3d tempArc = new CircularArc3d(startPoint, pointOnArc, endPoint);

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord ms = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                using (Arc arc = new Arc(tempArc.Center, tempArc.Radius, tempArc.StartAngle, tempArc.EndAngle))
                {
                    arc.Layer = layer;
                    // SỬ DỤNG BÍ DANH "AcColor"
                    arc.Color = AcColor.FromColorIndex(ColorMethod.ByAci, colorIndex);

                    arcId = ms.AppendEntity(arc);
                    tr.AddNewlyCreatedDBObject(arc, true);
                }
                tr.Commit();
            }
            return arcId;
        }
    }
}