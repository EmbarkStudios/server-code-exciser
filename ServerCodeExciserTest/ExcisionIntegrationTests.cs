using System;
using System.IO;
using ServerCodeExcision;
using ServerCodeExcisionCommon;

public class IntegrationTests
{
	private static string TestProblemPath = @"Problems";
	//private static string TestProblemPath = @"ProblemTestBed"; 
	private static string TestAnswerPath = @"Answers";
	private static string TestSolutionPath = @"Solutions";

	private static string CommonSubPath = @"Common";
	private static string AngelscriptSubPath = @"Angelscript";

	public static void Main(string[] args)
	{
		ConsoleColor initialColor = Console.ForegroundColor;
		bool excisionWasSuccessful = true;
		int nrCorrectAnswers = 0;
		int nrErrors = 0;
		int nrProblems = 0;

		var commonProblemPath = Path.Combine(TestProblemPath, CommonSubPath);
		var commonSolutionPath = Path.Combine(TestSolutionPath, CommonSubPath);

		// Run for Angelscript
		excisionWasSuccessful = excisionWasSuccessful && RunExciserIntegrationTests("as",
			Path.Combine(TestProblemPath, AngelscriptSubPath),
			Path.Combine(TestAnswerPath, AngelscriptSubPath),
			Path.Combine(TestSolutionPath, AngelscriptSubPath),
			commonProblemPath,
			commonSolutionPath,
			ref nrCorrectAnswers, ref nrErrors, ref nrProblems);

		if (excisionWasSuccessful)
		{
			Console.ForegroundColor = initialColor;
			Console.WriteLine("----------------------------");
			Console.WriteLine(nrCorrectAnswers > 0 && nrCorrectAnswers == nrProblems 
				? string.Format("{0} test(s) ran successfully.", nrCorrectAnswers)
				: string.Format("{0} error(s) detected running {1} tests", nrErrors, nrProblems));
		}
	}

	private static bool RunExciserIntegrationTests(string fileExtension, string testProblemPath, string testAnswerPath, string testSolutionPath, string commonProblemPath, string commonSolutionPath, ref int nrCorrectAnswers, ref int nrErrors, ref int nrProblems)
	{
		string problemPath = Path.Combine(Environment.CurrentDirectory, testProblemPath);
		string answerPath = Path.Combine(Environment.CurrentDirectory, testAnswerPath);
		string solutionPath = Path.Combine(Environment.CurrentDirectory, testSolutionPath);

		// First copy common problems to their language folders and rename them so they are picked up by the exciser if they exist.
		if (Directory.Exists(commonProblemPath) && Directory.Exists(commonSolutionPath))
		{
			CopyCommonTestFiles(fileExtension, problemPath, commonProblemPath);
			CopyCommonTestFiles(fileExtension, solutionPath, commonSolutionPath);
		}

		// Clean up earlier answers.
		if (Directory.Exists(answerPath))
		{
			Directory.Delete(answerPath, true);
		}

		string[] exciserArgs = 
		{
			problemPath,
			"-u",
			"-fe",
			"FullExcise1/.*",
			"-eaf",
			"AllFunctionExcise1/.*|||AllFunctionExcise2/.*",
			"-o",
			answerPath
		};
		
		var excisionReturnCode = (EExciserReturnValues)ServerCodeExciser.Main(exciserArgs);
		if (excisionReturnCode != EExciserReturnValues.Success)
		{
			Console.Error.WriteLine("Excision error: " + excisionReturnCode);
			return false;
		}
		
		if (Directory.Exists(answerPath))
		{
			foreach (var answerFilePath in Directory.EnumerateFiles(answerPath, "*." + fileExtension, SearchOption.AllDirectories))
			{
				nrProblems++;

				var relativePath = Path.GetRelativePath(answerPath, answerFilePath);
				var solutionFilePath = Path.Combine(solutionPath, relativePath);
				var fileName = Path.GetFileName(answerFilePath);

				var answer = File.ReadAllText(answerFilePath);
				var solution = File.ReadAllText(solutionFilePath);

				if(answer == solution)
				{
					Console.ForegroundColor = ConsoleColor.Green;
					Console.WriteLine(fileName + "'s answer matched the correct solution!");
					nrCorrectAnswers++;
				}
				else
				{
					Console.ForegroundColor = ConsoleColor.Red;
					Console.Error.WriteLine(fileName + "'s failed!");
					nrErrors++;
				}
			}
		}
		else
		{
			Console.Error.WriteLine("No test answers found in path: " + answerPath);
			return false;
		}

		// Clean up common folders if it went well
		CleanupTestFiles(problemPath);
		CleanupTestFiles(solutionPath);

		return true;
	}

	private static void CopyCommonTestFiles(string fileExtension, string targetRootPath, string commonRootPath)
	{
		// First clear target problem path
		CleanupTestFiles(targetRootPath);

		var targetCommonPath = Path.Combine(targetRootPath, "Common");
		foreach (var commonProblemFilePath in Directory.EnumerateFiles(commonRootPath, "*.*", SearchOption.AllDirectories))
		{
			var targetPath = Path.Combine(targetCommonPath, Path.GetRelativePath(commonRootPath, Path.ChangeExtension(commonProblemFilePath, fileExtension)));
			var targetDirectory = Path.GetDirectoryName(targetPath);
			if (targetDirectory != null)
			{
				Directory.CreateDirectory(targetDirectory);
				File.Copy(commonProblemFilePath, targetPath);
			}
		}
	}

	private static void CleanupTestFiles(string targetRootPath)
	{
		var targetCommonPath = Path.Combine(targetRootPath, "Common");
		if (Directory.Exists(targetCommonPath))
		{
			Directory.Delete(targetCommonPath, true);
		}
	}
}
