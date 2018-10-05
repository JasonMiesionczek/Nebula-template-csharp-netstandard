using System;
using System.Linq;
using System.Collections.Generic;
using Core.Plugin;
using Nebula.Compiler.Objects;
using Nebula.Compiler.Abstracts;
using Nebula.Models;
using System.Text.RegularExpressions;

public class RestSharpCompilerPlugin : ICompilerPlugin
{
    public List<RootObject> GetTopOfClassExtra(ApiConfig config)
    {
        return new List<RootObject> { BuildAuthenticator(config) };
    }

    public List<GenericProperty> GetProperties()
    {
        return new List<GenericProperty>() {
            new GenericProperty("Client", "RestClient")
        };
    }

    public GenericConstructor GetConstructor(string className, ApiConfig config)
    {
        var args = new List<GenericVariableDefinition>();
        var body = new List<string>();
        body.Add($"Client = new RestClient(\"{config.Host}\");");
        switch (config.AuthMethod)
        {
            case AuthenticationMethod.BasicHttp:
                args.Add(new GenericVariableDefinition("username", "string"));
                args.Add(new GenericVariableDefinition("password", "string"));
                body.Add("Client.Authenticator = new HttpBasicAuthenticator(username, password);");
                break;
            case AuthenticationMethod.CustomHeader:
                args.Add(new GenericVariableDefinition("authValue", "string"));
                body.Add("Client.Authenticator = new Authenticator { CustomHeader = authenticationValue };");
                break;
            case AuthenticationMethod.JwtBearer:
            case AuthenticationMethod.OAuthToken:
                args.Add(new GenericVariableDefinition("token", "string"));
                body.Add("Client.Authenticator = new Authenticator { AccessToken = token };");
                break;
        }
        
        return new GenericConstructor
        {
            Arguments = args,
            Name = $"{className}Client",
            Body = body
        };
    }

    private GenericClass BuildAuthenticator(ApiConfig config)
    {
        var props = new List<GenericProperty>();
        var body = new List<string>();

        Action<string> prepareAuthorizationBody = (authMethod) => {
            props.Add(new GenericProperty {
                    Name = "AccessToken", 
                    DataTypeString = "string"
                });
            body.AddRange(new [] {
                $"request.AddHeader(\"Authorization\", $\"{authMethod} {{AccessToken}}\"" 
            });
        };

        switch (config.AuthMethod)
        {
            case AuthenticationMethod.BasicHttp:
            case AuthenticationMethod.NoAuthentication:
                return null;
            case AuthenticationMethod.CustomHeader:
                props.Add(new GenericProperty {
                    Name = "CustomHeader",
                    DataTypeString = "string"
                });
                body.AddRange(new [] {
                    $"request.AddHeader(\"{config.CustomHeaderKey}\", $\"{{CustomHeader}}\"" 
                });
                break;
            case AuthenticationMethod.JwtBearer:
                prepareAuthorizationBody("bearer");
                break;
            case AuthenticationMethod.OAuthToken:
                prepareAuthorizationBody("token");
                break;
        }

        var funcs = new List<GenericFunction>
        {
            new GenericFunction
            {
                Name = "Authenticate",
                ReturnType = "void",
                Arguments = new List<GenericVariableDefinition>
                {
                    new GenericVariableDefinition("client", "IRestClient"),
                    new GenericVariableDefinition("request", "IRestRequest")
                },
                Body = body
            }
        };
        
        return new GenericClass
        {
            AccessModifier = Visibility.Private,
            Name = "Authenticator",
            Inheritence = new List<GenericClass> {
                new GenericClass { Name = "IAuthenticator"}
            },
            Properties = props,
            Functions = funcs
        };
    }
}