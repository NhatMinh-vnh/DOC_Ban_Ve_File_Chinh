using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using MyAutoCAD2026Plugin.Tests;
using NUnit.Framework;
using NUnit.Framework.Internal;
using System;
using System.Linq;
using System.Reflection;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MyAutoCAD2026Plugin.Commands
{
    public class InProcessTestRunner
    {
        [CommandMethod("RUN_ALL_TESTS")]
        public static void RunAllTestsCommand()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            ed.WriteMessage("\n\n"); // Thêm khoảng trắng cho dễ nhìn
            ed.WriteMessage("\n============================================================");
            ed.WriteMessage("\n BẮT ĐẦU CHẠY BỘ KIỂM THỬ TỰ ĐỘNG (IN-PROCESS TEST SUITE)");
            ed.WriteMessage("\n============================================================");

            Assembly testAssembly = Assembly.GetExecutingAssembly();
            var testFixtures = testAssembly.GetTypes()
                .Where(t => t.GetCustomAttributes(typeof(TestFixtureAttribute), true).Length > 0)
                .ToList();

            int testsRun = 0;
            int testsPassed = 0;
            Document originalDoc = AcApp.DocumentManager.MdiActiveDocument;

            foreach (var fixtureType in testFixtures)
            {
                ed.WriteMessage($"\n\n--- LỚP KIỂM THỬ: {fixtureType.Name} ---");
                var fixtureInstance = Activator.CreateInstance(fixtureType);
                if (fixtureInstance == null) continue;
                var testMethods = fixtureType.GetMethods()
                    .Where(m => m.GetCustomAttributes(typeof(TestAttribute), true).Length > 0)
                    .ToList();

                foreach (var testMethod in testMethods)
                {
                    testsRun++;
                    ed.WriteMessage($"\n  -> Đang chạy: {testMethod.Name}...");

                    Document testDoc = null;
                    try
                    {
                        testDoc = AcApp.DocumentManager.Add(null);
                        Database testDb = testDoc.Database;
                        using (DocumentLock acLckDoc = testDoc.LockDocument())
                        {
                            try
                            {
                                MethodInfo setupMethod = fixtureType.GetMethod("Setup");
                                MethodInfo teardownMethod = fixtureType.GetMethod("Teardown");

                                setupMethod?.Invoke(fixtureInstance, new object[] { testDb });
                                testMethod.Invoke(fixtureInstance, null);
                                teardownMethod?.Invoke(fixtureInstance, null);

                                ed.WriteMessage("    [THÀNH CÔNG] ✅");
                                testsPassed++;
                            }
                            catch (System.Exception ex)
                            {
                                // === PHẦN CẢI THIỆN HIỂN THỊ LỖI ===
                                // Lấy lỗi gốc từ NUnit (AssertionException) để có thông báo rõ ràng hơn.
                                System.Exception realException = ex.InnerException ?? ex;

                                // Tách thông báo lỗi của NUnit ra khỏi stack trace.
                                string errorMessage = realException.Message.Split(
                                    new[] { Environment.NewLine },
                                    StringSplitOptions.None)[0];

                                ed.WriteMessage($"    [THẤT BẠI] ❌");
                                ed.WriteMessage($"      Lý do: {errorMessage}"); // Chỉ in ra thông báo lỗi, không in đường dẫn file.
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        ed.WriteMessage($"    [LỖI NGHIÊM TRỌNG] ❌\n      Lỗi khi tạo Document test: {ex.Message}");
                    }
                    finally
                    {
                        if (testDoc != null && !testDoc.IsDisposed)
                        {
                            object oldFiledia = AcApp.GetSystemVariable("FILEDIA");
                            AcApp.SetSystemVariable("FILEDIA", 0);
                            testDoc.CloseAndDiscard();
                            AcApp.SetSystemVariable("FILEDIA", oldFiledia);
                        }
                    }
                }
            }

            AcApp.DocumentManager.MdiActiveDocument = originalDoc;
            ed.WriteMessage("\n\n============================================================");
            ed.WriteMessage($"\n TỔNG KẾT: {testsPassed}/{testsRun} BÀI TEST THÀNH CÔNG.");
            ed.WriteMessage("\n============================================================");
            ed.WriteMessage("\n");
        }
    }
}