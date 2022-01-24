using ServerCodeExcisionCommon;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace UnrealAngelscriptServerCodeExcision
{
	public class UnrealAngelscriptSymbolVisitor : UnrealAngelscriptSimpleVisitor
	{
		private IServerCodeExcisionLanguage _language;

		public UnrealAngelscriptSymbolVisitor(string script, IServerCodeExcisionLanguage language)
			: base(script)
		{
			_language = language;
		}

		public override UnrealAngelscriptNode VisitFunctionDefinition(UnrealAngelscriptParser.FunctionDefinitionContext context)
		{
			if (context.ChildCount > 2)
			{
				bool isMemberServerOnlyFunction = false;

				// Check to see for UFUNCTION Server meta.
				var maybeUfunctionSpec = context.GetChild(0) as UnrealAngelscriptParser.UfunctionContext;
				if (maybeUfunctionSpec != null)
				{
					// Check all annotations for server only
					for (int childIdx = 0; childIdx < maybeUfunctionSpec.ChildCount; childIdx++)
					{
						var annotationList = maybeUfunctionSpec.GetChild(childIdx) as UnrealAngelscriptParser.AnnotationListContext;
						if (annotationList != null)
						{
							for (int listChildIdx = 0; listChildIdx < annotationList.ChildCount; listChildIdx++)
							{
								var annotation = annotationList.GetChild(listChildIdx) as UnrealAngelscriptParser.AnnotationContext;
								if (annotation != null && annotation.GetText().ToLower() == "server")
								{
									isMemberServerOnlyFunction = true;
									break;
								}
							}
						}
					}
				}
				
				// Check to see if function name ends with _Server
				for (int childIdx = 0; childIdx < context.ChildCount; childIdx++)
				{
					var functionDeclarator = context.GetChild(childIdx) as UnrealAngelscriptParser.DeclaratorContext;
					if (functionDeclarator != null && functionDeclarator.ChildCount > 0)
					{
						var name = functionDeclarator.GetChild(0);
						if (name != null && name.GetText().ToLower().EndsWith("_server"))
						{
							isMemberServerOnlyFunction = true;
							break;
						}
					}
				}

				if (isMemberServerOnlyFunction)
				{
					var functionBody = context.GetChild(context.ChildCount - 1) as UnrealAngelscriptParser.FunctionBodyContext;
					DecorateFunctionBody(functionBody);

					// Don't visit further children.
					return null;
				}
			}

			return VisitChildren(context);
		}

		public override UnrealAngelscriptNode VisitSelectionStatement(UnrealAngelscriptParser.SelectionStatementContext context)
		{
			// In selection statements, we want to find the compound statement at the end of us and inject macros there.

			var conditionExpression = context.GetChild(2) as UnrealAngelscriptParser.ConditionContext;
			switch (IsExpressionServerOnly(conditionExpression))
			{
				case EExpressionType.ServerOnly:
				{
					// Index 4 is the control scope for ifs
					AddDetectedScopeFromSelectionChild(context, 4);
					break;
				}

				case EExpressionType.EverythingAfterBranchIsServerOnly:
				{
					// We want to inject right after the if-branch child, and continue all the way to the end of the parent scope.
					AddParentScopePostIfScope(context);
					break;
				}

				case EExpressionType.ElseIsServerOnly:
				{
					// Index 5 is the control scope for elses
					AddDetectedScopeFromSelectionChild(context, 6);
					break;
				}

				default:
					break;
			}

			return VisitChildren(context);
		}

		// Add injections to scopes either in if or else scopes.
		private void AddDetectedScopeFromSelectionChild(UnrealAngelscriptParser.SelectionStatementContext context, int childIdx)
		{
			var selectionScope = context.GetChild(childIdx)?.GetChild(0) as UnrealAngelscriptParser.CompoundStatementContext;
			if (selectionScope != null)
			{
				if (selectionScope.SourceInterval.Length > 2)
				{
					// If there is a return statement at the end, we must replace it with a suitable replacement, or code will stop compiling.
					// We want to move in one step, since our reference scope is the lower one.
					var returnData = GetDefaultReturnStatementForScope(selectionScope);

					ServerOnlyScopeData newData = new ServerOnlyScopeData(
					ExcisionUtils.FindScriptIndexForCodePoint(Script, selectionScope.Start.Line, selectionScope.Start.Column) + 1,
					ExcisionUtils.FindScriptIndexForCodePoint(Script, selectionScope.Stop.Line, 0));

					if (returnData.ReturnType != EReturnType.NoReturn)
					{
						string scopeIndentation = BuildIndentationForColumnCount(selectionScope.Start.Column + 1);
						newData.Opt_ElseContent = string.Format("#else\r\n{0}{1}\r\n", scopeIndentation, returnData.DefaultReturnString);
					}

					if (returnData.ReturnType != EReturnType.RootScopeReferenceReturn)
					{
						DetectedServerOnlyScopes.Add(newData);
					}
				}
			}
			else
			{
				// Perhaps it's a one-liner..?
				var oneLineScope = context.GetChild(childIdx) as UnrealAngelscriptParser.StatementContext;
				if (oneLineScope != null)
				{
					ServerOnlyScopeData newData = new ServerOnlyScopeData(
						MoveOneLine(ExcisionUtils.FindScriptIndexForCodePoint(Script, oneLineScope.Start.Line, 0), false),
						MoveOneLine(ExcisionUtils.FindScriptIndexForCodePoint(Script, oneLineScope.Stop.Line, oneLineScope.Stop.Column) + 1, true));

					// If there is a return statement at the end, we must replace it with a suitable replacement, or code will stop compiling.
					// For one-liners, we actually remove the entire scope, which means we must replace it completely.
					string elseScopeContent = "";
					var returnData = GetDefaultReturnStatementForScope(oneLineScope);
					if (returnData.ReturnType != EReturnType.NoReturn)
					{
						// If the one liner is a return value, we must provide a replacement return statement as well.
						string elseScopeIndentation = BuildIndentationForColumnCount(oneLineScope.Start.Column);
						elseScopeContent = string.Format("\r\n{0}{1}", elseScopeIndentation, returnData.DefaultReturnString);
					}

					// Since this was a one-liner, scope indentation should be one level lower than the one-liner
					string scopeIndentation = BuildIndentationForColumnCount(oneLineScope.Start.Column - 1);
					newData.Opt_ElseContent = string.Format("#else\r\n{0}{{{1}\r\n{0}}}\r\n", scopeIndentation, elseScopeContent);

					if (returnData.ReturnType != EReturnType.RootScopeReferenceReturn)
					{
						DetectedServerOnlyScopes.Add(newData);
					}
				}
			}
		}

		// Add injections after an if scope, until the end of the selection statement's parent scope.
		private void AddParentScopePostIfScope(UnrealAngelscriptParser.SelectionStatementContext context)
		{
			var ifScope = context.GetChild(4);
			if (ifScope == null)
			{
				return;
			}

			int ifScopeStopLine = -1;
			int ifScopeStopColumn = -1;
			var selectionScope = ifScope.GetChild(0) as UnrealAngelscriptParser.CompoundStatementContext;
			if (selectionScope != null)
			{
				ifScopeStopLine = selectionScope.Stop.Line;
				ifScopeStopColumn = selectionScope.Stop.Column;
			}
			else
			{
				// Perhaps it's a one-liner..?
				var oneLineScope = ifScope as UnrealAngelscriptParser.StatementContext;
				if (oneLineScope != null)
				{
					ifScopeStopLine = oneLineScope.Stop.Line;
					ifScopeStopColumn = oneLineScope.Stop.Column;
				}
			}

			var parentScope = ExcisionUtils.FindParentContextOfType<UnrealAngelscriptParser.CompoundStatementContext>(context);
			if (parentScope != null && ifScopeStopLine > 0 && ifScopeStopColumn > 0)
			{
				ServerOnlyScopeData newData = new ServerOnlyScopeData(
					ExcisionUtils.FindScriptIndexForCodePoint(Script, ifScopeStopLine, ifScopeStopColumn) + 1,
					ExcisionUtils.FindScriptIndexForCodePoint(Script, parentScope.Stop.Line, 0));

				// If there is a return statement at the end, we must replace it with a suitable replacement, or code will stop compiling.
				var returnData = GetDefaultReturnStatementForScope(parentScope);
				if (returnData.ReturnType != EReturnType.NoReturn)
				{
					string scopeIndentation = BuildIndentationForColumnCount(returnData.ReturnStatementRun.StartColumn);
					newData.Opt_ElseContent = string.Format("#else\r\n{0}{1}\r\n", scopeIndentation, returnData.DefaultReturnString);
				}

				if (returnData.ReturnType != EReturnType.RootScopeReferenceReturn)
				{
					DetectedServerOnlyScopes.Add(newData);
				}
			}
		}

		private int MoveOneLine(int curScriptIdx, bool forward)
		{
			int lineTerminatorsFound = 0;
			int testTerm = forward ? 0 : -1;
			while (curScriptIdx >= 0 && curScriptIdx < Script.Length && lineTerminatorsFound < 2)
			{
				if (Script[curScriptIdx + testTerm] == '\r' || Script[curScriptIdx + testTerm] == '\n')
				{
					lineTerminatorsFound++;
				}

				if (forward)
				{
					curScriptIdx++;
				}
				else
				{
					curScriptIdx--;
				}
			}

			return curScriptIdx;
		}

		public override UnrealAngelscriptNode VisitPostfixExpression(UnrealAngelscriptParser.PostfixExpressionContext context)
		{
			// In assert statements, we want to find the compound statement we ourselves belong to, and inject macros after ourselves.
			var maybeAssertSpecifier = context.GetChild(0) as UnrealAngelscriptParser.AssertSpecifierContext;
			if (maybeAssertSpecifier != null)
			{
				var assertExpression = context.GetChild(2) as UnrealAngelscriptParser.ExpressionListContext;
				if (IsExpressionServerOnly(assertExpression) == EExpressionType.ServerOnly)
				{
					// Finding the simple declaration here lets us find the end of the assert line.
					var simpleDeclaration = ExcisionUtils.FindDirectParentContextOfTypeWithDifferentSourceInterval<UnrealAngelscriptParser.SimpleDeclarationContext>(context, context.SourceInterval);
					var parentScope = ExcisionUtils.FindParentContextOfType<UnrealAngelscriptParser.CompoundStatementContext>(context);
					if (simpleDeclaration != null && parentScope != null)
					{
						// If there is a return statement at the end, we must replace it with a suitable replacement, or code will stop compiling.
						var returnData = GetDefaultReturnStatementForScope(parentScope);

						ServerOnlyScopeData newData = new ServerOnlyScopeData(
							ExcisionUtils.FindScriptIndexForCodePoint(Script, simpleDeclaration.Stop.Line, simpleDeclaration.Stop.Column) + 1,
							ExcisionUtils.FindScriptIndexForCodePoint(Script, parentScope.Stop.Line, 0));

						if (returnData.ReturnType != EReturnType.NoReturn)
						{
							string scopeIndentation = BuildIndentationForColumnCount(simpleDeclaration.Start.Column);
							newData.Opt_ElseContent = string.Format("#else\r\n{0}{1}\r\n", scopeIndentation, returnData.DefaultReturnString);
						}

						if (returnData.ReturnType != EReturnType.RootScopeReferenceReturn)
						{
							DetectedServerOnlyScopes.Add(newData);
						}
					}
				}
			}

			return VisitChildren(context);
		}

		private EExpressionType IsExpressionServerOnly(Antlr4.Runtime.Tree.IParseTree expressionTree)
		{
			if (expressionTree == null)
			{
				return EExpressionType.NotServerOnly;
			}

			// First, we drill down into the expression, looking for the server only symbol.
			var serverOnlyExpression = FindServerOnlySymbolTree(expressionTree);
			if (serverOnlyExpression == null)
			{
				return EExpressionType.NotServerOnly;
			}

			// Next, we need to walk the expression upward until we hit the statement it belongs to. As we do this, we take note of boolean operators along the way, and mutate our path accordingly.
			var conjunctivesInExpressionPath = new List<bool>();
			var isServerIsNegated = false;
			var isExpressionSimple = true;
			var currentExpression = serverOnlyExpression;
			while (currentExpression != null && currentExpression != expressionTree)
			{
				if (currentExpression is UnrealAngelscriptParser.LogicalOrExpressionContext && currentExpression.ChildCount > 1)
				{
					// We must check child count to make sure we are actually in a boolean expression, and not just an empty base.
					// This was a disjunctive, so add false to the path.
					conjunctivesInExpressionPath.Add(false);
					isExpressionSimple = false;
				}
				else if (currentExpression is UnrealAngelscriptParser.LogicalAndExpressionContext && currentExpression.ChildCount > 1)
				{
					// This was a conjunctive, so add true to the path.
					conjunctivesInExpressionPath.Add(true);
					isExpressionSimple = false;
				}
				else if (IsExpressionNegative(currentExpression))
				{
					// This was a negation, invert all entries in our path. Also invert the seed (the )
					isServerIsNegated = !isServerIsNegated;
					for (int pathIdx = 0; pathIdx < conjunctivesInExpressionPath.Count; pathIdx++)
					{
						conjunctivesInExpressionPath[pathIdx] = !conjunctivesInExpressionPath[pathIdx];
					}
				}

				currentExpression = currentExpression.Parent;
			}

			// After our path has been fully constructed, we now need to make sure that all the operators all the way up to the root expression are conjunctions. If they are, then this expression is server only.
			bool isExpressionConjunctive = true;
			foreach (bool pathEntry in conjunctivesInExpressionPath)
			{
				if (!pathEntry)
				{
					isExpressionConjunctive = false;
					break;
				}
			}

			if (isExpressionConjunctive)
			{
				if (isServerIsNegated)
				{
					// If we have a simple branch expression where IsServer is explicitly negated and conjunctive, then we should either guard the else scope, or everything after the if scope (if we can get away with it)
					var selectionContext = expressionTree.Parent as UnrealAngelscriptParser.SelectionStatementContext;
					if (isExpressionSimple && selectionContext != null && selectionContext.ChildCount > 4)
					{
						var dummyReturnStatement = new StatementRun();
						return IsLastStatementInScopeAReturn(selectionContext.GetChild(4), ref dummyReturnStatement) 
										? EExpressionType.EverythingAfterBranchIsServerOnly 
										: EExpressionType.ElseIsServerOnly;
					}
				}
				else
				{
					return EExpressionType.ServerOnly;	
				}
			}

			return EExpressionType.NotServerOnly;
		}

		private bool IsExpressionNegative(Antlr4.Runtime.Tree.IParseTree expression)
		{
			if (expression is UnrealAngelscriptParser.UnaryExpressionContext && expression.ChildCount > 1 && expression.GetText().StartsWith("!"))
			{
				return true;
			}

			if (expression is UnrealAngelscriptParser.EqualityExpressionContext && expression.ChildCount > 2)
			{
				return expression.GetChild(0).GetText() == "false" || expression.GetChild(2).GetText() == "false";
			}

			return false;
		}

		private Antlr4.Runtime.Tree.IParseTree FindServerOnlySymbolTree(Antlr4.Runtime.Tree.IParseTree currentTree)
		{
			for (int childIdx = 0; childIdx < currentTree.ChildCount; childIdx++)
			{
				var childTree = currentTree.GetChild(childIdx);
				var childTreeContent = childTree.GetText().Trim();

				if (MatchesAnyRegex(childTreeContent, _language.ServerOnlySymbolRegexes))
				{
					return childTree;
				}

				var maybeChild = FindServerOnlySymbolTree(childTree);
				if(maybeChild != null)
				{
					return maybeChild;
				}
			}

			return null;
		}

		private bool MatchesAnyRegex(string expression, List<string> Regexes)
		{				
			foreach (var serverOnlySymbolRegex in Regexes)
			{
				if (Regex.IsMatch(expression, serverOnlySymbolRegex))
				{
					return true;
				}
			}

			return false;
		}
	}
}
