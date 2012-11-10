using System;
using System.Diagnostics;

namespace SpaceClaim.AddInLibrary {
	static class Solver {
		public delegate bool TryGetOutput(double input, out double output);

		public static bool FindSolution(double inputGuess, double inputStepSize,
			double outputGoal, Comparison<double> compareOutputs, TryGetOutput tryGetOutput, out double input) {
			Debug.Assert(compareOutputs != null);
			Debug.Assert(tryGetOutput != null);
			Debug.Assert(inputStepSize > 0);

			double lowerGuess, upperGuess;
			input = inputGuess;

			double output;
			if (!tryGetOutput(input, out output))
				return false;

			int comparison = compareOutputs(output, outputGoal);
			switch (comparison) {
				default:
				case 0:
					return true;

				case -1:
					do {
						lowerGuess = input;
						input += inputStepSize;

						if (!tryGetOutput(input, out output))
							return false;

						comparison = compareOutputs(output, outputGoal);
						if (comparison == 0)
							return true;
					} while (comparison < 0);
					upperGuess = input;
					break;

				case +1:
					do {
						upperGuess = input;
						input -= inputStepSize;

						if (!tryGetOutput(input, out output))
							return false;

						comparison = compareOutputs(output, outputGoal);
						if (comparison == 0)
							return true;
					} while (comparison > 0);
					lowerGuess = input;
					break;
			}

			Debug.Assert(lowerGuess < upperGuess);

			while (true) {
				input = (lowerGuess + upperGuess) / 2;

				if (!tryGetOutput(input, out output))
					return false;

				switch (compareOutputs(output, outputGoal)) {
					case 0:
						return true;

					case -1:
						lowerGuess = input;
						break;

					case +1:
						upperGuess = input;
						break;
				}
			}
		}

    }
}
