namespace LudumDare60.Engine.Platform.Browser;

/// <summary>
/// The parts of an exception that <c>reporting.js</c> sends to Sentry.
/// </summary>
/// <param name="Type">Full name of the exception type, the issue title in Sentry.</param>
/// <param name="Message">The exception message.</param>
/// <param name="StackTrace">The managed stack trace, or <see langword="null"/> when there is none (a Release build
/// without <c>StackTraceSupport</c>, or an exception that was never thrown).</param>
/// <param name="Details"><see cref="Exception.ToString"/> of the original exception: inner exceptions included.</param>
internal readonly record struct ErrorReport(string Type, string Message, string? StackTrace, string Details)
{
    /// <summary>
    /// Describes <paramref name="exception"/>. An <see cref="AggregateException"/> with one inner exception is described
    /// by that inner exception, so a failed task groups in Sentry by what actually went wrong.
    /// </summary>
    public static ErrorReport From(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var cause = exception is AggregateException { InnerExceptions.Count: 1 } aggregate
            ? aggregate.InnerExceptions[0]
            : exception;
        var type = cause.GetType();
        return new ErrorReport(type.FullName ?? type.Name, cause.Message, cause.StackTrace, exception.ToString());
    }
}
