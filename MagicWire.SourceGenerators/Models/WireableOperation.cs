using System;
using System.Collections.Generic;

namespace MagicWire.SourceGenerators.Models;

/// <summary>
/// The wireable operation defines an operation that can be used in the wireable object.
/// </summary>
public sealed class WireableOperation(string methodName, string operationName, string returnType = "void")
{
    /// <summary>
    /// The name of the operation. This is also the exact name of the method in the C# code.
    /// </summary>
    public string MethodName { get; } = methodName;
 
    /// <summary>
    /// The name of the operation that is used in the frontend.
    /// </summary>
    public string OperationName { get; } = operationName;
    
    /// <summary>
    /// The return type of the operation. This is also the exact name of the return type in the C# code.
    /// </summary>
    public string ReturnType { get; } = returnType;
    
    /// <summary>
    /// A list of parameters that are used in the operation. Its a list of tuples where the first item is the name of the
    /// parameter and the second item is the type of the parameter.
    /// </summary>
    public List<Tuple<string, string>> Parameters { get; set; } = [];
}