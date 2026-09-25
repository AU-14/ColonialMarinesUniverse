using System.Linq;
using Content.Shared.CMU14.TacticalMap.Reconstruction;

namespace Content.Client.CMU14.TacticalMap.Reconstruction;

/// <summary>Unpublished edits stay local until the server acknowledges Send.</summary>
public sealed class CMUReconDraft
{
    public readonly List<CMUReconAnnotation> Additions = [];
    public readonly HashSet<int> Removals = [];
    public bool Changed => Additions.Count > 0 || Removals.Count > 0;
    public bool Sending { get; private set; }
    private int _request;

    public bool Add(CMUReconAnnotation annotation)
    {
        if (Sending || Additions.Count >= CMUReconRoutes.MaxAnnotations) return false;
        Additions.Add(annotation);
        return true;
    }

    public void Undo(CMUReconOrder[] orders)
    {
        if (Sending) return;
        if (Additions.Count > 0) Additions.RemoveAt(Additions.Count - 1);
        else if (orders.LastOrDefault(o => !Removals.Contains(o.Id)) is { Id: > 0 } last) Removals.Add(last.Id);
    }

    public void Clear(CMUReconOrder[] orders)
    {
        if (Sending) return;
        Additions.Clear();
        foreach (var order in orders) Removals.Add(order.Id);
    }

    public CMUReconSendMessage? Send(int generation)
    {
        if (Sending || !Changed) return null;
        Sending = true;
        return new CMUReconSendMessage(generation, ++_request, Additions.ToArray(), Removals.ToArray());
    }

    public bool Acknowledge(CMUReconSentMessage result)
    {
        if (!Sending || result.RequestId != _request) return false;
        Sending = false;
        if (result.Accepted) { Additions.Clear(); Removals.Clear(); }
        return true;
    }
}
