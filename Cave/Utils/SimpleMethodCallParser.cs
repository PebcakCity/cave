using System.Text.RegularExpressions;

using NLog;

using Cave.Interfaces;

namespace Cave.Utils
{
    /// <summary>
    /// A parser for simple method calls with simple parameter lists (no nesting
    /// calls/parentheses)
    /// </summary>
    public class SimpleMethodCallParser
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        public SimpleMethodCallParser() {}

        public static object? ParseCallString(string callString, IDebuggable instance)
        {
            try
            {
                Regex reMethodCall = new(@"\b([^()]+)\((.*)\)$");
                Match match = reMethodCall.Match(callString);

                /* Groups[0]: entire match, Groups[1]: method name, 
                   Groups[2]: parameter list */
                if ( match.Success && match.Groups.Count > 2 )
                {
                    string methodName = match.Groups[1].Value;
                    string paramStringTrimmed = match.Groups[2].Value.Trim();
                    var paramStrings = paramStringTrimmed.Equals(string.Empty) ? null :
                        Regex.Split(paramStringTrimmed, @"\s*,\s*");
                    var paramValues = (paramStrings == null) ? null :
                        new List<object>();

                    if ( paramStrings is not null )
                        foreach ( string param in paramStrings )
                            paramValues!.Add(GetParamValue(param));
                    return DoMethodCall(methodName, paramValues!, instance);
                }
            }
            catch { throw; }
            return null;
        }

        private static object? DoMethodCall(string methodName,
            IEnumerable<object?> parameters, IDebuggable instance)
        {
            try
            {
                var methodsAvailable = instance.GetMethods();
                if ( methodName.Equals("GetMethods", StringComparison.OrdinalIgnoreCase) )
                    return string.Join("\n", methodsAvailable);
                else
                {
                    int index = methodsAvailable.FindIndex(
                        name => name.Equals(methodName, StringComparison.OrdinalIgnoreCase)
                    );
                    if ( index >= 0 )
                    {
                        methodName = methodsAvailable[index];
                        return instance.InvokeMethod(methodName, parameters?.ToArray());
                    }
                }
                return null;
            }
            catch ( Exception ex )
            {
                Logger.Error($"{nameof(DoMethodCall)} :: {ex}");
                throw;
            }
        }

        private static object GetParamValue( string param )
        {
            switch ( param )
            {
                case "true": return true;
                case "false": return false;
                case "null": return null!;
                default:
                    double floatVal;
                    int intVal;
                    if ( int.TryParse(param, out intVal) )
                        return intVal;
                    else if ( double.TryParse(param, out floatVal) )
                        return floatVal;
                    else
                        return TrimQuotes(param);
            }
        }

        private static string TrimQuotes(string value)
        {
            return value.Replace("\"", "").Replace("\'", "").Replace("`", "");
        }
    }
}
