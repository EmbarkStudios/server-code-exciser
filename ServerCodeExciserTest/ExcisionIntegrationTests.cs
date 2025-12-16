using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServerCodeExcisionCommon;
using UnrealAngelscriptServerCodeExcision;

namespace ServerCodeExciser.Tests
{
    [TestClass]
    public class ExcisionIntegrationTests
    {
        private static string DataRootDir = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "..", "..", ".."));

        [TestMethod]
        public void ExciseAngelscriptCases()
        {
            int numTestFailures = 0;
            int numTestCases = 0;

            // Run for Angelscript
            var actual = RunExciserIntegrationTests(
                ".as",
                Path.Combine(DataRootDir, "Problems", "Angelscript"),
                Path.Combine(DataRootDir, "Answers", "Angelscript"),
                ref numTestFailures,
                ref numTestCases);

            Assert.AreEqual(EExciserReturnValues.Success, actual);
            Assert.AreEqual(0, numTestFailures);
        }

        [TestMethod]
        public void ExciseCommonCases()
        {
            int numTestFailures = 0;
            int numTestCases = 0;

            // Run for "common"
            var actual = RunExciserIntegrationTests(
                ".common",
                Path.Combine(DataRootDir, "Problems", "Common"),
                Path.Combine(DataRootDir, "Answers", "Common"),
                ref numTestFailures,
                ref numTestCases);

            Assert.AreEqual(EExciserReturnValues.Success, actual);
            Assert.AreEqual(0, numTestFailures);
        }

        private static EExciserReturnValues RunExciserIntegrationTests(string fileExtension, string inputPath, string outputPath, ref int numTestFailures, ref int numTestCases)
        {
            // Clean up earlier answers.
            if (Directory.Exists(outputPath))
            {
                Directory.Delete(outputPath, true);
            }

            string searchPattern = "*." + fileExtension.TrimStart('.');
            Console.WriteLine($"Running integration tests for {searchPattern} files...");

            EExciserReturnValues returnCode;
            try
            {
                var excisionParams = new ServerCodeExcisionParameters
                {
                    OutputPath = outputPath,
                    ShouldOutputUntouchedFiles = true,
                    FullExcisionRegexString = @"FullExcise1/.*",
                    ExciseAllFunctionsRegexString = @"AllFunctionExcise1/.*|||AllFunctionExcise2/.*",
                    StrictMode = true,
                    DontSkip = true,
                };
                excisionParams.InputPaths.Add(inputPath);

                var angelscriptServerCodeExciser = new ServerCodeExcisionProcessor(excisionParams);
                returnCode = angelscriptServerCodeExciser.ExciseServerCode(searchPattern, new UnrealAngelscriptServerCodeExcisionLanguage());
                Console.WriteLine($"ExciseServerCode for {searchPattern} files returned: {returnCode}");
            }
            catch (Exception)
            {
                throw;
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
}
