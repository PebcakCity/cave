using System.Collections;
using System.Text;

using NLog;
using NLog.LayoutRenderers;

namespace Cave.Utils
{
    /// <summary>
    /// NLog layout renderer for beautifying logs
    /// </summary>
    [LayoutRenderer("niceLog")]
    public class NiceLog : LayoutRenderer
    {
        protected override void Append( StringBuilder builder, LogEventInfo logEvent )
        {
            var time = logEvent.TimeStamp.ToString("yyyy-MM-dd HH:mm:ss.ffff");
            var level = logEvent.Level.ToString().ToUpper();
            var logger = logEvent.LoggerName;
            var ex = logEvent.Exception;

            // Requires $callsite be added to log layout
            // Set layout to:
            // "${niceLog}${callsite:className=false:methodName=false}"
            // ... to capture callsite info but keep it from displaying at the
            // bottom of the log entry in addition to showing in the header.
            var method = logEvent.CallerMemberName;

            // Calling with Exception and no message leaves Message set to "{0}"
            var msg = (ex is not null && logEvent.Message.Equals("{0}")) ?
                    "EXCEPTION OCCURRED" : logEvent.Message;

            builder.Append($"{time} >> {level} >> {logger}");
            builder.Append(string.IsNullOrEmpty(method) ?
                $" >> {msg}" : $" >> {method} >> {msg}");

            if ( ex is not null )
            {
                builder.Append($" >> {ex.GetType()}: {ex.Message}\n");
                if ( ex.Data.Count > 0 )
                {
                    builder.Append("Data:\n");
                    foreach ( DictionaryEntry de in ex.Data )
                    {
                        builder.Append(string.Format("{0,15} : {1}\n",
                            de.Key, de.Value));
                    }
                }
                // Exception types that we don't _ever_ want a stacktrace for
                // should override the StackTrace property to return null or an
                // empty string
                builder.Append(string.IsNullOrEmpty(ex.StackTrace) ?
                    "\n" : $"\nStackTrace:\n{ex.StackTrace}\n");
            }
        }
    }
}
