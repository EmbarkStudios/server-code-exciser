using ServerCodeExcisionCommon;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using UnrealAngelscriptServerCodeExcision;

namespace ServerCodeExcision
{
    internal sealed class ServerCodeExciserCommand : Command<ServerCodeExciserCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandOption("-o|--output <PATH>")]
            [Description("The path we will write out output to. If no output folder is specified, excision will occur in-place.")]
            public string? OutputPath { get; init; }

            [CommandOption("-a|--exciseallfunctions <REGEX>")]
            [Description("If this switch is specified, the next argument should be a string containing regex entries." +
                "If any of these regexes matches the relative path of any file to be processed, all functions in the file will be fully excised. " +
                "You can specify more than one entry by separating them with three pipes, eg: " +
                "Characters/Paul/.*|||Weapons/Rife.as")]
            public string? ExciseAllFunctionsRegexString { get; init; }

            [CommandOption("-f|--fullyexcise <REGEX>")]
            [Description("If this switch is specified, the next argument should be a string containing regex entries." +
                            "If any of these regexes matches the relative path of any file to be processed, the entire file will be excised." +
                            "You can specify more than one entry by separating them with three pipes, eg: " +
                            "Characters /Paul/.*|||Weapons/Rife.as")]
            public string? FullExcisionRegexString { get; init; }

            [CommandOption("-s|--dontskip")]
            [Description("Don't ever skip any files, even if they don't contain any server symbols.")]
            public bool DontSkip { get; init; }

            [CommandOption("-m|--minratio")]
            [Description("Specify a ratio in percent as the next argument. If the excised % of code is less than this ratio, an error will be thrown. Good to detect catastrophic changes in excision performance.")]
            public int? RequiredExcisionRatio { get; init; } = -1;

            [CommandOption("-t|--funcstats")]
            [Description("Outputs function stats instead of file stats. This is more accurate, but a lot slower, since it has to parse every file.")]
            public bool UseFunctionStats { get; init; }

            [CommandOption("-u|--unchanged")]
            [Description("If the system should output unchanged files. Default is no.")]
            public bool ShouldOutputUntouchedFiles { get; init; }

            [CommandOption("-d|--dryrun")]
            [Description("Processes everything, but does not write any output to disk.")]
            public bool IsDryRun { get; init; }

            [CommandOption("-v|--verify")]
            [Description("Verify that all analyzed code does not require modifications to excise server scopes.")]
            public bool Verify { get; init; }

            [CommandOption("--strict")]
            [Description("Ensure that all files can be analyzed without syntactic or lexicographical errors.")]
            public bool StrictMode { get; init; }

            [CommandArgument(0, "<INPUT_PATH>")]
            [Description("The input folder to excise.")]
            public string? InputPath { get; init; }
        }

        class RootPaths
        {
            [JsonPropertyName("AngelscriptScriptRoots")]
            public string[] AngelscriptScriptRoots { get;set;} = Array.Empty<string>();
        }

        public override int Execute([NotNull] CommandContext context, [NotNull] Settings settings)
        {
            var parameters = new ServerCodeExcisionParameters();
            parameters.OutputPath = settings.OutputPath ?? string.Empty;
            parameters.ExciseAllFunctionsRegexString = settings.ExciseAllFunctionsRegexString ?? string.Empty;
            parameters.FullExcisionRegexString = settings.FullExcisionRegexString ?? string.Empty;
            parameters.ShouldOutputUntouchedFiles = settings.ShouldOutputUntouchedFiles;
            parameters.IsDryRun = settings.IsDryRun || settings.ShouldOutputUntouchedFiles || settings.Verify;
            parameters.Verify = settings.Verify;
            parameters.StrictMode = settings.StrictMode;
            parameters.UseFunctionStats = settings.UseFunctionStats;
            parameters.DontSkip = settings.DontSkip;
            if (settings.RequiredExcisionRatio.HasValue)
            {
                parameters.RequiredExcisionRatio = settings.RequiredExcisionRatio.Value / 100.0f;
            }

            if (File.Exists(settings.InputPath))
            {
                var desc = File.ReadAllText(settings.InputPath);
                var paths = JsonSerializer.Deserialize<RootPaths>(desc);
                if (paths != null)
                { 
                    parameters.InputPaths.UnionWith(paths.AngelscriptScriptRoots);
                }
                else
                {
                    AnsiConsole.WriteLine("Invalid json provided.");
                    return (int)EExciserReturnValues.InternalExcisionError;
                }
            }
            else if (Directory.Exists(settings.InputPath))
            {
                parameters.InputPaths.Add(settings.InputPath);
            }
            else
            {
                AnsiConsole.WriteLine("Input directory does not exist.");
                return (int)EExciserReturnValues.BadInputPath;
            }

            foreach (var path in parameters.InputPaths)
            {
                AnsiConsole.WriteLine("Input path: " + path);
            }

            // Make sure to clear the output.
            if (Directory.Exists(parameters.OutputPath))
            {
                Directory.Delete(parameters.OutputPath, true);
            }

            try
            {
                var angelscriptServerCodeExciser = new ServerCodeExcisionProcessor(parameters);
                return (int)angelscriptServerCodeExciser.ExciseServerCode("*.as", new UnrealAngelscriptServerCodeExcisionLanguage());
            }
            catch (Exception e)
            {
                AnsiConsole.WriteException(e);
                return (int)EExciserReturnValues.InternalExcisionError;
            }
        }
    }


    public class ServerCodeExciser
    {
        public static int Main(string[] args)
        {
            return new CommandApp<ServerCodeExciserCommand>().Run(args);
        }
    }
}
