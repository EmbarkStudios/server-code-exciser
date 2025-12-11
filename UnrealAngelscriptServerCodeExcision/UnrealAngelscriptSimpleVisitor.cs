using System;
using System.Collections.Generic;
using System.Text;
using ServerCodeExcisionCommon;

namespace UnrealAngelscriptServerCodeExcision
{
    public class UnrealAngelscriptNode
    {
    }

    public class UnrealAngelscriptSimpleVisitor : UnrealAngelscriptParserBaseVisitor<UnrealAngelscriptNode>, IServerCodeVisitor
    {
        public List<ServerOnlyScopeData> DetectedServerOnlyScopes { get; protected set; }
        public Dictionary<int, HashSet<string>> ClassStartIdxDummyReferenceData { get; protected set; }
        public int TotalNumberOfFunctionCharactersVisited { get; protected set; }

        protected string Script;

        private static int Salt = 0;

        public UnrealAngelscriptSimpleVisitor(string script)
        {
            ClassStartIdxDummyReferenceData = new Dictionary<int, HashSet<string>>();
            DetectedServerOnlyScopes = new List<ServerOnlyScopeData>();

            TotalNumberOfFunctionCharactersVisited = 0;
            Script = script;
        }

        public void VisitContext(Antlr4.Runtime.Tree.IParseTree context)
        {
            Visit(context);
        }

        public override UnrealAngelscriptNode VisitFunctionBody(UnrealAngelscriptParser.FunctionBodyContext context)
        {
            var functionStartIndex = ExcisionUtils.FindScriptIndexForCodePoint(Script, new SourcePosition(context.Start.Line, context.Start.Column)) + 1;
            var functionEndIndex = ExcisionUtils.FindScriptIndexForCodePoint(Script, new SourcePosition(context.Stop.Line, context.Stop.Column)) + 1;
            TotalNumberOfFunctionCharactersVisited += Math.Abs(functionEndIndex - functionStartIndex);

            return VisitChildren(context);
        }

        protected ReturnData GetDefaultReturnStatementForScope(Antlr4.Runtime.Tree.IParseTree scopeContext)
        {
            // If the function has a return type, we must provide a valid replacement in the case the original one is compiled out.
            var returnData = new ReturnData();

            // First figure out the function's return type.
            var functionDefinition = ExcisionUtils.FindParentContextOfType<UnrealAngelscriptParser.FunctionDefinitionContext>(scopeContext);
            if (functionDefinition != null && functionDefinition.ChildCount > 1)
            {
                var returnTypeContext = ExcisionUtils.FindFirstDirectChildOfType<UnrealAngelscriptParser.DeclSpecifierSeqContext>(functionDefinition);

                // Now figure out if and what we should replace the return with.
                returnData.ReturnType = GetDefaultReturnStatementForReturnType(returnTypeContext, out returnData.DefaultReturnString);

                if (returnData.ReturnType != EReturnType.NoReturn)
                {
                    // Okay, we have a return type. We should check for a final return statement, and gather info about it.
                    var functionBody = functionDefinition.GetChild(functionDefinition.ChildCount - 1) as UnrealAngelscriptParser.CompoundStatementContext;
                    if (!IsLastStatementInScopeAReturn(scopeContext, ref returnData.ReturnStatementRun)
                        && functionBody == scopeContext)
                    {
                        // It seems our function has a return value, but doesn't end with a return statement.
                        // This must mean that all the return statements are in branches of the expression, and we should add our return definition at the end.

                        returnData.ReturnStatementRun.StartLine = functionBody.Stop.Line;
                        returnData.ReturnStatementRun.StartColumn = functionBody.Stop.Column;
                        returnData.ReturnStatementRun.StopLine = functionBody.Stop.Line;
                        returnData.ReturnStatementRun.StopColumn = functionBody.Stop.Column;
                    }
                }
            }

            return returnData;
        }

        protected bool IsLastStatementInScopeAReturn(Antlr4.Runtime.Tree.IParseTree scopeContext, ref StatementRun returnStatementRun)
        {
            if (scopeContext == null)
            {
                return false;
            }

            var jumpContext = scopeContext as UnrealAngelscriptParser.JumpStatementContext;
            if (jumpContext != null && jumpContext.GetChild(0).GetText() == "return")
            {
                returnStatementRun.StartLine = jumpContext.Start.Line;
                returnStatementRun.StartColumn = jumpContext.Start.Column;
                returnStatementRun.StopLine = jumpContext.Stop.Line;
                returnStatementRun.StopColumn = jumpContext.Stop.Column;
                return true;
            }

            var compoundStatementContext = scopeContext as UnrealAngelscriptParser.CompoundStatementContext;
            if (compoundStatementContext != null)
            {
                return IsLastStatementInScopeAReturn(compoundStatementContext.GetChild(scopeContext.ChildCount - 2), ref returnStatementRun);
            }

            if (scopeContext.ChildCount < 1)
            {
                return false;
            }

            var nextChild = scopeContext.GetChild(scopeContext.ChildCount - 1);
            if (nextChild is UnrealAngelscriptParser.SelectionStatementContext)
            {
                // Disallow entering further branches.
                return false;
            }

            return IsLastStatementInScopeAReturn(nextChild, ref returnStatementRun);
        }

        protected EReturnType GetDefaultReturnStatementForReturnType(UnrealAngelscriptParser.DeclSpecifierSeqContext returnTypeContext, out string defaultReturnStatement)
        {
            defaultReturnStatement = "";

            if (returnTypeContext == null || returnTypeContext.GetText() == "void")
            {
                // Void return types means we don't have to do anything.
                return EReturnType.NoReturn;
            }

            // First, we need to figure out the type text. This is the full type without qualifiers.
            string typeString = "";
            bool returnTypeFound = false;

            var asGenericContext = GetFirstChildOfType<UnrealAngelscriptParser.AsGenericContext>(returnTypeContext);
            var simpleTypeContext = GetFirstChildOfType<UnrealAngelscriptParser.SimpleTypeSpecifierContext>(returnTypeContext);
            if (asGenericContext != null)
            {
                // It is some type of generic, we should probably just try to construct it.
                typeString = asGenericContext.GetText().Replace("const", "const ");
                defaultReturnStatement = string.Format("return {0}();", typeString);
                returnTypeFound = true;
            }
            else if (simpleTypeContext != null)
            {
                // Could this simple type even be a class type..?
                var classTypeContext = GetFirstChildOfType<UnrealAngelscriptParser.ClassNameContext>(simpleTypeContext, true);
                if (classTypeContext != null)
                {
                    if (classTypeContext.GetText().StartsWith("F"))
                    {
                        // Struct. Construct in place.
                        typeString = simpleTypeContext.GetText();
                        defaultReturnStatement = string.Format("return {0}();", typeString);
                    }
                    else if (classTypeContext.GetText().StartsWith("E"))
                    {
                        // Enum. Force cast.
                        typeString = simpleTypeContext.GetText();
                        defaultReturnStatement = string.Format("return {0}(0);", typeString);
                    }
                    else
                    {
                        // Class type, return null
                        defaultReturnStatement = "return nullptr;";
                    }
                }
                else
                {
                    // No, it was just a simple type, we'll use default values!
                    switch (simpleTypeContext.GetText())
                    {
                        case "bool":
                            defaultReturnStatement = "return false;";
                            break;
                        case "float":
                        case "float32":
                            defaultReturnStatement = "return 0.0f;";
                            break;
                        case "double":
                        case "float64":
                            defaultReturnStatement = "return 0.0;";
                            break;
                        default:
                            // All kinds of different ints :)
                            defaultReturnStatement = "return 0;";
                            break;
                    }
                }

                returnTypeFound = true;
            }

            // If the return type is some type of reference, we cannot replace it with a stack variable.
            // This means we need to create a new dummy variable, and we also need to return a reference to it instead of something else.
            if (returnTypeFound && returnTypeContext.ChildCount > 0 &&
                (returnTypeContext.GetChild(returnTypeContext.ChildCount - 1).GetText() == "&"))
            {
                string dummyReferenceVariableName = typeString.Replace("::", "").Replace(",", "").Replace("<", "").Replace(">", "") + "ReferenceDummy" + Salt++;
                string dummyReferenceVariable = string.Format("{0} {1};", typeString, dummyReferenceVariableName);

                // For reference variables, we must register dummy variables to make sure something with longer lifetime scope can be returned.
                // I'd really like to just use a static variable here, but since AS doesn't support that, things get messy.
                int classStartIdx = -1;
                var classSpecifierContext = ExcisionUtils.FindParentContextOfType<UnrealAngelscriptParser.ClassSpecifierContext>(returnTypeContext);
                if (classSpecifierContext != null && classSpecifierContext.ChildCount > 2)
                {
                    var classMemberSpec = classSpecifierContext.GetChild(2) as UnrealAngelscriptParser.MemberSpecificationContext;
                    if (classMemberSpec != null)
                    {
                        classStartIdx = ExcisionUtils.FindScriptIndexForCodePoint(Script, new SourcePosition(classMemberSpec.Start.Line, 0));
                    }
                }

                if (classStartIdx < 0)
                {
                    // Reference return detected in non-class. Just leave this alone for now.
                    return EReturnType.RootScopeReferenceReturn;
                }

                HashSet<string> dummyVarSet = null;
                if (ClassStartIdxDummyReferenceData.ContainsKey(classStartIdx))
                {
                    dummyVarSet = ClassStartIdxDummyReferenceData[classStartIdx];
                }
                else
                {
                    dummyVarSet = new HashSet<string>();
                }

                dummyVarSet.Add(dummyReferenceVariable);
                ClassStartIdxDummyReferenceData[classStartIdx] = dummyVarSet;

                defaultReturnStatement = string.Format("return {0};", dummyReferenceVariableName);
                return EReturnType.ReferenceReturn;
            }

            return returnTypeFound ? EReturnType.ReplacedReturn : EReturnType.NoReturn;
        }

        protected Antlr4.Runtime.Tree.IParseTree GetFirstChildOfType<T>(Antlr4.Runtime.Tree.IParseTree specifierSequence, bool searchReverse = false)
            where T : class
        {
            if ((specifierSequence as T) != null)
            {
                return specifierSequence;
            }

            if (searchReverse)
            {
                for (int childIdx = specifierSequence.ChildCount - 1; childIdx >= 0; childIdx--)
                {
                    var childResult = GetFirstChildOfType<T>(specifierSequence.GetChild(childIdx), searchReverse);
                    if (childResult != null)
                    {
                        return childResult;
                    }
                }
            }
            else
            {
                for (int childIdx = 0; childIdx < specifierSequence.ChildCount; childIdx++)
                {
                    var childResult = GetFirstChildOfType<T>(specifierSequence.GetChild(childIdx), searchReverse);
                    if (childResult != null)
                    {
                        return childResult;
                    }
                }
            }

            return null;
        }

        protected string BuildIndentationForColumnCount(int nrCols)
        {
            var indentation = new StringBuilder("");
            for (int indentIdx = 0; indentIdx < nrCols; indentIdx++)
            {
                indentation.Append("\t");
            }

            return indentation.ToString();
        }

        protected void DecorateFunctionBody(UnrealAngelscriptParser.FunctionBodyContext context)
        {
            if (context == null)
            {
                return;
            }

            // Don't detect scopes that are just one line.
            if (context.Start.Line == context.Stop.Line)
            {
                return;
            }

            // If there is a return statement at the end, we must replace it with a suitable replacement, or code will stop compiling.
            var returnData = GetDefaultReturnStatementForScope(context);

            ServerOnlyScopeData newData = new ServerOnlyScopeData(
                context.Start.StopIndex + 1,
                ExcisionUtils.FindScriptIndexForCodePoint(Script, new SourcePosition(context.Stop.Line, 0)));

            if (returnData.ReturnType != EReturnType.NoReturn)
            {
                // We want to be one step inside the scope!
                string scopeIndentation = BuildIndentationForColumnCount(context.Start.Column + 1);
                newData.Opt_ElseContent = string.Format("#else\r\n{0}{1}\r\n", scopeIndentation, returnData.DefaultReturnString);
            }

            if (returnData.ReturnType != EReturnType.RootScopeReferenceReturn)
            {
                DetectedServerOnlyScopes.Add(newData);
            }
        }
    }
}
