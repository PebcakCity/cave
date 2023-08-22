using System.Reflection;

namespace Cave.Interfaces
{
    public interface IDebuggable
    {
        /// <summary>
        /// To be implemented by the IDebuggable device.  Gets a text string
        /// containing device state used for troubleshooting.
        /// </summary>
        /// <returns>A string containing information about the device.</returns>
        Task<string> GetDebugInfo();

        /// <summary>
        /// Default implementation.  Invokes a method with the required
        /// parameters and returns the method's result.
        /// </summary>
        /// <param name="methodName">Name of the method to invoke</param>
        /// <param name="parameters">One or more parameters (or an array of
        /// them) to pass to the method.</param>
        /// <returns></returns>
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

        /// <summary>
        /// Gets a list of all public methods declared by the debuggable's type.
        /// </summary>
        /// <returns>A list of the names of all public methods declared by the
        /// debuggable's type.
        /// </returns>
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
