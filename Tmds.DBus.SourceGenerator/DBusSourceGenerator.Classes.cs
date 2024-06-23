using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;


namespace Tmds.DBus.SourceGenerator
{
    public partial class DBusSourceGenerator
    {
        private static MethodDeclarationSyntax MakeWriteNullableStringMethod() =>
            MethodDeclaration(
                    PredefinedType(Token(SyntaxKind.VoidKeyword)),
                    "WriteNullableString")
                .AddModifiers(
                    Token(SyntaxKind.PublicKeyword),
                    Token(SyntaxKind.StaticKeyword))
                .AddParameterListParameters(
                    Parameter(
                            Identifier("writer"))
                        .WithType(
                            IdentifierName("MessageWriter"))
                        .AddModifiers(
                            Token(SyntaxKind.ThisKeyword),
                            Token(SyntaxKind.RefKeyword)),
                    Parameter(
                            Identifier("value"))
                        .WithType(
                            NullableType(
                                PredefinedType(Token(SyntaxKind.StringKeyword)))))
                .WithExpressionBody(
                    ArrowExpressionClause(
                        InvocationExpression(
                        MakeMemberAccessExpression("writer", "WriteString"))
                            .AddArgumentListArguments(
                                Argument(
                                    BinaryExpression(
                                        SyntaxKind.CoalesceExpression,
                                        IdentifierName("value"),
                                        MakeMemberAccessExpression("string", "Empty"))))))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

        private static MethodDeclarationSyntax MakeWriteObjectPathSafeMethod() =>
            MethodDeclaration(
                    PredefinedType(Token(SyntaxKind.VoidKeyword)),
                    "WriteObjectPathSafe")
                .AddModifiers(
                    Token(SyntaxKind.PublicKeyword),
                    Token(SyntaxKind.StaticKeyword))
                .AddParameterListParameters(
                    Parameter(
                            Identifier("writer"))
                        .WithType(
                            IdentifierName("MessageWriter"))
                        .AddModifiers(
                            Token(SyntaxKind.ThisKeyword),
                            Token(SyntaxKind.RefKeyword)),
                    Parameter(
                            Identifier("value"))
                        .WithType(
                            IdentifierName("ObjectPath")))
                .WithExpressionBody(
                    ArrowExpressionClause(
                        InvocationExpression(
                                MakeMemberAccessExpression("writer", "WriteObjectPath"))
                            .AddArgumentListArguments(
                                Argument(
                                    InvocationExpression(
                                        MakeMemberAccessExpression("value", "ToString"))))))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

        private const string SignalHelperClass = """
                                                  using System;
                                                  using System.Threading.Tasks;
                                                  
                                                  using Tmds.DBus.Protocol;

                                                  // <auto-generated/>
                                                  #pragma warning disable
                                                  #nullable enable
                                                  namespace Tmds.DBus.SourceGenerator
                                                  {
                                                      internal static class SignalHelper
                                                      {
                                                          public static ValueTask<IDisposable> WatchSignalAsync(Connection connection, MatchRule rule, Action<Exception?> handler, bool emitOnCapturedContext = true, ObserverFlags flags = ObserverFlags.None)
                                                              => connection.AddMatchAsync(rule, static (_, _) => null !, static (Exception? e, object _, object? _, object? handlerState) => ((Action<Exception?>)handlerState!).Invoke(e), null, handler, emitOnCapturedContext, flags);
                                                  
                                                          public static ValueTask<IDisposable> WatchSignalAsync<T>(Connection connection, MatchRule rule, MessageValueReader<T> reader, Action<Exception?, T> handler, bool emitOnCapturedContext = true, ObserverFlags flags = ObserverFlags.None)
                                                              => connection.AddMatchAsync(rule, reader, static (e, arg, _, handlerState) => ((Action<Exception?, T>)handlerState!).Invoke(e, arg), null, handler, emitOnCapturedContext, flags);
                                                  
                                                          public static ValueTask<IDisposable> WatchPropertiesChangedAsync<T>(Connection connection, string destination, string path, string @interface, MessageValueReader<PropertyChanges<T>> reader, Action<Exception?, PropertyChanges<T>> handler, bool emitOnCapturedContext = true, ObserverFlags flags = ObserverFlags.None)
                                                          {
                                                              MatchRule rule = new()
                                                              {
                                                                  Type = MessageType.Signal,
                                                                  Sender = destination,
                                                                  Path = path,
                                                                  Member = "PropertiesChanged",
                                                                  Interface = "org.freedesktop.DBus.Properties",
                                                                  Arg0 = @interface
                                                              };
                                                  
                                                              return WatchSignalAsync(connection, rule, reader, handler, emitOnCapturedContext, flags);
                                                          }
                                                      }
                                                  }
                                                  """;

        private const string PropertyChangesClass = """
                                                     using System;
                                                     
                                                     // <auto-generated/>
                                                     #pragma warning disable
                                                     #nullable enable
                                                     namespace Tmds.DBus.SourceGenerator
                                                     {
                                                         internal record PropertyChanges<TProperties>(TProperties Properties, string[] Invalidated, string[] Changed)
                                                         {
                                                             public bool HasChanged(string property) => Array.IndexOf(Changed, property) != -1;
                                                             public bool IsInvalidated(string property) => Array.IndexOf(Invalidated, property) != -1;
                                                         }
                                                     }

                                                     """;

        private const string DBusInterfaceHandlerInterface = """
                                                              using System;
                                                              using System.Threading.Tasks;
                                                              
                                                              using Tmds.DBus.Protocol;

                                                              // <auto-generated/>
                                                              #pragma warning disable
                                                              #nullable enable
                                                              namespace Tmds.DBus.SourceGenerator
                                                              {
                                                                  internal interface IDBusInterfaceHandler
                                                                  {
                                                                      PathHandler? PathHandler { get; set; }
                                                                  
                                                                      Connection Connection { get; }
                                                              
                                                                      string InterfaceName { get; }
                                                              
                                                                      ReadOnlyMemory<byte> IntrospectXml { get; }
                                                              
                                                                      void ReplyGetProperty(string name, MethodContext context);
                                                              
                                                                      void ReplyGetAllProperties(MethodContext context);
                                                                      
                                                                      void SetProperty(string name, ref Reader reader);
                                                                      
                                                                      ValueTask ReplyInterfaceRequest(MethodContext context);
                                                                  }
                                                              }

                                                              """;

        private const string PathHandlerClass = """
                                                 using System.Collections.Generic;
                                                 using System.Linq;
                                                 using System.Threading.Tasks;

                                                 using Tmds.DBus.Protocol;

                                                 // <auto-generated/>
                                                 #pragma warning disable
                                                 #nullable enable
                                                 namespace Tmds.DBus.SourceGenerator
                                                 {
                                                     internal class PathHandler : IMethodHandler
                                                     {
                                                         private readonly bool _runMethodHandlerSynchronously;
                                                         private readonly ICollection<IDBusInterfaceHandler> _dbusInterfaces;
                                                 
                                                         public PathHandler(string path, bool runMethodHandlerSynchronously = true)
                                                         {
                                                             Path = path;
                                                             _runMethodHandlerSynchronously = runMethodHandlerSynchronously;
                                                             _dbusInterfaces = new List<IDBusInterfaceHandler>();
                                                         }
                                                 
                                                         /// <inheritdoc />
                                                         public string Path { get; }
                                                 
                                                         /// <inheritdoc />
                                                         public async ValueTask HandleMethodAsync(MethodContext context)
                                                         {
                                                             switch (context.Request.InterfaceAsString)
                                                             {
                                                                 case "org.freedesktop.DBus.Properties":
                                                                     switch (context.Request.MemberAsString, context.Request.SignatureAsString)
                                                                     {
                                                                         case ("Get", "ss"):
                                                                         {
                                                                             Reply();
                                                                             void Reply()
                                                                             {
                                                                                 Reader reader = context.Request.GetBodyReader();
                                                                                 string @interface = reader.ReadString();
                                                                                 IDBusInterfaceHandler? handler = _dbusInterfaces.FirstOrDefault(x => x.InterfaceName == @interface);
                                                                                 if (handler is null)
                                                                                     return;
                                                                                 string member = reader.ReadString();
                                                                                 handler.ReplyGetProperty(member, context);
                                                                             }
                                                 
                                                                             break;
                                                                         }
                                                                         case ("GetAll", "s"):
                                                                         {
                                                                             Reply();
                                                                             void Reply()
                                                                             {
                                                                                 Reader reader = context.Request.GetBodyReader();
                                                                                 string @interface = reader.ReadString();
                                                                                 IDBusInterfaceHandler? handler = _dbusInterfaces.FirstOrDefault(x => x.InterfaceName == @interface);
                                                                                 if (handler is null)
                                                                                     return;
                                                                                 handler.ReplyGetAllProperties(context);
                                                                             }
                                                 
                                                                             break;
                                                                         }
                                                                         case ("Set", "ssv"):
                                                                         {
                                                                             Reply();
                                                                             void Reply()
                                                                             {
                                                                                 Reader reader = context.Request.GetBodyReader();
                                                                                 string @interface = reader.ReadString();
                                                                                 IDBusInterfaceHandler? handler = _dbusInterfaces.FirstOrDefault(x => x.InterfaceName == @interface);
                                                                                 if (handler is null)
                                                                                     return;
                                                                                 string member = reader.ReadString();
                                                                                 handler.SetProperty(member, ref reader);
                                                                                 if (!context.NoReplyExpected)
                                                                                 {
                                                                                     using MessageWriter writer = context.CreateReplyWriter(null);
                                                                                     context.Reply(writer.CreateMessage());
                                                                                 }
                                                                             }
                                                                             
                                                                             break;
                                                                         }
                                                                     }
                                                 
                                                                     break;
                                                                 case "org.freedesktop.DBus.Introspectable":
                                                                     context.ReplyIntrospectXml(_dbusInterfaces.Select(static x => x.IntrospectXml).ToArray());
                                                                     break;
                                                                 default:
                                                                    IDBusInterfaceHandler? handler = _dbusInterfaces.FirstOrDefault(x => x.InterfaceName == context.Request.InterfaceAsString);
                                                                        if (handler is not null)
                                                                            await handler.ReplyInterfaceRequest(context);
                                                                    break;
                                                             }
                                                         }
                                                 
                                                         /// <inheritdoc />
                                                         public bool RunMethodHandlerSynchronously(Message message) => _runMethodHandlerSynchronously;
                                                 
                                                         /// <inheritdoc />
                                                         public void Add(IDBusInterfaceHandler item)
                                                         {
                                                             item.PathHandler = this;
                                                             _dbusInterfaces.Add(item);
                                                         }
                                                 
                                                         /// <inheritdoc />
                                                         public bool Contains(IDBusInterfaceHandler item) => _dbusInterfaces.Contains(item);
                                                 
                                                         /// <inheritdoc />
                                                         public bool Remove(IDBusInterfaceHandler item)
                                                         {
                                                             item.PathHandler = null;
                                                             return _dbusInterfaces.Remove(item);
                                                         }
                                                 
                                                         /// <inheritdoc />
                                                         public int Count => _dbusInterfaces.Count;
                                                     }
                                                 }
                                                 """;
    }
}
