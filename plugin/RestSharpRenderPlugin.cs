using System;
using System.Linq;
using System.Collections.Generic;
using Core.Plugin;
using Nebula.Compiler.Objects;
using Nebula.Compiler.Abstracts;
using System.Text.RegularExpressions;

public class RestSharpRenderPlugin : IRenderPlugin
{
    public List<string> RenderClientImports()
    {
        return new List<string> {
            "RestSharp",
            "RestSharp.Authenticators"
        };
    }

    private List<string> RenderUrlSegment(string url, List<string> args)
    {
        var output = new List<string>();
        // look in the URL for {variable} strings and then try and find a matching function argument
        // if we find it, generate the appropriate request.AddUrlSegment call
        // for any argument that is not part of the URL, send that as a parameter
        var regex = new Regex(@"({[a-z]+})", RegexOptions.IgnoreCase);
        var matches = regex.Matches(url);
        var usedArgs = new List<string>();
        foreach (Match m in matches)
        {
            var parameterName = m.Value.Replace("{", "").Replace("}", "");
            var matchingArg = args.Where(a => a == parameterName).FirstOrDefault() 
                ?? throw new Exception("No matching argument for URL parameter: " + parameterName);
            
            usedArgs.Add(matchingArg);
            output.Add($"request.AddUrlSegment(\"{parameterName}\", {matchingArg});");
        }

        var unusedArgs = args.Where(a => !usedArgs.Contains(a));
        output.AddRange(unusedArgs.Select(arg => $"request.AddParameter(\"{arg}\", {arg});"));

        return output;
    }

    public List<string> RenderAbstractFunction(string url, string prefix, string returnType, string method, List<string> args)
    {
        var output = new List<string>();
        output.Add("// this came from plugin");
        output.Add($"var request = new RestRequest(\"{prefix}{url}\", Method.{method});");
        output.AddRange(RenderUrlSegment(url, args));
        output.Add($"var response = Client.Execute<{returnType}>(request);");
        output.Add("return response.Data;");

        return output;
    }
}
