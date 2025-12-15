using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServerCodeExcisionCommon;
using UnrealAngelscriptServerCodeExcision;

namespace ServerCodeExciser.Tests
{
    [TestClass]
    public class ExcisionProcessorTests
    {
        [TestMethod]
        public void OneLineBranchElseOneLineTest()
        {
            var script = @"class UOneLineBranchElseOneLineTest
{
	void Test()
	{
		int SomethingBefore = 0;
		SomethingBefore++;

		if(System::IsServer())
			SomethingBefore++;
		else
			SomethingBefore++;

		if(!System::IsServer())
			SomethingBefore++;
		else
			SomethingBefore++;

		int ButNotThis = 0;
		ButNotThis++;
	}
};
";

            var expected = @"class UOneLineBranchElseOneLineTest
{
	void Test()
	{
		int SomethingBefore = 0;
		SomethingBefore++;

		if(System::IsServer())
#ifdef WITH_SERVER
			SomethingBefore++;
#else
		{
		}
#endif // WITH_SERVER
		else
			SomethingBefore++;

		if(!System::IsServer())
			SomethingBefore++;
		else
#ifdef WITH_SERVER
			SomethingBefore++;
#else
		{
		}
#endif // WITH_SERVER

		int ButNotThis = 0;
		ButNotThis++;
	}
};
";

            var processor = new ServerCodeExcisionProcessor(new ServerCodeExcisionParameters
            {
                StrictMode = true,
                DontSkip = true,
            });
            var stats = new ExcisionStats();

            using (new FileSystem().CreateDisposableFile(out IFileInfo outputFile))
            {
                var result = processor.ProcessCodeFile(script, outputFile.FullName, EExcisionMode.ServerOnlyScopes, new UnrealAngelscriptServerCodeExcisionLanguage(), ref stats);
                Assert.AreEqual(EExciserReturnValues.Success, result);

                string actual = outputFile.ReadAllText();
                Console.WriteLine($"-- Expected --\n{expected}");
                Console.WriteLine($"-- Actual --\n{actual}");
                Assert.AreEqual(expected, actual);
            }
        }
    }
}
