// Các thư viện cần thiết cho việc test
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using MyAutoCAD2026Plugin; // Using namespace của dự án chính để thấy GeometryCreator
using NUnit.Framework;
using AcColor = Autodesk.AutoCAD.Colors.Color;

namespace MyAutoCAD2026Plugin.Tests
{
    [TestFixture]
    public class GeometryCreatorTests
    {
        // Biến này sẽ chứa database giả lập trong bộ nhớ của chúng ta.
        // Nó sẽ được tạo mới cho MỖI bài test.
        private Database _db;

        // *** PHẦN SETUP VÀ TEARDOWN QUAN TRỌNG ***
        // Hàm này được NUnit tự động gọi TRƯỚC KHI chạy mỗi bài test [Test].
        [SetUp]
        public void Setup()
        {
            // Tạo một database mới, trống, trong bộ nhớ.
            // Đây là chìa khóa để cô lập môi trường test.
            _db = new Database(false, true);
            System.Diagnostics.Debug.WriteLine("New in-memory database created.");
        }

        // Hàm này được NUnit tự động gọi SAU KHI mỗi bài test [Test] kết thúc.
        [TearDown]
        public void Teardown()
        {
            // Giải phóng tài nguyên của database sau khi test xong.
            // Điều này đảm bảo không bị rò rỉ bộ nhớ.
            _db.Dispose();
            System.Diagnostics.Debug.WriteLine("In-memory database disposed.");
        }


        // *** BẮT ĐẦU CÁC BÀI TEST ***

        [Test]
        public void CreateLine_ShouldCreateLineWithCorrectProperties()
        {
            // --- 1. ARRANGE (SẮP ĐẶT) ---
            // Chuẩn bị tất cả các dữ liệu đầu vào cần thiết.
            var startPt = new Point3d(10, 20, 0);
            var endPt = new Point3d(50, 60, 0);
            string layerName = "MY_TEST_LINE_LAYER";
            var color = AcColor.FromRgb(255, 0, 0); // Màu đỏ

            // --- 2. ACT (HÀNH ĐỘNG) ---
            // Gọi phương thức chúng ta muốn kiểm tra.
            ObjectId lineId = GeometryCreator.CreateLine(_db, startPt, endPt, layerName, color);

            // --- 3. ASSERT (KHẲNG ĐỊNH) ---
            // Kiểm tra xem kết quả có đúng như mong đợi không.
            Assert.That(lineId.IsNull, Is.False, "Line ObjectId should not be null.");

            // Mở đối tượng vừa tạo ra từ database để kiểm tra chi tiết các thuộc tính.
            using (var tr = _db.TransactionManager.StartTransaction())
            {
                var line = tr.GetObject(lineId, OpenMode.ForRead) as Line;

                // Khẳng định rằng đối tượng lấy ra thực sự là một Line.
                Assert.That(line, Is.Not.Null, "Object should be a Line.");

                // Khẳng định các thuộc tính hình học và phi hình học là chính xác.
                Assert.That(line.StartPoint, Is.EqualTo(startPt), "Line start point is incorrect.");
                Assert.That(line.EndPoint, Is.EqualTo(endPt), "Line end point is incorrect.");
                Assert.That(line.Layer, Is.EqualTo(layerName), "Line layer is incorrect.");
                Assert.That(line.Color, Is.EqualTo(color), "Line color is incorrect.");

                // Kiểm tra xem layer đã thực sự được tạo trong database chưa.
                LayerTable lt = tr.GetObject(_db.LayerTableId, OpenMode.ForRead) as LayerTable;
                Assert.That(lt.Has(layerName), Is.True, "Layer should have been created in the database.");

                tr.Commit();
            }
        }

        [Test]
        public void CreatePolyline_WithLessThanTwoPoints_ShouldReturnNullId()
        {
            // --- 1. ARRANGE ---
            var points = new Point3dCollection();
            points.Add(new Point3d(0, 0, 0)); // Chỉ có 1 điểm

            // --- 2. ACT ---
            ObjectId plineId = GeometryCreator.CreatePolyline(_db, points, "ANY_LAYER", AcColor.FromColorIndex(ColorMethod.ByLayer, 0));

            // --- 3. ASSERT ---
            // Hàm này phải trả về ObjectId.Null khi đầu vào không hợp lệ.
            Assert.That(plineId.IsNull, Is.True, "Creating a polyline with less than 2 points should return a null ObjectId.");
        }

        [Test]
        public void CreateAndReadXData_ShouldAttachAndRetrieveCorrectData()
        {
            // --- 1. ARRANGE ---
            // Tạo một đối tượng Circle để gắn XData vào.
            var center = Point3d.Origin;
            double radius = 100.0;
            string layerName = "XDATA_TEST_LAYER";
            var color = AcColor.FromColorIndex(ColorMethod.ByAci, 3); // Màu xanh lá

            // Tạo các đối tượng "con" giả lập để lấy ObjectId của chúng.
            // Trong môi trường test, chúng ta tạo các đối tượng thật trong database giả lập.
            ObjectId childId1 = GeometryCreator.CreateLine(_db, new Point3d(0, 0, 0), new Point3d(1, 1, 0), "CHILD_LAYER", color);
            ObjectId childId2 = GeometryCreator.CreateLine(_db, new Point3d(2, 2, 0), new Point3d(3, 3, 0), "CHILD_LAYER", color);

            // Chuẩn bị dữ liệu XData gốc để so sánh.
            var originalXData = new DoorXData
            {
                DoorType = "Single_Swing",
                Height = 2200.5,
                Width = 900.0,
                ChildIds = new ObjectIdCollection { childId1, childId2 }
            };

            // --- 2. ACT (GHI DỮ LIỆU) ---
            // Tạo hình tròn và đồng thời gắn XData vào nó.
            ObjectId circleId = GeometryCreator.CreateCircle(_db, center, radius, layerName, color, originalXData);

            // --- 3. ASSERT (GHI DỮ LIỆU) ---
            Assert.That(circleId.IsNull, Is.False, "Circle with XData should be created successfully.");

            // --- 4. ACT (ĐỌC DỮ LIỆU) ---
            // Gọi hàm ReadDoorXData để đọc lại thông tin từ đối tượng vừa tạo.
            DoorXData readXData = GeometryCreator.ReadDoorXData(_db, circleId);

            // --- 5. ASSERT (ĐỌC DỮ LIỆU) ---
            // Khẳng định rằng dữ liệu đọc về không phải là null và có nội dung chính xác.
            Assert.That(readXData, Is.Not.Null, "Read XData should not be null.");

            // So sánh từng thuộc tính của XData đọc được với XData gốc.
            Assert.That(readXData.DoorType, Is.EqualTo(originalXData.DoorType), "XData DoorType is incorrect.");
            Assert.That(readXData.Height, Is.EqualTo(originalXData.Height), "XData Height is incorrect.");
            Assert.That(readXData.Width, Is.EqualTo(originalXData.Width), "XData Width is incorrect.");

            // So sánh danh sách ObjectId của các đối tượng con.
            Assert.That(readXData.ChildIds.Count, Is.EqualTo(originalXData.ChildIds.Count), "XData ChildIds count is incorrect.");
            Assert.That(readXData.ChildIds[0], Is.EqualTo(originalXData.ChildIds[0]), "First ChildId is incorrect.");
            Assert.That(readXData.ChildIds[1], Is.EqualTo(originalXData.ChildIds[1]), "Second ChildId is incorrect.");
        }

        [Test]
        public void CreateArc_WithCollinearPoints_ShouldReturnNullId()
        {
            // --- 1. ARRANGE ---
            // 3 điểm thẳng hàng
            var startPt = new Point3d(0, 0, 0);
            var ptOnArc = new Point3d(1, 0, 0);
            var endPt = new Point3d(2, 0, 0);

            // --- 2. ACT ---
            ObjectId arcId = GeometryCreator.CreateArc(_db, startPt, ptOnArc, endPt, "ARC_LAYER", AcColor.FromColorIndex(ColorMethod.ByLayer, 0));

            // --- 3. ASSERT ---
            // Hàm phải xử lý được exception và trả về ObjectId.Null
            Assert.That(arcId.IsNull, Is.True, "Creating an arc with collinear points should return a null ObjectId.");
        }
        [Test]
        public void CreatePolyline_WithValidPoints_ShouldCreateCorrectPolyline()
        {
            // --- 1. ARRANGE ---
            var points = new Point3dCollection
            {
                new Point3d(0, 0, 0),
                new Point3d(100, 50, 0),
                new Point3d(150, -50, 0)
            };
            string layerName = "MY_POLYLINE_LAYER";
            var color = AcColor.FromColorIndex(ColorMethod.ByAci, 5); // Màu xanh dương

            // --- 2. ACT ---
            ObjectId plineId = GeometryCreator.CreatePolyline(_db, points, layerName, color);

            // --- 3. ASSERT ---
            Assert.That(plineId.IsNull, Is.False, "Polyline ObjectId should not be null.");

            using (var tr = _db.TransactionManager.StartTransaction())
            {
                var pline = tr.GetObject(plineId, OpenMode.ForRead) as Polyline;

                Assert.That(pline, Is.Not.Null, "Object should be a Polyline.");
                Assert.That(pline.NumberOfVertices, Is.EqualTo(points.Count), "Polyline should have the correct number of vertices.");

                // Kiểm tra tọa độ của từng đỉnh để đảm bảo tính chính xác về hình học
                for (int i = 0; i < points.Count; i++)
                {
                    // Lưu ý: Polyline 2D lưu trữ Point2d
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
            // --- 1. ARRANGE ---
            var center = new Point3d(123.45, 678.90, 0);
            double radius = 55.5;
            string layerName = "MY_CIRCLE_LAYER";
            var color = AcColor.FromRgb(255, 255, 0); // Màu vàng

            // --- 2. ACT ---
            ObjectId circleId = GeometryCreator.CreateCircle(_db, center, radius, layerName, color);

            // --- 3. ASSERT ---
            Assert.That(circleId.IsNull, Is.False, "Circle ObjectId should not be null.");

            using (var tr = _db.TransactionManager.StartTransaction())
            {
                var circle = tr.GetObject(circleId, OpenMode.ForRead) as Circle;

                Assert.That(circle, Is.Not.Null, "Object should be a Circle.");

                // Sử dụng Is.EqualTo(...).Within(...) để so sánh số thực, tránh sai số do dấu phẩy động.
                Assert.That(circle.Center, Is.EqualTo(center).Within(0.0001), "Circle center is incorrect.");
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
            // --- 1. ARRANGE ---
            var startPt = new Point3d(100, 0, 0);
            var ptOnArc = new Point3d(0, 100, 0);
            var endPt = new Point3d(-100, 0, 0);
            string layerName = "MY_ARC_LAYER";
            var color = AcColor.FromColorIndex(ColorMethod.ByAci, 6); // Màu Magenta

            // Sử dụng CircularArc3d để tính toán trước các thuộc tính hình học mong đợi
            // Đây là cách test rất tin cậy.
            var expectedGeom = new CircularArc3d(startPt, ptOnArc, endPt);

            // --- 2. ACT ---
            ObjectId arcId = GeometryCreator.CreateArc(_db, startPt, ptOnArc, endPt, layerName, color);

            // --- 3. ASSERT ---
            Assert.That(arcId.IsNull, Is.False, "Arc ObjectId should not be null.");

            using (var tr = _db.TransactionManager.StartTransaction())
            {
                var arc = tr.GetObject(arcId, OpenMode.ForRead) as Arc;
                Assert.That(arc, Is.Not.Null, "Object should be an Arc.");

                // So sánh các thuộc tính hình học với giá trị đã tính toán trước
                Assert.That(arc.Center, Is.EqualTo(expectedGeom.Center).Within(0.0001), "Arc center is incorrect.");
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
            // --- 1. ARRANGE ---
            string layerName = "EXISTING_LAYER";
            ObjectId existingLayerId;

            // Bước 1: Tạo trước một layer trong database.
            using (var tr = _db.TransactionManager.StartTransaction())
            {
                var lt = tr.GetObject(_db.LayerTableId, OpenMode.ForWrite) as LayerTable;
                using (var ltr = new LayerTableRecord { Name = layerName })
                {
                    existingLayerId = lt.Add(ltr);
                    tr.AddNewlyCreatedDBObject(ltr, true);
                }
                tr.Commit();
            }

            Assert.That(existingLayerId.IsNull, Is.False, "Setup step failed: Could not create the initial layer.");

            // --- 2. ACT ---
            // Gọi một hàm tạo bất kỳ sử dụng lại chính tên layer đó.
            // Hàm GetOrCreateLayer sẽ được gọi ngầm bên trong.
            ObjectId lineId = GeometryCreator.CreateLine(_db, Point3d.Origin, new Point3d(1, 1, 0), layerName, AcColor.FromColorIndex(ColorMethod.ByLayer, 0));

            // --- 3. ASSERT ---
            Assert.That(lineId.IsNull, Is.False, "Line should be created even if layer exists.");

            using (var tr = _db.TransactionManager.StartTransaction())
            {
                var line = tr.GetObject(lineId, OpenMode.ForRead) as Line;

                // Khẳng định quan trọng nhất: LayerId của đối tượng line mới tạo
                // PHẢI GIỐNG HỆT với LayerId của layer chúng ta đã tạo ở bước Arrange.
                // Điều này chứng tỏ hàm đã "Get" (lấy) chứ không "Create" (tạo mới).
                Assert.That(line.LayerId, Is.EqualTo(existingLayerId), "The method should have used the existing layer, not created a new one.");

                tr.Commit();
            }
        }
    }
}