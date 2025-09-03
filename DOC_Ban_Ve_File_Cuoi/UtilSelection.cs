using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

// TẠO BÍ DANH ĐỂ TRÁNH XUNG ĐỘT
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MyAutoCAD2026Plugin
{
    // Lớp tĩnh chứa các hàm tiện ích, không cần tạo đối tượng để sử dụng
    public static class UtilSelection
    {
        // === ĐỊNH NGHĨA SẴN CÁC LOẠI ĐỐI TƯỢNG HAY DÙNG ===
        // Dùng toán tử OR (dấu phẩy) để gom nhóm tất cả các loại Polyline
        public const string ALL_POLYLINES = "LWPOLYLINE,POLYLINE,POLYLINE2D,POLYLINE3D";
        public const string ALL_LINES = "LINE";
        public const string ALL_ARCS = "ARC";
        public const string ALL_CIRCLES = "CIRCLE";
        public const string ALL_TEXTS = "MTEXT,TEXT";

        /// <summary>
        /// Tự động chọn TẤT CẢ các đối tượng của một loại cụ thể trong không gian hiện tại (Model hoặc Layout).
        /// Không yêu cầu người dùng quét chuột.
        /// </summary>
        /// <param name="objectTypeFilter">Chuỗi tên DXF của các loại đối tượng cần lọc, cách nhau bởi dấu phẩy.</param>
        /// <returns>Trả về một SelectionSet chứa các đối tượng đã được chọn, hoặc null nếu không có.</returns>
        public static SelectionSet SelectAllObjectsByType(string objectTypeFilter)
        {
            // Lấy về các đối tượng Document và Editor cần thiết
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return null;
            Editor editor = doc.Editor;

            // 1. Tạo bộ lọc theo loại đối tượng
            // Dòng này tạo ra một "công thức" lọc. Ví dụ: nếu objectTypeFilter là "LINE",
            // nó sẽ tạo ra một điều kiện "chỉ lấy các đối tượng có mã DXF 0 là LINE".
            TypedValue[] filterValues = new TypedValue[]
            {
                new TypedValue((int)DxfCode.Start, objectTypeFilter)
            };
            SelectionFilter filter = new SelectionFilter(filterValues);

            // 2. Gọi SelectAll để chọn tất cả đối tượng khớp với bộ lọc trong không gian hiện tại
            // Đây là phương thức tự động chọn, không cần người dùng tương tác.
            PromptSelectionResult psr = editor.SelectAll(filter);

            // 3. Xử lý kết quả
            // Nếu lệnh chạy thành công và tìm thấy ít nhất 1 đối tượng
            if (psr.Status == PromptStatus.OK)
                return psr.Value; // Trả về tập hợp các đối tượng đã chọn
            else
                return null; // Trả về null nếu không tìm thấy gì hoặc có lỗi
        }
    }
}