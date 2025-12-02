using ServerCodeExcision;
using ServerCodeExcisionCommon;
using Spectre.Console;
using System;
using System.IO;
using UnrealAngelscriptServerCodeExcision;

public class IntegrationTests
{
    private static string TestProblemPath = @"Problems";
    private static string TestAnswerPath = @"Answers";

    public static int Main(string[] args)
    {
        int numTestFailures = 0;
        int numTestCases = 0;

        try
        {
            // Run for Angelscript
            var angelscriptResult = RunExciserIntegrationTests(
                ".as",
                Path.Combine(Environment.CurrentDirectory, TestProblemPath, "Angelscript"),
                Path.Combine(Environment.CurrentDirectory, TestAnswerPath, "Angelscript"),
                ref numTestFailures,
                ref numTestCases);

            // Run for "common"
            var commonResult = RunExciserIntegrationTests(
                ".common",
                Path.Combine(Environment.CurrentDirectory, TestProblemPath, "Common"),
                Path.Combine(Environment.CurrentDirectory, TestAnswerPath, "Common"),
                ref numTestFailures,
                ref numTestCases);

            Console.WriteLine("----------------------------");
            Console.WriteLine($"{numTestCases - numTestFailures} test(s) passed.");
            Console.WriteLine($"{numTestFailures} test(s) failed.");
        }
        catch (Exception e)
        {
            AnsiConsole.WriteException(e);
            return 1;
        }

        return numTestFailures == 0 ? 0 : 1;
    }

    private static EExciserReturnValues RunExciserIntegrationTests(string fileExtension, string inputPath, string outputPath, ref int numTestFailures, ref int numTestCases)
    {
        // Clean up earlier answers.
        if (Directory.Exists(outputPath))
        {
            Directory.Delete(outputPath, true);
        }

        string searchPattern = "*" + fileExtension.TrimStart('.');

        EExciserReturnValues returnCode;
        try
        {
            var excisionParams = new ServerCodeExcisionParameters
            {
                OutputPath = outputPath,
                ShouldOutputUntouchedFiles = true,
                FullExcisionRegexString = @"FullExcise1/.*",
                ExciseAllFunctionsRegexString = @"AllFunctionExcise1/.*|||AllFunctionExcise2/.*",
            };
            excisionParams.InputPaths.Add(inputPath);

            var angelscriptServerCodeExciser = new ServerCodeExcisionProcessor(excisionParams);
            returnCode = angelscriptServerCodeExciser.ExciseServerCode(searchPattern, new UnrealAngelscriptServerCodeExcisionLanguage());
            Console.WriteLine($"ExciseServerCode for {fileExtension} files returned: {returnCode}");
        }
        catch (Exception e)
        {
            AnsiConsole.WriteException(e);
            return EExciserReturnValues.InternalExcisionError;
        }

        foreach (var answerFilePath in Directory.EnumerateFiles(outputPath, searchPattern, SearchOption.AllDirectories))
        {
            numTestCases++;

            var solutionFilePath = Path.Combine(inputPath, Path.GetRelativePath(outputPath, answerFilePath)) + ".solution";

            var fileName = Path.GetFileName(answerFilePath);
            var answer = File.ReadAllText(answerFilePath);
            var solution = File.ReadAllText(solutionFilePath);

            if (answer == solution)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(fileName + " passed!");
                Console.ForegroundColor = ConsoleColor.Gray;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine(fileName + " failed!");
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine("--- Expected: ---");
                Console.WriteLine(solution);
                Console.WriteLine("--- Actual: ---");
                Console.WriteLine(answer);
                numTestFailures++;
            }
        }

        return returnCode;
	}
}
