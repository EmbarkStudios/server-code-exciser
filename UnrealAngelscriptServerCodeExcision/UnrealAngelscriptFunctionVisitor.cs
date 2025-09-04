namespace UnrealAngelscriptServerCodeExcision
{
    public class UnrealAngelscriptFunctionVisitor : UnrealAngelscriptSimpleVisitor
    {
        public UnrealAngelscriptFunctionVisitor(string script)
            : base(script)
        {
        }

        public override UnrealAngelscriptNode VisitFunctionBody(UnrealAngelscriptParser.FunctionBodyContext context)
        {
            // We want to decorate all function bodies!
            DecorateFunctionBody(context);
            return base.VisitFunctionBody(context);
        }
    }
}
