using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using NUnit.Framework;
using AcColor = Autodesk.AutoCAD.Colors.Color;

namespace MyAutoCAD2026Plugin.Tests
{
    [TestFixture]
    public class GeometryCreatorTests
    {
        // === PHẦN SỬA ĐỔI CHO C# 7.3 ===
        // Khai báo biến mà không có toán tử !.
        private Database _db;

        // *** SỬA ĐỔI SETUP VÀ TEARDOWN ***
        [SetUp]
        public void Setup(Database db)
        {
            _db = db;
        }

        [TearDown]
        public void Teardown()
        {
        }

        // === CÁC BÀI TEST GIỮ NGUYÊN LOGIC, CHỈ SỬA CÁCH XỬ LÝ NULL TRUYỀN THỐNG ===
        [Test]
        public void CreateLine_ShouldCreateLineWithCorrectProperties()
        {
            var startPt = new Point3d(10, 20, 0);
            var endPt = new Point3d(50, 60, 0);
            string layerName = "MY_TEST_LINE_LAYER";
            var color = AcColor.FromRgb(255, 0, 0);

            ObjectId lineId = GeometryCreator.CreateLine(_db, startPt, endPt, layerName, color);

            Assert.That(lineId.IsNull, Is.False, "Line ObjectId should not be null.");
            using (var tr = _db.TransactionManager.StartTransaction())
            {
                var line = tr.GetObject(lineId, OpenMode.ForRead) as Line;
                Assert.That(line, Is.Not.Null, "Object should be a Line.");

                // Vì câu lệnh Assert ở trên đã đảm bảo line không null,
                // chúng ta có thể tự tin truy cập các thuộc tính của nó.
                Assert.That(line.StartPoint, Is.EqualTo(startPt), "Line start point is incorrect.");
                Assert.That(line.EndPoint, Is.EqualTo(endPt), "Line end point is incorrect.");
                Assert.That(line.Layer, Is.EqualTo(layerName), "Line layer is incorrect.");
                Assert.That(line.Color, Is.EqualTo(color), "Line color is incorrect.");

                LayerTable lt = tr.GetObject(_db.LayerTableId, OpenMode.ForRead) as LayerTable;
                Assert.That(lt, Is.Not.Null);
                Assert.That(lt.Has(layerName), Is.True, "Layer should have been created in the database.");

                tr.Commit();
            }
        }

        // ... Các bài test khác giữ nguyên code ...
        [Test]
        public void CreatePolyline_WithLessThanTwoPoints_ShouldReturnNullId()
        {
            var points = new Point3dCollection { new Point3d(0, 0, 0) };
            ObjectId plineId = GeometryCreator.CreatePolyline(_db, points, "ANY_LAYER", AcColor.FromColorIndex(ColorMethod.ByLayer, 0));
            Assert.That(plineId.IsNull, Is.True, "Creating a polyline with less than 2 points should return a null ObjectId.");
        }

        [Test]
        public void CreateAndReadXData_ShouldAttachAndRetrieveCorrectData()
        {
            var center = Point3d.Origin;
            double radius = 100.0;
            string layerName = "XDATA_TEST_LAYER";
            var color = AcColor.FromColorIndex(ColorMethod.ByAci, 3);
            ObjectId childId1 = GeometryCreator.CreateLine(_db, new Point3d(0, 0, 0), new Point3d(1, 1, 0), "CHILD_LAYER", color);
            ObjectId childId2 = GeometryCreator.CreateLine(_db, new Point3d(2, 2, 0), new Point3d(3, 3, 0), "CHILD_LAYER", color);
            var originalXData = new DoorXData
            {
                DoorType = "Single_Swing",
                Height = 2200.5,
                Width = 900.0,
                ChildIds = new ObjectIdCollection { childId1, childId2 }
            };

            ObjectId circleId = GeometryCreator.CreateCircle(_db, center, radius, layerName, color, originalXData);
            Assert.That(circleId.IsNull, Is.False, "Circle with XData should be created successfully.");

            DoorXData readXData = GeometryCreator.ReadDoorXData(_db, circleId);
            Assert.That(readXData, Is.Not.Null, "Read XData should not be null.");

            Assert.That(readXData.DoorType, Is.EqualTo(originalXData.DoorType), "XData DoorType is incorrect.");
            Assert.That(readXData.Height, Is.EqualTo(originalXData.Height), "XData Height is incorrect.");
            Assert.That(readXData.Width, Is.EqualTo(originalXData.Width), "XData Width is incorrect.");
            Assert.That(readXData.ChildIds.Count, Is.EqualTo(originalXData.ChildIds.Count), "XData ChildIds count is incorrect.");
            Assert.That(readXData.ChildIds[0], Is.EqualTo(originalXData.ChildIds[0]), "First ChildId is incorrect.");
            Assert.That(readXData.ChildIds[1], Is.EqualTo(originalXData.ChildIds[1]), "Second ChildId is incorrect.");
        }

        [Test]
        public void CreateArc_WithCollinearPoints_ShouldReturnNullId()
        {
            var startPt = new Point3d(0, 0, 0);
            var ptOnArc = new Point3d(1, 0, 0);
            var endPt = new Point3d(2, 0, 0);
            ObjectId arcId = GeometryCreator.CreateArc(_db, startPt, ptOnArc, endPt, "ARC_LAYER", AcColor.FromColorIndex(ColorMethod.ByLayer, 0));
            Assert.That(arcId.IsNull, Is.True, "Creating an arc with collinear points should return a null ObjectId.");
        }

        [Test]
        public void CreatePolyline_WithValidPoints_ShouldCreateCorrectPolyline()
        {
            var points = new Point3dCollection
            {
                new Point3d(0, 0, 0),
                new Point3d(100, 50, 0),
                new Point3d(150, -50, 0)
            };
            string layerName = "MY_POLYLINE_LAYER";
            var color = AcColor.FromColorIndex(ColorMethod.ByAci, 5);
            ObjectId plineId = GeometryCreator.CreatePolyline(_db, points, layerName, color);
            Assert.That(plineId.IsNull, Is.False, "Polyline ObjectId should not be null.");
            using (var tr = _db.TransactionManager.StartTransaction())
            {
                var pline = tr.GetObject(plineId, OpenMode.ForRead) as Polyline;
                Assert.That(pline, Is.Not.Null, "Object should be a Polyline.");
                Assert.That(pline.NumberOfVertices, Is.EqualTo(points.Count), "Polyline should have the correct number of vertices.");
                for (int i = 0; i < points.Count; i++)
                {
                    Point2d expectedPt = new Point2d(points[i].X, points[i].Y);
                    Assert.That(pline.GetPoint2dAt(i), Is.EqualTo(expectedPt), $"Vertex at index {i} is incorrect.");
                }
                Assert.That(pline.Layer, Is.EqualTo(layerName), "Polyline layer is incorrect.");
                Assert.That(pline.Color, Is.EqualTo(color), "Polyline color is incorrect.");
                tr.Commit();
            }
        }

        [Test]
        public void CreateCircle_ShouldCreateCircleWithCorrectProperties()
        {
            var center = new Point3d(123.45, 678.90, 0);
            double radius = 55.5;
            string layerName = "MY_CIRCLE_LAYER";
            var color = AcColor.FromRgb(255, 255, 0);
            ObjectId circleId = GeometryCreator.CreateCircle(_db, center, radius, layerName, color);
            Assert.That(circleId.IsNull, Is.False, "Circle ObjectId should not be null.");
            using (var tr = _db.TransactionManager.StartTransaction())
            {
                var circle = tr.GetObject(circleId, OpenMode.ForRead) as Circle;
                Assert.That(circle, Is.Not.Null, "Object should be a Circle.");
                Assert.That(circle.Center.X, Is.EqualTo(center.X).Within(0.0001), "Circle center X coordinate is incorrect.");
                Assert.That(circle.Center.Y, Is.EqualTo(center.Y).Within(0.0001), "Circle center Y coordinate is incorrect.");
                Assert.That(circle.Center.Z, Is.EqualTo(center.Z).Within(0.0001), "Circle center Z coordinate is incorrect.");
                Assert.That(circle.Radius, Is.EqualTo(radius).Within(0.0001), "Circle radius is incorrect.");
                Assert.That(circle.Normal, Is.EqualTo(Vector3d.ZAxis), "Circle normal vector should be Z-axis.");
                Assert.That(circle.Layer, Is.EqualTo(layerName), "Circle layer is incorrect.");
                Assert.That(circle.Color, Is.EqualTo(color), "Circle color is incorrect.");
                tr.Commit();
            }
        }

        [Test]
        public void CreateArc_WithValidPoints_ShouldCreateCorrectArc()
        {
            var startPt = new Point3d(100, 0, 0);
            var ptOnArc = new Point3d(0, 100, 0);
            var endPt = new Point3d(-100, 0, 0);
            string layerName = "MY_ARC_LAYER";
            var color = AcColor.FromColorIndex(ColorMethod.ByAci, 6);
            var expectedGeom = new CircularArc3d(startPt, ptOnArc, endPt);
            ObjectId arcId = GeometryCreator.CreateArc(_db, startPt, ptOnArc, endPt, layerName, color);
            Assert.That(arcId.IsNull, Is.False, "Arc ObjectId should not be null.");
            using (var tr = _db.TransactionManager.StartTransaction())
            {
                var arc = tr.GetObject(arcId, OpenMode.ForRead) as Arc;
                Assert.That(arc, Is.Not.Null, "Object should be an Arc.");
                Assert.That(arc.Center.X, Is.EqualTo(expectedGeom.Center.X).Within(0.0001), "Arc center X is incorrect.");
                Assert.That(arc.Center.Y, Is.EqualTo(expectedGeom.Center.Y).Within(0.0001), "Arc center Y is incorrect.");
                Assert.That(arc.Center.Z, Is.EqualTo(expectedGeom.Center.Z).Within(0.0001), "Arc center Z is incorrect.");
                Assert.That(arc.Radius, Is.EqualTo(expectedGeom.Radius).Within(0.0001), "Arc radius is incorrect.");
                Assert.That(arc.StartAngle, Is.EqualTo(expectedGeom.StartAngle).Within(0.0001), "Arc start angle is incorrect.");
                Assert.That(arc.EndAngle, Is.EqualTo(expectedGeom.EndAngle).Within(0.0001), "Arc end angle is incorrect.");
                Assert.That(arc.Layer, Is.EqualTo(layerName), "Arc layer is incorrect.");
                Assert.That(arc.Color, Is.EqualTo(color), "Arc color is incorrect.");
                tr.Commit();
            }
        }

        [Test]
        public void GetOrCreateLayer_WhenLayerAlreadyExists_ShouldReturnExistingObjectId()
        {
            string layerName = "EXISTING_LAYER";
            ObjectId existingLayerId;
            using (var tr = _db.TransactionManager.StartTransaction())
            {
                var lt = tr.GetObject(_db.LayerTableId, OpenMode.ForWrite) as LayerTable;
                using (var ltr = new LayerTableRecord { Name = layerName })
                {
                    Assert.That(lt, Is.Not.Null);
                    existingLayerId = lt.Add(ltr);
                    tr.AddNewlyCreatedDBObject(ltr, true);
                }
                tr.Commit();
            }
            Assert.That(existingLayerId.IsNull, Is.False, "Setup step failed: Could not create the initial layer.");
            ObjectId lineId = GeometryCreator.CreateLine(_db, Point3d.Origin, new Point3d(1, 1, 0), layerName, AcColor.FromColorIndex(ColorMethod.ByLayer, 0));
            Assert.That(lineId.IsNull, Is.False, "Line should be created even if layer exists.");
            using (var tr = _db.TransactionManager.StartTransaction())
            {
                var line = tr.GetObject(lineId, OpenMode.ForRead) as Line;
                Assert.That(line, Is.Not.Null);
                Assert.That(line.LayerId, Is.EqualTo(existingLayerId), "The method should have used the existing layer, not created a new one.");
                tr.Commit();
            }
        }
    }
}