namespace WaveMP.Core.Library.Covers;

/// <summary>
/// Reacts to a changed cover rule of an album or a track, e.g. to bring cached covers up to date. Runs after the rule
/// is stored; a failing handler is logged and does not undo the change.
/// </summary>
public interface ICoverRuleChangeHandler
{
    ValueTask OnCoverRuleChangedAsync(CoverRuleChange change, CancellationToken cancellationToken);
}
