// Các thư viện cần thiết
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System.Collections.Generic;
using System.Linq;

// TẠO BÍ DANH ĐỂ CODE GỌN HƠN
using AcColor = Autodesk.AutoCAD.Colors.Color;

namespace MyAutoCAD2026Plugin
{
    #region Data Container Class for XData

    public class DoorXData
    {
        public string DoorType { get; set; } = string.Empty;
        public double Height { get; set; }
        public double Width { get; set; }
        public ObjectIdCollection ChildIds { get; set; } = new ObjectIdCollection();
        public const string RegAppName = "MY_DOOR_CREATOR_APP";
    }

    #endregion

    public static class GeometryCreator
    {
        #region Private Helper Methods

        private static ObjectId GetOrCreateLayer(Database db, Transaction tr, string layerName)
        {
            LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
            if (lt.Has(layerName)) return lt[layerName];

            using (LayerTableRecord newLtr = new LayerTableRecord())
            {
                newLtr.Name = layerName;
                tr.GetObject(db.LayerTableId, OpenMode.ForWrite);
                lt.Add(newLtr);
                tr.AddNewlyCreatedDBObject(newLtr, true);
                return newLtr.ObjectId;
            }
        }

        private static void RegisterApplicationName(Database db, Transaction tr, string appName)
        {
            RegAppTable rat = tr.GetObject(db.RegAppTableId, OpenMode.ForRead) as RegAppTable;
            if (!rat.Has(appName))
            {
                using (RegAppTableRecord ratr = new RegAppTableRecord())
                {
                    ratr.Name = appName;
                    tr.GetObject(db.RegAppTableId, OpenMode.ForWrite);
                    rat.Add(ratr);
                    tr.AddNewlyCreatedDBObject(ratr, true);
                }
            }
        }

        #endregion

        #region Geometry Creation (With XData Overloads)

        // --- CreateLine ---
        public static ObjectId CreateLine(Database db, Point3d startPt, Point3d endPt, string layerName, AcColor color)
        {
            return CreateLine(db, startPt, endPt, layerName, color, null);
        }
        public static ObjectId CreateLine(Database db, Point3d startPt, Point3d endPt, string layerName, AcColor color, DoorXData xdata)
        {
            ObjectId lineId = ObjectId.Null;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTableRecord ms = tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite) as BlockTableRecord;
                ObjectId layerId = GetOrCreateLayer(db, tr, layerName);

                using (Line line = new Line(startPt, endPt))
                {
                    // === PHẦN XỬ LÝ LAYER VÀ MÀU SẮC (VẪN GIỮ NGUYÊN) ===
                    line.LayerId = layerId;
                    line.Color = color;

                    if (xdata != null)
                    {
                        RegisterApplicationName(db, tr, DoorXData.RegAppName);
                        using (ResultBuffer rb = BuildXDataBuffer(xdata))
                        {
                            line.XData = rb;
                        }
                    }

                    ms.AppendEntity(line);
                    tr.AddNewlyCreatedDBObject(line, true);
                    lineId = line.ObjectId;
                }
                tr.Commit();
            }
            return lineId;
        }

        // --- CreateCircle (ĐÃ BỔ SUNG) ---
        public static ObjectId CreateCircle(Database db, Point3d center, double radius, string layerName, AcColor color)
        {
            return CreateCircle(db, center, radius, layerName, color, null);
        }
        public static ObjectId CreateCircle(Database db, Point3d center, double radius, string layerName, AcColor color, DoorXData xdata)
        {
            ObjectId circleId = ObjectId.Null;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTableRecord ms = tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite) as BlockTableRecord;
                ObjectId layerId = GetOrCreateLayer(db, tr, layerName);

                using (Circle circle = new Circle(center, Vector3d.ZAxis, radius))
                {
                    // === PHẦN XỬ LÝ LAYER VÀ MÀU SẮC (VẪN GIỮ NGUYÊN) ===
                    circle.LayerId = layerId;
                    circle.Color = color;

                    if (xdata != null)
                    {
                        RegisterApplicationName(db, tr, DoorXData.RegAppName);
                        using (ResultBuffer rb = BuildXDataBuffer(xdata))
                        {
                            circle.XData = rb;
                        }
                    }

                    ms.AppendEntity(circle);
                    tr.AddNewlyCreatedDBObject(circle, true);
                    circleId = circle.ObjectId;
                }
                tr.Commit();
            }
            return circleId;
        }

        // --- CreatePolyline ---
        public static ObjectId CreatePolyline(Database db, Point3dCollection points, string layerName, AcColor color)
        {
            return CreatePolyline(db, points, layerName, color, null);
        }
        public static ObjectId CreatePolyline(Database db, Point3dCollection points, string layerName, AcColor color, DoorXData xdata)
        {
            if (points.Count < 2) return ObjectId.Null;
            ObjectId plineId = ObjectId.Null;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTableRecord ms = tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite) as BlockTableRecord;
                ObjectId layerId = GetOrCreateLayer(db, tr, layerName);

                using (Polyline pline = new Polyline())
                {
                    for (int i = 0; i < points.Count; i++)
                    {
                        pline.AddVertexAt(i, new Point2d(points[i].X, points[i].Y), 0, 0, 0);
                    }

                    // === PHẦN XỬ LÝ LAYER VÀ MÀU SẮC (VẪN GIỮ NGUYÊN) ===
                    pline.LayerId = layerId;
                    pline.Color = color;

                    if (xdata != null)
                    {
                        RegisterApplicationName(db, tr, DoorXData.RegAppName);
                        using (ResultBuffer rb = BuildXDataBuffer(xdata))
                        {
                            pline.XData = rb;
                        }
                    }

                    ms.AppendEntity(pline);
                    tr.AddNewlyCreatedDBObject(pline, true);
                    plineId = pline.ObjectId;
                }
                tr.Commit();
            }
            return plineId;
        }

        // --- CreateArc (ĐÃ BỔ SUNG) ---
        private static bool ArePointsCollinear(Point3d p1, Point3d p2, Point3d p3)
        {
            Vector3d vectorA = p2 - p1;
            Vector3d vectorB = p3 - p1;
            // SỬA LỖI: Thêm cặp dấu () để gọi phương thức IsZeroLength().
            // Đây là một hành động kiểm tra, không phải là một thuộc tính.
            return vectorA.CrossProduct(vectorB).IsZeroLength();
        }

        public static ObjectId CreateArc(Database db, Point3d startPt, Point3d ptOnArc, Point3d endPt, string layerName, AcColor color)
        {
            return CreateArc(db, startPt, ptOnArc, endPt, layerName, color, null);
        }

        public static ObjectId CreateArc(Database db, Point3d startPt, Point3d ptOnArc, Point3d endPt, string layerName, AcColor color, DoorXData xdata)
        {
            // === PHẦN SỬA LỖI QUAN TRỌNG ===
            // Thêm bước kiểm tra 3 điểm thẳng hàng ngay từ đầu.
            // Nếu thẳng hàng, trả về ObjectId.Null ngay lập tức, đúng như bài test mong đợi.
            if (ArePointsCollinear(startPt, ptOnArc, endPt))
            {
                return ObjectId.Null;
            }

            ObjectId arcId = ObjectId.Null;
            try
            {
                // Logic còn lại không đổi...
                CircularArc3d tempArc = new CircularArc3d(startPt, ptOnArc, endPt);
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTableRecord ms = tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite) as BlockTableRecord;
                    ObjectId layerId = GetOrCreateLayer(db, tr, layerName);
                    using (Arc arc = new Arc(tempArc.Center, tempArc.Radius, tempArc.StartAngle, tempArc.EndAngle))
                    {
                        arc.LayerId = layerId;
                        arc.Color = color;
                        if (xdata != null)
                        {
                            RegisterApplicationName(db, tr, DoorXData.RegAppName);
                            using (ResultBuffer rb = BuildXDataBuffer(xdata))
                            {
                                arc.XData = rb;
                            }
                        }
                        ms.AppendEntity(arc);
                        tr.AddNewlyCreatedDBObject(arc, true);
                        arcId = arc.ObjectId;
                    }
                    tr.Commit();
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
                return ObjectId.Null;
            }
            return arcId;
        }
        #endregion

        #region XData Utilities

        /// <summary>
        /// (Backend-Private) Hàm helper để xây dựng ResultBuffer từ đối tượng DoorXData.
        /// Giúp tránh lặp lại code.
        /// </summary>
        private static ResultBuffer BuildXDataBuffer(DoorXData xdata)
        {
            ResultBuffer rb = new ResultBuffer(
                new TypedValue((int)DxfCode.ExtendedDataRegAppName, DoorXData.RegAppName),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, xdata.DoorType),
                new TypedValue((int)DxfCode.ExtendedDataReal, xdata.Height),
                new TypedValue((int)DxfCode.ExtendedDataReal, xdata.Width),
                new TypedValue((int)DxfCode.ExtendedDataControlString, "{"));

            foreach (ObjectId id in xdata.ChildIds)
            {
                // === PHẦN SỬA LỖI QUAN TRỌNG ===
                // Chuyển Handle (long) thành chuỗi HEXADECIMAL thay vì decimal.
                // "X" là định dạng chuẩn cho số hex.
                rb.Add(new TypedValue((int)DxfCode.ExtendedDataHandle, id.Handle.Value.ToString("X")));
            }
            rb.Add(new TypedValue((int)DxfCode.ExtendedDataControlString, "}"));
            return rb;
        }

        public static DoorXData ReadDoorXData(Database db, ObjectId entityId)
        {
            DoorXData data = null;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Entity ent = tr.GetObject(entityId, OpenMode.ForRead) as Entity;
                if (ent == null) return null;

                ResultBuffer rb = ent.GetXDataForApplication(DoorXData.RegAppName);
                if (rb == null) return null;

                using (rb)
                {
                    List<TypedValue> values = rb.AsArray().ToList();
                    data = new DoorXData
                    {
                        DoorType = values[1].Value.ToString(),
                        Height = (double)values[2].Value,
                        Width = (double)values[3].Value
                    };

                    bool isReadingHandles = false;
                    foreach (TypedValue tv in values)
                    {
                        if (tv.TypeCode == (int)DxfCode.ExtendedDataControlString && tv.Value.ToString() == "{")
                        {
                            isReadingHandles = true;
                            continue;
                        }
                        if (tv.TypeCode == (int)DxfCode.ExtendedDataControlString && tv.Value.ToString() == "}") break;

                        if (isReadingHandles && tv.TypeCode == (int)DxfCode.ExtendedDataHandle)
                        {
                            if (db.TryGetObjectId(new Handle(long.Parse(tv.Value.ToString(), System.Globalization.NumberStyles.HexNumber)), out ObjectId childId))
                            {
                                data.ChildIds.Add(childId);
                            }
                        }
                    }
                }
            }
            return data;
        }

        #endregion
    }
}