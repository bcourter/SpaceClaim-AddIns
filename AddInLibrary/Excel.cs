#if false
using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;
using SpaceClaim.Api.V10;
using SpaceClaim.Api.V10.Extensibility;
using SpaceClaim.Api.V10.Geometry;
using SpaceClaim.Api.V10.Modeler;
using SpaceClaim.AddInLibrary.Properties;
using Microsoft.Office.Core;
using Microsoft.Office.Interop;
using Excel = Microsoft.Office.Interop.Excel;
/// <summary>
/// AddIn helper functions and other utilities
/// </summary>

namespace SpaceClaim.AddInLibrary {
	public class ExcelWorksheet {
		const string excelNameUnitSuffix = "_units";
		static Excel.Application excelApp = new Excel.Application();

		// TBD make the workbook and sheet global until I figure out how to connect to the active one
		Excel._Workbook excelWorkBook;
		Excel._Worksheet excelSheet;

		// Work around localization incompatabiltiies (http://support.microsoft.com/kb/320369)
		System.Globalization.CultureInfo osCultureInfo = System.Threading.Thread.CurrentThread.CurrentCulture;

		public ExcelWorksheet() {
			excelApp.Visible = true;
			System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");

			excelWorkBook = excelApp.ActiveWorkbook;
			if (excelWorkBook == null)
				excelWorkBook = (Excel._Workbook) excelApp.Workbooks.Add(Missing.Value);

			excelSheet = (Excel._Worksheet) excelWorkBook.ActiveSheet;
			excelSheet.Name = "SpaceClaim";
		}

		public void SetCell(int row, int column, string value) {
			// TBD: add a custom property to link the design to the path of the worksheet
			// window.Document.CustomProperties.
			try {
				Excel.Range range = excelSheet.get_Range(String.Format("{0}{1}", GetColumnFromInt(column), row), Missing.Value);
				range.set_Value(Missing.Value, value);
			}
			catch (Exception e) {
				MessageBox.Show("Exception " + e.Message + " Stack Trace: " + e.StackTrace);
			}

			System.Threading.Thread.CurrentThread.CurrentCulture = osCultureInfo;
		}

		public void SetCell(int row, int column, double value) {
			SetCell(row, column, value.ToString());
		}

		//http://support.microsoft.com/kb/302096
		public Object[,] GetRangeObjects() {
			try {
				Excel.Range range = excelSheet.UsedRange;
				return (System.Object[,]) range.get_Value(Missing.Value);
			}
			catch (Exception e) {
				MessageBox.Show("Exception " + e.Message + " Stack Trace: " + e.StackTrace);
			}

			return null;
		}

		public string[,] GetRangeStrings() {
			Object[,] objects = GetRangeObjects();

			//Determine the dimensions of the array.
			long iRows;
			long iCols;
			iRows = objects.GetUpperBound(0);
			iCols = objects.GetUpperBound(1);

			var strings = new string[iRows, iCols];
			for (long rowCounter = 1; rowCounter <= iRows; rowCounter++) {
				for (long colCounter = 1; colCounter <= iCols; colCounter++) {
					strings[rowCounter, colCounter] = objects[rowCounter, colCounter].ToString();
				}
			}

			return strings;
		}

		static string GetColumnFromInt(int x) { // TBD support more than 26 columns
			if (x > 26)
				throw new ArgumentException("Only 26 columns supported; Blake is a very lazy programmer");

			char column = (char) (64 + x);
			return column.ToString();
		}
	}
}
#endif