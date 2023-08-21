using System.Reflection;

namespace Cave.Interfaces
{
    public interface IDebuggable
    {
        Task<string> GetDebugInfo();

        object? InvokeMethod(string methodName, params object?[]? parameters)
        {
            Type thisType = GetType();
            MethodInfo? method = thisType.GetMethod(methodName,
                BindingFlags.Public |
                BindingFlags.Static |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly |
                BindingFlags.IgnoreCase
            );
            if (method != null)
                return method.Invoke(this, parameters);
            return null;
        }

        List<string> GetMethods()
        {
            Type thisType = GetType();
            MethodInfo[] methods = thisType.GetMethods(
                BindingFlags.Public |
                BindingFlags.Static |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly
            );
            return methods.Select(method => method.Name).ToList();
        }
    }
}
