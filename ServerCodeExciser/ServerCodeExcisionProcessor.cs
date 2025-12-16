using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Antlr4.Runtime;
using ServerCodeExcisionCommon;

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
        public bool StrictMode { get; set; } = false;
        public bool UseFunctionStats { get; set; } = false;
        public bool DontSkip { get; set; } = false;
        public float RequiredExcisionRatio { get; set; } = 0.0f;
        public EExcisionLanguage ExcisionLanguage { get; set; } = EExcisionLanguage.Unknown;
    }

    public class ServerCodeExcisionProcessor
    {
        private ServerCodeExcisionParameters _parameters;
        private List<Regex> _functionExciseRegexes;
        private List<Regex> _fullyExciseRegexes;

        public ServerCodeExcisionProcessor(ServerCodeExcisionParameters parameters)
        {
            _parameters = parameters;

            _functionExciseRegexes = new List<Regex>();
            if (!string.IsNullOrEmpty(_parameters.ExciseAllFunctionsRegexString))
            {
                var allFunctionExciseRegexStrings = _parameters.ExciseAllFunctionsRegexString.Split("|||");
                foreach (var regexString in allFunctionExciseRegexStrings)
                {
                    _functionExciseRegexes.Add(new Regex(regexString));
                    Console.WriteLine("Added all function excise regex: " + regexString);
                }
            }

            _fullyExciseRegexes = new List<Regex>();
            if (!string.IsNullOrEmpty(_parameters.FullExcisionRegexString))
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
            var globalStopwatch = Stopwatch.StartNew();
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
                    var fileInfo = new FileInfo(allFiles[fileIdx]);
                    var relativePath = Path.GetRelativePath(inputPath, allFiles[fileIdx]);
                    var excisionMode = EExcisionMode.ServerOnlyScopes;

                    // Should we full excise this file?
                    var relativePathRegex = relativePath.Replace(@"\", "/");
                    foreach (var fullExciseRegex in _fullyExciseRegexes)
                    {
                        if (fullExciseRegex.IsMatch(relativePathRegex))
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
                            if (allFunctionExciseRegex.IsMatch(relativePathRegex))
                            {
                                excisionMode = EExcisionMode.AllFunctions;
                                break;
                            }
                        }
                    }

                    try
                    {
                        var stats = new ExcisionStats();
                        var stopwatch = Stopwatch.StartNew();
                        var outputPath = (!string.IsNullOrEmpty(_parameters.OutputPath)) ? Path.Combine(_parameters.OutputPath, relativePath) : fileInfo.FullName;
                        var ret = ProcessCodeFile(fileInfo, outputPath, excisionMode, excisionLanguage, ref stats);
                        stopwatch.Stop();

                        if (stats.CharactersExcised > 0)
                        {
                            System.Diagnostics.Debug.Assert(stats.TotalNrCharacters > 0, "Something is terribly wrong. We have excised characters, but no total characters..?");
                            var excisionRatio = (float)stats.CharactersExcised / (float)stats.TotalNrCharacters * 100.0f;
                            Console.WriteLine($"[{fileIdx + 1}/{allFiles.Length}] Excised {excisionRatio:0.00}% of server only code (took {stopwatch.Elapsed.TotalMilliseconds:0.0}ms): {fileInfo.FullName}");
                        }
                        else
                        {
                            Console.WriteLine($"[{fileIdx + 1}/{allFiles.Length}] No action required (took {stopwatch.Elapsed.TotalMilliseconds:0.0}ms): {fileInfo.FullName}");
                        }

                        globalStats.CharactersExcised += stats.CharactersExcised;
                        globalStats.TotalNrCharacters += stats.TotalNrCharacters;
                    }
                    catch (Exception)
                    {
                        Console.Error.WriteLine($"[{fileIdx + 1}/{allFiles.Length}] Failed to parse: {fileInfo.FullName}");
                        throw;
                    }
                }
            }

            globalStopwatch.Stop();

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
                var totalExcisionRatio = (float)globalStats.CharactersExcised / (float)globalStats.TotalNrCharacters * 100.0f;
                Console.WriteLine("----------------------------");
                Console.WriteLine("Excised {0:0.00}% ({1}/{2} characters) of server only code from the script files.",
                            totalExcisionRatio, globalStats.CharactersExcised, globalStats.TotalNrCharacters);

                Console.WriteLine($"Excision took {globalStopwatch.Elapsed.TotalSeconds:0.0}s.");

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

        internal EExciserReturnValues ProcessCodeFile(FileInfo inputFile, string outputFile, EExcisionMode excisionMode, IServerCodeExcisionLanguage excisionLanguage, ref ExcisionStats stats)
        {
            using StreamReader reader = inputFile.OpenText();
            return ProcessCodeFile(reader.ReadToEnd(), outputFile, excisionMode, excisionLanguage, ref stats);
        }

        internal EExciserReturnValues ProcessCodeFile(string script, string outputFile, EExcisionMode excisionMode, IServerCodeExcisionLanguage excisionLanguage, ref ExcisionStats stats)
        {
            stats.TotalNrCharacters = script.Length;

            // Setup parsing and output.
            List<KeyValuePair<int, string>> serverCodeInjections = new List<KeyValuePair<int, string>>();
            var inputStream = new AntlrInputStream(script);
            var lexer = excisionLanguage.CreateLexer<UnrealAngelscriptLexer>(inputStream);
            lexer.AddErrorListener(new ExcisionLexerErrorListener());
            var commonTokenStream = new CommonTokenStream(lexer);
            var parser = excisionLanguage.CreateParser<UnrealAngelscriptParser>(commonTokenStream);
            parser.AddErrorListener(new ExcisionParserErrorListener());

            if (_parameters.StrictMode)
            {
                parser.ErrorHandler = new BailErrorStrategy();
            }

            IServerCodeVisitor? visitor = null;
            if (excisionMode == EExcisionMode.Full)
            {
                // We want to excise this entire file.
                serverCodeInjections.Add(new KeyValuePair<int, string>(0, excisionLanguage.ServerScopeStartString + "\r\n"));
                serverCodeInjections.Add(new KeyValuePair<int, string>(script.Length, excisionLanguage.ServerScopeEndString + "\r\n"));
                stats.CharactersExcised += script.Length;
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
                visitor.VisitContext(parser.script());

                if (_parameters.UseFunctionStats)
                {
                    stats.TotalNrCharacters = visitor.TotalNumberOfFunctionCharactersVisited;
                }

                // Determine if there are any existing preprocessor server-code exclusions in the source file.
                var preprocessorScopes = PreprocessorParser.Parse(commonTokenStream);
                var detectedPreprocessorServerOnlyScopes = new List<PreprocessorScope>();
                FindPreprocessorScopesForSymbolRecursive(
                    preprocessorScopes,
                    scope => scope.Directive.Contains(excisionLanguage.ServerScopeStartString, StringComparison.Ordinal),
                    detectedPreprocessorServerOnlyScopes);

                // Process scopes we've evaluated must be server only.
                foreach (ServerOnlyScopeData currentScope in visitor.DetectedServerOnlyScopes)
                {
                    if (currentScope.Span.StartIndex == -1 || currentScope.Span.EndIndex == -1)
                    {
                        continue;
                    }

                    int startIndex = currentScope.Span.StartIndex + 1;

                    int endIndex = script[currentScope.Span.EndIndex] switch
                    {
                        '}' => currentScope.Span.EndIndex - 1, // scope is closed prior to '}'
                        ';' => currentScope.Span.EndIndex + 1, // treat ';' as part of the scope
                        _ => throw new NotImplementedException(),
                    };

                    // Skip if there's already a server-code exclusion for the scope. (We don't want have duplicate guards.)
                    var (StartIndex, EndIndex) = TrimWhitespace(script, startIndex, endIndex);
                    if (detectedPreprocessorServerOnlyScopes.Any(x => StartIndex >= x.Span.StartIndex && EndIndex <= x.Span.EndIndex))
                    {
                        continue; // We're inside an existing scope.
                    }

                    const bool DebugMarkers = false;
                    var builder = new StringBuilder();

                    var startText = $"{excisionLanguage.ServerScopeStartString}\r\n";
                    builder.Append(DebugMarkers ? "<START>" : "");
                    builder.Append(startText);
                    builder.Append(DebugMarkers ? "</START>" : "");

                    if (startText.StartsWith("#"))
                    {
                        startIndex = ScanToNextLineBreak(script, startIndex);
                    }
                    serverCodeInjections.Add(new KeyValuePair<int, string>(startIndex, builder.ToString()));

                    builder.Clear();

                    if (!string.IsNullOrEmpty(currentScope.Opt_ElseContent))
                    {
                        builder.Append(DebugMarkers ? "<ELSE>" : "");
                        builder.Append(currentScope.Opt_ElseContent);
                        builder.Append(DebugMarkers ? "</ELSE>" : "");
                    }

                    var endText = $"{excisionLanguage.ServerScopeEndString}\r\n";
                    builder.Append(DebugMarkers ? "<END>" : "");
                    builder.Append(endText);
                    builder.Append(DebugMarkers ? "</END>" : "");

                    if (endText.StartsWith("#"))
                    {
                        if (!script[startIndex..endIndex].Contains('\n'))
                        {
                            endIndex = ScanToNextLineBreak(script, endIndex); // one liner?
                        }
                        else
                        {
                            endIndex = ScanToStartOfLine(script, endIndex);
                        }
                    }
                    serverCodeInjections.Add(new KeyValuePair<int, string>(endIndex, builder.ToString()));

                    stats.CharactersExcised += currentScope.Span.EndIndex - currentScope.Span.StartIndex;
                }

                // Next we must add dummy reference variables if they exist.
                // If there is already a block of dummy reference variables we skip adding new ones, there is no guarantee we are adding the right code.
                var dummyVarScope = "#ifndef " + excisionLanguage.ServerPrecompilerSymbol;
                var detectedIfndefScopes = new List<PreprocessorScope>();
                FindPreprocessorScopesForSymbolRecursive(
                    preprocessorScopes,
                    scope => scope.Directive.Contains(dummyVarScope, StringComparison.Ordinal),
                    detectedIfndefScopes);
                
                if (!detectedIfndefScopes.Any())
                {
                    foreach (KeyValuePair<int, HashSet<string>> dummyRefDataPair in visitor.ClassStartIdxDummyReferenceData)
                    {
                        var dummyRefDataBlockString = new StringBuilder(dummyVarScope);
                        foreach (var dummyVarDef in dummyRefDataPair.Value)
                        {
                            dummyRefDataBlockString.Append("\r\n\t" + dummyVarDef);
                        }

                        dummyRefDataBlockString.Append("\r\n" + excisionLanguage.ServerScopeEndString + "\r\n\r\n");

                        serverCodeInjections.Add(new KeyValuePair<int, string>(dummyRefDataPair.Key, dummyRefDataBlockString.ToString()));
                    }
                }
            }

            // Now sort them in the reverse order, since adding later will not affect earlier adds.
            serverCodeInjections.Sort(delegate (KeyValuePair<int, string> pair1, KeyValuePair<int, string> pair2)
            {
                return pair2.Key.CompareTo(pair1.Key);
            });

            var answerText = new StringBuilder(script);

            // Now insert them in that reversed order.
            bool fileHasChanged = false;
            foreach (var injection in serverCodeInjections)
            {
                answerText.Insert(injection.Key, injection.Value);
                fileHasChanged = true;
            }

            if (fileHasChanged || _parameters.ShouldOutputUntouchedFiles)
            {
                try
                {
                    if (!_parameters.IsDryRun)
                    {
                        var outputDirectoryPath = Path.GetDirectoryName(outputFile)!;
                        if (!Directory.Exists(outputDirectoryPath))
                        {
                            Directory.CreateDirectory(outputDirectoryPath);
                        }

                        if (File.Exists(outputFile))
                        {
                            // If the file exists, we might have to clear read-only from p4 etc. This is common for in-place excision.
                            File.SetAttributes(outputFile, FileAttributes.Normal);
                        }

                        File.WriteAllText(outputFile, answerText.ToString());
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                    return EExciserReturnValues.BadOutputPath;
                }
            }

            return EExciserReturnValues.Success;
        }

        private static bool IsWhitespace(char c)
        {
            return c == ' ' || c == '\t' || c == '\r' || c == '\n';
        }

        private static int ScanToStartOfLine(ReadOnlySpan<char> script, int index)
        {
            if (script[index] == '\n')
            {
                return index;
            }
            while (index > 0)
            {
                if (script[index - 1] == '\n')
                {
                    return index;
                }
                index--;
            }
            return index;
        }

        private static int ScanToNextLineBreak(ReadOnlySpan<char> script, int index)
        {
            while (index < script.Length)
            {
                if (script[index] == '\n')
                {
                    return index + 1;
                }
                index++;
            }
            return index;
        }

        /// <summary>
        /// Resize a scope range by excluding whitespace characters.
        /// </summary>
        private static (int StartIndex, int StopIndex) TrimWhitespace(ReadOnlySpan<char> script, int startIndex, int stopIndex)
        {
            while (IsWhitespace(script[startIndex]))
            {
                startIndex++;
            }

            while (IsWhitespace(script[stopIndex]))
            {
                stopIndex--;
            }

            return (startIndex, stopIndex);
        }

        private static void FindPreprocessorScopesForSymbolRecursive(List<PreprocessorScope> scopes, Predicate<PreprocessorScope> predicate, List<PreprocessorScope> result)
        {
            foreach (var scope in scopes)
            {
                if (predicate(scope))
                {
                    result.Add(scope);
                }
                FindPreprocessorScopesForSymbolRecursive(scope.Children, predicate, result);
            }
        }
    }
}
