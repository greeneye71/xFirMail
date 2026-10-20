// SPDX-License-Identifier: EUPL-1.2
using System.Diagnostics;

namespace Xfir.Core;

public sealed record ProcessingLimits
{
    public int MaxXmlDepth { get; init; } = 64;
    public int MaxXmlNodes { get; init; } = 100_000;
    public int MaxFields { get; init; } = 20_000;
    public int MaxPages { get; init; } = 100;
    public TimeSpan MaxDuration { get; init; } = TimeSpan.FromSeconds(30);
}

public sealed class ProcessingLimitException(string message) : Exception(message);

/// <summary>One budget and field cache for one document operation; not shared between threads or documents.</summary>
public sealed class ProcessingBudget
{
    private readonly long started = Stopwatch.GetTimestamp();
    private readonly CancellationToken cancellationToken;
    private readonly Dictionary<XmlPart, IReadOnlyList<DataField>> fields = [];
    private long nodes;
    private long fieldCount;
    private int pages;
    public ProcessingLimits Limits { get; }

    public ProcessingBudget(ProcessingLimits? limits = null, CancellationToken cancellationToken = default)
    {
        Limits = limits ?? new();
        if (Limits.MaxXmlDepth < 1 || Limits.MaxXmlNodes < 1 || Limits.MaxFields < 1 || Limits.MaxPages < 1 || Limits.MaxDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(limits), "I limiti di elaborazione devono essere positivi.");
        this.cancellationToken = cancellationToken;
    }

    public void Check()
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Stopwatch.GetElapsedTime(started) >= Limits.MaxDuration)
            throw new ProcessingLimitException("Superato il tempo massimo di elaborazione del documento.");
    }

    internal void XmlNode(int depth, int attributes, bool element)
    {
        Check();
        if (element && depth > Limits.MaxXmlDepth) throw new ProcessingLimitException("XML con annidamento eccessivo.");
        if ((nodes += 1L + attributes) > Limits.MaxXmlNodes) throw new ProcessingLimitException("Superato il limite complessivo di nodi XML.");
    }

    public IReadOnlyList<DataField> GetFields(XmlPart part)
    {
        Check();
        if (fields.TryGetValue(part, out var cached)) return cached;
        var values = new List<DataField>();
        foreach (var field in part.EnumerateFields())
        {
            Check();
            if (++fieldCount > Limits.MaxFields) throw new ProcessingLimitException("Superato il limite complessivo di campi del documento.");
            values.Add(field);
        }
        return fields[part] = values.AsReadOnly();
    }

    public void AddPage()
    {
        Check();
        if (++pages > Limits.MaxPages) throw new ProcessingLimitException("Superato il limite di pagine PDF. Nessuna copia parziale è stata salvata.");
    }
}
