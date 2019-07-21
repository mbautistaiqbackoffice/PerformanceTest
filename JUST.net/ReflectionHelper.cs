// ReSharper disable IdentifierTypo
namespace JUST
{
    #region Usings

    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.RegularExpressions;

    #endregion

    internal static class ReflectionHelper
    {
        private const string EXTERNAL_ASSEMBLY_REGEX = "([\\w.]+)[:]{2}([\\w.]+)[:]{0,2}([\\w.]*)";
        internal static readonly Regex ExternalAssemblyRegex = new Regex(EXTERNAL_ASSEMBLY_REGEX, RegexOptions.Compiled);
        private static readonly Dictionary<string, (Type, MethodInfo)> _Types = new Dictionary<string, (Type, MethodInfo)>();

        internal static object caller(Assembly assembly,
                                      string myClass,
                                      string myMethod,
                                      object[] parameters,
                                      bool convertParameters,
                                      JUSTContext context)
        {
            var typeKey = $"{(assembly == null ? string.Empty : assembly.FullName)}-{myClass}-{myMethod}";

            Type type;
            MethodInfo methodInfo;
            if (_Types.ContainsKey(typeKey))
            {
                type = _Types[typeKey].Item1;
                methodInfo = _Types[typeKey].Item2;
            }
            else
            {
                type = assembly?.GetType(myClass) ?? Type.GetType(myClass);
                methodInfo = type.GetTypeInfo().GetMethod(myMethod);
                _Types.Add(typeKey, (type, methodInfo));
            }

            var instance = type != null && methodInfo != null && !methodInfo.IsStatic ? Activator.CreateInstance(type) : null;

            try
            {
                return InvokeCustomMethod(methodInfo, parameters, convertParameters, context);
            }
            catch
            {
                var mode = context.EvaluationMode;
                if (mode == EvaluationMode.Strict)
                    throw;
                return methodInfo != null ? GetDefaultValue(methodInfo.ReturnType) : null;
            }
        }

        internal static object InvokeCustomMethod(MethodInfo methodInfo, object[] parameters, bool convertParameters, JUSTContext context)
        {
            var instance = !methodInfo.IsStatic & methodInfo.DeclaringType != null ? Activator.CreateInstance(methodInfo.DeclaringType) : null;

            if (!convertParameters)
                return methodInfo.Invoke(instance, parameters);

            var parameterInfos = methodInfo.GetParameters();

            return methodInfo.Invoke(instance,
                                     parameterInfos.Select((t, i) => GetTypedValue(t.ParameterType, parameters[i], context.EvaluationMode))
                                                   .ToArray());
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
                        typedValue = Activator.CreateInstance(pType, val);
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

        private static object GetDefaultValue(Type t) => t.IsValueType ? Activator.CreateInstance(t) : null;
    }
}