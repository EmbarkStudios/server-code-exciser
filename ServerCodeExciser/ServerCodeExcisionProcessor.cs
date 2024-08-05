using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Antlr4.Runtime;
using ServerCodeExcisionCommon;

namespace ServerCodeExcision
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
        private ServerCodeExcisionParameters _parameters;
        private List<Regex> _functionExciseRegexes;
        private List<Regex> _fullyExciseRegexes;

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
                            var excisionRatio = (float)stats.CharactersExcised / (float)stats.TotalNrCharacters * 100.0f;
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
                        Console.WriteLine("Failed to parse ({0}/{1}): {2}", fileIdx + 1, allFiles.Length, fileName);
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
                var totalExcisionRatio = (float)globalStats.CharactersExcised / (float)globalStats.TotalNrCharacters * 100.0f;
                Console.WriteLine("----------------------------");
                Console.WriteLine("Excised {0:0.00}% ({1}/{2} characters) of server only code from the script files.",
                            totalExcisionRatio, globalStats.CharactersExcised, globalStats.TotalNrCharacters);

                var timeTaken = endTime - startTime;
                Console.WriteLine("Excision took {0:0} hours, {1:0} minutes and {2:0.0} seconds.\n\n", timeTaken.Hours, timeTaken.Minutes, timeTaken.Seconds);

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
            List<KeyValuePair<int, string>> serverCodeInjections = new List<KeyValuePair<int, string>>();
            var inputStream = new AntlrInputStream(script);
            var lexer = excisionLanguage.CreateLexer(inputStream);
            lexer.AddErrorListener(new ExcisionLexerErrorListener());
            var commonTokenStream = new CommonTokenStream(lexer);
            var parser = excisionLanguage.CreateParser(commonTokenStream);
            var answerText = new StringBuilder();
            answerText.Append(script);

            IServerCodeVisitor? visitor = null;
            if (excisionMode == EExcisionMode.Full)
            {
                // We want to excise this entire file.
                serverCodeInjections.Add(new KeyValuePair<int, string>(0, excisionLanguage.ServerScopeStartString + "\r\n"));
                serverCodeInjections.Add(new KeyValuePair<int, string>(script.Length, excisionLanguage.ServerScopeEndString));
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
                visitor.VisitContext(parser.GetParseTree());
                if (_parameters.UseFunctionStats)
                {
                    stats.TotalNrCharacters = visitor.TotalNumberOfFunctionCharactersVisited;
                }

                // First process all server only scopes.
                foreach (ServerOnlyScopeData currentScope in visitor.DetectedServerOnlyScopes)
                {
                    if (currentScope.StartIndex == -1
                        || currentScope.StopIndex == -1
                        || InjectedMacroAlreadyExistsAtLocation(answerText, currentScope.StartIndex, true, excisionLanguage.ServerScopeStartString)
                        || InjectedMacroAlreadyExistsAtLocation(answerText, currentScope.StartIndex, false, excisionLanguage.ServerScopeStartString)
                        || InjectedMacroAlreadyExistsAtLocation(answerText, currentScope.StopIndex, false, excisionLanguage.ServerScopeEndString))
                    {
                        continue;
                    }

                    // If there are already injected macros where we want to go, we should skip injecting.
                    System.Diagnostics.Debug.Assert(currentScope.StopIndex > currentScope.StartIndex, "There must be some invalid pattern here! Stop is before start!");
                    serverCodeInjections.Add(new KeyValuePair<int, string>(currentScope.StartIndex, excisionLanguage.ServerScopeStartString));
                    serverCodeInjections.Add(new KeyValuePair<int, string>(currentScope.StopIndex, currentScope.Opt_ElseContent + excisionLanguage.ServerScopeEndString));
                    stats.CharactersExcised += currentScope.StopIndex - currentScope.StartIndex;
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

                    dummyRefDataBlockString.Append("\r\n" + excisionLanguage.ServerScopeEndString + "\r\n");

                    // If there is already a block of dummy reference variables we skip adding new ones, there is no guarantee we are adding the right code.
                    if (InjectedMacroAlreadyExistsAtLocation(answerText, dummyRefDataPair.Key, false, dummyVarScope + "\r\n"))
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
                }
            }

            return stats;
        }

        private bool InjectedMacroAlreadyExistsAtLocation(StringBuilder script, int index, bool lookAhead, string macro)
        {
            int startIndex = lookAhead ? index : (index - macro.Length);
            int endIndex = lookAhead ? (index + macro.Length) : index;

            if (startIndex < 0 || startIndex >= script.Length
                || endIndex < 0 || endIndex >= script.Length)
            {
                return false;
            }

            string scriptSection = script.ToString(startIndex, macro.Length);
            return scriptSection == macro;
        }
    }
}
