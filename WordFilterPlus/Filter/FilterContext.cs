namespace WordFilterPlus.Filter;

/// <summary>
/// Thread-local slot that holds the Thing currently being evaluated by IsFilterPass.
/// Set by transpiler-injected code at each call site; cleared immediately after the call.
/// </summary>
internal static class FilterContext
{
    [System.ThreadStatic]
    internal static Thing? Current;
}
