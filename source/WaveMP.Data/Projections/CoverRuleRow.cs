using WaveMP.Data.Entities;

namespace WaveMP.Data.Projections;

/// <summary>
/// The cover rules of an album or a track as stored; <c>null</c> inherits the rule of the next level.
/// </summary>
/// <param name="Rule">The rule of the album or track itself.</param>
/// <param name="AlbumRule">For a track the rule of its album; <c>null</c> for albums, for tracks without album and if
/// the album inherits the global rule.</param>
public sealed record CoverRuleRow(CoverPriority? Rule, CoverPriority? AlbumRule);
