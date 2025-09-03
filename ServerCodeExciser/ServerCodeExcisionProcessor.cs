using Antlr4.Runtime;
using ServerCodeExcisionCommon;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace ServerCodeExciser
{
    public class ServerCodeExcisionParameters
    {
        public ISet<string> InputPaths { get; } = new HashSet<string>();
        public string OutputPath { get; set; } = string.Empty;
        public string ExciseAllFunctionsRegexString { get; set; } = string.Empty;
        public string FullExcisionRegexString { get; set; } = string.Empty;
        public bool ShouldOutputUntouchedFiles { get; set; } = false;
        public bool IsDryRun { get; set; } = false;
        public bool Verify { get; set; } = false;
        public bool UseFunctionStats { get; set; } = false;
        public bool DontSkip { get; set; } = false;
        public float RequiredExcisionRatio { get; set; } = -1.0f;
        public EExcisionLanguage ExcisionLanguage { get; set; } = EExcisionLanguage.Unknown;
    }

    public class ServerCodeExcisionProcessor
    {
        private readonly ServerCodeExcisionParameters _parameters;
        private readonly List<Regex> _functionExciseRegexes;
        private readonly List<Regex> _fullyExciseRegexes;
        private readonly List<string> _filesFailedToParse = new List<string>();

        public ServerCodeExcisionProcessor(ServerCodeExcisionParameters parameters)
        {
            _parameters = parameters;

            _functionExciseRegexes = new List<Regex>();
            if (_parameters.ExciseAllFunctionsRegexString != "")
            {
                var allFunctionExciseRegexStrings = _parameters.ExciseAllFunctionsRegexString.Split("|||");
                foreach (var regexString in allFunctionExciseRegexStrings)
                {
                    _functionExciseRegexes.Add(new Regex(regexString));
                    Console.WriteLine("Added all function excise regex: " + regexString);
                }
            }

            _fullyExciseRegexes = new List<Regex>();
            if (_parameters.FullExcisionRegexString != "")
            {
                var fullyExciseRegexStrings = _parameters.FullExcisionRegexString.Split("|||");
                foreach (var regexString in fullyExciseRegexStrings)
                {
                    _fullyExciseRegexes.Add(new Regex(regexString));
                    Console.WriteLine("Added fully excise regex: " + regexString);
                }
            }
        }

        public EExciserReturnValues ExciseServerCode(string filePattern, IServerCodeExcisionLanguage excisionLanguage)
        {
            var startTime = DateTime.UtcNow;
            var globalStats = new ExcisionStats();

            var options = new EnumerationOptions();
            options.RecurseSubdirectories = true;
            foreach (var inputPath in _parameters.InputPaths)
            {
                var allFiles = Directory.GetFiles(inputPath, filePattern, options);
                if (allFiles.Length < 1)
                {
                    Console.Error.WriteLine("The input path did not contain any files, cannot excise!");
                    return EExciserReturnValues.InputPathEmpty;
                }

                for (int fileIdx = 0; fileIdx < allFiles.Length; fileIdx++)
                {
                    var fileName = allFiles[fileIdx];

                    var relativePath = Path.GetRelativePath(inputPath, fileName).Replace(@"\", "/");
                    var excisionMode = EExcisionMode.ServerOnlyScopes;

                    // Should we full excise this file?
                    foreach (var fullExciseRegex in _fullyExciseRegexes)
                    {
                        if (fullExciseRegex.IsMatch(relativePath))
                        {
                            excisionMode = EExcisionMode.Full;
                            break;
                        }
                    }

                    if (excisionMode == EExcisionMode.ServerOnlyScopes)
                    {
                        // Okay, then maybe we should be only excising functions..?
                        foreach (var allFunctionExciseRegex in _functionExciseRegexes)
                        {
                            if (allFunctionExciseRegex.IsMatch(relativePath))
                            {
                                excisionMode = EExcisionMode.AllFunctions;
                                break;
                            }
                        }
                    }

                    try
                    {
                        var stats = ProcessCodeFile(fileName, inputPath, excisionMode, excisionLanguage);
                        if (stats.CharactersExcised > 0)
                        {
                            System.Diagnostics.Debug.Assert(stats.TotalNrCharacters > 0, "Something is terribly wrong. We have excised characters, but no total characters..?");
                            var excisionRatio = stats.CharactersExcised / (float)stats.TotalNrCharacters * 100.0f;
                            Console.WriteLine("Excised {0:0.00}% of server only code in file ({1}/{2}): {3}",
                                    excisionRatio, fileIdx + 1, allFiles.Length, fileName);
                        }
                        else
                        {
                            Console.WriteLine("No server only code found in file ({0}/{1}): {2}", fileIdx + 1, allFiles.Length, fileName);
                        }

                        globalStats.CharactersExcised += stats.CharactersExcised;
                        globalStats.TotalNrCharacters += stats.TotalNrCharacters;
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine("Failed to parse ({0}/{1}): {2}", fileIdx + 1, allFiles.Length, fileName);
                        _filesFailedToParse.Add(fileName);
                    }
                }
            }

            var endTime = DateTime.UtcNow;

            // In verification mode error codes reverse the normal behavior of the exciser.
            // Modifications required -> error
            // No modifications required -> success
            if (_parameters.Verify)
            {
                if (globalStats.CharactersExcised > 0)
                {
                    Console.Error.WriteLine("Executed in verification mode. Manual server code excision is required.");
                    return EExciserReturnValues.RequiresExcision;
                }
                else
                {
                    Console.WriteLine("Executed in verification mode. No modifications are required.");
                    return EExciserReturnValues.Success;
                }
            }

            if (globalStats.CharactersExcised > 0)
            {
                System.Diagnostics.Debug.Assert(globalStats.TotalNrCharacters > 0, "Something is terribly wrong.");
                var totalExcisionRatio = globalStats.CharactersExcised / (float)globalStats.TotalNrCharacters * 100.0f;
                Console.WriteLine("----------------------------");
                Console.WriteLine("Excised {0:0.00}% ({1}/{2} characters) of server only code from the script files.",
                            totalExcisionRatio, globalStats.CharactersExcised, globalStats.TotalNrCharacters);

                var timeTaken = endTime - startTime;
                Console.WriteLine("Excision took {0:0} hours, {1:0} minutes and {2:0.0} seconds.\n\n", timeTaken.Hours, timeTaken.Minutes, timeTaken.Seconds);

                foreach (var file in _filesFailedToParse)
                {
                    Console.Error.WriteLine($"Failed to parse: {file}");
                }

                if (_parameters.RequiredExcisionRatio > 0.0f && totalExcisionRatio < _parameters.RequiredExcisionRatio)
                {
                    Console.Error.WriteLine("A required excision ratio of {0}% was set, but excision only reached {1}%!", _parameters.RequiredExcisionRatio, totalExcisionRatio);
                    return EExciserReturnValues.RequiredExcisionRatioNotReached;
                }
            }
            else
            {
                Console.Error.WriteLine("The exciser ran, but nothing was actually excised. Something must have gone wrong.");
                return EExciserReturnValues.NothingExcised;
            }

            return EExciserReturnValues.Success;
        }

        private ExcisionStats ProcessCodeFile(string fileName, string inputPath, EExcisionMode excisionMode, IServerCodeExcisionLanguage excisionLanguage)
        {
            var stats = new ExcisionStats();

            var relativePath = Path.GetRelativePath(inputPath, fileName);
            var script = File.ReadAllText(fileName);
            stats.TotalNrCharacters = script.Length;

            // Setup parsing and output.
            var injections = new InjectionTable();
            var inputStream = new AntlrInputStream(script);
            var lexer = excisionLanguage.CreateLexer(inputStream);
            lexer.AddErrorListener(new ExcisionLexerErrorListener());
            var commonTokenStream = new CommonTokenStream(lexer);
            var parser = excisionLanguage.CreateParser(commonTokenStream);

            IServerCodeVisitor? visitor = null;
            if (excisionMode == EExcisionMode.Full)
            {
                injections.Add(0, excisionLanguage.ServerScopeStartString);
                injections.Add(script.Length, excisionLanguage.ServerScopeEndString);
            }
            else if (excisionMode == EExcisionMode.AllFunctions)
            {
                // We want to excise all functions in this file no matter if there are any server symbols or not.
                visitor = excisionLanguage.CreateFunctionVisitor(script);
            }
            else if (!_parameters.DontSkip && !excisionLanguage.AnyServerOnlySymbolsInScript(script))
            {
                // There are no interesting symbols in this script file. We should just skip it!
                stats.CharactersExcised = 0;
                if (_parameters.UseFunctionStats)
                {
                    visitor = excisionLanguage.CreateSimpleVisitor(script);
                }
            }
            else
            {
                // We want to excise all server only symbols in this file.
                visitor = excisionLanguage.CreateSymbolVisitor(script);
            }

            // Gather all the injections we want to make
            if (visitor != null)
            {
                visitor.VisitContext(parser.GetParseTree());
                if (_parameters.UseFunctionStats)
                {
                    stats.TotalNrCharacters = visitor.TotalNumberOfFunctionCharactersVisited;
                }

                // First process all server only scopes.
                foreach (ServerOnlyScopeData currentScope in visitor.DetectedServerOnlyScopes)
                {
                    if (string.IsNullOrEmpty(currentScope.Context))
                    {
                        injections.Add(currentScope.StartLine, excisionLanguage.ServerScopeStartString);
                        injections.Add(currentScope.StopLine, excisionLanguage.ServerScopeEndString);
                    }
                    else
                    {
                        injections.Add(currentScope.StartLine, excisionLanguage.ServerScopeStartString + $" // {currentScope.Context}");
                        injections.Add(currentScope.StopLine, excisionLanguage.ServerScopeEndString + $" // {currentScope.Context}");
                    }
                }
            }

            // generate new script.
            var builder = new ScriptBuilder();
            using (var reader = new StringReader(script))
            {
                int lineIndex = 1;
                for (; ; )
                {
                    var line = reader.ReadLine();
                    if (line == null)
                        break;

                    foreach (var text in injections.Get(lineIndex))
                    {
                        builder.AddLine(text);
                    }

                    if (line.Contains("UEmbarkServerEventsSubsystem::Get()") && !builder.IsInScope("WITH_SERVER"))
                    {
                        builder.AddLine("// The next line is server only code, but we cannot suggest a fix.");
                    }

                    builder.AddLine(line);
                    lineIndex++;
                }
            }

            // detect changes.
            var newText = builder.ToString();
            bool fileHasChanged = newText != script;

            if (fileHasChanged || _parameters.ShouldOutputUntouchedFiles)
            {
                stats.CharactersExcised = newText.Length - script.Length;
                var outputPath = !string.IsNullOrEmpty(_parameters.OutputPath) ? Path.Combine(_parameters.OutputPath, relativePath) : fileName;
                var outputDirectoryPath = Path.GetDirectoryName(outputPath)!;
                if (!Directory.Exists(outputDirectoryPath))
                {
                    Directory.CreateDirectory(outputDirectoryPath);
                }

                try
                {
                    if (!_parameters.IsDryRun)
                    {
                        if (File.Exists(outputPath))
                        {
                            // If the file exists, we might have to clear read-only from p4 etc. This is common for in-place excision.
                            File.SetAttributes(outputPath, FileAttributes.Normal);
                        }

                        File.WriteAllText(outputPath, newText);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }

            return stats;
        }
    }
}
