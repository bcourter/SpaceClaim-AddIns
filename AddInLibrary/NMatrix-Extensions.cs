// Extra methods for cMatrixLib for TrackerProject
using System;
using System.Linq;

namespace MatrixLibrary {
	public partial class NMatrix {

		// From http://www.dreamincode.net/code/snippet1312.htm
		/******************************************************************************/
		/* Perform Gauss-Jordan elimination with row-pivoting to obtain the solution to 
		 * the system of linear equations
		 * A X = B
		 * 
		 * Arguments:
		 * 		lhs		-	left-hand side of the equation, matrix A
		 * 		rhs		-	right-hand side of the equation, matrix B
		 * 		nrows	-	number of rows in the arrays lhs and rhs
		 * 		ncolsrhs-	number of columns in the array rhs
		 * 
		 * The function uses Gauss-Jordan elimination with pivoting.  The solution X to 
		 * the linear system winds up stored in the array rhs; create a copy to pass to
		 * the function if you wish to retain the original RHS array.
		 * 
		 * Passing the identity matrix as the rhs argument results in the inverse of 
		 * matrix A, if it exists.
		 * 
		 * No library or header dependencies, but requires the function swaprows, which 
		 * is included here.
		 */
#if false
//  swaprows - exchanges the contents of row0 and row1 in a 2d array
void swaprows(double** arr, long row0, long row1) {
    double* temp;
    temp=arr[row0];
    arr[row0]=arr[row1];
    arr[row1]=temp;
}
#endif

		public static bool TryGaussJordanElimination(NMatrix a, NMatrix b, out NMatrix result) {
			if (a.NoRows != b.NoRows)
				throw new ArgumentException();

			result = null;

			NMatrix lhs = a.Copy();
			NMatrix rhs = b.Copy();

			int rowCount = lhs.NoRows;
			int colCount = rhs.NoCols + lhs.NoCols;

			//	augment lhs array with rhs array and store in arr2
			var matrixArray = new double[rowCount, colCount];
			for (int row = 0; row < rowCount; ++row) {
				for (int col = 0; col < lhs.NoCols; ++col) {
					matrixArray[row, col] = lhs[row, col];
				}
				for (int col = 0; col < rhs.NoCols; ++col) {
					matrixArray[row, lhs.NoCols + col] = rhs[row, col];
				}
			}

			//	perform forward elimination to get arr2 in row-echelon form
			for (int row = 0; row < rowCount; ++row) {
				//	run along diagonal, swapping rows to move zeros in working position 
				//	(along the diagonal) downwards
				if (IsZero(matrixArray[row, row]))
					if (row == (rowCount - 1))
						return false; //  no solution

				for (int insideRow = row + 1; insideRow < rowCount; insideRow++) {
					if (!IsZero(matrixArray[insideRow, row])) {
						SwapRows(matrixArray, row, insideRow);
						break;
					}
				}

				//	divide working row by value of working position to get a 1 on the
				//	diagonal
				if (IsZero(matrixArray[row, row]))
					return false;

				double diagonal = matrixArray[row, row];
				for (int col = 0; col < colCount; ++col)
					matrixArray[row, col] /= diagonal;

				//	eliminate value below working position by subtracting a multiple of 
				//	the current row
				for (int insideRow = row + 1; insideRow < rowCount; ++insideRow) {
					double coefficient = matrixArray[insideRow, insideRow];
					for (int col = 0; col < colCount; ++col)
						matrixArray[insideRow, col] -= coefficient * matrixArray[insideRow, col];
				}
			}

			//	backward substitution steps
			for (int dindex = rowCount - 1; dindex >= 0; --dindex) {
				//	eliminate value above working position by subtracting a multiple of 
				//	the current row
				for (int row = dindex - 1; row >= 0; --row) {
					double coefficient = matrixArray[row, dindex];
					for (int col = 0; col < colCount; ++col)
						matrixArray[row, col] -= coefficient * matrixArray[dindex, col];
				}
			}

			result = new NMatrix(matrixArray);
			return true;
		}

		private static bool IsZero(double value) {
			return Math.Abs(value) < 0.0000000001;
		}

		public NMatrix Copy() {
			return new NMatrix(this.toArray);
		}

		public double[] GetColumn(int col) {
			var result = new double[NoRows];
			for (int i = 0; i < NoRows; i++)
				result[i] = this[i, col];

			return result;
		}

		public double[] GetRow(int row) {
			var result = new double[NoCols];
			for (int i = 0; i < NoCols; i++)
				result[i] = this[row, i];

			return result;
		}

	}
}