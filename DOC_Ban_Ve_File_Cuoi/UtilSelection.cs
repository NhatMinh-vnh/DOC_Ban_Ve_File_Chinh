using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using System.Collections.Generic;
using System;
using System.Linq;

// TẠO BÍ DANH ĐỂ TRÁNH XUNG ĐỘT
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MyAutoCAD2026Plugin
{
    public static class UtilSelection
    {
        // Các hằng số vẫn giữ nguyên vai trò quan trọng
        public const string ALL_POLYLINES = "LWPOLYLINE,POLYLINE,POLYLINE2D,POLYLINE3D";
        public const string ALL_LINES = "LINE";
        public const string ALL_ARCS = "ARC";
        public const string ALL_CIRCLES = "CIRCLE";
        public const string ALL_TEXTS = "MTEXT,TEXT";
        public const string LINE_AND_POLYLINE = "LINE,LWPOLYLINE,POLYLINE,POLYLINE2D,POLYLINE3D";

        /// <summary>
        /// Phương thức lựa chọn TẤT CẢ TRONG MỘT, xử lý mọi logic lọc.
        /// </summary>
        /// <param name="objectTypes">Danh sách các loại đối tượng người dùng đã chọn (ví dụ: "Line", "Polyline").</param>
        /// <param name="layerName">Tên layer để lọc (có thể là chuỗi rỗng).</param>
        /// <param name="colorName">Tên màu hoặc mã màu để lọc (có thể là chuỗi rỗng).</param>
        /// <param name="isInteractive">True nếu muốn chọn tương tác, False nếu muốn chọn tất cả.</param>
        /// <returns>Tập hợp các đối tượng được chọn.</returns>
        public static SelectionSet SelectObjectsByCriteria(List<string> objectTypes, string layerName, string colorName, bool isInteractive)
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null || objectTypes == null || objectTypes.Count == 0) return null;
            Editor editor = doc.Editor;

            // --- TOÀN BỘ LOGIC XÂY DỰNG BỘ LỌC ĐÃ ĐƯỢC CHUYỂN VÀO ĐÂY ---
            var filterList = new List<TypedValue>();

            // 1. Xử lý logic OR cho các loại đối tượng
            List<string> dxfNames = new List<string>();
            foreach (var type in objectTypes)
            {
                switch (type)
                {
                    case "Line": dxfNames.Add(ALL_LINES); break;
                    case "Polyline": dxfNames.Add(ALL_POLYLINES); break;
                    case "Arc": dxfNames.Add(ALL_ARCS); break;
                    case "Circle": dxfNames.Add(ALL_CIRCLES); break;
                    case "Text": dxfNames.Add(ALL_TEXTS); break;
                }
            }
            string finalFilterString = string.Join(",", dxfNames.Distinct());
            filterList.Add(new TypedValue((int)DxfCode.Start, finalFilterString));

            // 2. Xử lý logic cho Layer (nếu có)
            if (!string.IsNullOrWhiteSpace(layerName))
            {
                filterList.Add(new TypedValue((int)DxfCode.LayerName, layerName));
            }

            // 3. Xử lý logic cho Màu (nếu có)
            if (!string.IsNullOrWhiteSpace(colorName))
            {
                short colorIndex = ConvertColorNameToIndex(colorName);
                if (colorIndex != -1)
                {
                    filterList.Add(new TypedValue((int)DxfCode.Color, colorIndex));
                }
                else
                {
                    editor.WriteMessage($"\nMàu '{colorName}' không hợp lệ, bộ lọc màu bị bỏ qua.");
                }
            }

            // 4. Xử lý logic AND để kết hợp các điều kiện
            if (filterList.Count > 1)
            {
                filterList.Insert(0, new TypedValue((int)DxfCode.Operator, "<AND"));
                filterList.Add(new TypedValue((int)DxfCode.Operator, "AND>"));
            }

            SelectionFilter filter = new SelectionFilter(filterList.ToArray());
            PromptSelectionResult psr;

            // 5. Quyết định gọi GetSelection hay SelectAll
            if (isInteractive)
            {
                psr = editor.GetSelection(filter);
            }
            else
            {
                psr = editor.SelectAll(filter);
            }

            if (psr.Status == PromptStatus.OK)
                return psr.Value;
            else
                return null;
        }

        // Hàm ConvertColorNameToIndex giữ nguyên
        public static short ConvertColorNameToIndex(string colorName)
        {
            if (string.IsNullOrWhiteSpace(colorName)) return -1;
            switch (colorName.ToUpper())
            {
                case "RED": return 1;
                case "YELLOW": return 2;
                case "GREEN": return 3;
                case "CYAN": return 4;
                case "BLUE": return 5;
                case "MAGENTA": return 6;
                case "WHITE": return 7;
                default:
                    if (short.TryParse(colorName, out short colorIndex))
                    {
                        if (colorIndex >= 0 && colorIndex <= 256) return colorIndex;
                    }
                    return -1;
            }
        }
    }
}