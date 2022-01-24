<!-- Allow this file to not have a first line heading -->
<!-- markdownlint-disable-file MD041 -->

<!-- inline html -->
<!-- markdownlint-disable-file MD033 -->

# :u5272: Server Code Exciser

<br/><br/>

<!--- FIXME: Write short catchy description/tagline of project --->
An antl4r based program that can automatically remove server only code from Unreal projects using Angelscript (https://angelscript.hazelight.se/)

Thanks to Tom van Dijck for initiating this project and suggesting to use Antl4r as the base!

# Setup
I would strongly recommend using VSCode as that will give you the visual antl4r debugger. You need these extensions:
* ANTLR4 grammar syntax support
* C#

Then, you should just have to open the _Grammar/AngelscriptParser.g4_ file and save it once to generate all the antl4r stuff.

# Launch Settings
Since the launch targets include local paths, I don't think it makes sense to have these checked in in project scope, so I'll just put some launch config templates here instead! <3
You will have to put your own arguments in there of course :)
These configs will excise a full script folder and visually debug one AS file, respectively!

{
	// Use IntelliSense to learn about possible attributes.
	// Hover to view descriptions of existing attributes.
	// For more information, visit: https://go.microsoft.com/fwlink/?linkid=830387
	"version": "0.2.0",
	"configurations": [
		{
			"name": "Excise Server Angelscript",
			"type": "coreclr",
			"request": "launch",
			"preLaunchTask": "build",
			"program": "${workspaceFolder}/ServerCodeExciser/bin/Debug/net5.0/ServerCodeExciser.exe",
			"args": ["C:/Projects/MyProject/Script"],
			"cwd": "${workspaceFolder}/ServerCodeExciser",
			"console": "internalConsole",
			"stopAtEntry": false
		},
		{
			"name": "Debug Angelscript Grammar For Input",
			"type": "antlr-debug",
			"request": "launch",
			"preLaunchTask": "build",
			"grammar": "${workspaceFolder}/UnrealAngelscriptParser/Grammar/UnrealAngelscriptParser.g4",
			"input": "C:/Projects/MyProject/Script/Foo.as",
			"printParseTree": true,
			"visualParseTree": true
		}
	]
}

I usually try running the full parser target to see which files are breaking. Then I point the grammar target to that file and run it. After you see the errors, you can then start removing code from the file until only the thing that causes the error is left, and then start tweaking the grammar to fix it! Happy antlering!

[![Embark](https://img.shields.io/badge/embark-open%20source-blueviolet.svg)](https://embark.dev)

## Contribution

[![Contributor Covenant](https://img.shields.io/badge/contributor%20covenant-v1.4-ff69b4.svg)](../main/CODE_OF_CONDUCT.md)

We welcome community contributions to this project.

Please read our [Contributor Guide](CONTRIBUTING.md) for more information on how to get started.
Please also read our [Contributor Terms](CONTRIBUTING.md#contributor-terms) before you make any contributions.

Any contribution intentionally submitted for inclusion in an Embark Studios project, shall comply with the Rust standard licensing model (MIT OR Apache 2.0) and therefore be dual licensed as described below, without any additional terms or conditions:

### License

This contribution is dual licensed under EITHER OF

* Apache License, Version 2.0, ([LICENSE-APACHE](LICENSE-APACHE) or <http://www.apache.org/licenses/LICENSE-2.0>)
* MIT license ([LICENSE-MIT](LICENSE-MIT) or <http://opensource.org/licenses/MIT>)

at your option.

For clarity, "your" refers to Embark or any other licensee/user of the contribution.
