// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming

// ReSharper disable StringLiteralTypo
// ReSharper disable InconsistentlySynchronizedField

using System.Collections.Generic;

namespace JUST
{
    #region Usings

    using System;
    using System.Collections.Concurrent;
    using System.ComponentModel;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.RegularExpressions;

    using Newtonsoft.Json.Linq;

    #endregion

    internal static class ReflectionHelper
    {
        private const string EXTERNAL_ASSEMBLY_REGEX = "([\\w.]+)[:]{2}([\\w.]+)[:]{0,2}([\\w.]*)";
        internal static readonly Regex ExternalAssemblyRegex = new Regex(EXTERNAL_ASSEMBLY_REGEX, RegexOptions.Compiled);
        private static readonly ConcurrentDictionary<string, MethodInfo> _Types = new ConcurrentDictionary<string, MethodInfo>();
        private static readonly ConcurrentDictionary<Type, object> _Instances = new ConcurrentDictionary<Type, object>();

        internal static object caller(Assembly assembly,
                                      string myClass,
                                      string myMethod,
                                      object[] parameters,
                                      bool convertParameters,
                                      JUSTContext context)
        {
            if (myClass.Equals("JUST.Transformer"))
            {
                // Avoid reflection where possible
                switch (myMethod)
                {
                    case "concat":
                        return Transformer.concat(parameters[0] as string, parameters[1] as string, parameters[2] as JUSTContext);

                    case "concatall":
                        return Transformer.concatall(parameters[0] as JArray, parameters[1] as JUSTContext);

                    case "concatallatpath":
                        return Transformer.concatallatpath(parameters[0] as JArray, parameters[1] as string, parameters[2] as JUSTContext);

                    case "currentvalue":
                        return Transformer.currentvalue(parameters[0] as JArray, parameters[1] as JToken);

                    case "currentvalueatpath":
                        return Transformer.currentvalueatpath(parameters[0] as JArray, parameters[1] as JToken, parameters[2] as string);

                    case "exists":
                        return Transformer.exists(parameters[0] as string, parameters[1] as JUSTContext);

                    case "existsandnotempty":
                        return Transformer.existsandnotempty(parameters[0] as string, parameters[1] as JUSTContext);

                    case "firstindexof":
                        return Transformer.firstindexof(parameters[0] as string, parameters[1] as string, parameters[2] as JUSTContext);
                    
                    case "grouparrayby":
                        return Transformer.grouparrayby(parameters[0] as string, parameters[1] as string, parameters[2] as string, parameters[3] as JUSTContext);

                    case "ifcondition":
                        return Transformer.ifcondition(parameters[0],
                                                       parameters[1],
                                                       parameters[2],
                                                       parameters[3],
                                                       parameters[4] as JUSTContext);

                    case "lastindex":
                        return Transformer.lastindex(parameters[0] as JArray, parameters[1] as JToken);
                    
                    case "lastindexof":
                        return Transformer.lastindexof(parameters[0] as string, parameters[1] as string, parameters[2] as JUSTContext);
                    
                    case "lastvalue":
                        return Transformer.lastvalue(parameters[0] as JArray, parameters[1] as JToken);

                    case "lastvalueatpath":
                        return Transformer.lastvalueatpath(parameters[0] as JArray, parameters[1] as JToken, parameters[2] as string);
                    
                    case "stringcontains":
                        return Transformer.stringcontains(parameters[0] as object[]);

                    case "stringequals":
                        return Transformer.stringequals(parameters[0] as object[]);

                    case "valueof":
                        return Transformer.valueof(parameters[0] as string, parameters[1] as JUSTContext);

                    case "xconcat":
                        return Transformer.xconcat(parameters[0] as object[]);
                }
            }

            var typeKey = $"{(assembly == null ? string.Empty : assembly.FullName)}-{myClass}-{myMethod}";

            MethodInfo methodInfo;
            if (_Types.ContainsKey(typeKey))
            {
                methodInfo = _Types[typeKey];
            }
            else
            {
                var type = assembly?.GetType(myClass) ?? Type.GetType(myClass);
                methodInfo = type.GetTypeInfo().GetMethod(myMethod);
                _Types.TryAdd(typeKey, methodInfo);
            }

            try
            {
                return InvokeCustomMethod(methodInfo, parameters, convertParameters, context);
            }
            catch (Exception ex)
            {
                var mode = context.EvaluationMode;
                if (mode == EvaluationMode.Strict)
                    throw;
                return methodInfo != null ? GetDefaultValue(methodInfo.ReturnType) : null;
            }
        }

        internal static object InvokeCustomMethod(MethodInfo methodInfo, object[] parameters, bool convertParameters, JUSTContext context)
        {
            var instance = !methodInfo.IsStatic & methodInfo.DeclaringType != null ? GetInstance(methodInfo.DeclaringType) : null;

            if (!convertParameters)
                return methodInfo.Invoke(instance, parameters);

            var typedParameters = new List<object>();
            var parameterInfos = methodInfo.GetParameters();
            for (var i = 0; i < parameterInfos.Length; i++)
            {
                var pType = parameterInfos[i].ParameterType;
                typedParameters.Add(GetTypedValue(pType, parameters[i], context.EvaluationMode));
            }

            try
            {
                return methodInfo.Invoke(instance, typedParameters.ToArray());
            }
            catch(Exception ex)
            {
                if (ex.InnerException != null)
                {
                    throw ex.InnerException;
                }
                throw;
            }
        }

        private static object GetInstance(Type type, object val = null)
        {
            if (type == null)
                return null;

            if (!_Instances.ContainsKey(type))
            {
                lock (_Instances)
                {
                    var instance = val == null ? Activator.CreateInstance(type) : Activator.CreateInstance(type, val);
                    _Instances.TryAdd(type, instance);
                }
            }

            return _Instances[type];
        }

        internal static object CallExternalAssembly(string functionName, object[] parameters, JUSTContext context)
        {
            var match = ExternalAssemblyRegex.Match(functionName);
            var isAssemblyDefined = match.Groups.Count == 4 && match.Groups[3].Value != string.Empty;
            var assemblyName = isAssemblyDefined ? match.Groups[1].Value : null;
            var namespc = match.Groups[isAssemblyDefined ? 2 : 1].Value;
            var methodName = match.Groups[isAssemblyDefined ? 3 : 2].Value;

            var assembly = GetAssembly(isAssemblyDefined, assemblyName, namespc, methodName);
            if (assembly != null)
                return caller(assembly, namespc, methodName, FilterParameters(parameters), true, context);

            throw new MissingMethodException((assemblyName != null ? $"{assemblyName}." : string.Empty) + $"{namespc}.{methodName}");
        }

        internal static MethodInfo SearchCustomFunction(string assemblyName, string namespc, string methodName)
        {
            var assembly = GetAssembly(assemblyName != null, assemblyName, namespc, methodName);
            var type = assembly?.GetType(namespc) ?? Type.GetType(namespc);
            return type?.GetTypeInfo().GetMethod(methodName);
        }

        private static Assembly GetAssembly(bool isAssemblyDefined, string assemblyName, string namespc, string methodName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            if (isAssemblyDefined)
            {
                var assemblyFileName = !assemblyName.EndsWith(".dll") ? $"{assemblyName}.dll" : assemblyName;

                // TODO: SingleOrDefault fails, dll registered twice????
                // TODO: Possible alternative to AppDomain: https://github.com/dotnet/coreclr/issues/14680
                var assembly = assemblies.FirstOrDefault(a => a.ManifestModule.Name == assemblyFileName);
                if (assembly != null)
                    return assembly;

                var assemblyLocation = Path.Combine(Directory.GetCurrentDirectory(), assemblyFileName);
                assembly = Assembly.LoadFile(assemblyLocation);
                AppDomain.CurrentDomain.Load(assembly.GetName());

                return assembly;
            }

            foreach (var assembly in assemblies.Where(a => !a.FullName.StartsWith("System.")))
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }

                if (types.Any(typeInfo => string.Compare(typeInfo.FullName, namespc, StringComparison.OrdinalIgnoreCase) == 0))
                    return assembly;
            }

            return null;
        }

        private static object[] FilterParameters(object[] parameters)
        {
            if (string.IsNullOrEmpty(parameters[0]?.ToString() ?? string.Empty))
                parameters = parameters.Skip(1).ToArray();

            if (parameters.Length > 0 && parameters.Last().ToString() == "{}")
                parameters = parameters.Take(parameters.Length - 1).ToArray();

            return parameters;
        }

        internal static object GetTypedValue(Type pType, object val, EvaluationMode mode)
        {
            var typedValue = val;
            var converter = TypeDescriptor.GetConverter(pType);
            try
            {
                if (val == null || val.GetType() == pType)
                    return val;

                if (converter.CanConvertFrom(val.GetType()))
                {
                    typedValue = converter.ConvertFrom(null, CultureInfo.InvariantCulture, val);
                }
                else if (pType.IsPrimitive)
                {
                    typedValue = Convert.ChangeType(val, pType);
                }
                else if (!pType.IsAbstract)
                {
                    var method = (MethodBase)pType.GetMethod("Parse",
                                                             BindingFlags.Static | BindingFlags.Public,
                                                             null,
                                                             new[] { val.GetType() },
                                                             null);
                    if (method == null)
                        method = pType.GetConstructor(new[] { val.GetType() });

                    if (method?.IsConstructor ?? false)
                        typedValue = GetInstance(pType, val);
                    else
                        typedValue = method?.Invoke(null, new[] { val }) ?? (typeof(string) == pType ? val.ToString() : val);
                }
            }
            catch
            {
                if (mode == EvaluationMode.Strict)
                    throw;

                typedValue = GetDefaultValue(pType);
            }

            return typedValue;
        }

        private static object GetDefaultValue(Type t) => GetInstance(t);
    }
}