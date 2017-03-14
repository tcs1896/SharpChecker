using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SharpChecker.ASTUtilities;

namespace SharpChecker
{
    class SCBaseSyntaxWalker : CSharpSyntaxWalker
    {
        private SyntaxNode docRoot;
        //private CompilationAnalysisContext context;
        private SemanticModel semanticModel;
        private SyntaxAnnotation synAnn;
        private SyntaxTree tree;

        public SCBaseSyntaxWalker(SyntaxTree tree, SemanticModel semanticModel, SyntaxAnnotation synAnn)
        {
            this.docRoot = tree.GetRoot();
            this.tree = tree;
            this.semanticModel = semanticModel;
            this.synAnn = synAnn;
        }

        public SyntaxTree GetTree()
        {
            return tree.WithRootAndOptions(docRoot, new CSharpParseOptions());
        }

        public override void Visit(SyntaxNode node)
        {
            switch (node.Kind())
            {
                case SyntaxKind.InvocationExpression:
                    var invocationExpr = node as InvocationExpressionSyntax;
                    AnalyzeInvocationExpr(invocationExpr);
                    break;
                //case SyntaxKind.SimpleAssignmentExpression:
                //    var assignmentExpression = context.Node as AssignmentExpressionSyntax;
                //    var assnExprAttrs = AnalyzeAssignmentExpression(context, assignmentExpression);
                //    List<List<String>> asmtAttrs = new List<List<string>>();
                //    asmtAttrs.Add(assnExprAttrs.Item2);
                //    return new Tuple<AttributeType, List<List<string>>>(assnExprAttrs.Item1, asmtAttrs);
                default:
                    break;// return new Tuple<AttributeType, List<List<string>>>(AttributeType.NotImplemented, null);
            }
            base.Visit(node);
        }

        // static void Main(string[] args)
        // {
        //     var tree = CSharpSyntaxTree.ParseText(@"
        // public class MyClass
        // {
        //     public void MyMethod()
        //     {
        //     }
        //     public void MyMethod(int n)
        //     {
        //     }
        //");

        //     var walker = new CustomWalker();
        //     walker.Visit(tree.GetRoot());
        // }

        /// <summary>
        /// Get the annotated types of the formal parameters of a method
        /// </summary>
        /// <param name="context"></param>
        /// <param name="invocationExpr"></param>
        /// <returns></returns>
        private Tuple<AttributeType, List<List<string>>> AnalyzeInvocationExpr(InvocationExpressionSyntax invocationExpr)
        {
            var identifierNameExpr = invocationExpr.Expression as IdentifierNameSyntax;
            if (identifierNameExpr == null)
            {
                return new Tuple<AttributeType, List<List<string>>>(AttributeType.NotImplemented, null);
            }

            //This will lookup the method associated with the invocation expression
            var memberSymbol = semanticModel.GetSymbolInfo(identifierNameExpr).Symbol as IMethodSymbol;
            //If we failed to lookup the symbol then bail
            if (memberSymbol == null)
            {
                return new Tuple<AttributeType, List<List<string>>>(AttributeType.NotImplemented, null);
            }

            //In addition to the symbols we need the argument syntax so that we may hang annotations on our AST
            var argList = invocationExpr.ArgumentList.Arguments;

            //Check to see if any of the formal parameters of the method being invoked have associated attributes
            //In order to do this, we need to lookup the appropriate method signature.  
            ImmutableArray<IParameterSymbol> paramSymbols = memberSymbol.Parameters;
            //Create the lists of lists which will hold the attributes associated with each of the attributes in the attribute list
            List<List<string>> attrListParams = new List<List<string>>();
            bool hasAttrs = false;

            //Iterate over the parameters with an explicit index so we can compare the appropriate argument below
            for (int i = 0; i < paramSymbols.Count(); i++)
            {
                //Create a new list to hold the attributes
                List<string> paramAttrs = new List<string>();

                //Get the formal parameter
                var param = paramSymbols[i];
                //Get the attributes associated with this parameter
                var attributes = param.GetAttributes();

                for (int j = 0; j < attributes.Count(); j++)
                {
                    var attr = attributes[j];

                    var oldArg = argList[i];

                    var newArg = oldArg.WithAdditionalAnnotations(synAnn);
                    docRoot = docRoot?.ReplaceNode(oldArg, newArg);

                    //invocationExpr = invocationExpr.ReplaceNode(oldArg, newArg);

                    //var paramSyntax = param.DeclaringSyntaxReferences;
                    //if(paramSyntax.Count() != 1)
                    //{
                    //    //There are either 0 or multiple declaring symbols.  If we hit this
                    //    //we may need to get a little more granular.

                    //}
                    //else
                    //{
                    //    var oldParam = paramSyntax.First();
                    //    var newParam = oldParam

                    //    root = root.ReplaceNode(oldUsing, newUsing);

                    //}

                    paramAttrs.Add(attr.AttributeClass.ToString());
                    hasAttrs = true;
                }

                attrListParams.Add(paramAttrs);
            }

            //If we didn't find any annotations then we return the appropriate enum value indicating as much
            if (hasAttrs)
            {
                return new Tuple<AttributeType, List<List<string>>>(AttributeType.HasAnnotation, attrListParams);
            }
            else
            {
                return new Tuple<AttributeType, List<List<string>>>(AttributeType.NoAnnotation, null);
            }
        }
    }
}
