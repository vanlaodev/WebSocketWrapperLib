using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CSharp;

namespace WebSocketWrapperLib
{
    public static class GenericContractGenerator
    {
        private const string Prefix = @"using System;
using WebSocketWrapperLib;

namespace {ns}
{
    public class {classNm} : {interfaceNm}
    {
        private readonly Func<GenericContractGenerator.InvocationInfo, object> _wrapped;

        public {classNm}(Func<GenericContractGenerator.InvocationInfo, object> wrapped)
        {
            _wrapped = wrapped;
        }
";

        private const string Suffix = @"    }
}";

        private const string MethodTemplate = @"        public {returnType} {methodName}({methodParams})
        {
            var info = new GenericContractGenerator.InvocationInfo();
            info.Contract = ""{interfaceNm}"";
            info.Method = ""{methodName}"";
            {methodSetParamsSection}
            var result = _wrapped(info);
            {methodReturnStatement}
        }";

        private const string MethodSetParamStatementTemplate =
            @"            info.Parameters.Add({paramVal});";

        private const string MethodReturnStatementTemplate = @"return ({returnType})Convert.ChangeType(result,typeof({returnType}));";

        public static T Generate<T>(Func<InvocationInfo, object> callback)
        {
            var contractType = typeof(T);
            if (contractType.IsInterface)
            {
                var ns = "X" + Math.Abs(Guid.NewGuid().GetHashCode());
                var classNm = "Generated" + contractType.Name;
                var prefix = Prefix.Replace("{ns}", ns).Replace("{classNm}", classNm).Replace("{interfaceNm}", contractType.FullName);
                var methods = string.Join(Environment.NewLine + Environment.NewLine,
                    contractType.GetMethods().Select(m =>
                    {
                        var methodParams = string.Join(",",
                            m.GetParameters().Select(p => string.Format("{0} {1}", p.ParameterType.FullName, p.Name)));
                        var methodSetParamsSection = string.Join(Environment.NewLine,
                            m.GetParameters()
                                .Select(
                                    p =>
                                        MethodSetParamStatementTemplate.Replace("{paramVal}", p.Name)));
                        var methodReturnStatement = typeof(void) == m.ReturnType
                            ? ""
                            : MethodReturnStatementTemplate.Replace("{returnType}", m.ReturnType.FullName);
                        return MethodTemplate.Replace("{returnType}", m.ReturnType.FullName)
                            .Replace("{methodName}", m.Name)
                            .Replace("{methodParams}", methodParams)
                            .Replace("{interfaceNm}", contractType.FullName)
                            .Replace("{methodSetParamsSection}", methodSetParamsSection)
                            .Replace("{methodReturnStatement}", methodReturnStatement)
                            .Replace("System.Void", "void");
                    }));
                var code = prefix + methods + Suffix;
                var provider = new CSharpCodeProvider();
                var cp = new CompilerParameters();
                cp.ReferencedAssemblies.Add(contractType.Assembly.Location);
                cp.ReferencedAssemblies.Add(typeof(GenericContractGenerator).Assembly.Location);
                cp.TreatWarningsAsErrors = false;
                cp.GenerateInMemory = true;
                var cr = provider.CompileAssemblyFromSource(cp, code);
                if (cr.Errors.HasErrors)
                {
                    throw new Exception("Failed to generate generic contract: " + string.Join(Environment.NewLine, cr.Errors.Cast<CompilerError>().Select(x => x.ErrorText)));
                }
                return (T)Activator.CreateInstance(cr.CompiledAssembly.GetType(ns + "." + classNm), callback);
            }
            throw new Exception("Failed to generate generic contract.");
        }

        public class InvocationInfo
        {
            public string Contract { get; set; }
            public string Method { get; set; }
            public List<object> Parameters { get; set; }

            public InvocationInfo()
            {
                Parameters = new List<object>();
            }
        }
    }
}
