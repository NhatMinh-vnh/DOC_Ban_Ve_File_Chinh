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
        // === CODE QUAN TRỌNG 1: ĐỊNH NGHĨA SẴN CÁC BỘ LỌC ===
        // Các hằng số này giúp code dễ đọc và dễ bảo trì.
        public const string ALL_POLYLINES = "LWPOLYLINE,POLYLINE,POLYLINE2D,POLYLINE3D";
        public const string ALL_LINES = "LINE";
        public const string ALL_ARCS = "ARC";
        public const string ALL_CIRCLES = "CIRCLE";
        public const string ALL_TEXTS = "MTEXT,TEXT";
        

        /// <summary>
        /// Chế độ 1: Tự động chọn TẤT CẢ các đối tượng dựa trên bộ lọc.
        /// </summary>
        public static SelectionSet SelectAllObjects(List<TypedValue> filterList)
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null || filterList == null || filterList.Count == 0) return null;
            Editor editor = doc.Editor;

            // Nếu có nhiều hơn 1 điều kiện, tự động bọc chúng trong một toán tử AND
            if (filterList.Count > 1)
            {
                filterList.Insert(0, new TypedValue((int)DxfCode.Operator, "<AND"));
                filterList.Add(new TypedValue((int)DxfCode.Operator, "AND>"));
            }

            // Tạo đối tượng SelectionFilter từ danh sách các điều kiện
            SelectionFilter filter = new SelectionFilter(filterList.ToArray());

            // === CODE QUAN TRỌNG 2: SỬ DỤNG editor.SelectAll() ===
            // Yêu cầu bộ máy AutoCAD quét toàn bộ bản vẽ và trả về kết quả
            PromptSelectionResult psr = editor.SelectAll(filter);

            if (psr.Status == PromptStatus.OK)
                return psr.Value;
            else
                return null;
        }

        /// <summary>
        /// Chế độ 2: Yêu cầu người dùng quét chọn đối tượng dựa trên bộ lọc.
        /// </summary>
        public static SelectionSet GetSelection(List<TypedValue> filterList)
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null || filterList == null || filterList.Count == 0) return null;
            Editor editor = doc.Editor;

            if (filterList.Count > 1)
            {
                filterList.Insert(0, new TypedValue((int)DxfCode.Operator, "<AND"));
                filterList.Add(new TypedValue((int)DxfCode.Operator, "AND>"));
            }

            SelectionFilter filter = new SelectionFilter(filterList.ToArray());

            // === CODE QUAN TRỌNG 3: SỬ DỤNG editor.GetSelection() ===
            // Yêu cầu người dùng tương tác bằng chuột (quét cửa sổ, đa giác...)
            PromptSelectionResult psr = editor.GetSelection(filter);

            if (psr.Status == PromptStatus.OK)
                return psr.Value;
            else
                return null;
        }

        /// <summary>
        /// "Phiên dịch" tên màu sang mã số màu (Color Index) của AutoCAD.
        /// </summary>
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