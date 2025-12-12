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
                        var stats = new ExcisionStats();
                        var stopwatch = Stopwatch.StartNew();
                        var ret = ProcessCodeFile(fileName, inputPath, excisionMode, excisionLanguage, ref stats);
                        stopwatch.Stop();

                        if (stats.CharactersExcised > 0)
                        {
                            System.Diagnostics.Debug.Assert(stats.TotalNrCharacters > 0, "Something is terribly wrong. We have excised characters, but no total characters..?");
                            var excisionRatio = (float)stats.CharactersExcised / (float)stats.TotalNrCharacters * 100.0f;
                            Console.WriteLine($"[{fileIdx + 1}/{allFiles.Length}] Excised {excisionRatio:0.00}% of server only code (took {stopwatch.Elapsed.TotalMilliseconds:0.0}ms): {fileName}");
                        }
                        else
                        {
                            Console.WriteLine($"[{fileIdx + 1}/{allFiles.Length}] No action required (took {stopwatch.Elapsed.TotalMilliseconds:0.0}ms): {fileName}");
                        }

                        globalStats.CharactersExcised += stats.CharactersExcised;
                        globalStats.TotalNrCharacters += stats.TotalNrCharacters;
                    }
                    catch (Exception)
                    {
                        Console.Error.WriteLine($"[{fileIdx + 1}/{allFiles.Length}] Failed to parse: {fileName}");
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

        private EExciserReturnValues ProcessCodeFile(string fileName, string inputPath, EExcisionMode excisionMode, IServerCodeExcisionLanguage excisionLanguage, ref ExcisionStats stats)
        {
            var relativePath = Path.GetRelativePath(inputPath, fileName);
            var script = File.ReadAllText(fileName);
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
                var nodes = PreprocessorParser.Parse(commonTokenStream);
                var detectedPreprocessorServerOnlyScopes = FindScopesForSymbol(nodes, excisionLanguage.ServerScopeStartString);

                // Process scopes we've evaluated must be server only.
                foreach (ServerOnlyScopeData currentScope in visitor.DetectedServerOnlyScopes)
                {
                    if (currentScope.Span.StartIndex == -1 || currentScope.Span.EndIndex == -1)
                    {
                        continue;
                    }

                    int ScanToStartOfLine(int index)
                    {
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

                    int TrimWhitespace2(int index)
                    {
                        while (index > 0)
                        {
                            switch (script[index - 1])
                            {
                                case ' ':
                                case '\t':
                                    break;
                                default:
                                    return index;
                            }

                            index--;
                        }
                        return index;
                    }

                    int ScanToNextLineBreak(int index)
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

                    const bool markers = false;

                    if (markers)
                    {
                        serverCodeInjections.Add(new KeyValuePair<int, string>(currentScope.Span.StartIndex + 1, "<<!!>>"));
                    }

                    /*var startIndex = script[currentScope.Span.StartIndex] switch
                    {
                        '{' => ScanToNextLineBreak(currentScope.Span.StartIndex + 1),
                        ';' => ScanToNextLineBreak(currentScope.Span.StartIndex + 1),
                        ')' => ScanToNextLineBreak(currentScope.Span.StartIndex + 1),
                        _ => currentScope.Span.StartIndex,
                    };*/

                    int startIndex = currentScope.Span.StartIndex + 1;

                    var endIndex = script[currentScope.Span.EndIndex] switch
                    {
                        '}' => ScanToStartOfLine(currentScope.Span.EndIndex),
                        ';' => ScanToNextLineBreak(currentScope.Span.EndIndex),
                        _ => currentScope.Span.EndIndex,
                    };

                    // Skip if there's already a server-code exclusion for the scope. (We don't want have duplicate guards.)
                    var (StartIndex, StopIndex) = TrimWhitespace(script, startIndex, endIndex);
                    if (detectedPreprocessorServerOnlyScopes.Any(x => StartIndex >= x.Span.StartIndex && StopIndex <= x.Span.EndIndex))
                    {
                        continue; // We're inside an existing scope.
                    }

                    //var startIndex = ScanToNextLineBreak(currentScope.Span.StartIndex + 1);
                    var startText = $"{excisionLanguage.ServerScopeStartString}\r\n";
                    if (startText.StartsWith("#"))
                    {
                        startIndex = ScanToNextLineBreak(currentScope.Span.StartIndex + 1);
                    }

                    var builder = new StringBuilder();
                    builder.Append(markers ? "<START>" : "");
                    builder.Append(startText);
                    builder.Append(markers ? "</START>" : "");
                    serverCodeInjections.Add(new KeyValuePair<int, string>(startIndex, builder.ToString()));

                    /*if (!string.IsNullOrEmpty(currentScope.Opt_ElseContent))
                    {
                        serverCodeInjections.Add(new KeyValuePair<int, string>(currentScope.Opt_ElseIndex, currentScope.Opt_ElseContent));
                    }*/


                    //string elseText = "";

                    //var endIndex = ScanToStartOfLine(currentScope.Span.EndIndex);
                    //var endIndex = TrimWhitespace2(currentScope.Span.EndIndex + 1);
                    //string endText = $"<ELSE>{currentScope.Opt_ElseContent}</ELSE>{excisionLanguage.ServerScopeEndString}<END>\r\n";// excisionLanguage.ServerScopeEndString;
                    //endText += "\r\n" + new string('\t', currentScope.Span.End.Column);

                    //serverCodeInjections.Add(new KeyValuePair<int, string>(endIndex, endText));

                    var endText = $"{excisionLanguage.ServerScopeEndString}\r\n";
                    if (endText.StartsWith("#"))
                    {
                        endIndex = ScanToStartOfLine(endIndex);
                    }

                    builder = new StringBuilder();

                    if (!string.IsNullOrEmpty(currentScope.Opt_ElseContent))
                    {
                        builder.Append(markers ? "<ELSE>" : "");
                        builder.Append(currentScope.Opt_ElseContent);
                        builder.Append(markers ? "</ELSE>" : "");
                    }

                    builder.Append(markers ? "<END>" : "");
                    builder.Append(endText);
                    builder.Append(markers ? "</END>" : "");

                    serverCodeInjections.Add(new KeyValuePair<int, string>(endIndex, builder.ToString()));

                    stats.CharactersExcised += currentScope.Span.EndIndex - currentScope.Span.StartIndex;
                }

                // Next we must add dummy reference variables if they exist.
                foreach (KeyValuePair<int, HashSet<string>> dummyRefDataPair in visitor.ClassStartIdxDummyReferenceData)
                {
                    var dummyRefDataBlockString = new StringBuilder();
                    var dummyVarScope = "#ifndef " + excisionLanguage.ServerPrecompilerSymbol;
                    dummyRefDataBlockString.Append(dummyVarScope);
                    foreach (var dummyVarDef in dummyRefDataPair.Value)
                    {
                        dummyRefDataBlockString.Append("\r\n\t" + dummyVarDef);
                    }

                    dummyRefDataBlockString.Append("\r\n" + excisionLanguage.ServerScopeEndString + "\r\n\r\n");

                    // If there is already a block of dummy reference variables we skip adding new ones, there is no guarantee we are adding the right code.
                    if (InjectedMacroAlreadyExistsAtLocation(script, dummyRefDataPair.Key, false, true, dummyVarScope + "\r\n"))
                    {
                        continue;
                    }

                    serverCodeInjections.Add(new KeyValuePair<int, string>(dummyRefDataPair.Key, dummyRefDataBlockString.ToString()));
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
                var outputPath = (!string.IsNullOrEmpty(_parameters.OutputPath)) ? Path.Combine(_parameters.OutputPath, relativePath) : fileName;
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

                        File.WriteAllText(outputPath, answerText.ToString());
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

        /// <summary>
        /// Resize a scope range by excluding whitespace characters.
        /// </summary>
        private static (int StartIndex, int StopIndex) TrimWhitespace(string script, int startIndex, int stopIndex)
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

        public static List<PreprocessorNode> FindScopesForSymbol(List<PreprocessorNode> scopes, string symbol)
        {
            var result = new List<PreprocessorNode>();
            FindScopesForSymbolRecursive(scopes, symbol, result);
            return result;
        }

        private static void FindScopesForSymbolRecursive(List<PreprocessorNode> scopes, string symbol, List<PreprocessorNode> result)
        {
            foreach (var scope in scopes)
            {
                if (scope.Directive.Contains(symbol, StringComparison.Ordinal))
                {
                    result.Add(scope);
                }
                FindScopesForSymbolRecursive(scope.Children, symbol, result);
            }
        }


        private bool InjectedMacroAlreadyExistsAtLocation(string script, int index, bool lookAhead, bool ignoreWhitespace, string macro)
        {
            if (lookAhead)
            {
                if (ignoreWhitespace)
                {
                    while (index < script.Length && IsWhitespace(script[index]))
                    {
                        index++;
                    }
                }

                if (script.Length - index < macro.Length)
                {
                    return false;
                }

                return script[index..(index + macro.Length)].Equals(macro);
            }
            else
            {
                if (ignoreWhitespace)
                {
                    while (index > 0 && IsWhitespace(script[index]))
                    {
                        index--;
                    }
                }

                if (index - macro.Length < 0)
                {
                    return false;
                }

                return script[(index - macro.Length)..index].Equals(macro);
            }
        }
    }
}
