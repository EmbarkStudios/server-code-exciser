using System;
using System.IO;
using UnrealAngelscriptServerCodeExcision;
using ServerCodeExcisionCommon;

namespace ServerCodeExcision
{
	public class ServerCodeExciser
	{
		public static void PrintHelp()
		{
			Console.WriteLine(
@"Usage:
ServerCodeExciser.exe {InputPath} [Switches]

Required arguments:
{InputPath}:				This is the path to read input files from. For example the absolute path to your game's Script/ folder.

Switches:
-output, -o:				Expects the next argument to be the path we will write out output to. If no output folder is specified, excision will occur in-place.
-exciseallfunctions, -eaf:	If this switch is specified, the next argument should be a string containing regex entries. 
							If any of these regexes matches the relative path of any file to be processed, all functions in the file will be fully excised.
							You can specify more than one entry by separating them with three pipes, eg:
							Characters/Paul/.*|||Weapons/Rife.as
-fullyexcise, -fe:			If this switch is specified, the next argument should be a string containing regex entries. 
							If any of these regexes matches the relative path of any file to be processed, the entire file will be excised.
							You can specify more than one entry by separating them with three pipes, eg:
							Characters/Paul/.*|||Weapons/Rife.as
-forcelang, -fl:			Disables language autodetection and forces a language to be used. Expects the next argument to be a valid language ID. Valid ID's are: { as }.
-dontskip, -ds:				Don't ever skip any files, even if they don't contain any server symbols.
-minratio, -mr:				Specify a ratio in percent as the next argument. If the excised % of code is less than this ratio, an error will be thrown. Good to detect catastrophic changes in excision performance.
-funcstats, -fs:			Outputs function stats instead of file stats. This is more accurate, but a lot slower, since it has to parse every file.
-unchanged, -u:				If the system should output unchanged files. Default is no.
-dryrun, -d:				Processes everything, but does not write any output to disk.
-help, -h:					Display this help text.
");
		}

		public static int Main(string[] args)
		{
			if (args.Length < 1)
			{
				Console.Error.WriteLine("You must provide an input path to read input files from as the first argument.");
				return (int)EExciserReturnValues.BadInputPath;
			}

			var parameters = new ServerCodeExcisionParameters(args[0]);
			Console.WriteLine("Set input path to: " + parameters.InputPath);
			if (!Directory.Exists(parameters.InputPath))
			{
				Console.Error.WriteLine("Input directory does not exist.");
				return (int)EExciserReturnValues.BadInputPath;
			}

			for (int argIdx = 1; argIdx < args.Length; argIdx++)
			{
				string formattedArgument = args[argIdx].ToLowerInvariant();
				switch (formattedArgument)
				{
					case "-output":
					case "-o":
					{
						int outputPathIdx = argIdx + 1;
						if(outputPathIdx > 1 && outputPathIdx < args.Length)
						{
							parameters.OutputPath = args[outputPathIdx];
							Console.WriteLine("Set output path to: " + parameters.OutputPath);
						}
						else
						{
							Console.Error.WriteLine("Could not parse output path argument! You must enter a path after the switch!");
							return (int)EExciserReturnValues.BadOutputPath;
						}

						argIdx++;
						break;
					}

					case "-exciseallfunctions":
					case "-eaf":
					{
						int aePathIdx = argIdx + 1;
						if(aePathIdx > 1 && aePathIdx < args.Length)
						{
							parameters.ExciseAllFunctionsRegexString = args[aePathIdx];
							Console.WriteLine("Set excise all functions string to: " + parameters.ExciseAllFunctionsRegexString);
						}
						else
						{
							Console.Error.WriteLine("Could not parse function excise string argument! You must enter a valid string after the switch!");
							return (int)EExciserReturnValues.BadArgument;
						}

						argIdx++;
						break;
					}

					case "-fullyexcise":
					case "-fe":
					{
						int fePathIdx = argIdx + 1;
						if(fePathIdx > 1 && fePathIdx < args.Length)
						{
							parameters.FullExcisionRegexString = args[fePathIdx];
							Console.WriteLine("Set full excise string to: " + parameters.FullExcisionRegexString);
						}
						else
						{
							Console.Error.WriteLine("Could not parse full excise string argument! You must enter a string after the switch!");
							return (int)EExciserReturnValues.BadArgument;
						}

						argIdx++;
						break;
					}

					case "-forcelang":
					case "-fl":
					{
						int flPathIdx = argIdx + 1;
						if (flPathIdx > 1 && flPathIdx < args.Length)
						{
							switch (args[flPathIdx].ToLower().Trim())
							{
								case "as":
								{
									parameters.ExcisionLanguage = EExcisionLanguage.Angelscript;
									break;
								}

								default:
								{
									Console.Error.WriteLine("Could not parse force language string argument! You must enter a valid language ID after the switch!");
									return (int)EExciserReturnValues.BadArgument;
								}
							}

							Console.WriteLine("Set forced excision language to: " + parameters.ExcisionLanguage);
						}
						else
						{
							Console.Error.WriteLine("Could not parse full excise string argument! You must enter a string after the switch!");
							return (int)EExciserReturnValues.BadArgument;
						}

						argIdx++;
						break;
					}

					case "-dontskip":
					case "-ds":
					{
						parameters.DontSkip = true;
						break;
					}

					case "-minratio":
					case "-mr":
					{
						int ratioPathIdx = argIdx + 1;
						if(ratioPathIdx > 1 && ratioPathIdx < args.Length && float.TryParse(args[ratioPathIdx], out parameters.RequiredExcisionRatio))
						{
							Console.WriteLine("Set full excise string to: " + parameters.FullExcisionRegexString);
						}
						else
						{
							Console.Error.WriteLine("Could not parse required ratio argument! You must enter a valid ratio float after the switch!");
							return (int)EExciserReturnValues.BadArgument;
						}

						argIdx++;
						break;
					}

					case "-funcstats":
					case "-fs":
					{
						parameters.UseFunctionStats = true;
						break;
					}

					case "-unchanged":
					case "-u":
					{
						parameters.ShouldOutputUntouchedFiles = true;
						break;
					}

					case "-dryrun":
					case "-d":
					{
						parameters.IsDryRun = true;
						break;
					}

					case "-help":
					case "-h":
					default:
					{
						PrintHelp();
						break;
					}
				}
			}

			// Make sure to clear the output.
			if (Directory.Exists(parameters.OutputPath))
			{
				Directory.Delete(parameters.OutputPath, true);
			}

			if (parameters.ExcisionLanguage == EExcisionLanguage.Unknown)
			{
				// Try to autodetect the language
				foreach (var filePath in Directory.EnumerateFiles(parameters.InputPath, "*.*", SearchOption.AllDirectories))
				{
					if (filePath.EndsWith(".as"))
					{
						// Found an AS file, let's assume AS.
						parameters.ExcisionLanguage = EExcisionLanguage.Angelscript;
						break;
					}
				}
			}

			try
			{
				switch (parameters.ExcisionLanguage)
				{
					case EExcisionLanguage.Angelscript:
					{
						var angelscriptServerCodeExciser = new ServerCodeExcisionProcessor(parameters);
						return (int)angelscriptServerCodeExciser.ExciseServerCode("*.as", new UnrealAngelscriptServerCodeExcisionLanguage());
					}

					default:
					{
						Console.Error.WriteLine("ExcisionLanguage could not be parsed, and no language was forced. Aborting...");
						return (int)EExciserReturnValues.UnknownExcisionLanguage;
					}
				}
			}
			catch (Exception e)
			{
				Console.Error.WriteLine("Internal error during excision: " + e.ToString());
				return (int)EExciserReturnValues.InternalExcisionError;
			}
		}
	}
}
